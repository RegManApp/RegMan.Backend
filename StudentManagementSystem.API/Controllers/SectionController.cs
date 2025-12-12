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
            try
            {
                return Ok(await sectionService.CreateSectionAsync(sectionDTO));
            }
            catch (Exception ex) 
            {
                return Ok(ex.Message);
            }
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSectionByIdAsync(int id)
        {
            try
            {
                return Ok(await sectionService.GetSectionByIdAsync(id));
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
        [HttpPut]
        public async Task<IActionResult> UpdateSectionAsync(UpdateSectionDTO sectionDTO)
        {
            try
            {
                return Ok(await sectionService.UpdateSectionAsync(sectionDTO));
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
    }
}

