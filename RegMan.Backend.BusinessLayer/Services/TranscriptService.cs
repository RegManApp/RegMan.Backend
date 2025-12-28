using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.TranscriptDTOs;
using RegMan.Backend.BusinessLayer.Helpers;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class TranscriptService : ITranscriptService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IAuditLogService auditLogService;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly InstitutionSettings institutionSettings;

        public TranscriptService(
            IUnitOfWork unitOfWork,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor,
            IOptions<InstitutionSettings> institutionOptions)
        {
            this.unitOfWork = unitOfWork;
            this.auditLogService = auditLogService;
            this.httpContextAccessor = httpContextAccessor;
            this.institutionSettings = institutionOptions?.Value ?? new InstitutionSettings();
        }

        private static int SemesterSortKey(string semester)
        {
            if (string.IsNullOrWhiteSpace(semester))
                return 99;

            return semester.Trim().ToLowerInvariant() switch
            {
                "winter" => 0,
                "spring" => 1,
                "summer" => 2,
                "fall" => 3,
                _ => 99
            };
        }

        private static string BuildTermName(string semester, int year)
        {
            if (string.IsNullOrWhiteSpace(semester))
                return year > 0 ? year.ToString() : string.Empty;

            return year > 0 ? $"{year} {semester}" : semester;
        }

        private static string? BuildSectionSubType(Section? section)
        {
            var slotTypes = section?.Slots?
                .Select(s => s.SlotType.ToString())
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (slotTypes == null || slotTypes.Count == 0)
                return null;

            return string.Join("/", slotTypes);
        }

        private int TermSortKey(int year, string semester)
        {
            var semKey = SemesterSortKey(semester);
            return (year * 10) + semKey;
        }

        private static Transcript? PickLatestAttempt(IEnumerable<Transcript> attempts)
        {
            return attempts
                .OrderByDescending(t => t.Year)
                .ThenByDescending(t => SemesterSortKey(t.Semester))
                .ThenByDescending(t => t.CompletedAt)
                .ThenByDescending(t => t.TranscriptId)
                .FirstOrDefault();
        }

        private ViewTranscriptDTO? PickLatestAttempt(IEnumerable<ViewTranscriptDTO> attempts)
        {
            return attempts
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => SemesterSortKey(r.Semester))
                .ThenByDescending(r => r.CompletedAt ?? DateTime.MinValue)
                .ThenByDescending(r => r.TranscriptId)
                .FirstOrDefault();
        }

        private IEnumerable<Transcript> ApplyGpaPolicy(IEnumerable<Transcript> transcripts)
        {
            if (institutionSettings.TranscriptGpaPolicy == TranscriptGpaPolicy.AllAttempts)
                return transcripts;

            return transcripts
                .GroupBy(t => t.CourseId)
                .Select(g => PickLatestAttempt(g)!)
                .Where(t => t != null);
        }

        private IEnumerable<ViewTranscriptDTO> ApplyGpaPolicy(IEnumerable<ViewTranscriptDTO> rows)
        {
            if (institutionSettings.TranscriptGpaPolicy == TranscriptGpaPolicy.AllAttempts)
                return rows;

            return rows
                .GroupBy(r => r.CourseId)
                .Select(g => PickLatestAttempt(g)!)
                .Where(r => r != null);
        }

        // =========================
        // Helpers
        // =========================
        private (string userId, string email) GetUserInfo()
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new Exception("User context not found.");

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new Exception("UserId not found.");

            var email = user.FindFirstValue(ClaimTypes.Email)
                ?? "unknown@email.com";

            return (userId, email);
        }

        // =====================================
        // Create Transcript (Admin/Instructor)
        // =====================================
        public async Task<ViewTranscriptDTO> CreateTranscriptAsync(CreateTranscriptDTO dto)
        {
            // Validate grade
            if (!GradeHelper.IsRecognizedGrade(dto.Grade))
                throw new ArgumentException($"Invalid grade: {dto.Grade}");

            // Check student exists
            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == dto.StudentId)
                ?? throw new Exception($"Student with ID {dto.StudentId} not found.");

            // Check course exists
            var course = await unitOfWork.Courses.GetByIdAsync(dto.CourseId)
                ?? throw new Exception($"Course with ID {dto.CourseId} not found.");

            // Check section exists
            var section = await unitOfWork.Sections.GetByIdAsync(dto.SectionId)
                ?? throw new Exception($"Section with ID {dto.SectionId} not found.");

            // Check if transcript already exists for this student-course-section
            var existingTranscript = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .AnyAsync(t => t.StudentId == dto.StudentId && t.CourseId == dto.CourseId && t.SectionId == dto.SectionId);

            if (existingTranscript)
                throw new Exception("Transcript entry already exists for this student, course, and section.");

            var transcript = new Transcript
            {
                StudentId = dto.StudentId,
                CourseId = dto.CourseId,
                SectionId = dto.SectionId,
                Grade = dto.Grade.ToUpper(),
                GradePoints = GradeHelper.GetGradePoints(dto.Grade),
                Semester = dto.Semester,
                Year = dto.Year,
                CreditHours = course.CreditHours,
                CompletedAt = DateTime.UtcNow
            };

            await unitOfWork.Transcripts.AddAsync(transcript);
            await unitOfWork.SaveChangesAsync();

            // Update student's completed credits and GPA
            await RecalculateAndUpdateStudentGPAAsync(dto.StudentId);

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "CREATE", "Transcript", transcript.TranscriptId.ToString());

            return new ViewTranscriptDTO
            {
                TranscriptId = transcript.TranscriptId,
                StudentId = transcript.StudentId,
                StudentName = student.User.FullName,
                CourseId = transcript.CourseId,
                CourseName = course.CourseName,
                CourseCode = course.CourseCode,
                Status = "COMPLETED",
                CreditHours = transcript.CreditHours,
                Grade = transcript.Grade,
                GradePoints = transcript.GradePoints,
                CountsTowardGpa = GradeHelper.CountsTowardGpa(transcript.Grade),
                QualityPoints = GradeHelper.CountsTowardGpa(transcript.Grade)
                    ? (double?)(transcript.GradePoints * transcript.CreditHours)
                    : null,
                Semester = transcript.Semester,
                Year = transcript.Year,
                CompletedAt = transcript.CompletedAt
            };
        }

        // =====================================
        // Update Grade (Admin/Instructor)
        // =====================================
        public async Task<ViewTranscriptDTO> UpdateGradeAsync(UpdateTranscriptDTO dto)
        {
            if (!GradeHelper.IsRecognizedGrade(dto.Grade))
                throw new ArgumentException($"Invalid grade: {dto.Grade}");

            var transcript = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Include(t => t.Student)
                    .ThenInclude(s => s.User)
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.TranscriptId == dto.TranscriptId)
                ?? throw new Exception($"Transcript with ID {dto.TranscriptId} not found.");

            transcript.Grade = dto.Grade.ToUpper();
            transcript.GradePoints = GradeHelper.GetGradePoints(dto.Grade);

            unitOfWork.Transcripts.Update(transcript);
            await unitOfWork.SaveChangesAsync();

            // Recalculate GPA
            await RecalculateAndUpdateStudentGPAAsync(transcript.StudentId);

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "UPDATE", "Transcript", transcript.TranscriptId.ToString());

            return new ViewTranscriptDTO
            {
                TranscriptId = transcript.TranscriptId,
                StudentId = transcript.StudentId,
                StudentName = transcript.Student.User.FullName,
                CourseId = transcript.CourseId,
                CourseName = transcript.Course.CourseName,
                CourseCode = transcript.Course.CourseCode,
                Status = "COMPLETED",
                CreditHours = transcript.CreditHours,
                Grade = transcript.Grade,
                GradePoints = transcript.GradePoints,
                CountsTowardGpa = GradeHelper.CountsTowardGpa(transcript.Grade),
                QualityPoints = GradeHelper.CountsTowardGpa(transcript.Grade)
                    ? (double?)(transcript.GradePoints * transcript.CreditHours)
                    : null,
                Semester = transcript.Semester,
                Year = transcript.Year,
                CompletedAt = transcript.CompletedAt
            };
        }

        // =====================================
        // Delete Transcript (Admin)
        // =====================================
        public async Task<string> DeleteTranscriptAsync(int transcriptId)
        {
            var transcript = await unitOfWork.Transcripts.GetByIdAsync(transcriptId)
                ?? throw new Exception($"Transcript with ID {transcriptId} not found.");

            var studentId = transcript.StudentId;

            await unitOfWork.Transcripts.DeleteAsync(transcriptId);
            await unitOfWork.SaveChangesAsync();

            // Recalculate GPA after deletion
            await RecalculateAndUpdateStudentGPAAsync(studentId);

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "DELETE", "Transcript", transcriptId.ToString());

            return $"Transcript with ID {transcriptId} deleted successfully.";
        }

        // =====================================
        // Get Transcript By ID
        // =====================================
        public async Task<ViewTranscriptDTO> GetTranscriptByIdAsync(int transcriptId)
        {
            var transcript = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Include(t => t.Student)
                    .ThenInclude(s => s.User)
                .Include(t => t.Course)
                .FirstOrDefaultAsync(t => t.TranscriptId == transcriptId)
                ?? throw new Exception($"Transcript with ID {transcriptId} not found.");

            return new ViewTranscriptDTO
            {
                TranscriptId = transcript.TranscriptId,
                StudentId = transcript.StudentId,
                StudentName = transcript.Student.User.FullName,
                CourseId = transcript.CourseId,
                CourseName = transcript.Course.CourseName,
                CourseCode = transcript.Course.CourseCode,
                Status = "COMPLETED",
                CreditHours = transcript.CreditHours,
                Grade = transcript.Grade,
                GradePoints = transcript.GradePoints,
                CountsTowardGpa = GradeHelper.CountsTowardGpa(transcript.Grade),
                QualityPoints = GradeHelper.CountsTowardGpa(transcript.Grade)
                    ? (double?)(transcript.GradePoints * transcript.CreditHours)
                    : null,
                Semester = transcript.Semester,
                Year = transcript.Year,
                CompletedAt = transcript.CompletedAt
            };
        }

        // =====================================
        // Get Transcripts By Student ID
        // =====================================
        public async Task<IEnumerable<ViewTranscriptDTO>> GetTranscriptsByStudentIdAsync(int studentId)
        {
            return await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Include(t => t.Student)
                    .ThenInclude(s => s.User)
                .Include(t => t.Course)
                .Where(t => t.StudentId == studentId)
                .OrderByDescending(t => t.Year)
                .ThenBy(t => t.Semester)
                .Select(t => new ViewTranscriptDTO
                {
                    TranscriptId = t.TranscriptId,
                    StudentId = t.StudentId,
                    StudentName = t.Student.User.FullName,
                    CourseId = t.CourseId,
                    CourseName = t.Course.CourseName,
                    CourseCode = t.Course.CourseCode,
                    Status = "COMPLETED",
                    CreditHours = t.CreditHours,
                    Grade = t.Grade,
                    GradePoints = t.GradePoints,
                    CountsTowardGpa = GradeHelper.IsValidGrade(t.Grade),
                    QualityPoints = GradeHelper.IsValidGrade(t.Grade)
                        ? (double?)(t.GradePoints * t.CreditHours)
                        : null,
                    Semester = t.Semester,
                    Year = t.Year,
                    CompletedAt = t.CompletedAt
                })
                .ToListAsync();
        }

        // =====================================
        // Get Transcripts By Semester
        // =====================================
        public async Task<IEnumerable<ViewTranscriptDTO>> GetTranscriptsBySemesterAsync(string semester, int year)
        {
            return await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Include(t => t.Student)
                    .ThenInclude(s => s.User)
                .Include(t => t.Course)
                .Where(t => t.Semester == semester && t.Year == year)
                .Select(t => new ViewTranscriptDTO
                {
                    TranscriptId = t.TranscriptId,
                    StudentId = t.StudentId,
                    StudentName = t.Student.User.FullName,
                    CourseId = t.CourseId,
                    CourseName = t.Course.CourseName,
                    CourseCode = t.Course.CourseCode,
                    Status = "COMPLETED",
                    CreditHours = t.CreditHours,
                    Grade = t.Grade,
                    GradePoints = t.GradePoints,
                    CountsTowardGpa = GradeHelper.IsValidGrade(t.Grade),
                    QualityPoints = GradeHelper.IsValidGrade(t.Grade)
                        ? (double?)(t.GradePoints * t.CreditHours)
                        : null,
                    Semester = t.Semester,
                    Year = t.Year,
                    CompletedAt = t.CompletedAt
                })
                .ToListAsync();
        }

        // =====================================
        // Get Student Full Transcript
        // =====================================
        public async Task<StudentTranscriptSummaryDTO> GetStudentFullTranscriptAsync(string studentUserId)
        {
            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.User)
                .Include(s => s.AcademicPlan)
                .Include(s => s.Transcripts)
                    .ThenInclude(t => t.Course)
                .Include(s => s.Transcripts)
                    .ThenInclude(t => t.Section)
                        .ThenInclude(sec => sec.Slots)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section!)
                        .ThenInclude(sec => sec.Course)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section!)
                        .ThenInclude(sec => sec.Slots)
                .FirstOrDefaultAsync(s => s.UserId == studentUserId)
                ?? throw new Exception("Student not found.");

            return BuildStudentTranscriptSummary(student);
        }

        // =====================================
        // Get My Transcript (Student)
        // =====================================
        public async Task<StudentTranscriptSummaryDTO> GetMyTranscriptAsync(string userId)
        {
            return await GetStudentFullTranscriptAsync(userId);
        }

        // =====================================
        // Admin: Search Students (name/id/email)
        // =====================================
        public async Task<IEnumerable<StudentLookupDTO>> SearchStudentsAsync(string query, int take = 10)
        {
            var q = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q))
                return new List<StudentLookupDTO>();

            var canParseId = int.TryParse(q, out var studentId);

            // Note: keep this query simple and indexed-friendly.
            var baseQuery = unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .AsNoTracking()
                .Include(s => s.User)
                .Where(s => s.User != null);

            if (canParseId)
            {
                baseQuery = baseQuery.Where(s => s.StudentId == studentId);
            }
            else
            {
                var like = $"%{q}%";
                baseQuery = baseQuery.Where(s =>
                    EF.Functions.Like(s.User.FullName, like) ||
                    (s.User.Email != null && EF.Functions.Like(s.User.Email, like))
                );
            }

            return await baseQuery
                .OrderBy(s => s.StudentId)
                .Take(Math.Clamp(take, 1, 50))
                .Select(s => new StudentLookupDTO
                {
                    StudentUserId = s.UserId,
                    StudentId = s.StudentId,
                    FullName = s.User.FullName,
                    Email = s.User.Email ?? string.Empty
                })
                .ToListAsync();
        }

        // =====================================
        // Calculate Student GPA
        // =====================================
        public async Task<double> CalculateStudentGPAAsync(int studentId)
        {
            var transcripts = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Where(t => t.StudentId == studentId)
                .ToListAsync();

            if (!transcripts.Any())
                return 0.0;

            // GPA should follow configured retake policy and only count eligible grades.
            var attemptsForGpa = ApplyGpaPolicy(transcripts)
                .Where(t => GradeHelper.CountsTowardGpa(t.Grade))
                .ToList();

            var gpaCredits = attemptsForGpa.Sum(t => t.CreditHours);
            var qualityPoints = attemptsForGpa.Sum(t => t.GradePoints * t.CreditHours);

            return gpaCredits > 0 ? Math.Round(qualityPoints / gpaCredits, 2) : 0.0;
        }

        // =====================================
        // Calculate Semester GPA
        // =====================================
        public async Task<double> CalculateSemesterGPAAsync(int studentId, string semester, int year)
        {
            var transcripts = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Where(t => t.StudentId == studentId && t.Semester == semester && t.Year == year)
                .ToListAsync();

            if (!transcripts.Any())
                return 0.0;

            var attemptsForGpa = ApplyGpaPolicy(transcripts)
                .Where(t => GradeHelper.CountsTowardGpa(t.Grade))
                .ToList();

            var gpaCredits = attemptsForGpa.Sum(t => t.CreditHours);
            var qualityPoints = attemptsForGpa.Sum(t => t.GradePoints * t.CreditHours);

            return gpaCredits > 0 ? Math.Round(qualityPoints / gpaCredits, 2) : 0.0;
        }

        // =====================================
        // Calculate Simulated GPA (no persistence)
        // simulatedCourses can contain either replacements (TranscriptId) or new courses (CreditHours + Grade)
        // =====================================
        public async Task<double> CalculateSimulatedGPAAsync(int studentId, IEnumerable<SimulateCourseDTO> simulatedCourses)
        {
            var transcripts = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Where(t => t.StudentId == studentId)
                .ToListAsync();

            // Create a working list of (creditHours, gradePoints)
            var working = new List<(int CreditHours, double GradePoints)>();

            // Start with existing transcripts
            foreach (var t in transcripts)
            {
                working.Add((t.CreditHours, t.GradePoints));
            }

            // Apply simulations
            foreach (var sim in simulatedCourses ?? Enumerable.Empty<SimulateCourseDTO>())
            {
                if (string.IsNullOrWhiteSpace(sim.Grade) || !GradeHelper.IsValidGrade(sim.Grade))
                    continue; // ignore invalid entries

                var gp = GradeHelper.GetGradePoints(sim.Grade);

                if (sim.TranscriptId.HasValue)
                {
                    // replace existing transcript with same id
                    var idx = transcripts.FindIndex(x => x.TranscriptId == sim.TranscriptId.Value);
                    if (idx >= 0)
                    {
                        // replace in working list at same position
                        working[idx] = (transcripts[idx].CreditHours, gp);
                    }
                    else if (sim.CreditHours.HasValue)
                    {
                        // if transcript id not found, treat as new course
                        working.Add((sim.CreditHours.Value, gp));
                    }
                }
                else if (sim.CreditHours.HasValue)
                {
                    working.Add((sim.CreditHours.Value, gp));
                }
            }

            if (!working.Any())
                return 0.0;

            double totalQualityPoints = working.Sum(w => w.GradePoints * w.CreditHours);
            int totalCredits = working.Sum(w => w.CreditHours);

            return totalCredits > 0 ? Math.Round(totalQualityPoints / totalCredits, 2) : 0.0;
        }

        // =====================================
        // Recalculate And Update Student GPA
        // =====================================
        public async Task RecalculateAndUpdateStudentGPAAsync(int studentId)
        {
            var student = await unitOfWork.StudentProfiles.GetByIdAsync(studentId)
                ?? throw new Exception($"Student with ID {studentId} not found.");

            var transcripts = await unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Where(t => t.StudentId == studentId)
                .ToListAsync();

            if (!transcripts.Any())
            {
                student.GPA = 0.0;
                student.CompletedCredits = 0;
            }
            else
            {
                // Earned credits should not double-count retakes.
                var earnedCredits = transcripts
                    .GroupBy(t => t.CourseId)
                    .Sum(g => g.Any(a => GradeHelper.IsPassing(a.Grade)) ? g.First().CreditHours : 0);

                // GPA should follow configured retake policy.
                var attemptsForGpa = ApplyGpaPolicy(transcripts)
                    .Where(t => GradeHelper.CountsTowardGpa(t.Grade))
                    .ToList();

                var gpaCredits = attemptsForGpa.Sum(t => t.CreditHours);
                var qualityPoints = attemptsForGpa.Sum(t => t.GradePoints * t.CreditHours);

                student.GPA = gpaCredits > 0 ? Math.Round(qualityPoints / gpaCredits, 2) : 0.0;
                student.CompletedCredits = earnedCredits;
            }

            unitOfWork.StudentProfiles.Update(student);
            await unitOfWork.SaveChangesAsync();
        }

        // =====================================
        // Get All Transcripts (Admin)
        // =====================================
        public async Task<IEnumerable<ViewTranscriptDTO>> GetAllTranscriptsAsync(
            int? studentId = null,
            int? courseId = null,
            string? semester = null,
            int? year = null,
            string? grade = null)
        {
            var query = unitOfWork.Transcripts
                .GetAllAsQueryable()
                .Include(t => t.Student)
                    .ThenInclude(s => s.User)
                .Include(t => t.Course)
                .AsQueryable();

            if (studentId.HasValue)
                query = query.Where(t => t.StudentId == studentId.Value);

            if (courseId.HasValue)
                query = query.Where(t => t.CourseId == courseId.Value);

            if (!string.IsNullOrWhiteSpace(semester))
                query = query.Where(t => t.Semester == semester);

            if (year.HasValue)
                query = query.Where(t => t.Year == year.Value);

            if (!string.IsNullOrWhiteSpace(grade))
                query = query.Where(t => t.Grade == grade.ToUpper());

            return await query
                .OrderByDescending(t => t.Year)
                .ThenBy(t => t.Semester)
                .Select(t => new ViewTranscriptDTO
                {
                    TranscriptId = t.TranscriptId,
                    StudentId = t.StudentId,
                    StudentName = t.Student.User.FullName,
                    CourseId = t.CourseId,
                    CourseName = t.Course.CourseName,
                    CourseCode = t.Course.CourseCode,
                    Status = "COMPLETED",
                    CreditHours = t.CreditHours,
                    Grade = t.Grade,
                    GradePoints = t.GradePoints,
                    CountsTowardGpa = GradeHelper.CountsTowardGpa(t.Grade),
                    QualityPoints = GradeHelper.CountsTowardGpa(t.Grade)
                        ? (double?)(t.GradePoints * t.CreditHours)
                        : null,
                    Semester = t.Semester,
                    Year = t.Year,
                    CompletedAt = t.CompletedAt
                })
                .ToListAsync();
        }

        // =========================
        // Helper: Build Summary DTO
        // =========================
        private StudentTranscriptSummaryDTO BuildStudentTranscriptSummary(StudentProfile student)
        {
            var transcriptRows = new List<ViewTranscriptDTO>();
            var completedKeySet = new HashSet<(int SectionId, int CourseId)>();

            // Completed/graded records (source of truth for GPA)
            foreach (var t in student.Transcripts ?? Enumerable.Empty<Transcript>())
            {
                completedKeySet.Add((t.SectionId, t.CourseId));

                var countsTowardGpa = !string.IsNullOrWhiteSpace(t.Grade) && GradeHelper.CountsTowardGpa(t.Grade);
                var qualityPoints = countsTowardGpa ? (double?)(t.GradePoints * t.CreditHours) : null;

                transcriptRows.Add(new ViewTranscriptDTO
                {
                    TranscriptId = t.TranscriptId,
                    EnrollmentId = null,
                    StudentId = t.StudentId,
                    StudentName = student.User.FullName,
                    CourseId = t.CourseId,
                    CourseName = t.Course?.CourseName ?? string.Empty,
                    CourseCode = t.Course?.CourseCode ?? string.Empty,
                    SubType = BuildSectionSubType(t.Section),
                    Status = "COMPLETED",
                    CreditHours = t.CreditHours,
                    Grade = t.Grade,
                    GradePoints = t.GradePoints,
                    QualityPoints = qualityPoints,
                    CountsTowardGpa = countsTowardGpa,
                    Semester = t.Semester,
                    Year = t.Year,
                    CompletedAt = t.CompletedAt
                });
            }

            // In-progress / withdrawn records from enrollments
            foreach (var e in student.Enrollments ?? Enumerable.Empty<Enrollment>())
            {
                if (e.Section == null || e.Section.Course == null)
                    continue;

                // Avoid duplicates if enrollment already has a transcript record
                if (completedKeySet.Contains((e.SectionId, e.Section.CourseId)))
                    continue;

                if (e.Status != Status.Enrolled && e.Status != Status.Dropped && e.Status != Status.Completed)
                    continue;

                var semester = e.Section.Semester ?? string.Empty;
                var year = e.Section.Year.Year;
                var creditHours = e.Section.Course.CreditHours;

                var status = e.Status switch
                {
                    Status.Dropped => "WITHDRAWN",
                    Status.Completed => "COMPLETED",
                    _ => "IN_PROGRESS"
                };

                var grade = status == "WITHDRAWN" ? "W" : (e.Grade ?? string.Empty);
                var countsTowardGpa = status == "COMPLETED" && !string.IsNullOrWhiteSpace(grade) && GradeHelper.CountsTowardGpa(grade);
                var gradePoints = countsTowardGpa ? GradeHelper.GetGradePoints(grade) : 0.0;
                var qualityPoints = countsTowardGpa ? (double?)(gradePoints * creditHours) : null;

                transcriptRows.Add(new ViewTranscriptDTO
                {
                    TranscriptId = 0,
                    EnrollmentId = e.EnrollmentId,
                    StudentId = student.StudentId,
                    StudentName = student.User.FullName,
                    CourseId = e.Section.CourseId,
                    CourseName = e.Section.Course.CourseName,
                    CourseCode = e.Section.Course.CourseCode,
                    SubType = BuildSectionSubType(e.Section),
                    Status = status,
                    CreditHours = creditHours,
                    Grade = grade,
                    GradePoints = gradePoints,
                    QualityPoints = qualityPoints,
                    CountsTowardGpa = countsTowardGpa,
                    Semester = semester,
                    Year = year,
                    CompletedAt = status == "COMPLETED" ? e.ApprovedAt : null
                });
            }

            // Group rows by term
            var termGroups = transcriptRows
                .GroupBy(r => new { r.Year, r.Semester })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => SemesterSortKey(g.Key.Semester))
                .Select(g =>
                {
                    var attemptedCredits = g
                        .Where(r => r.Status == "COMPLETED" || r.Status == "IN_PROGRESS" || r.Status == "WITHDRAWN")
                        .Sum(r => r.CreditHours);

                    var earnedCredits = g
                        .Where(r => r.Status == "COMPLETED" && !string.IsNullOrWhiteSpace(r.Grade) && GradeHelper.IsPassing(r.Grade))
                        .Sum(r => r.CreditHours);

                    var transferCredits = g
                        .Where(r => r.Status == "COMPLETED" && !string.IsNullOrWhiteSpace(r.Grade) && GradeHelper.IsTransferCredit(r.Grade))
                        .Sum(r => r.CreditHours);

                    var gpaCredits = g
                        .Where(r => r.CountsTowardGpa)
                        .Sum(r => r.CreditHours);

                    var qualityPoints = g
                        .Where(r => r.CountsTowardGpa && r.QualityPoints.HasValue)
                        .Sum(r => r.QualityPoints!.Value);

                    var gpa = gpaCredits > 0 ? Math.Round(qualityPoints / gpaCredits, 2) : 0.0;

                    var summary = new TranscriptTermSummaryDTO
                    {
                        AttemptedCredits = attemptedCredits,
                        EarnedCredits = earnedCredits,
                        GpaCredits = gpaCredits,
                        TransferCredits = transferCredits,
                        QualityPoints = Math.Round(qualityPoints, 2),
                        GPA = gpa
                    };

                    var orderedCourses = g
                        .OrderBy(r => r.CourseCode)
                        .ThenBy(r => r.CourseName)
                        .ToList();

                    return new SemesterTranscriptDTO
                    {
                        Semester = g.Key.Semester,
                        Year = g.Key.Year,
                        TermName = BuildTermName(g.Key.Semester, g.Key.Year),
                        InstitutionName = string.IsNullOrWhiteSpace(institutionSettings.InstitutionName)
                            ? institutionSettings.UniversityName
                            : institutionSettings.InstitutionName,
                        SemesterGPA = summary.GPA,
                        SemesterCredits = summary.AttemptedCredits,
                        Summary = summary,
                        Courses = orderedCourses
                    };
                })
                .ToList();

            // Overall summary
            // - Attempted credits: reflect all attempts (including withdraw/in-progress)
            // - Earned credits: should not double-count retakes
            // - GPA: follows configured retake policy
            var overallAttemptedCredits = termGroups.Sum(tg => tg.Summary.AttemptedCredits);

            var completedRows = transcriptRows
                .Where(r => r.Status == "COMPLETED")
                .ToList();

            var overallEarnedCredits = completedRows
                .GroupBy(r => r.CourseId)
                .Sum(g => g.Any(a => !string.IsNullOrWhiteSpace(a.Grade) && GradeHelper.IsPassing(a.Grade))
                    ? g.First().CreditHours
                    : 0);

            var overallTransferCredits = completedRows
                .GroupBy(r => r.CourseId)
                .Sum(g => g.Any(a => !string.IsNullOrWhiteSpace(a.Grade) && GradeHelper.IsTransferCredit(a.Grade))
                    ? g.First().CreditHours
                    : 0);

            var gpaAttemptRows = ApplyGpaPolicy(completedRows)
                .Where(r => !string.IsNullOrWhiteSpace(r.Grade) && GradeHelper.CountsTowardGpa(r.Grade))
                .ToList();

            var overallGpaCredits = gpaAttemptRows.Sum(r => r.CreditHours);
            var overallQualityPoints = gpaAttemptRows
                .Where(r => r.QualityPoints.HasValue)
                .Sum(r => r.QualityPoints!.Value);
            var overallGpa = overallGpaCredits > 0 ? Math.Round(overallQualityPoints / overallGpaCredits, 2) : 0.0;

            var totalCreditsRequired = student.AcademicPlan?.TotalCreditsRequired ?? 0;
            var completionPercentage = totalCreditsRequired > 0
                ? Math.Round((double)overallEarnedCredits / totalCreditsRequired * 100, 2)
                : 0.0;

            return new StudentTranscriptSummaryDTO
            {
                Header = new TranscriptHeaderDTO
                {
                    TranscriptType = "Unofficial",
                    UniversityName = institutionSettings.UniversityName,
                    RegistrarOfficeName = institutionSettings.RegistrarOfficeName,
                    RegistrarAddressLines = institutionSettings.RegistrarAddressLines ?? new List<string>(),
                    StudentFullName = student.User.FullName,
                    StudentId = student.StudentId,
                    StudentEmail = student.User.Email ?? string.Empty,
                    ProgramOrDegree = student.AcademicPlan?.MajorName,
                    Curriculum = student.AcademicPlanId,
                    DegreeAwarded = null,
                    Honors = null,
                    PreviousInstitution = null
                },
                StudentId = student.StudentId,
                StudentName = student.User.FullName,
                Email = student.User.Email ?? string.Empty,
                MajorName = student.AcademicPlan?.MajorName ?? string.Empty,
                CumulativeGPA = overallGpa,
                TotalCreditsCompleted = overallEarnedCredits,
                TotalCreditsRequired = totalCreditsRequired,
                CompletionPercentage = completionPercentage,
                Semesters = termGroups,
                OverallSummary = new TranscriptOverallSummaryDTO
                {
                    AttemptedCredits = overallAttemptedCredits,
                    EarnedCredits = overallEarnedCredits,
                    GpaCredits = overallGpaCredits,
                    TransferCredits = overallTransferCredits,
                    QualityPoints = Math.Round(overallQualityPoints, 2),
                    GPA = overallGpa
                }
            };
        }
    }
}
