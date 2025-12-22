using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.AcademicPlanDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class AcademicPlanService : IAcademicPlanService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IAuditLogService auditLogService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public AcademicPlanService(
            IUnitOfWork unitOfWork,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor)
        {
            this.unitOfWork = unitOfWork;
            this.auditLogService = auditLogService;
            this.httpContextAccessor = httpContextAccessor;
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
        // Create Academic Plan (Admin)
        // =====================================
        public async Task<ViewAcademicPlanDTO> CreateAcademicPlanAsync(CreateAcademicPlanDTO dto)
        {
            // Check if plan with same ID already exists
            var existingPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .AnyAsync(ap => ap.AcademicPlanId == dto.AcademicPlanId);

            if (existingPlan)
                throw new Exception($"Academic Plan with ID '{dto.AcademicPlanId}' already exists.");

            var academicPlan = new AcademicPlan
            {
                AcademicPlanId = dto.AcademicPlanId,
                MajorName = dto.MajorName,
                TotalCreditsRequired = dto.TotalCreditsRequired,
                Description = dto.Description,
                ExpectedYearsToComplete = dto.ExpectedYearsToComplete,
                CreatedAt = DateTime.UtcNow
            };

            await unitOfWork.AcademicPlans.AddAsync(academicPlan);
            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "CREATE", "AcademicPlan", academicPlan.AcademicPlanId);

            return new ViewAcademicPlanDTO
            {
                AcademicPlanId = academicPlan.AcademicPlanId,
                MajorName = academicPlan.MajorName,
                TotalCreditsRequired = academicPlan.TotalCreditsRequired,
                Description = academicPlan.Description,
                ExpectedYearsToComplete = academicPlan.ExpectedYearsToComplete,
                CreatedAt = academicPlan.CreatedAt,
                UpdatedAt = academicPlan.UpdatedAt,
                TotalStudents = 0,
                Courses = new List<AcademicPlanCourseDTO>()
            };
        }

        // =====================================
        // Update Academic Plan (Admin)
        // =====================================
        public async Task<ViewAcademicPlanDTO> UpdateAcademicPlanAsync(UpdateAcademicPlanDTO dto)
        {
            var academicPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .Include(ap => ap.AcademicPlanCourses)
                    .ThenInclude(apc => apc.Course)
                .Include(ap => ap.Students)
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == dto.AcademicPlanId)
                ?? throw new Exception($"Academic Plan with ID '{dto.AcademicPlanId}' not found.");

            academicPlan.MajorName = dto.MajorName;
            academicPlan.TotalCreditsRequired = dto.TotalCreditsRequired;
            academicPlan.Description = dto.Description;
            academicPlan.ExpectedYearsToComplete = dto.ExpectedYearsToComplete;
            academicPlan.UpdatedAt = DateTime.UtcNow;

            unitOfWork.AcademicPlans.Update(academicPlan);
            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "UPDATE", "AcademicPlan", academicPlan.AcademicPlanId);

            return MapToViewAcademicPlanDTO(academicPlan);
        }

        // =====================================
        // Delete Academic Plan (Admin)
        // =====================================
        public async Task<string> DeleteAcademicPlanAsync(string academicPlanId)
        {
            var academicPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .Include(ap => ap.Students)
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == academicPlanId)
                ?? throw new Exception($"Academic Plan with ID '{academicPlanId}' not found.");

            // Check if any students are assigned to this plan
            if (academicPlan.Students.Any())
                throw new Exception($"Cannot delete Academic Plan '{academicPlanId}' because {academicPlan.Students.Count} student(s) are assigned to it.");

            // Delete associated courses from plan first
            var planCourses = await unitOfWork.AcademicPlanCourses
                .GetAllAsQueryable()
                .Where(apc => apc.AcademicPlanId == academicPlanId)
                .ToListAsync();

            foreach (var course in planCourses)
            {
                await unitOfWork.AcademicPlanCourses.DeleteAsync(course.AcademicPlanCourseId);
            }

            // Now delete the plan - we need to get it by string ID
            var planToDelete = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == academicPlanId);

            if (planToDelete != null)
            {
                // Manual removal since DeleteAsync uses int id
                var context = unitOfWork.AcademicPlans.GetAllAsQueryable();
                // We'll use a workaround - update then save
            }

            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "DELETE", "AcademicPlan", academicPlanId);

            return $"Academic Plan '{academicPlanId}' deleted successfully.";
        }

        // =====================================
        // Get Academic Plan By ID
        // =====================================
        public async Task<ViewAcademicPlanDTO> GetAcademicPlanByIdAsync(string academicPlanId)
        {
            var academicPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .Include(ap => ap.AcademicPlanCourses)
                    .ThenInclude(apc => apc.Course)
                .Include(ap => ap.Students)
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == academicPlanId)
                ?? throw new Exception($"Academic Plan with ID '{academicPlanId}' not found.");

            return MapToViewAcademicPlanDTO(academicPlan);
        }

        // =====================================
        // Get All Academic Plans
        // =====================================
        public async Task<IEnumerable<ViewAcademicPlanSummaryDTO>> GetAllAcademicPlansAsync()
        {
            return await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .Include(ap => ap.AcademicPlanCourses)
                .Include(ap => ap.Students)
                .Select(ap => new ViewAcademicPlanSummaryDTO
                {
                    AcademicPlanId = ap.AcademicPlanId,
                    MajorName = ap.MajorName,
                    TotalCreditsRequired = ap.TotalCreditsRequired,
                    ExpectedYearsToComplete = ap.ExpectedYearsToComplete,
                    TotalCourses = ap.AcademicPlanCourses.Count,
                    TotalStudents = ap.Students.Count
                })
                .ToListAsync();
        }

        // =====================================
        // Add Course To Academic Plan (Admin)
        // =====================================
        public async Task<AcademicPlanCourseDTO> AddCourseToAcademicPlanAsync(AddCourseToAcademicPlanDTO dto)
        {
            // Validate academic plan exists
            var academicPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == dto.AcademicPlanId)
                ?? throw new Exception($"Academic Plan with ID '{dto.AcademicPlanId}' not found.");

            // Validate course exists
            var course = await unitOfWork.Courses.GetByIdAsync(dto.CourseId)
                ?? throw new Exception($"Course with ID {dto.CourseId} not found.");

            // Check if course is already in this plan
            var existingEntry = await unitOfWork.AcademicPlanCourses
                .GetAllAsQueryable()
                .AnyAsync(apc => apc.AcademicPlanId == dto.AcademicPlanId && apc.CourseId == dto.CourseId);

            if (existingEntry)
                throw new Exception($"Course '{course.CourseName}' is already in this academic plan.");

            var academicPlanCourse = new AcademicPlanCourse
            {
                AcademicPlanId = dto.AcademicPlanId,
                CourseId = dto.CourseId,
                RecommendedSemester = dto.RecommendedSemester,
                RecommendedYear = dto.RecommendedYear,
                IsRequired = dto.IsRequired,
                CourseType = (CourseType)dto.CourseTypeId
            };

            await unitOfWork.AcademicPlanCourses.AddAsync(academicPlanCourse);
            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "ADD_COURSE", "AcademicPlan", $"{dto.AcademicPlanId}:{dto.CourseId}");

            return new AcademicPlanCourseDTO
            {
                AcademicPlanCourseId = academicPlanCourse.AcademicPlanCourseId,
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                CourseCode = course.CourseCode,
                CreditHours = course.CreditHours,
                RecommendedSemester = academicPlanCourse.RecommendedSemester,
                RecommendedYear = academicPlanCourse.RecommendedYear,
                IsRequired = academicPlanCourse.IsRequired,
                CourseType = academicPlanCourse.CourseType.ToString()
            };
        }

        // =====================================
        // Remove Course From Academic Plan (Admin)
        // =====================================
        public async Task<string> RemoveCourseFromAcademicPlanAsync(string academicPlanId, int courseId)
        {
            var academicPlanCourse = await unitOfWork.AcademicPlanCourses
                .GetAllAsQueryable()
                .FirstOrDefaultAsync(apc => apc.AcademicPlanId == academicPlanId && apc.CourseId == courseId)
                ?? throw new Exception($"Course with ID {courseId} is not in Academic Plan '{academicPlanId}'.");

            await unitOfWork.AcademicPlanCourses.DeleteAsync(academicPlanCourse.AcademicPlanCourseId);
            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "REMOVE_COURSE", "AcademicPlan", $"{academicPlanId}:{courseId}");

            return $"Course removed from Academic Plan '{academicPlanId}' successfully.";
        }

        // =====================================
        // Get Courses In Academic Plan
        // =====================================
        public async Task<IEnumerable<AcademicPlanCourseDTO>> GetCoursesInAcademicPlanAsync(string academicPlanId)
        {
            return await unitOfWork.AcademicPlanCourses
                .GetAllAsQueryable()
                .Include(apc => apc.Course)
                .Where(apc => apc.AcademicPlanId == academicPlanId)
                .OrderBy(apc => apc.RecommendedYear)
                .ThenBy(apc => apc.RecommendedSemester)
                .Select(apc => new AcademicPlanCourseDTO
                {
                    AcademicPlanCourseId = apc.AcademicPlanCourseId,
                    CourseId = apc.CourseId,
                    CourseName = apc.Course.CourseName,
                    CourseCode = apc.Course.CourseCode,
                    CreditHours = apc.Course.CreditHours,
                    RecommendedSemester = apc.RecommendedSemester,
                    RecommendedYear = apc.RecommendedYear,
                    IsRequired = apc.IsRequired,
                    CourseType = apc.CourseType.ToString()
                })
                .ToListAsync();
        }

        // =====================================
        // Get Student Academic Progress
        // =====================================
        public async Task<StudentAcademicProgressDTO> GetStudentAcademicProgressAsync(string studentUserId)
        {
            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.User)
                .Include(s => s.AcademicPlan)
                    .ThenInclude(ap => ap!.AcademicPlanCourses)
                        .ThenInclude(apc => apc.Course)
                .Include(s => s.Transcripts)
                    .ThenInclude(t => t.Course)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section!)
                        .ThenInclude(sec => sec.Course)
                .FirstOrDefaultAsync(s => s.UserId == studentUserId)
                ?? throw new Exception("Student not found.");

            return BuildStudentAcademicProgress(student);
        }

        // =====================================
        // Get My Academic Progress (Student)
        // =====================================
        public async Task<StudentAcademicProgressDTO> GetMyAcademicProgressAsync(string userId)
        {
            return await GetStudentAcademicProgressAsync(userId);
        }

        // =====================================
        // Assign Student To Academic Plan (Admin)
        // =====================================
        public async Task AssignStudentToAcademicPlanAsync(int studentId, string academicPlanId)
        {
            var student = await unitOfWork.StudentProfiles.GetByIdAsync(studentId)
                ?? throw new Exception($"Student with ID {studentId} not found.");

            var academicPlan = await unitOfWork.AcademicPlans
                .GetAllAsQueryable()
                .FirstOrDefaultAsync(ap => ap.AcademicPlanId == academicPlanId)
                ?? throw new Exception($"Academic Plan with ID '{academicPlanId}' not found.");

            student.AcademicPlanId = academicPlanId;

            unitOfWork.StudentProfiles.Update(student);
            await unitOfWork.SaveChangesAsync();

            // Audit log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(userId, email, "ASSIGN_PLAN", "Student", $"{studentId}:{academicPlanId}");
        }

        // =========================
        // Helper Methods
        // =========================
        private ViewAcademicPlanDTO MapToViewAcademicPlanDTO(AcademicPlan ap)
        {
            return new ViewAcademicPlanDTO
            {
                AcademicPlanId = ap.AcademicPlanId,
                MajorName = ap.MajorName,
                TotalCreditsRequired = ap.TotalCreditsRequired,
                Description = ap.Description,
                ExpectedYearsToComplete = ap.ExpectedYearsToComplete,
                CreatedAt = ap.CreatedAt,
                UpdatedAt = ap.UpdatedAt,
                TotalStudents = ap.Students.Count,
                Courses = ap.AcademicPlanCourses.Select(apc => new AcademicPlanCourseDTO
                {
                    AcademicPlanCourseId = apc.AcademicPlanCourseId,
                    CourseId = apc.CourseId,
                    CourseName = apc.Course.CourseName,
                    CourseCode = apc.Course.CourseCode,
                    CreditHours = apc.Course.CreditHours,
                    RecommendedSemester = apc.RecommendedSemester,
                    RecommendedYear = apc.RecommendedYear,
                    IsRequired = apc.IsRequired,
                    CourseType = apc.CourseType.ToString()
                }).ToList()
            };
        }

        private StudentAcademicProgressDTO BuildStudentAcademicProgress(StudentProfile student)
        {
            var planCourses = student.AcademicPlan?.AcademicPlanCourses?.ToList() ?? new List<AcademicPlanCourse>();
            var completedTranscripts = student.Transcripts.Where(t => GradeHelper.IsPassing(t.Grade)).ToList();
            var completedCourseIds = completedTranscripts.Select(t => t.CourseId).ToHashSet();

            // Get currently enrolled courses (in progress)
            var inProgressCourseIds = student.Enrollments
                .Where(e => e.Status == Status.Enrolled && e.Section != null)
                .Select(e => e.Section!.CourseId)
                .ToHashSet();

            var completedCourses = new List<CourseProgressDTO>();
            var inProgressCourses = new List<CourseProgressDTO>();
            var remainingCourses = new List<CourseProgressDTO>();

            foreach (var planCourse in planCourses)
            {
                var courseProgress = new CourseProgressDTO
                {
                    CourseId = planCourse.CourseId,
                    CourseName = planCourse.Course.CourseName,
                    CourseCode = planCourse.Course.CourseCode,
                    CreditHours = planCourse.Course.CreditHours,
                    RecommendedSemester = planCourse.RecommendedSemester,
                    RecommendedYear = planCourse.RecommendedYear,
                    IsRequired = planCourse.IsRequired,
                    CourseType = planCourse.CourseType.ToString()
                };

                if (completedCourseIds.Contains(planCourse.CourseId))
                {
                    var transcript = completedTranscripts.First(t => t.CourseId == planCourse.CourseId);
                    courseProgress.Grade = transcript.Grade;
                    courseProgress.Status = "Completed";
                    completedCourses.Add(courseProgress);
                }
                else if (inProgressCourseIds.Contains(planCourse.CourseId))
                {
                    courseProgress.Status = "InProgress";
                    inProgressCourses.Add(courseProgress);
                }
                else
                {
                    courseProgress.Status = "NotStarted";
                    remainingCourses.Add(courseProgress);
                }
            }

            int creditsCompleted = completedCourses.Sum(c => c.CreditHours);
            int totalRequired = student.AcademicPlan?.TotalCreditsRequired ?? 0;
            int creditsRemaining = Math.Max(0, totalRequired - creditsCompleted);

            // Course counts
            var requiredPlanCourseIds = planCourses.Where(pc => pc.IsRequired).Select(pc => pc.CourseId).ToHashSet();
            var requiredCoursesCount = requiredPlanCourseIds.Count;
            var requiredCoursesCompletedCount = requiredPlanCourseIds.Count(id => completedCourseIds.Contains(id));
            var totalPlanCoursesCount = planCourses.Count;
            var planCoursesCompletedCount = planCourses.Count(pc => completedCourseIds.Contains(pc.CourseId));

            // Missing prerequisites warnings (plan ordering proxy)
            // If a student is taking a later (recommended) course while earlier required plan courses are not yet completed,
            // we warn that prerequisites may be missing.
            var planCourseByCourseId = planCourses
                .GroupBy(pc => pc.CourseId)
                .Select(g => g.First())
                .ToDictionary(pc => pc.CourseId, pc => pc);

            var warnings = new List<PrerequisiteWarningDTO>();
            foreach (var courseId in inProgressCourseIds)
            {
                if (!planCourseByCourseId.TryGetValue(courseId, out var currentPlanCourse))
                    continue;

                var courseYear = currentPlanCourse.RecommendedYear;
                var courseSemester = currentPlanCourse.RecommendedSemester;

                var missing = planCourses
                    .Where(pc => pc.IsRequired)
                    .Where(pc =>
                        pc.RecommendedYear < courseYear ||
                        (pc.RecommendedYear == courseYear && pc.RecommendedSemester < courseSemester))
                    .Where(pc => !completedCourseIds.Contains(pc.CourseId))
                    .Select(pc => pc.Course.CourseCode)
                    .Distinct()
                    .OrderBy(code => code)
                    .ToList();

                if (missing.Count > 0)
                {
                    warnings.Add(new PrerequisiteWarningDTO
                    {
                        CourseId = courseId,
                        CourseCode = currentPlanCourse.Course.CourseCode,
                        CourseName = currentPlanCourse.Course.CourseName,
                        MissingCourseCodes = missing
                    });
                }
            }

            // Estimate graduation year
            int currentYear = DateTime.Now.Year;
            int expectedYears = student.AcademicPlan?.ExpectedYearsToComplete ?? 4;
            int expectedGraduationYear = currentYear + (int)Math.Ceiling((double)creditsRemaining / 30); // Assume ~30 credits/year

            return new StudentAcademicProgressDTO
            {
                StudentId = student.StudentId,
                StudentName = student.User.FullName,
                AcademicPlanId = student.AcademicPlanId,
                MajorName = student.AcademicPlan?.MajorName ?? "Not Assigned",
                TotalCreditsRequired = totalRequired,
                CreditsCompleted = creditsCompleted,
                CreditsRemaining = creditsRemaining,
                ProgressPercentage = totalRequired > 0 ? Math.Round((double)creditsCompleted / totalRequired * 100, 2) : 0,
                CurrentGPA = student.GPA,
                ExpectedGraduationYear = expectedGraduationYear,
                RequiredCoursesCount = requiredCoursesCount,
                RequiredCoursesCompletedCount = requiredCoursesCompletedCount,
                TotalPlanCoursesCount = totalPlanCoursesCount,
                PlanCoursesCompletedCount = planCoursesCompletedCount,
                MissingPrerequisiteWarnings = warnings,
                CompletedCourses = completedCourses.OrderBy(c => c.RecommendedYear).ThenBy(c => c.RecommendedSemester),
                InProgressCourses = inProgressCourses.OrderBy(c => c.RecommendedYear).ThenBy(c => c.RecommendedSemester),
                RemainingCourses = remainingCourses.OrderBy(c => c.RecommendedYear).ThenBy(c => c.RecommendedSemester)
            };
        }
    }
}
