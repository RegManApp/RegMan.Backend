using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.API.DTOs.DevTools;
using RegMan.Backend.API.Seeders;
using RegMan.Backend.BusinessLayer.DTOs.AuthDTOs;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using RegMan.Backend.DAL.Contracts;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DevToolsController : ControllerBase
    {
        private readonly IWebHostEnvironment env;
        private readonly AppDbContext db;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly UserManager<BaseUser> userManager;
        private readonly TokenService tokenService;
        private readonly IUnitOfWork unitOfWork;

        public DevToolsController(
            IWebHostEnvironment env,
            AppDbContext db,
            RoleManager<IdentityRole> roleManager,
            UserManager<BaseUser> userManager,
            TokenService tokenService,
            IUnitOfWork unitOfWork)
        {
            this.env = env;
            this.db = db;
            this.roleManager = roleManager;
            this.userManager = userManager;
            this.tokenService = tokenService;
            this.unitOfWork = unitOfWork;
        }

        private bool IsEnabled()
        {
            return env.IsDevelopment();
        }

        [HttpPost("seed")]
        [AllowAnonymous]
        public async Task<IActionResult> SeedDemoData()
        {
            if (!IsEnabled())
                return NotFound();

            await RoleSeeder.SeedRolesAsync(roleManager);
            await AcademicPlanSeeder.SeedDefaultAcademicPlanAsync(db);
            await AcademicCalendarSeeder.EnsureDefaultRowAsync(db);

            await DemoDataSeeder.SeedAsync(db, userManager, roleManager);

            var users = await DemoDataSeeder.GetDemoUsersAsync(userManager);
            return Ok(ApiResponse<SeedResultDto>.SuccessResponse(new SeedResultDto
            {
                Message = "Demo data seeded (idempotent).",
                Users = users
            }));
        }

        [HttpPost("reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetDatabaseAndSeedDemoData()
        {
            if (!IsEnabled())
                return NotFound();

            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            await RoleSeeder.SeedRolesAsync(roleManager);
            await AcademicPlanSeeder.SeedDefaultAcademicPlanAsync(db);
            await AcademicCalendarSeeder.EnsureDefaultRowAsync(db);
            await DemoDataSeeder.SeedAsync(db, userManager, roleManager);

            var users = await DemoDataSeeder.GetDemoUsersAsync(userManager);
            return Ok(ApiResponse<SeedResultDto>.SuccessResponse(new SeedResultDto
            {
                Message = "Database reset + demo data seeded.",
                Users = users
            }));
        }

        [HttpGet("users")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDemoUsers()
        {
            if (!IsEnabled())
                return NotFound();

            var users = await DemoDataSeeder.GetDemoUsersAsync(userManager);
            return Ok(ApiResponse<List<DemoUserInfoDto>>.SuccessResponse(users));
        }

        [HttpPost("login-as")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAs([FromBody] LoginAsRequestDto request)
        {
            if (!IsEnabled())
                return NotFound();

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Email is required",
                    StatusCodes.Status400BadRequest
                ));
            }

            var user = await userManager.Users
                .Include(u => u.InstructorProfile)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return NotFound(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status404NotFound
                ));
            }

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
    }
}
