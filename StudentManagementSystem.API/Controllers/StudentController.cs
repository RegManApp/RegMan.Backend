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
        public async Task<IActionResult> GetStudentsFilteredAsync(int? GPA, int? CompletedCredits,  string? AcademicPlanId) 
        {
            List<ViewStudentProfileDTO> result = await studentProfileService.GetAllStudentsAsync(GPA, CompletedCredits, AcademicPlanId);
            return Ok(ApiResponse<List<ViewStudentProfileDTO>>.SuccessResponse(result));
        }
        [Authorize(Roles = "Admin")]
        [HttpPut("update-student")]
        public async Task<IActionResult> UpdateStudentAsync(UpdateStudentProfileDTO studentProfileDTO) 
        {
            var result = await studentProfileService.UpdateProfileAdminAsync(studentProfileDTO);
            return Ok(ApiResponse<ViewStudentProfileDTO>.SuccessResponse(result));
        }  
        [Authorize(Roles = "Student")]
        [HttpPut]
        public async Task<IActionResult> ChangeStudentPassword(ChangePasswordDTO passwordDTO) 
        {
            await studentProfileService.ChangeStudentPassword(passwordDTO);
            return Ok(ApiResponse<string>.SuccessResponse("Password changed successfully!"));
        }  
    }
}
