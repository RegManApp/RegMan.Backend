using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.TranscriptDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GpaController : ControllerBase
    {
        private readonly ITranscriptService transcriptService;
        private readonly IUnitOfWork unitOfWork;

        public GpaController(ITranscriptService transcriptService, IUnitOfWork unitOfWork)
        {
            this.transcriptService = transcriptService;
            this.unitOfWork = unitOfWork;
        }

        // GET: /api/gpa/my - Get current student's GPA and enrollments
        [Authorize(Roles = "Student")]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyGPA()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", 401));

            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section)
                        .ThenInclude(sec => sec!.Course)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
                return NotFound(ApiResponse<string>.FailureResponse("Student profile not found", 404));

            var enrollments = student.Enrollments
                .Where(e => e.Section?.Course != null)
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.Grade,
                    GradePoints = e.Grade != null ? GradeHelper.GetGradePoints(e.Grade) : 0,
                    Status = e.Status.ToString(),
                    CourseName = e.Section!.Course.CourseName,
                    CourseCode = e.Section!.Course.CourseCode,
                    CreditHours = e.Section!.Course.CreditHours,
                    SectionName = e.Section!.SectionName,
                    Semester = e.Section!.Semester
                })
                .ToList();

            var completedEnrollments = enrollments.Where(e => e.Grade != null && e.Status == "Completed").ToList();
            var totalCredits = completedEnrollments.Sum(e => e.CreditHours);
            var totalGradePoints = completedEnrollments.Sum(e => e.GradePoints * e.CreditHours);
            var calculatedGPA = totalCredits > 0 ? Math.Round(totalGradePoints / totalCredits, 2) : 0;

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                StudentId = student.StudentId,
                StudentName = student.User.FullName,
                CurrentGPA = calculatedGPA,
                StoredGPA = student.GPA,
                CompletedCredits = student.CompletedCredits,
                RegisteredCredits = student.RegisteredCredits,
                Enrollments = enrollments
            }));
        }

        // GET: /api/gpa/student/{studentId} - Admin view student's GPA
        [Authorize(Roles = "Admin")]
        [HttpGet("student/{studentId}")]
        public async Task<IActionResult> GetStudentGPA(int studentId)
        {
            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.User)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section)
                        .ThenInclude(sec => sec!.Course)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                return NotFound(ApiResponse<string>.FailureResponse("Student not found", 404));

            var enrollments = student.Enrollments
                .Where(e => e.Section?.Course != null)
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.Grade,
                    GradePoints = e.Grade != null ? GradeHelper.GetGradePoints(e.Grade) : 0,
                    Status = e.Status.ToString(),
                    CourseName = e.Section!.Course.CourseName,
                    CourseCode = e.Section!.Course.CourseCode,
                    CreditHours = e.Section!.Course.CreditHours,
                    SectionName = e.Section!.SectionName,
                    Semester = e.Section!.Semester
                })
                .ToList();

            var completedEnrollments = enrollments.Where(e => e.Grade != null).ToList();
            var totalCredits = completedEnrollments.Sum(e => e.CreditHours);
            var totalGradePoints = completedEnrollments.Sum(e => e.GradePoints * e.CreditHours);
            var calculatedGPA = totalCredits > 0 ? Math.Round(totalGradePoints / totalCredits, 2) : 0;

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                StudentId = student.StudentId,
                StudentName = student.User.FullName,
                StudentEmail = student.User.Email,
                CurrentGPA = calculatedGPA,
                StoredGPA = student.GPA,
                CompletedCredits = student.CompletedCredits,
                RegisteredCredits = student.RegisteredCredits,
                Enrollments = enrollments
            }));
        }

        // PUT: /api/gpa/student/{studentId}/grade - Update a student's grade
        [Authorize(Roles = "Admin,Instructor")]
        [HttpPut("enrollment/{enrollmentId}/grade")]
        public async Task<IActionResult> UpdateGrade(int enrollmentId, [FromBody] UpdateGradeDTO dto)
        {
            var enrollment = await unitOfWork.Enrollments
                .GetAllAsQueryable()
                .Include(e => e.Student)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null)
                return NotFound(ApiResponse<string>.FailureResponse("Enrollment not found", 404));

            // Validate grade
            if (!GradeHelper.IsValidGrade(dto.Grade))
                return BadRequest(ApiResponse<string>.FailureResponse("Invalid grade. Use: A, A-, B+, B, B-, C+, C, C-, D+, D, F", 400));

            // Check if instructor can only grade their own sections
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.IsInRole("Instructor"))
            {
                var instructor = await unitOfWork.InstructorProfiles
                    .GetAllAsQueryable()
                    .FirstOrDefaultAsync(i => i.UserId == userId);

                if (instructor == null || enrollment.Section?.InstructorId != instructor.InstructorId)
                    return StatusCode(
                        StatusCodes.Status403Forbidden,
                        ApiResponse<string>.FailureResponse("Forbidden", StatusCodes.Status403Forbidden));
            }

            // Update the grade
            enrollment.Grade = dto.Grade.ToUpper();

            // Mark as completed if grade is passing
            if (GradeHelper.IsPassing(dto.Grade))
            {
                enrollment.Status = Status.Completed;
            }

            await unitOfWork.SaveChangesAsync();

            // Recalculate and update student's GPA
            double? newGpa = null;
            if (enrollment.Student != null)
            {
                newGpa = await RecalculateStudentGPA(enrollment.Student.StudentId);
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                enrollment.EnrollmentId,
                enrollment.Grade,
                GradePoints = GradeHelper.GetGradePoints(dto.Grade),
                Status = enrollment.Status.ToString(),
                NewGpa = newGpa
            }, "Grade updated successfully"));
        }

        private async Task<double?> RecalculateStudentGPA(int studentId)
        {
            var student = await unitOfWork.StudentProfiles
                .GetAllAsQueryable()
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Section)
                        .ThenInclude(sec => sec!.Course)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null) return null;

            var completedEnrollments = student.Enrollments
                .Where(e => e.Grade != null && e.Section?.Course != null)
                .ToList();

            var totalCredits = completedEnrollments.Sum(e => e.Section!.Course.CreditHours);
            var totalGradePoints = completedEnrollments.Sum(e =>
                GradeHelper.GetGradePoints(e.Grade!) * e.Section!.Course.CreditHours);

            student.GPA = totalCredits > 0 ? Math.Round(totalGradePoints / totalCredits, 2) : 0;
            student.CompletedCredits = completedEnrollments
                .Where(e => e.Status == Status.Completed)
                .Sum(e => e.Section!.Course.CreditHours);

            await unitOfWork.SaveChangesAsync();
            return student.GPA;
        }

        // POST: /api/gpa/simulate
        [HttpPost("simulate")]
        public async Task<IActionResult> SimulateGPAAsync([FromBody] SimulateGpaRequestDTO dto)
        {
            int? studentId = dto.StudentId;

            if (!studentId.HasValue)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId))
                    return BadRequest(ApiResponse<string>.FailureResponse("User not found.", 400));

                var student = await unitOfWork.StudentProfiles
                    .GetAllAsQueryable()
                    .FirstOrDefaultAsync(s => s.UserId == userId);
                studentId = student?.StudentId;
            }

            if (!studentId.HasValue || studentId.Value <= 0)
                return BadRequest(ApiResponse<string>.FailureResponse("StudentId is required.", 400));

            var currentGpa = await transcriptService.CalculateStudentGPAAsync(studentId.Value);
            var simulatedGpa = await transcriptService.CalculateSimulatedGPAAsync(studentId.Value, dto.SimulatedCourses);

            var response = new SimulateGpaResponseDTO
            {
                CurrentGPA = currentGpa,
                SimulatedGPA = simulatedGpa
            };

            return Ok(ApiResponse<SimulateGpaResponseDTO>.SuccessResponse(response, "Simulated GPA calculated"));
        }
    }

    public class UpdateGradeDTO
    {
        public string Grade { get; set; } = string.Empty;
    }
}
