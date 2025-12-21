using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Auth;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;
using RegMan.Backend.BusinessLayer.DTOs.CartDTOs;
using RegMan.Backend.BusinessLayer.DTOs.EnrollmentDTOs;
using RegMan.Backend.DAL.Contracts;



namespace RegMan.Backend.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        // =========================
        // Withdraw Requests
        // =========================
        private static List<WithdrawRequestDTO> withdrawRequests = new();


        [HttpPost("students/{studentId}/withdraw-request")]
        public IActionResult SubmitWithdrawRequest(string studentId, [FromBody] WithdrawRequestDTO dto)
        {
            // Only allow during withdraw period
            if (withdrawStartDate == null || withdrawEndDate == null)
                return BadRequest(ApiResponse<string>.FailureResponse("Withdraw period not set", StatusCodes.Status400BadRequest));
            var now = DateTime.UtcNow.Date;
            if (now < withdrawStartDate.Value.Date || now > withdrawEndDate.Value.Date)
                return BadRequest(ApiResponse<string>.FailureResponse("Not in withdraw period", StatusCodes.Status400BadRequest));
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(ApiResponse<string>.FailureResponse("Reason required", StatusCodes.Status400BadRequest));
            withdrawRequests.Add(new WithdrawRequestDTO
            {
                StudentId = studentId,
                EnrollmentId = dto.EnrollmentId,
                Reason = dto.Reason,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            });
            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request submitted"));
        }

        [HttpGet("withdraw-requests")]
        public IActionResult GetWithdrawRequests()
        {
            return Ok(ApiResponse<List<WithdrawRequestDTO>>.SuccessResponse(withdrawRequests));
        }

        [HttpPost("withdraw-requests/{requestId}/approve")]
        public IActionResult ApproveWithdrawRequest(int requestId)
        {
            var req = withdrawRequests.FirstOrDefault(r => r.RequestId == requestId);
            if (req == null) return NotFound(ApiResponse<string>.FailureResponse("Request not found", StatusCodes.Status404NotFound));
            req.Status = "Approved";
            // TODO: Drop enrollment in DB
            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request approved"));
        }

        [HttpPost("withdraw-requests/{requestId}/deny")]
        public IActionResult DenyWithdrawRequest(int requestId)
        {
            var req = withdrawRequests.FirstOrDefault(r => r.RequestId == requestId);
            if (req == null) return NotFound(ApiResponse<string>.FailureResponse("Request not found", StatusCodes.Status404NotFound));
            req.Status = "Denied";
            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request denied"));
        }

        public class WithdrawRequestDTO
        {
            private static int _nextId = 1;
            public WithdrawRequestDTO() { RequestId = _nextId++; }
            public int RequestId { get; set; }
            public string StudentId { get; set; } = "";
            public int EnrollmentId { get; set; }
            public string Reason { get; set; } = "";
            public string Status { get; set; } = "Pending";
            public DateTime SubmittedAt { get; set; }
        }
        private readonly UserManager<BaseUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IAuditLogService auditLogService;
        private readonly ICartService cartService;
        private readonly IEnrollmentService enrollmentService;
        private readonly IUnitOfWork unitOfWork;



        public AdminController(
            UserManager<BaseUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditLogService auditLogService,
            ICartService cartService,
            IEnrollmentService enrollmentService,
            IUnitOfWork unitOfWork)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.auditLogService = auditLogService;
            this.cartService = cartService;
            this.enrollmentService = enrollmentService;
            this.unitOfWork = unitOfWork;
        }

        // =========================
        // Registration End Date (GET/SET)
        // =========================
        private static DateTime? registrationEndDate = null;
        private static DateTime? withdrawStartDate = null;
        private static DateTime? withdrawEndDate = null;

        // Removed GET endpoint for registration/withdraw dates from admin-only controller

        [HttpPost("registration-end-date")]
        public IActionResult SetRegistrationEndDate([FromBody] RegistrationEndDateDTO dto)
        {
            if (DateTime.TryParse(dto.RegistrationEndDate, out var regDate))
            {
                registrationEndDate = regDate;
                // Withdraw starts at registration end
                if (DateTime.TryParse(dto.WithdrawEndDate, out var withdrawEnd))
                {
                    withdrawStartDate = regDate;
                    withdrawEndDate = withdrawEnd;
                }
                return Ok(ApiResponse<string>.SuccessResponse("Registration and withdraw dates updated"));
            }
            return BadRequest(ApiResponse<string>.FailureResponse("Invalid date format", StatusCodes.Status400BadRequest));
        }

        public class RegistrationEndDateDTO
        {
            public string RegistrationEndDate { get; set; } = "";
            public string WithdrawEndDate { get; set; } = "";
        }
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
        // Admin can create users with any valid role
        // =========================
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDTO dto)
        {
            var validRoles = new[] { "Admin", "Student", "Instructor" };
            if (!validRoles.Contains(dto.Role))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid role. Valid roles are: Admin, Student, Instructor",
                    StatusCodes.Status400BadRequest
                ));
            }

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

            // Create role-specific profile
            if (dto.Role == "Student")
            {
                var defaultAcademicPlan = await unitOfWork.AcademicPlans.GetAllAsQueryable().FirstOrDefaultAsync();
                var studentProfile = new StudentProfile
                {
                    UserId = user.Id,
                    FamilyContact = dto.FamilyContact ?? "",
                    CompletedCredits = 0,
                    RegisteredCredits = 0,
                    GPA = 0.0,
                    AcademicPlanId = dto.AcademicPlanId ?? defaultAcademicPlan?.AcademicPlanId ?? "default"
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
            }
            else if (dto.Role == "Instructor")
            {
                var instructorProfile = new InstructorProfile
                {
                    UserId = user.Id,
                    Title = dto.Title ?? "Instructor",
                    Degree = dto.Degree ?? InstructorDegree.Lecturer,
                    Department = dto.Department ?? "General"
                };
                await unitOfWork.InstructorProfiles.AddAsync(instructorProfile);
                await unitOfWork.SaveChangesAsync();
            }
            else if (dto.Role == "Admin")
            {
                var adminProfile = new AdminProfile
                {
                    UserId = user.Id,
                    Title = dto.Title ?? "Administrator"
                };
                await unitOfWork.AdminProfiles.AddAsync(adminProfile);
                await unitOfWork.SaveChangesAsync();
            }

            // ===== Audit Log =====
            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(
                adminId,
                adminEmail,
                "CREATE",
                "User",
                user.Id
            );

            return Ok(ApiResponse<object>.SuccessResponse(
                new { UserId = user.Id, user.Email, user.FullName, user.Role },
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
        // Update User
        // =========================
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDTO dto)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status404NotFound
                ));

            // Update user properties
            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;

            if (!string.IsNullOrWhiteSpace(dto.Address))
                user.Address = dto.Address;

            if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
            {
                // Check if email already exists
                var existingUser = await userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null && existingUser.Id != id)
                {
                    return BadRequest(ApiResponse<string>.FailureResponse(
                        "Email already in use by another user",
                        StatusCodes.Status400BadRequest
                    ));
                }
                user.Email = dto.Email;
                user.UserName = dto.Email;
                user.NormalizedEmail = dto.Email.ToUpper();
                user.NormalizedUserName = dto.Email.ToUpper();
            }

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Failed to update user",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            // Audit Log
            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(adminId, adminEmail, "UPDATE", "User", id);

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.Address
            }, "User updated successfully"));
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

        // =========================
        // Get All Students
        // =========================
        [HttpGet("students")]
        public async Task<IActionResult> GetAllStudents(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = userManager.Users
                .Include(u => u.StudentProfile)
                .Where(u => u.Role == "Student");

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search)));
            }

            var totalItems = await query.CountAsync();
            var students = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.Address,
                    StudentProfile = u.StudentProfile == null ? null : new
                    {
                        u.StudentProfile.StudentId,
                        u.StudentProfile.CompletedCredits,
                        u.StudentProfile.RegisteredCredits,
                        u.StudentProfile.GPA
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Items = students,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            }));
        }

        // =========================
        // Get Student By Id
        // =========================
        [HttpGet("students/{id}")]
        public async Task<IActionResult> GetStudentById(string id)
        {
            var user = await userManager.Users
                .Include(u => u.StudentProfile)
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Student");

            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Student not found",
                    StatusCodes.Status404NotFound
                ));

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.Address,
                StudentProfile = user.StudentProfile == null ? null : new
                {
                    user.StudentProfile.StudentId,
                    user.StudentProfile.CompletedCredits,
                    user.StudentProfile.RegisteredCredits,
                    user.StudentProfile.GPA
                }
            }));
        }

        // =========================
        // Update Student
        // =========================
        [HttpPut("students/{id}")]
        public async Task<IActionResult> UpdateStudent(string id, [FromBody] UpdateStudentDTO dto)
        {
            var user = await userManager.Users
                .Include(u => u.StudentProfile)
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Student");

            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Student not found",
                    StatusCodes.Status404NotFound
                ));

            // Update user properties
            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;

            if (!string.IsNullOrWhiteSpace(dto.Address))
                user.Address = dto.Address;

            if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
            {
                var existingUser = await userManager.FindByEmailAsync(dto.Email);
                if (existingUser != null && existingUser.Id != id)
                {
                    return BadRequest(ApiResponse<string>.FailureResponse(
                        "Email already in use by another user",
                        StatusCodes.Status400BadRequest
                    ));
                }
                user.Email = dto.Email;
                user.UserName = dto.Email;
                user.NormalizedEmail = dto.Email.ToUpper();
                user.NormalizedUserName = dto.Email.ToUpper();
            }

            // Update student profile properties
            if (user.StudentProfile != null)
            {
                if (dto.GPA.HasValue)
                    user.StudentProfile.GPA = dto.GPA.Value;

                if (dto.CompletedCredits.HasValue)
                    user.StudentProfile.CompletedCredits = dto.CompletedCredits.Value;

                if (dto.RegisteredCredits.HasValue)
                    user.StudentProfile.RegisteredCredits = dto.RegisteredCredits.Value;
            }

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Failed to update student",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            // Save student profile changes
            await unitOfWork.SaveChangesAsync();

            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(adminId, adminEmail, "UPDATE", "Student", id);

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                user.Id,
                user.Email,
                user.FullName,
                user.Role,
                user.Address,
                StudentProfile = user.StudentProfile == null ? null : new
                {
                    user.StudentProfile.StudentId,
                    user.StudentProfile.CompletedCredits,
                    user.StudentProfile.RegisteredCredits,
                    user.StudentProfile.GPA
                }
            }, "Student updated successfully"));
        }

        // =========================
        // Delete Student
        // =========================
        [HttpDelete("students/{id}")]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            var user = await userManager.Users
                .Include(u => u.StudentProfile)
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Student");

            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Student not found",
                    StatusCodes.Status404NotFound
                ));

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(ApiResponse<object>.FailureResponse(
                    "Failed to delete student",
                    StatusCodes.Status400BadRequest,
                    result.Errors.Select(e => e.Description)
                ));
            }

            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(adminId, adminEmail, "DELETE", "Student", id);

            return Ok(ApiResponse<string>.SuccessResponse("Student deleted successfully"));
        }

        // =========================
        // Get All Enrollments
        // =========================
        [HttpGet("enrollments")]
        public async Task<IActionResult> GetAllEnrollments(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    (e.Student != null && e.Student.User != null && e.Student.User.FullName != null && e.Student.User.FullName.Contains(search)) ||
                    (e.Student != null && e.Student.User != null && e.Student.User.Email != null && e.Student.User.Email.Contains(search)) ||
                    (e.Section != null && e.Section.Course != null && e.Section.Course.CourseName.Contains(search)) ||
                    (e.Section != null && e.Section.Course != null && e.Section.Course.CourseCode.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Status>(status, out var statusEnum))
            {
                query = query.Where(e => e.Status == statusEnum);
            }

            var totalItems = await query.CountAsync();
            var enrollments = await query
                .OrderByDescending(e => e.EnrolledAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.EnrollmentId,
                    EnrollmentDate = e.EnrolledAt,
                    e.Grade,
                    Status = e.Status.ToString(),
                    Student = new
                    {
                        StudentId = e.Student != null ? e.Student.StudentId : 0,
                        FullName = e.Student != null && e.Student.User != null ? e.Student.User.FullName : "",
                        Email = e.Student != null && e.Student.User != null ? e.Student.User.Email : ""
                    },
                    Section = new
                    {
                        SectionId = e.Section != null ? e.Section.SectionId : 0,
                        SectionName = e.Section != null ? e.Section.SectionName : "",
                        Course = e.Section != null && e.Section.Course != null ? new
                        {
                            e.Section.Course.CourseId,
                            Code = e.Section.Course.CourseCode,
                            Name = e.Section.Course.CourseName
                        } : null
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Items = enrollments,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                CurrentPage = page,
                PageSize = pageSize
            }));
        }

        // =========================
        // Update User Role
        // =========================
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UpdateRoleDTO dto)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "User not found",
                    StatusCodes.Status404NotFound
                ));

            // Valid roles
            var validRoles = new[] { "Admin", "Student", "Instructor" };
            if (!validRoles.Contains(dto.NewRole))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid role. Valid roles are: Admin, Student, Instructor",
                    StatusCodes.Status400BadRequest
                ));
            }

            // Remove current roles
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);

            // Add new role
            await userManager.AddToRoleAsync(user, dto.NewRole);
            user.Role = dto.NewRole;
            await userManager.UpdateAsync(user);

            var (adminId, adminEmail) = GetAdminInfo();
            await auditLogService.LogAsync(adminId, adminEmail, "UPDATE_ROLE", "User", id);

            return Ok(ApiResponse<string>.SuccessResponse($"User role updated to {dto.NewRole}"));
        }

        // =========================
        // Get Student Enrollments
        // =========================
        [HttpGet("students/{studentId}/enrollments")]
        public async Task<IActionResult> GetStudentEnrollments(string studentId)
        {
            var enrollments = await enrollmentService.GetStudentEnrollmentsAsync(studentId);
            return Ok(ApiResponse<IEnumerable<ViewEnrollmentDTO>>.SuccessResponse(enrollments));
        }

    }

    public class UpdateRoleDTO
    {
        public string NewRole { get; set; } = null!;
    }

    public class UpdateUserDTO
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class UpdateStudentDTO
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public double? GPA { get; set; }
        public int? CompletedCredits { get; set; }
        public int? RegisteredCredits { get; set; }
    }
}
