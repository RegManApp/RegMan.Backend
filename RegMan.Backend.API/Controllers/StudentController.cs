using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.StudentDTOs;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
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
        private string GetStudentID()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("id")
                ?? string.Empty;
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
        [HttpGet("me")]
        public async Task<IActionResult> GetMyStudentProfileAsync()
        {
            string studentId = GetStudentID();
            if (string.IsNullOrWhiteSpace(studentId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));
            var result = await studentProfileService.GetProfileByIdAsync(studentId);
            return Ok(ApiResponse<ViewStudentProfileDTO>.SuccessResponse(result));
        }
        [HttpGet("students")]
        public async Task<IActionResult> GetStudentsFilteredAsync(int? GPA, int? CompletedCredits, string? AcademicPlanId)
        {
            List<ViewStudentProfileDTO> result = await studentProfileService.GetAllStudentsAsync(GPA, CompletedCredits, AcademicPlanId);
            return Ok(ApiResponse<List<ViewStudentProfileDTO>>.SuccessResponse(result));
        }
        [Authorize(Roles = "Admin,Student")]
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
            // Never trust client-provided email for password changes
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(email))
                passwordDTO.Email = email;

            await studentProfileService.ChangeStudentPassword(passwordDTO);
            return Ok(ApiResponse<string>.SuccessResponse("Password changed successfully!"));
        }
    }
}
