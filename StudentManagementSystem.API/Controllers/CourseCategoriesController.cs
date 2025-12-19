using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/coursecategories")]
    [ApiController]
    [Authorize]
    public class CourseCategoriesController : ControllerBase
    {
        // For demo: static list. Replace with DB/service if needed.
        private static readonly List<object> Categories = new List<object>
        {
            new { Id = 1, Name = "Core" },
            new { Id = 2, Name = "Elective" },
            new { Id = 3, Name = "General Education" },
            new { Id = 4, Name = "Major" },
            new { Id = 5, Name = "Minor" }
        };

        [HttpGet]
        public IActionResult GetCategories()
        {
            return Ok(Categories);
        }
    }
}
