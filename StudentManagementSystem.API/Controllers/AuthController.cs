using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs;
using StudentManagementSystem.BusinessLayer.Services;
using StudentManagementSystem.DAL.Contracts;
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
        private readonly IUnitOfWork unitOfWork;

        public AuthController(
            UserManager<BaseUser> userManager,
            SignInManager<BaseUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            TokenService tokenService,
            IAuditLogService auditLogService,
            IUnitOfWork unitOfWork)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.roleManager = roleManager;
            this.tokenService = tokenService;
            this.auditLogService = auditLogService;
            this.unitOfWork = unitOfWork;
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
        // Login
        // =========================
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDTO dto)
        {
            var user = await userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized("Invalid credentials");

            var valid = await userManager.CheckPasswordAsync(user, dto.Password);
            if (!valid)
                return Unauthorized("Invalid credentials");

            var roles = await userManager.GetRolesAsync(user);

            var accessToken = tokenService.GenerateAccessToken(user, roles);

            var refreshToken = tokenService.GenerateRefreshToken();
            var hashed = tokenService.HashToken(refreshToken);

            var refreshEntity = new RefreshToken
            {
                TokenHash = hashed,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Device = Request.Headers["User-Agent"].ToString()
            };

            await unitOfWork.RefreshTokens.AddAsync(refreshEntity);
            await unitOfWork.SaveChangesAsync();

            return Ok(new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }

        // =========================
        // Me
        // =========================
        [Authorize]
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

        // =========================
        // Refresh Token
        // =========================
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshTokenRequestDTO dto)
        {
            var hash = tokenService.HashToken(dto.RefreshToken);

            var storedToken = await unitOfWork.RefreshTokens
                .GetAllAsQueryable()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r =>
                    r.TokenHash == hash &&
                    !r.IsRevoked &&
                    r.ExpiresAt > DateTime.UtcNow);

            if (storedToken == null)
                return Unauthorized("Invalid refresh token");

            storedToken.IsRevoked = true;

            var roles = await userManager.GetRolesAsync(storedToken.User);

            var newAccess = tokenService.GenerateAccessToken(storedToken.User, roles);
            var newRefresh = tokenService.GenerateRefreshToken();

            await unitOfWork.RefreshTokens.AddAsync(new RefreshToken
            {
                TokenHash = tokenService.HashToken(newRefresh),
                UserId = storedToken.UserId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Device = storedToken.Device
            });

            await unitOfWork.SaveChangesAsync();

            return Ok(new LoginResponseDTO
            {
                AccessToken = newAccess,
                RefreshToken = newRefresh
            });
        }

        // =========================
        // Logout
        // =========================
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(RefreshTokenRequestDTO dto)
        {
            var hash = tokenService.HashToken(dto.RefreshToken);

            var token = await unitOfWork.RefreshTokens
                .GetAllAsQueryable()
                .FirstOrDefaultAsync(r => r.TokenHash == hash);

            if (token != null)
            {
                token.IsRevoked = true;
                await unitOfWork.SaveChangesAsync();
            }

            return Ok("Logged out successfully");
        }
    }
}
