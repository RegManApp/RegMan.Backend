using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SectionController : ControllerBase
    {
        private readonly ISectionService sectionService;
        public SectionController(ISectionService sectionService)
        {
            this.sectionService = sectionService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateSectionAsync(CreateSectionDTO sectionDTO) 
        {
            return Ok(await sectionService.CreateSectionAsync(sectionDTO));
        }
    }
}
