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
    [Authorize(Roles ="Student")]
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService cartService;
        public CartController(ICartService cartService)
        {
            this.cartService = cartService;
        }
        private int GetStudentID()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim))
                throw new InvalidOperationException("User ID claim (NameIdentifier) is missing from the authorized token.");
            if (!int.TryParse(idClaim, out int studentId))
            {
                throw new InvalidOperationException($"The user ID claim '{idClaim}' is not in a valid integer format.");
            }
            return studentId;
        }
        // Add To Cart
        [HttpPost]
        public async Task<IActionResult> AddToCartAsync([FromQuery] int scheduleSlotId)
        {
            int userId = GetStudentID();
            await cartService.AddToCartAsync(userId, scheduleSlotId);
            return Ok(ApiResponse<string>
                    .SuccessResponse("Added to cart successfully"));
        }
        // Remove From Cart
        [HttpDelete("{cartItemId}")]
        public async Task<IActionResult> RemoveFromCartAsync(int cartItemId)
        {
            int userId = GetStudentID();            
            ViewCartDTO response = await cartService.RemoveFromCartAsync(userId, cartItemId);
            return Ok(ApiResponse<ViewCartDTO>.SuccessResponse(response));                       
        }
        [HttpGet]
        public async Task<IActionResult> ViewCartAsync()
        {
            int userId = GetStudentID();
            ViewCartDTO response = await cartService.ViewCartAsync(userId);
            return Ok(ApiResponse<ViewCartDTO>.SuccessResponse(response));
        }
    }
}
