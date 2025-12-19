using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize(Roles = "Instructor,Admin")]
    [ApiController]
    [Route("api/advising")]
    public class AdvisingController : ControllerBase
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly UserManager<BaseUser> userManager;
        private readonly IAuditLogService auditLogService;

        public AdvisingController(
            IUnitOfWork unitOfWork,
            UserManager<BaseUser> userManager,
            IAuditLogService auditLogService)
        {
            this.unitOfWork = unitOfWork;
            this.userManager = userManager;
            this.auditLogService = auditLogService;
        }

        private (string id, string email) GetUserInfo()
        {
            return (
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("User ID not found"),
                User.FindFirstValue(ClaimTypes.Email) ?? "unknown@user.com"
            );
        }

        // =========================
        // Get Pending Enrollments
        // =========================
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingEnrollments(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Instructor)
                        .ThenInclude(i => i!.User)
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .Where(e => e.Status == Status.Pending);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    (e.Student != null && e.Student.User != null && e.Student.User.FullName != null && e.Student.User.FullName.Contains(search)) ||
                    (e.Section != null && e.Section.Course != null && e.Section.Course.CourseName.Contains(search)) ||
                    (e.Section != null && e.Section.Course != null && e.Section.Course.CourseCode.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var enrollments = await query
                .OrderByDescending(e => e.EnrolledAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.EnrollmentId,
                    RequestDate = e.EnrolledAt,
                    Student = new
                    {
                        e.Student!.StudentId,
                        e.Student.User!.FullName,
                        e.Student.User.Email,
                        e.Student.CompletedCredits,
                        e.Student.GPA
                    },
                    Section = new
                    {
                        e.Section!.SectionId,
                        e.Section.SectionName,
                        Course = new
                        {
                            e.Section.Course!.CourseId,
                            e.Section.Course.CourseCode,
                            e.Section.Course.CourseName,
                            e.Section.Course.CreditHours
                        },
                        Instructor = e.Section.Instructor == null ? null : new
                        {
                            e.Section.Instructor.InstructorId,
                            e.Section.Instructor.User!.FullName
                        }
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Items = enrollments,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            }));
        }

        // =========================
        // Approve Enrollment
        // =========================
        [HttpPost("{enrollmentId}/approve")]
        public async Task<IActionResult> ApproveEnrollment(int enrollmentId)
        {
            var enrollment = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Enrollment not found",
                    StatusCodes.Status404NotFound));

            if (enrollment.Status != Status.Pending)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Only pending enrollments can be approved",
                    StatusCodes.Status400BadRequest));

            var (userId, userEmail) = GetUserInfo();

            enrollment.Status = Status.Enrolled;
            enrollment.ApprovedBy = userId;
            enrollment.ApprovedAt = DateTime.UtcNow;

            // Update student's registered credits
            if (enrollment.Student != null && enrollment.Section?.Course != null)
            {
                enrollment.Student.RegisteredCredits += enrollment.Section.Course.CreditHours;
            }

            unitOfWork.Enrollments.Update(enrollment);
            await unitOfWork.SaveChangesAsync();

            await auditLogService.LogAsync(userId, userEmail, "APPROVE", "Enrollment", enrollmentId.ToString());

            return Ok(ApiResponse<string>.SuccessResponse("Enrollment approved successfully"));
        }

        // =========================
        // Decline Enrollment
        // =========================
        [HttpPost("{enrollmentId}/decline")]
        public async Task<IActionResult> DeclineEnrollment(int enrollmentId, [FromBody] DeclineEnrollmentDTO dto)
        {
            var enrollment = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Enrollment not found",
                    StatusCodes.Status404NotFound));

            if (enrollment.Status != Status.Pending)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Only pending enrollments can be declined",
                    StatusCodes.Status400BadRequest));

            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Decline reason is required",
                    StatusCodes.Status400BadRequest));

            var (userId, userEmail) = GetUserInfo();

            enrollment.Status = Status.Declined;
            enrollment.DeclineReason = dto.Reason;
            enrollment.ApprovedBy = userId;
            enrollment.ApprovedAt = DateTime.UtcNow;

            unitOfWork.Enrollments.Update(enrollment);
            await unitOfWork.SaveChangesAsync();

            await auditLogService.LogAsync(userId, userEmail, "DECLINE", "Enrollment", enrollmentId.ToString());

            return Ok(ApiResponse<string>.SuccessResponse("Enrollment declined"));
        }

        // =========================
        // Get All Enrollments (for advisors)
        // =========================
        [HttpGet("enrollments")]
        public async Task<IActionResult> GetAllEnrollments(
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Status>(status, out var statusEnum))
            {
                query = query.Where(e => e.Status == statusEnum);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    (e.Student != null && e.Student.User != null && e.Student.User.FullName != null && e.Student.User.FullName.Contains(search)) ||
                    (e.Section != null && e.Section.Course != null && e.Section.Course.CourseName.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var enrollments = await query
                .OrderByDescending(e => e.EnrolledAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.EnrolledAt,
                    Status = e.Status.ToString(),
                    e.Grade,
                    e.DeclineReason,
                    e.ApprovedBy,
                    e.ApprovedAt,
                    Student = new
                    {
                        e.Student!.StudentId,
                        e.Student.User!.FullName,
                        e.Student.User.Email
                    },
                    Section = new
                    {
                        e.Section!.SectionId,
                        e.Section.SectionName,
                        Course = new
                        {
                            e.Section.Course!.CourseCode,
                            e.Section.Course.CourseName,
                            e.Section.Course.CreditHours
                        }
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Items = enrollments,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            }));
        }

        // =========================
        // Get Advising Stats
        // =========================
        [HttpGet("stats")]
        public async Task<IActionResult> GetAdvisingStats()
        {
            var pending = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Where(e => e.Status == Status.Pending)
                .CountAsync();

            var approved = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Where(e => e.Status == Status.Enrolled)
                .CountAsync();

            var declined = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Where(e => e.Status == Status.Declined)
                .CountAsync();

            var todayRequests = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Where(e => e.EnrolledAt.Date == DateTime.UtcNow.Date)
                .CountAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                PendingCount = pending,
                ApprovedCount = approved,
                DeclinedCount = declined,
                TodayRequestsCount = todayRequests
            }));
        }
    }

    public class DeclineEnrollmentDTO
    {
        public string Reason { get; set; } = null!;
    }
}
