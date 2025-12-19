using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.OfficeHoursDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfficeHoursController : ControllerBase
    {
        private readonly IOfficeHoursService officeHoursService;
        public OfficeHoursController(IOfficeHoursService officeHoursService)
        {
            this.officeHoursService = officeHoursService;
        }

        [HttpGet("id")]
        public async Task<IActionResult> GetOfficeHoursById(int id) 
        {
            var response = await officeHoursService.GetOfficeHoursByInstructorIdAsync(id);
            return Ok(ApiResponse<List<ViewOfficeHoursDTO>>.SuccessResponse(response));
        }
        [Authorize(Roles ="Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateOfficeHoursByAdmin(CreateOfficeHoursDTO officeHoursDTO) 
        {
            var response = await officeHoursService.CreateOfficeHours(officeHoursDTO);
            return Ok(ApiResponse<ViewOfficeHoursDTO>.SuccessResponse(response));
        }
        [Authorize(Roles ="Admin")]
        [HttpDelete("id")]
        public async Task<IActionResult> CreateOfficeHoursByAdmin(int id) 
        {
            await officeHoursService.DeleteOfficeHour(id);
            return Ok(ApiResponse<string>.SuccessResponse("Successfully deleted office hours."));
        }
    }
}
