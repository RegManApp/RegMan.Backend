using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;

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
      

    }
}
