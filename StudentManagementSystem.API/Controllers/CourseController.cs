using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.Common;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // أي request لازم يكون authenticated
    public class CourseController : ControllerBase
    {
        private readonly ICourseService courseService;

        public CourseController(ICourseService courseService)
        {
            this.courseService = courseService;
        }

        // =========================
        // Get Course Details By Id
        // Admin + Instructor
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourseByIdAsync(int id)
        {
            var course = await courseService.GetCourseByIdAsync(id);

            return Ok(
                ApiResponse<ViewCourseDetailsDTO>
                    .SuccessResponse(course)
            );
        }

        // =========================
        // Get All Courses (Summary) - Paginated
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAllCoursesAsync(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? search = null,
            [FromQuery] string? courseName = null,
            [FromQuery] int? creditHours = null,
            [FromQuery] string? courseCode = null,
            [FromQuery] int? courseCategoryId = null)
        {
            var courses = await courseService.GetAllCoursesPaginatedAsync(
                page,
                pageSize,
                search,
                courseName,
                creditHours,
                courseCode,
                courseCategoryId
            );

            return Ok(
                ApiResponse<PaginatedResponse<ViewCourseSummaryDTO>>
                    .SuccessResponse(courses)
            );
        }

        // =========================
        // Create Course
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateCourseAsync(
            [FromBody] CreateCourseDTO courseDTO)
        {
            var createdCourse = await courseService.CreateCourseAsync(courseDTO);

            return Ok(
                ApiResponse<ViewCourseDetailsDTO>
                    .SuccessResponse(createdCourse, "Course created successfully")
            );
        }

        // =========================
        // Update Course
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> UpdateCourseAsync(
            [FromBody] UpdateCourseDTO courseDTO)
        {
            var updatedCourse = await courseService.UpdateCourseAsync(courseDTO);

            return Ok(
                ApiResponse<ViewCourseDetailsDTO>
                    .SuccessResponse(updatedCourse, "Course updated successfully")
            );
        }

        // =========================
        // Delete Course
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourseAsync(int id)
        {
            await courseService.DeleteCourseAsync(id);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse("Course deleted successfully")
            );
        }
    }
}
