using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseController : ControllerBase
    {
        private readonly ICourseService courseService;

        public CourseController(ICourseService courseService)
        {
            this.courseService = courseService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourseByIdAsync(int id)
        {
            var course = await courseService.GetCourseByIdAsync(id);
            return Ok(course);
        }

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

            return Ok(courses);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCourseAsync(
            [FromBody] CreateCourseDTO courseDTO)
        {
            var createdCourse = await courseService.CreateCourseAsync(courseDTO);
            return Ok(createdCourse);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourseAsync(int id)
        {
            var result = await courseService.DeleteCourseAsync(id);
            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateCourseAsync(
            [FromBody] UpdateCourseDTO courseDTO)
        {
            var updatedCourse = await courseService.UpdateCourseAsync(courseDTO);
            return Ok(updatedCourse);
        }
    }
}