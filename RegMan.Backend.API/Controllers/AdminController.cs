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
        private static string FormatDate(DateTime? utcDate)
        {
            return utcDate?.ToString("yyyy-MM-dd") ?? "";
        }

        private static DateTime? ParseDateOrNull(string? date)
        {
            if (string.IsNullOrWhiteSpace(date)) return null;
            if (!DateTime.TryParse(date, out var local)) return null;
            return DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
        }

        // =========================
        // Withdraw Requests
        // =========================
        [HttpPost("students/{studentId}/withdraw-request")]
        public async Task<IActionResult> SubmitWithdrawRequest(string studentId, [FromBody] WithdrawRequestDTO dto)
        {
            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            if (settings?.WithdrawStartDateUtc == null || settings.WithdrawEndDateUtc == null)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Withdraw period not set",
                    StatusCodes.Status400BadRequest
                ));
            }

            var now = DateTime.UtcNow.Date;
            if (now < settings.WithdrawStartDateUtc.Value.Date || now > settings.WithdrawEndDateUtc.Value.Date)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Not in withdraw period",
                    StatusCodes.Status400BadRequest
                ));
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(ApiResponse<string>.FailureResponse("Reason required", StatusCodes.Status400BadRequest));

            var entity = new WithdrawRequest
            {
                StudentUserId = studentId,
                EnrollmentId = dto.EnrollmentId,
                Reason = dto.Reason.Trim(),
                Status = WithdrawRequestStatus.Pending,
                SubmittedAtUtc = DateTime.UtcNow
            };

            await unitOfWork.WithdrawRequests.AddAsync(entity);
            await unitOfWork.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request submitted"));
        }

        [HttpGet("withdraw-requests")]
        public async Task<IActionResult> GetWithdrawRequests()
        {
            var requests = await unitOfWork.WithdrawRequests.GetAllAsQueryable()
                .AsNoTracking()
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Select(r => new WithdrawRequestDTO
                {
                    RequestId = r.WithdrawRequestId,
                    StudentId = r.StudentUserId,
                    EnrollmentId = r.EnrollmentId,
                    Reason = r.Reason,
                    Status = r.Status.ToString(),
                    SubmittedAt = r.SubmittedAtUtc
                })
                .ToListAsync();

            return Ok(ApiResponse<List<WithdrawRequestDTO>>.SuccessResponse(requests));
        }

        [HttpPost("withdraw-requests/{requestId}/approve")]
        public async Task<IActionResult> ApproveWithdrawRequest(int requestId)
        {
            var req = await unitOfWork.WithdrawRequests.GetAllAsQueryable()
                .FirstOrDefaultAsync(r => r.WithdrawRequestId == requestId);
            if (req == null)
                return NotFound(ApiResponse<string>.FailureResponse("Request not found", StatusCodes.Status404NotFound));

            if (req.Status != WithdrawRequestStatus.Pending)
                return BadRequest(ApiResponse<string>.FailureResponse("Only pending requests can be approved", StatusCodes.Status400BadRequest));

            var enrollment = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Section)
                .FirstOrDefaultAsync(e => e.EnrollmentId == req.EnrollmentId);

            if (enrollment == null)
                return BadRequest(ApiResponse<string>.FailureResponse("Enrollment not found for this request", StatusCodes.Status400BadRequest));

            if (enrollment.Status != Status.Enrolled && enrollment.Status != Status.Pending)
                return BadRequest(ApiResponse<string>.FailureResponse("Enrollment is not active; cannot withdraw", StatusCodes.Status400BadRequest));

            req.Status = WithdrawRequestStatus.Approved;
            req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            req.ReviewedAtUtc = DateTime.UtcNow;

            // Drop enrollment in DB
            enrollment.Status = Status.Dropped;
            if (enrollment.Section != null)
                enrollment.Section.AvailableSeats++;

            await unitOfWork.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request approved"));
        }

        [HttpPost("withdraw-requests/{requestId}/deny")]
        public async Task<IActionResult> DenyWithdrawRequest(int requestId)
        {
            var req = await unitOfWork.WithdrawRequests.GetAllAsQueryable()
                .FirstOrDefaultAsync(r => r.WithdrawRequestId == requestId);
            if (req == null)
                return NotFound(ApiResponse<string>.FailureResponse("Request not found", StatusCodes.Status404NotFound));

            req.Status = WithdrawRequestStatus.Denied;
            req.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            req.ReviewedAtUtc = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Withdraw request denied"));
        }

        public class WithdrawRequestDTO
        {
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
        [HttpPost("registration-end-date")]
        public async Task<IActionResult> SetRegistrationEndDate([FromBody] RegistrationEndDateDTO dto)
        {
            if (!DateTime.TryParse(dto.RegistrationEndDate, out var regDateLocal) ||
                !DateTime.TryParse(dto.WithdrawEndDate, out var withdrawEndLocal))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid date format",
                    StatusCodes.Status400BadRequest
                ));
            }

            var registrationEndUtc = DateTime.SpecifyKind(regDateLocal.Date, DateTimeKind.Utc);
            var withdrawEndUtc = DateTime.SpecifyKind(withdrawEndLocal.Date, DateTimeKind.Utc);
            if (withdrawEndUtc < registrationEndUtc)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Withdraw end date must be on/after registration end date",
                    StatusCodes.Status400BadRequest
                ));
            }

            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            if (settings == null)
            {
                settings = new AcademicCalendarSettings { SettingsKey = "default" };
                await unitOfWork.AcademicCalendarSettings.AddAsync(settings);
            }

            settings.RegistrationEndDateUtc = registrationEndUtc;
            settings.WithdrawStartDateUtc = registrationEndUtc;
            settings.WithdrawEndDateUtc = withdrawEndUtc;
            settings.UpdatedAtUtc = DateTime.UtcNow;

            await unitOfWork.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Registration and withdraw dates updated"));
        }

        public class RegistrationEndDateDTO
        {
            public string RegistrationEndDate { get; set; } = "";
            public string WithdrawEndDate { get; set; } = "";
        }

        // =========================
        // Academic Calendar Settings (GET/SET)
        // Admin sets: registration open/close, withdraw start/end
        // =========================
        [HttpGet("academic-calendar-settings")]
        public async Task<IActionResult> GetAcademicCalendarSettings()
        {
            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            var payload = new
            {
                registrationStartDate = FormatDate(settings?.RegistrationStartDateUtc),
                registrationEndDate = FormatDate(settings?.RegistrationEndDateUtc),
                withdrawStartDate = FormatDate(settings?.WithdrawStartDateUtc),
                withdrawEndDate = FormatDate(settings?.WithdrawEndDateUtc),
                updatedAtUtc = settings?.UpdatedAtUtc
            };

            return Ok(ApiResponse<object>.SuccessResponse(payload));
        }

        public class AcademicCalendarSettingsDTO
        {
            public string RegistrationStartDate { get; set; } = "";
            public string RegistrationEndDate { get; set; } = "";
            public string WithdrawStartDate { get; set; } = "";
            public string WithdrawEndDate { get; set; } = "";
        }

        [HttpPut("academic-calendar-settings")]
        public async Task<IActionResult> SetAcademicCalendarSettings([FromBody] AcademicCalendarSettingsDTO dto)
        {
            var regStartUtc = ParseDateOrNull(dto.RegistrationStartDate);
            var regEndUtc = ParseDateOrNull(dto.RegistrationEndDate);
            var withdrawStartUtc = ParseDateOrNull(dto.WithdrawStartDate);
            var withdrawEndUtc = ParseDateOrNull(dto.WithdrawEndDate);

            if (regStartUtc == null || regEndUtc == null || withdrawStartUtc == null || withdrawEndUtc == null)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "All four dates are required (yyyy-MM-dd)",
                    StatusCodes.Status400BadRequest));
            }

            if (regEndUtc.Value.Date < regStartUtc.Value.Date)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Registration close date must be on/after registration open date",
                    StatusCodes.Status400BadRequest));

            if (withdrawEndUtc.Value.Date < withdrawStartUtc.Value.Date)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Withdraw end date must be on/after withdraw start date",
                    StatusCodes.Status400BadRequest));

            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            if (settings == null)
            {
                settings = new AcademicCalendarSettings { SettingsKey = "default" };
                await unitOfWork.AcademicCalendarSettings.AddAsync(settings);
            }

            settings.RegistrationStartDateUtc = regStartUtc.Value;
            settings.RegistrationEndDateUtc = regEndUtc.Value;
            settings.WithdrawStartDateUtc = withdrawStartUtc.Value;
            settings.WithdrawEndDateUtc = withdrawEndUtc.Value;
            settings.UpdatedAtUtc = DateTime.UtcNow;

            await unitOfWork.SaveChangesAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Academic calendar timeline updated"));
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
                    u.Address,
                    u.CreatedAt
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
                user.Address,
                user.CreatedAt
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
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid section id",
                    StatusCodes.Status400BadRequest
                ));

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
