using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.Auth;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<BaseUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IAuditLogService auditLogService;

        public AdminController(
            UserManager<BaseUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.auditLogService = auditLogService;
        }

        // =========================
        // Helper: Admin Info
        // =========================
        private (string adminId, string adminEmail) GetAdminInfo()
        {
            return (
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new Exception("Admin ID not found"),
                User.FindFirstValue(ClaimTypes.Email)
                    ?? "unknown@admin.com"
            );
        }

        // =========================
        // Create User
        // =========================
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            if (!await roleManager.RoleExistsAsync(dto.Role))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Role does not exist",
                    StatusCodes.Status400BadRequest
                ));
            }

            var user = new BaseUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email,
                Address = dto.Address,
                Role = dto.Role
            };

            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "User creation failed",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            await userManager.AddToRoleAsync(user, dto.Role);

            // ===== Audit Log =====
            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(
                adminId,
                adminEmail,
                "CREATE",
                "User",
                user.Id
            );

            return Ok(ApiResponse<string>.SuccessResponse(
                "User created successfully"
            ));
        }
    }
}
