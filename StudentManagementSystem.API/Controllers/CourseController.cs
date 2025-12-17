using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
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
        // Get All Courses (Summary)
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAllCoursesAsync(
            [FromQuery] string? courseName,
            [FromQuery] int? creditHours,
            [FromQuery] string? courseCode,
            [FromQuery] int? courseCategoryId)
        {
            var courses = await courseService.GetAllCoursesAsync(
                courseName,
                creditHours,
                courseCode,
                courseCategoryId
            );

            return Ok(
                ApiResponse<IEnumerable<ViewCourseSummaryDTO>>
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
