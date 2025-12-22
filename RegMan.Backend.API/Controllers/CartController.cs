using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.CartDTOs;
using RegMan.Backend.BusinessLayer.DTOs.CourseDTOs;
using RegMan.Backend.DAL.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Authorize(Roles = "Student")]
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService cartService;
        private readonly IEnrollmentService enrollmentService;
        private readonly IUnitOfWork unitOfWork;

        public CartController(ICartService cartService, IEnrollmentService enrollmentService, IUnitOfWork unitOfWork)
        {
            this.cartService = cartService;
            this.enrollmentService = enrollmentService;
            this.unitOfWork = unitOfWork;
        }

        private async Task<(bool ok, string message)> EnsureRegistrationOpenAsync()
        {
            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            if (settings?.RegistrationStartDateUtc == null || settings.RegistrationEndDateUtc == null)
                return (false, "Registration timeline is not configured yet.");

            var today = DateTime.UtcNow.Date;
            var start = settings.RegistrationStartDateUtc.Value.Date;
            var end = settings.RegistrationEndDateUtc.Value.Date;

            if (today < start)
                return (false, $"Registration is not open yet. Opens on {start:yyyy-MM-dd} (UTC).");

            if (today > end)
                return (false, $"Registration is closed. Closed on {end:yyyy-MM-dd} (UTC).");

            return (true, "");
        }
        private string GetStudentID()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(studentId))
                throw new InvalidOperationException("User ID claim (NameIdentifier) is missing from the authorized token.");
            return studentId;
        }
        // Add To Cart by Schedule Slot ID
        [HttpPost]
        public async Task<IActionResult> AddToCartAsync([FromQuery] int scheduleSlotId)
        {
            string userId = GetStudentID();

            var gate = await EnsureRegistrationOpenAsync();
            if (!gate.ok)
                return BadRequest(ApiResponse<string>.FailureResponse(gate.message, StatusCodes.Status400BadRequest));

            await cartService.AddToCartAsync(userId, scheduleSlotId);
            return Ok(ApiResponse<string>
                    .SuccessResponse("Added to cart successfully"));
        }

        // Add To Cart by Course ID (finds first available section/scheduleSlot)
        [HttpPost("by-course/{courseId}")]
        public async Task<IActionResult> AddToCartByCourseAsync(int courseId)
        {
            string userId = GetStudentID();

            var gate = await EnsureRegistrationOpenAsync();
            if (!gate.ok)
                return BadRequest(ApiResponse<string>.FailureResponse(gate.message, StatusCodes.Status400BadRequest));

            await cartService.AddToCartByCourseAsync(userId, courseId);
            return Ok(ApiResponse<string>
                    .SuccessResponse("Course added to cart successfully"));
        }
        // Remove From Cart
        [HttpDelete("{cartItemId}")]
        public async Task<IActionResult> RemoveFromCartAsync(int cartItemId)
        {
            string userId = GetStudentID();
            ViewCartDTO response = await cartService.RemoveFromCartAsync(userId, cartItemId);
            return Ok(ApiResponse<ViewCartDTO>.SuccessResponse(response));
        }
        [HttpGet]
        public async Task<IActionResult> ViewCartAsync()
        {
            string userId = GetStudentID();
            ViewCartDTO response = await cartService.ViewCartAsync(userId);
            return Ok(ApiResponse<ViewCartDTO>.SuccessResponse(response));
        }

        [HttpPost("enroll")]
        public async Task<IActionResult> EnrollFromCart()
        {
            string userId = GetStudentID();

            var gate = await EnsureRegistrationOpenAsync();
            if (!gate.ok)
                return BadRequest(ApiResponse<string>.FailureResponse(gate.message, StatusCodes.Status400BadRequest));

            await enrollmentService.EnrollFromCartAsync(userId);

            return Ok(ApiResponse<string>.SuccessResponse(
                "Enrollment completed successfully"
            ));
        }

        // Alias for enroll - checkout
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            string userId = GetStudentID();

            var gate = await EnsureRegistrationOpenAsync();
            if (!gate.ok)
                return BadRequest(ApiResponse<string>.FailureResponse(gate.message, StatusCodes.Status400BadRequest));

            await enrollmentService.EnrollFromCartAsync(userId);

            return Ok(ApiResponse<string>.SuccessResponse(
                "Checkout completed successfully"
            ));
        }

        // Get current student's enrollments
        [HttpGet("my-enrollments")]
        public async Task<IActionResult> GetMyEnrollments()
        {
            string userId = GetStudentID();
            var enrollments = await enrollmentService.GetStudentEnrollmentsAsync(userId);
            return Ok(ApiResponse<object>.SuccessResponse(enrollments));
        }

    }
}
