using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs;
using StudentManagementSystem.BusinessLayer.Services;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<BaseUser> userManager;
        private readonly SignInManager<BaseUser> signInManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly TokenService tokenService;
        private readonly IAuditLogService auditLogService;

        public AuthController(
            UserManager<BaseUser> userManager,
            SignInManager<BaseUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            TokenService tokenService,
            IAuditLogService auditLogService)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this.tokenService = tokenService;
            this.auditLogService = auditLogService;
        }

        // =========================
        // Register (AUDIT LOG)
        // =========================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
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
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
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
            await auditLogService.LogAsync(
                user.Id,
                user.Email!,
                "CREATE",
                "User",
                user.Id
            );

            return Ok(ApiResponse<string>.SuccessResponse(
                "User registered successfully"
            ));
        }

        // =========================
        // Login (NO AUDIT LOG)
        // =========================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Invalid credentials",
                    StatusCodes.Status401Unauthorized
                ));
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Invalid credentials",
                    StatusCodes.Status401Unauthorized
                ));
            }

            var roles = await userManager.GetRolesAsync(user);
            var token = tokenService.GenerateToken(user, roles);

            return Ok(ApiResponse<string>.SuccessResponse(token));
        }

        // =========================
        // Me
        // =========================
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Unauthorized",
                    StatusCodes.Status401Unauthorized
                ));
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status401Unauthorized
                ));
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.FullName,
                user.Email,
                user.Role
            }));
        }
    }
}
