using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SectionController : ControllerBase
    {
        private readonly ISectionService sectionService;
        public SectionController(ISectionService sectionService)
        {
            this.sectionService = sectionService;
        }
        [Authorize(Roles = "Admin")]
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
        [HttpGet]
        public async Task<IActionResult> GetAllSectionsAsync(string? semester, DateTime? year, int? instructorId, int? courseId, int? seats)
        {
            try
            {
                return Ok(await sectionService.GetAllSectionsAsync(semester, year, instructorId, courseId, seats));
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSectionAsync(int id)
        {
            try
            {
                return Ok(await sectionService.DeleteSectionAsync(id));
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }
    }
}

