using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CartDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize(Roles = "Student")]
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService cartService;
        private readonly IEnrollmentService enrollmentService;
        public CartController(ICartService cartService, IEnrollmentService enrollmentService)
        {
            this.cartService = cartService;
            this.enrollmentService = enrollmentService;
        }
        private string GetStudentID()
        {
            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(studentId))
                throw new InvalidOperationException("User ID claim (NameIdentifier) is missing from the authorized token.");
            return studentId;
        }
        // Add To Cart
        [HttpPost]
        public async Task<IActionResult> AddToCartAsync([FromQuery] int scheduleSlotId)
        {
            string userId = GetStudentID();
            await cartService.AddToCartAsync(userId, scheduleSlotId);
            return Ok(ApiResponse<string>
                    .SuccessResponse("Added to cart successfully"));
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
