using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/coursecategories")]
    [ApiController]
    [Authorize]
    public class CourseCategoriesController : ControllerBase
    {
        // Get all course categories from enum
        [HttpGet]
        public IActionResult GetCategories()
        {
            var categories = Enum.GetValues<CourseCategory>()
                .Select((c, index) => new
                {
                    Id = (int)c,
                    Name = c.ToString(),
                    Value = c.ToString()
                })
                .ToList();

            return Ok(ApiResponse<object>.SuccessResponse(categories));
        }

        // Get category by ID
        [HttpGet("{id}")]
        public IActionResult GetCategoryById(int id)
        {
            if (!Enum.IsDefined(typeof(CourseCategory), id))
                return NotFound(ApiResponse<string>.FailureResponse("Category not found", 404));

            var category = (CourseCategory)id;
            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                Id = id,
                Name = category.ToString(),
                Value = category.ToString()
            }));
        }
    }
}
