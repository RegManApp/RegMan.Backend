using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/withdraw-requests")]
    public class WithdrawRequestsController : ControllerBase
    {
        private readonly IUnitOfWork unitOfWork;

        public WithdrawRequestsController(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        private string GetUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("User ID claim is missing from the token.");
            return userId;
        }

        public class CreateWithdrawRequestDTO
        {
            public int EnrollmentId { get; set; }
            public string Reason { get; set; } = "";
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWithdrawRequestDTO dto)
        {
            if (dto.EnrollmentId <= 0)
                return BadRequest(ApiResponse<string>.FailureResponse("EnrollmentId is required", StatusCodes.Status400BadRequest));

            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(ApiResponse<string>.FailureResponse("Reason is required", StatusCodes.Status400BadRequest));

            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            if (settings?.WithdrawStartDateUtc == null || settings.WithdrawEndDateUtc == null)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Withdraw period not set",
                    StatusCodes.Status400BadRequest));
            }

            var today = DateTime.UtcNow.Date;
            var start = settings.WithdrawStartDateUtc.Value.Date;
            var end = settings.WithdrawEndDateUtc.Value.Date;
            if (today < start || today > end)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    $"Not in withdraw period. Valid window: {start:yyyy-MM-dd} to {end:yyyy-MM-dd} (UTC).",
                    StatusCodes.Status400BadRequest));
            }

            var userId = GetUserId();

            var enrollment = await unitOfWork.Enrollments.GetAllAsQueryable()
                .AsNoTracking()
                .Include(e => e.Student)
                .FirstOrDefaultAsync(e => e.EnrollmentId == dto.EnrollmentId);

            if (enrollment == null)
                return NotFound(ApiResponse<string>.FailureResponse("Enrollment not found", StatusCodes.Status404NotFound));

            if (enrollment.Student?.UserId != userId)
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.FailureResponse("Forbidden", StatusCodes.Status403Forbidden));

            if (enrollment.Status != Status.Enrolled)
                return BadRequest(ApiResponse<string>.FailureResponse("Only enrolled courses can be withdrawn", StatusCodes.Status400BadRequest));

            var alreadyPending = await unitOfWork.WithdrawRequests.GetAllAsQueryable()
                .AsNoTracking()
                .AnyAsync(r => r.EnrollmentId == dto.EnrollmentId && r.StudentUserId == userId && r.Status == WithdrawRequestStatus.Pending);

            if (alreadyPending)
                return BadRequest(ApiResponse<string>.FailureResponse("A withdraw request for this enrollment is already pending", StatusCodes.Status400BadRequest));

            var entity = new WithdrawRequest
            {
                StudentUserId = userId,
                EnrollmentId = dto.EnrollmentId,
                Reason = dto.Reason.Trim(),
                Status = WithdrawRequestStatus.Pending,
                SubmittedAtUtc = DateTime.UtcNow
            };

            await unitOfWork.WithdrawRequests.AddAsync(entity);
            await unitOfWork.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request submitted"));
        }

        [HttpGet("my")]
        public async Task<IActionResult> MyRequests()
        {
            var userId = GetUserId();
            var requests = await unitOfWork.WithdrawRequests.GetAllAsQueryable()
                .AsNoTracking()
                .Where(r => r.StudentUserId == userId)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Select(r => new
                {
                    requestId = r.WithdrawRequestId,
                    enrollmentId = r.EnrollmentId,
                    reason = r.Reason,
                    status = r.Status.ToString(),
                    submittedAtUtc = r.SubmittedAtUtc,
                    reviewedAtUtc = r.ReviewedAtUtc
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(requests));
        }
    }
}
