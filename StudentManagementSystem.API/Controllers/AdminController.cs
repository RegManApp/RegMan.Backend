using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.Auth;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;
using StudentManagementSystem.BusinessLayer.DTOs.CartDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.EnrollmentDTOs;



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
        private readonly ICartService cartService;
        private readonly IEnrollmentService enrollmentService;



        public AdminController(
            UserManager<BaseUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService, ICartService cartService, IEnrollmentService enrollmentService)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.auditLogService = auditLogService;
            this.cartService = cartService;
            this.enrollmentService = enrollmentService;


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

        // =========================
        // Get Dashboard Stats
        // =========================
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var allUsers = userManager.Users;
            var totalUsers = await allUsers.CountAsync();
            var totalStudents = await allUsers.CountAsync(u => u.Role == "Student");
            var totalInstructors = await allUsers.CountAsync(u => u.Role == "Instructor");
            var totalAdmins = await allUsers.CountAsync(u => u.Role == "Admin");

            // Get course count from enrollments or courses
            var totalEnrollments = await enrollmentService.CountAllAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                TotalUsers = totalUsers,
                TotalStudents = totalStudents,
                TotalInstructors = totalInstructors,
                TotalAdmins = totalAdmins,
                TotalEnrollments = totalEnrollments
            }));
        }

        // =========================
        // Get All Users
        // =========================
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? email,
            [FromQuery] string? role,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(u => u.Email != null && u.Email.Contains(email));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);

            var totalItems = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.Email)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.Address
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Items = users,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                CurrentPage = pageNumber,
                PageSize = pageSize
            }));
        }

        // =========================
        // Get User By Id
        // =========================
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status404NotFound
                ));

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.Address
            }));
        }

        // =========================
        // Delete User
        // =========================
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status404NotFound
                ));

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Failed to delete user",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            // Audit Log
            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(
                adminId,
                adminEmail,
                "DELETE",
                "User",
                id
            );

            return Ok(ApiResponse<string>.SuccessResponse("User deleted successfully"));
        }

        [HttpGet("students/{studentId}/cart")]
        public async Task<IActionResult> ViewStudentCart(string studentId)
        {
            var cart = await cartService.ViewCartAsync(studentId);

            return Ok(ApiResponse<ViewCartDTO>.SuccessResponse(cart));
        }

        [HttpPost("students/{studentId}/force-enroll")]
        public async Task<IActionResult> ForceEnroll(
            string studentId,
            [FromBody] ForceEnrollDTO dto)
        {
            if (dto.SectionId <= 0)
                return BadRequest("Invalid section id");

            await enrollmentService.ForceEnrollAsync(studentId, dto.SectionId);

            return Ok(ApiResponse<string>.SuccessResponse(
                "Student enrolled successfully (forced)"
            ));
        }



    }
}
