using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class CourseService : ICourseService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<Course> courseRepository;
        private readonly IAuditLogService auditLogService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public CourseService(
            IUnitOfWork unitOfWork,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor)
        {
            this.unitOfWork = unitOfWork;
            this.courseRepository = unitOfWork.Courses;
            this.auditLogService = auditLogService;
            this.httpContextAccessor = httpContextAccessor;
        }

        // =========================
        // Helpers
        // =========================
        private (string userId, string email) GetUserInfo()
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new Exception("User context not found.");

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new Exception("UserId not found.");

            var email = user.FindFirstValue(ClaimTypes.Email)
                ?? "unknown@email.com";

            return (userId, email);
        }

        // =========================
        // Create
        // =========================
        public async Task<ViewCourseDetailsDTO> CreateCourseAsync(CreateCourseDTO courseDTO)
        {
            if (courseDTO == null)
                throw new ArgumentNullException(nameof(courseDTO));

            var course = new Course
            {
                CourseName = courseDTO.CourseName,
                CreditHours = courseDTO.CreditHours,
                CourseCode = courseDTO.CourseCode,
                CourseCategory = (CourseCategory)courseDTO.CourseCategoryId,
                Description = courseDTO.Description
            };

            await unitOfWork.Courses.AddAsync(course);
            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "CREATE",
                "Course",
                course.CourseId.ToString()
            );

            return new ViewCourseDetailsDTO
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                CreditHours = course.CreditHours,
                Description = course.Description,
                CourseCode = course.CourseCode,
                CourseCategoryId = (int)course.CourseCategory,
                CourseCategoryName = course.CourseCategory.ToString()
            };
        }

        // =========================
        // Delete (FIXED)
        // =========================
        public async Task<string> DeleteCourseAsync(int id)
        {
            bool deleted = await unitOfWork.Courses.DeleteAsync(id);

            if (!deleted)
                throw new Exception($"Course with ID {id} not found.");

            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "DELETE",
                "Course",
                id.ToString()
            );

            return $"Course with ID {id} deleted successfully.";
        }

        // =========================
        // Get All
        // =========================
        public async Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync(
            string? courseName,
            int? creditHours,
            string? courseCode,
            int? courseCategoryId)
        {
            var query = unitOfWork.Courses.GetAllAsQueryable();

            if (!string.IsNullOrWhiteSpace(courseName))
                query = query.Where(c => c.CourseName.Contains(courseName));

            if (creditHours.HasValue)
                query = query.Where(c => c.CreditHours == creditHours.Value);

            if (!string.IsNullOrWhiteSpace(courseCode))
                query = query.Where(c => c.CourseCode.Contains(courseCode));

            if (courseCategoryId.HasValue)
                query = query.Where(c => (int)c.CourseCategory == courseCategoryId.Value);

            return await query
                .Select(c => new ViewCourseSummaryDTO
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    CreditHours = c.CreditHours,
                    CourseCode = c.CourseCode
                })
                .ToListAsync();
        }

        // =========================
        // Get Summary By Id
        // =========================
        public async Task<ViewCourseSummaryDTO> GetCourseSummaryByIdAsync(int id)
        {
            Expression<Func<Course, bool>> filter = c => c.CourseId == id;

            Expression<Func<Course, ViewCourseSummaryDTO>> projection = c =>
                new ViewCourseSummaryDTO
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    CreditHours = c.CreditHours,
                    CourseCode = c.CourseCode
                };

            var query = courseRepository.GetFilteredAndProjected(filter, projection);
            var result = await query.SingleOrDefaultAsync();

            if (result == null)
                throw new Exception($"Course with ID: {id} not found.");

            return result;
        }

        // =========================
        // Get Details By Id
        // =========================
        public async Task<ViewCourseDetailsDTO> GetCourseByIdAsync(int id)
        {
            Expression<Func<Course, bool>> filter = c => c.CourseId == id;

            Expression<Func<Course, ViewCourseDetailsDTO>> projection = c =>
                new ViewCourseDetailsDTO
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    CreditHours = c.CreditHours,
                    Description = c.Description,
                    CourseCode = c.CourseCode,
                    CourseCategoryId = (int)c.CourseCategory,
                    CourseCategoryName = c.CourseCategory.ToString()
                };

            var query = courseRepository.GetFilteredAndProjected(filter, projection);
            var result = await query.SingleOrDefaultAsync();

            if (result == null)
                throw new Exception($"Course with ID: {id} not found.");

            return result;
        }

        // =========================
        // Update
        // =========================
        public async Task<ViewCourseDetailsDTO> UpdateCourseAsync(UpdateCourseDTO courseDTO)
        {
            if (courseDTO == null)
                throw new ArgumentNullException(nameof(courseDTO));

            var existingCourse = await unitOfWork.Courses.GetByIdAsync(courseDTO.CourseId);
            if (existingCourse == null)
                throw new Exception($"Course with ID {courseDTO.CourseId} not found.");

            existingCourse.CreditHours = courseDTO.CreditHours;
            existingCourse.CourseCode = courseDTO.CourseCode;
            existingCourse.CourseCategory = (CourseCategory)courseDTO.CourseCategoryId;
            existingCourse.Description = courseDTO.Description;

            unitOfWork.Courses.Update(existingCourse);
            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "UPDATE",
                "Course",
                existingCourse.CourseId.ToString()
            );

            return new ViewCourseDetailsDTO
            {
                CourseId = existingCourse.CourseId,
                CourseName = existingCourse.CourseName,
                CreditHours = existingCourse.CreditHours,
                Description = existingCourse.Description,
                CourseCode = existingCourse.CourseCode,
                CourseCategoryId = (int)existingCourse.CourseCategory,
                CourseCategoryName = existingCourse.CourseCategory.ToString()
            };
        }
    }
}
