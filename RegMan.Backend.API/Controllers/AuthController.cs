using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.AuthDTOs;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
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
        // Public registration is ALWAYS as Student
        // Admins create other roles via AdminController
        // =========================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            // Force role to Student for public registration - prevent role escalation
            const string allowedRole = "Student";

            if (!await roleManager.RoleExistsAsync(allowedRole))
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
                Role = allowedRole // Always Student for public registration
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

            await userManager.AddToRoleAsync(user, allowedRole);

            // Create StudentProfile and Cart for the new student
            var defaultAcademicPlan = await unitOfWork.AcademicPlans.GetAllAsQueryable().FirstOrDefaultAsync();
            var studentProfile = new StudentProfile
            {
                UserId = user.Id,
                FamilyContact = "",
                CompletedCredits = 0,
                RegisteredCredits = 0,
                GPA = 0.0,
                AcademicPlanId = defaultAcademicPlan?.AcademicPlanId ?? "default"
            };
            await unitOfWork.StudentProfiles.AddAsync(studentProfile);
            await unitOfWork.SaveChangesAsync();

            // Create Cart for student
            var cart = new Cart
            {
                StudentProfileId = studentProfile.StudentId
            };
            await unitOfWork.Carts.AddAsync(cart);
            await unitOfWork.SaveChangesAsync();

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
            var user = await userManager.Users
                .Include(u => u.InstructorProfile)
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Invalid credentials",
                    StatusCodes.Status401Unauthorized
                ));

            var valid = await userManager.CheckPasswordAsync(user, dto.Password);
            if (!valid)
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Invalid credentials",
                    StatusCodes.Status401Unauthorized
                ));

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

            return Ok(ApiResponse<LoginResponseDTO>.SuccessResponse(new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Email = user.Email!,
                FullName = user.FullName,
                Role = roles.FirstOrDefault() ?? user.Role,
                UserId = user.Id,
                InstructorTitle = user.InstructorProfile?.Title ?? null
            }));
        }

        // =========================
        // Change Password
        // =========================
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
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

            var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Password change failed",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            // Audit Log
            await auditLogService.LogAsync(
                user.Id,
                user.Email!,
                "UPDATE",
                "Password",
                user.Id
            );

            return Ok(ApiResponse<string>.SuccessResponse(
                "Password changed successfully"
            ));
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

            var user = await userManager.Users
                .Include(u => u.InstructorProfile)
                .Include(u => u.StudentProfile)
                    .ThenInclude(sp => sp!.AcademicPlan)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status401Unauthorized
                ));
            }

            // Build response based on role
            object? profileData = null;
            if (user.Role == "Student" && user.StudentProfile != null)
            {
                profileData = new
                {
                    StudentId = user.StudentProfile.StudentId,
                    CompletedCredits = user.StudentProfile.CompletedCredits,
                    RegisteredCredits = user.StudentProfile.RegisteredCredits,
                    GPA = user.StudentProfile.GPA,
                    FamilyContact = user.StudentProfile.FamilyContact,
                    AcademicPlan = user.StudentProfile.AcademicPlan != null ? new
                    {
                        user.StudentProfile.AcademicPlan.AcademicPlanId,
                        AcademicPlanName = user.StudentProfile.AcademicPlan.MajorName,
                        TotalCreditHours = user.StudentProfile.AcademicPlan.TotalCreditsRequired
                    } : null
                };
            }
            else if (user.Role == "Instructor" && user.InstructorProfile != null)
            {
                profileData = new
                {
                    InstructorId = user.InstructorProfile.InstructorId,
                    Title = user.InstructorProfile.Title,
                    Degree = user.InstructorProfile.Degree.ToString(),
                    Department = user.InstructorProfile.Department
                };
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.Address,
                InstructorTitle = user.InstructorProfile?.Title,
                Profile = profileData
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
