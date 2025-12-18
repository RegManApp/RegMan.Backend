using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.API.Common;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.StudentDTOs;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly IStudentProfileService studentProfileService;
        public StudentController(IStudentProfileService studentProfileService) 
        {
            this.studentProfileService = studentProfileService;
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateStudentAsync(CreateStudentDTO studentDTO) 
        {
            var result = await studentProfileService.CreateProfileAsync(studentDTO);
            return Ok(ApiResponse<ViewStudentProfileDTO>.SuccessResponse(result));
        }  
        [HttpGet]
        public async Task<IActionResult> GetStudentByIdAsync(int id) 
        {
            var result = await studentProfileService.GetProfileByIdAsync(id);
            return Ok(ApiResponse<ViewStudentProfileDTO>.SuccessResponse(result));
        }  
        [HttpGet("students")]
        public async Task<IActionResult> GetStudentsFilteredAsync([FromBody]int? GPA, [FromBody]int? CompletedCredits, [FromBody] string? AcademicPlanId) 
        {
            List<ViewStudentProfileDTO> result = await studentProfileService.GetAllStudentsAsync(GPA, CompletedCredits, AcademicPlanId);
            return Ok(ApiResponse<List<ViewStudentProfileDTO>>.SuccessResponse(result));
        }  
    }
}
