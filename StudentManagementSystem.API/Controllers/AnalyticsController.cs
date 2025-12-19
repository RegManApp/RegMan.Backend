using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly UserManager<BaseUser> userManager;

        public AnalyticsController(IUnitOfWork unitOfWork, UserManager<BaseUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.userManager = userManager;
        }

        // =========================
        // Dashboard Overview
        // =========================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            // User counts
            var totalUsers = await userManager.Users.CountAsync();
            var totalStudents = await userManager.Users.Where(u => u.Role == "Student").CountAsync();
            var totalInstructors = await userManager.Users.Where(u => u.Role == "Instructor").CountAsync();
            var totalAdmins = await userManager.Users.Where(u => u.Role == "Admin").CountAsync();

            // Course counts
            var totalCourses = await unitOfWork.Courses.GetAllAsQueryable().CountAsync();
            var totalSections = await unitOfWork.Sections.GetAllAsQueryable().CountAsync();

            // Enrollment stats
            var enrollmentStats = await unitOfWork.Enrollments.GetAllAsQueryable()
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            var totalEnrollments = enrollmentStats.Sum(e => e.Count);
            var pendingEnrollments = enrollmentStats.FirstOrDefault(e => e.Status == "Pending")?.Count ?? 0;
            var activeEnrollments = enrollmentStats.FirstOrDefault(e => e.Status == "Enrolled")?.Count ?? 0;
            var declinedEnrollments = enrollmentStats.FirstOrDefault(e => e.Status == "Declined")?.Count ?? 0;

            // Room stats
            var totalRooms = await unitOfWork.Rooms.GetAllAsQueryable().CountAsync();

            // Academic Plan stats
            var totalAcademicPlans = await unitOfWork.AcademicPlans.GetAllAsQueryable().CountAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Users = new
                {
                    Total = totalUsers,
                    Students = totalStudents,
                    Instructors = totalInstructors,
                    Admins = totalAdmins
                },
                Courses = new
                {
                    Total = totalCourses,
                    Sections = totalSections
                },
                Enrollments = new
                {
                    Total = totalEnrollments,
                    Pending = pendingEnrollments,
                    Active = activeEnrollments,
                    Declined = declinedEnrollments,
                    ByStatus = enrollmentStats
                },
                Infrastructure = new
                {
                    Rooms = totalRooms,
                    AcademicPlans = totalAcademicPlans
                }
            }));
        }

        // =========================
        // Enrollment Trends (Last 30 days)
        // =========================
        [HttpGet("enrollment-trends")]
        public async Task<IActionResult> GetEnrollmentTrends()
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var trends = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Where(e => e.EnrolledAt >= thirtyDaysAgo)
                .GroupBy(e => e.EnrolledAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Pending = g.Count(e => e.Status == Status.Pending),
                    Approved = g.Count(e => e.Status == Status.Enrolled),
                    Declined = g.Count(e => e.Status == Status.Declined)
                })
                .OrderBy(t => t.Date)
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(trends));
        }

        // =========================
        // Course Statistics
        // =========================
        [HttpGet("course-stats")]
        public async Task<IActionResult> GetCourseStats()
        {
            var sections = await unitOfWork.Sections.GetAllAsQueryable()
                .Include(s => s.Course)
                .Include(s => s.Enrollments)
                .ToListAsync();

            var courseStats = sections
                .GroupBy(s => new { s.Course!.CourseId, s.Course.CourseCode, s.Course.CourseName, s.Course.CreditHours })
                .Select(g => new
                {
                    g.Key.CourseId,
                    g.Key.CourseCode,
                    g.Key.CourseName,
                    Credits = g.Key.CreditHours,
                    SectionCount = g.Count(),
                    TotalEnrollments = g.SelectMany(s => s.Enrollments).Count(),
                    ActiveEnrollments = g.SelectMany(s => s.Enrollments).Count(e => e.Status == Status.Enrolled),
                    PendingEnrollments = g.SelectMany(s => s.Enrollments).Count(e => e.Status == Status.Pending)
                })
                .OrderByDescending(c => c.TotalEnrollments)
                .Take(10)
                .ToList();

            return Ok(ApiResponse<object>.SuccessResponse(courseStats));
        }

        // =========================
        // GPA Distribution
        // =========================
        [HttpGet("gpa-distribution")]
        public async Task<IActionResult> GetGPADistribution()
        {
            var students = await userManager.Users
                .Include(u => u.StudentProfile)
                .Where(u => u.Role == "Student" && u.StudentProfile != null)
                .Select(u => u.StudentProfile!.GPA)
                .ToListAsync();

            var distribution = new
            {
                Excellent = students.Count(g => g >= 3.5),      // 3.5 - 4.0
                Good = students.Count(g => g >= 3.0 && g < 3.5), // 3.0 - 3.49
                Average = students.Count(g => g >= 2.5 && g < 3.0), // 2.5 - 2.99
                BelowAverage = students.Count(g => g >= 2.0 && g < 2.5), // 2.0 - 2.49
                AtRisk = students.Count(g => g < 2.0),           // < 2.0
                AverageGPA = students.Any() ? Math.Round(students.Average(), 2) : 0
            };

            return Ok(ApiResponse<object>.SuccessResponse(distribution));
        }

        // =========================
        // Credits Distribution
        // =========================
        [HttpGet("credits-distribution")]
        public async Task<IActionResult> GetCreditsDistribution()
        {
            var students = await userManager.Users
                .Include(u => u.StudentProfile)
                .Where(u => u.Role == "Student" && u.StudentProfile != null)
                .Select(u => u.StudentProfile!.CompletedCredits)
                .ToListAsync();

            var distribution = new
            {
                Freshman = students.Count(c => c < 30),      // 0-29 credits
                Sophomore = students.Count(c => c >= 30 && c < 60), // 30-59 credits
                Junior = students.Count(c => c >= 60 && c < 90),    // 60-89 credits
                Senior = students.Count(c => c >= 90),              // 90+ credits
                AverageCredits = students.Any() ? Math.Round(students.Average(), 1) : 0
            };

            return Ok(ApiResponse<object>.SuccessResponse(distribution));
        }

        // =========================
        // Instructor Statistics
        // =========================
        [HttpGet("instructor-stats")]
        public async Task<IActionResult> GetInstructorStats()
        {
            var sections = await unitOfWork.Sections.GetAllAsQueryable()
                .Include(s => s.Instructor)
                    .ThenInclude(i => i!.User)
                .Include(s => s.Enrollments)
                .Where(s => s.InstructorId != null)
                .ToListAsync();

            var instructorStats = sections
                .GroupBy(s => new
                {
                    s.Instructor!.InstructorId,
                    s.Instructor.User.FullName,
                    s.Instructor.Title,
                    s.Instructor.Degree,
                    s.Instructor.Department
                })
                .Select(g => new
                {
                    Id = g.Key.InstructorId,
                    g.Key.FullName,
                    g.Key.Title,
                    Degree = g.Key.Degree.ToString(),
                    g.Key.Department,
                    SectionsCount = g.Count(),
                    TotalStudents = g.SelectMany(s => s.Enrollments).Count(e => e.Status == Status.Enrolled)
                })
                .OrderByDescending(i => i.TotalStudents)
                .Take(10)
                .ToList();

            return Ok(ApiResponse<object>.SuccessResponse(instructorStats));
        }

        // =========================
        // Recent Activity
        // =========================
        [HttpGet("recent-activity")]
        public async Task<IActionResult> GetRecentActivity([FromQuery] int limit = 20)
        {
            var recentEnrollments = await unitOfWork.Enrollments.GetAllAsQueryable()
                .Include(e => e.Student)
                    .ThenInclude(s => s!.User)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .OrderByDescending(e => e.EnrolledAt)
                .Take(limit)
                .Select(e => new
                {
                    Type = "Enrollment",
                    e.EnrolledAt,
                    Description = $"{e.Student!.User!.FullName} - {e.Section!.Course!.CourseCode}",
                    Status = e.Status.ToString()
                })
                .ToListAsync();

            var recentUsers = await userManager.Users
                .OrderByDescending(u => u.Id)
                .Take(limit)
                .Select(u => new
                {
                    Type = "NewUser",
                    CreatedAt = DateTime.UtcNow, // Placeholder since we don't track creation date
                    Description = $"New {u.Role}: {u.FullName}",
                    Status = "Created"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                RecentEnrollments = recentEnrollments,
                RecentUsers = recentUsers
            }));
        }

        // =========================
        // Section Capacity Stats
        // =========================
        [HttpGet("section-capacity")]
        public async Task<IActionResult> GetSectionCapacityStats()
        {
            var sections = await unitOfWork.Sections.GetAllAsQueryable()
                .Include(s => s.Course)
                .Include(s => s.Enrollments)
                .Select(s => new
                {
                    s.SectionId,
                    s.SectionName,
                    Course = s.Course!.CourseCode,
                    Capacity = s.AvailableSeats,
                    Enrolled = s.Enrollments.Count(e => e.Status == Status.Enrolled),
                    Available = s.AvailableSeats - s.Enrollments.Count(e => e.Status == Status.Enrolled),
                    UtilizationPercent = s.AvailableSeats > 0
                        ? Math.Round((double)s.Enrollments.Count(e => e.Status == Status.Enrolled) / s.AvailableSeats * 100, 1)
                        : 0
                })
                .OrderByDescending(s => s.UtilizationPercent)
                .ToListAsync();

            var summary = new
            {
                TotalSections = sections.Count,
                FullSections = sections.Count(s => s.Available <= 0),
                AlmostFullSections = sections.Count(s => s.UtilizationPercent >= 80 && s.UtilizationPercent < 100),
                EmptySections = sections.Count(s => s.Enrolled == 0),
                AverageUtilization = sections.Any() ? Math.Round(sections.Average(s => s.UtilizationPercent), 1) : 0
            };

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Summary = summary,
                Sections = sections.Take(20)
            }));
        }
    }
}
