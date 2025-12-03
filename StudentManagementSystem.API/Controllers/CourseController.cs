using Microsoft.AspNetCore.Http;
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
        public async Task<IActionResult> GetCourseById(int id)
        {

            try 
            {
                var course = await courseService.GetCourseById(id);
                return Ok(course);
            }
            catch (Exception ex) 
            {
                return Ok(ex.Message);
            }

        }
        [HttpGet]
        public async Task<IActionResult> GetAllCourses([FromQuery] string? courseName, [FromQuery] int? creditHours, [FromQuery] int? availableSeats, [FromQuery] string? courseCode, [FromQuery] int? courseCategoryId)
        {
            try
            {
                var courses = await courseService.GetAllCoursesAsync(courseName, creditHours, availableSeats, courseCode, courseCategoryId);
                return Ok(courses);
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDTO courseDTO)
        {
            try
            {
                var createdCourse = await courseService.CreateCourse(courseDTO);
                return Ok(createdCourse);
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var result = await courseService.DeleteCourse(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
        [HttpPut]
        public async Task<IActionResult> UpdateCourse([FromBody] UpdateCourseDTO courseDTO)
        {
            try
            {
                var updatedCourse = await courseService.UpdateCourse(courseDTO);
                return Ok(updatedCourse);
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }


    }
}
