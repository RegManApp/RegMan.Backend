using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.AcademicPlanDTOs;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AcademicPlanController : ControllerBase
    {
        private readonly IAcademicPlanService academicPlanService;

        public AcademicPlanController(IAcademicPlanService academicPlanService)
        {
            this.academicPlanService = academicPlanService;
        }

        // =========================
        // Get My Academic Progress (Student)
        // =========================
        [Authorize(Roles = "Student")]
        [HttpGet("my-progress")]
        public async Task<IActionResult> GetMyAcademicProgressAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Unauthorized",
                    StatusCodes.Status401Unauthorized));

            var progress = await academicPlanService.GetMyAcademicProgressAsync(userId);

            return Ok(
                ApiResponse<StudentAcademicProgressDTO>
                    .SuccessResponse(progress)
            );
        }

        // =========================
        // Get Student Academic Progress (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("student-progress/{studentUserId}")]
        public async Task<IActionResult> GetStudentAcademicProgressAsync(string studentUserId)
        {
            var progress = await academicPlanService.GetStudentAcademicProgressAsync(studentUserId);

            return Ok(
                ApiResponse<StudentAcademicProgressDTO>
                    .SuccessResponse(progress)
            );
        }

        // =========================
        // Get All Academic Plans (All Roles)
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAllAcademicPlansAsync()
        {
            var plans = await academicPlanService.GetAllAcademicPlansAsync();

            return Ok(
                ApiResponse<IEnumerable<ViewAcademicPlanSummaryDTO>>
                    .SuccessResponse(plans)
            );
        }

        // =========================
        // Get Academic Plan By ID (All Roles)
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("{academicPlanId}")]
        public async Task<IActionResult> GetAcademicPlanByIdAsync(string academicPlanId)
        {
            var plan = await academicPlanService.GetAcademicPlanByIdAsync(academicPlanId);

            return Ok(
                ApiResponse<ViewAcademicPlanDTO>
                    .SuccessResponse(plan)
            );
        }

        // =========================
        // Get Courses In Academic Plan (All Roles)
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("{academicPlanId}/courses")]
        public async Task<IActionResult> GetCoursesInAcademicPlanAsync(string academicPlanId)
        {
            var courses = await academicPlanService.GetCoursesInAcademicPlanAsync(academicPlanId);

            return Ok(
                ApiResponse<IEnumerable<AcademicPlanCourseDTO>>
                    .SuccessResponse(courses)
            );
        }

        // =========================
        // Create Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateAcademicPlanAsync(
            [FromBody] CreateAcademicPlanDTO dto)
        {
            var plan = await academicPlanService.CreateAcademicPlanAsync(dto);

            return Ok(
                ApiResponse<ViewAcademicPlanDTO>
                    .SuccessResponse(plan, "Academic Plan created successfully")
            );
        }

        // =========================
        // Update Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> UpdateAcademicPlanAsync(
            [FromBody] UpdateAcademicPlanDTO dto)
        {
            var plan = await academicPlanService.UpdateAcademicPlanAsync(dto);

            return Ok(
                ApiResponse<ViewAcademicPlanDTO>
                    .SuccessResponse(plan, "Academic Plan updated successfully")
            );
        }

        // =========================
        // Delete Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{academicPlanId}")]
        public async Task<IActionResult> DeleteAcademicPlanAsync(string academicPlanId)
        {
            var result = await academicPlanService.DeleteAcademicPlanAsync(academicPlanId);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse(result)
            );
        }

        // =========================
        // Add Course To Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost("add-course")]
        public async Task<IActionResult> AddCourseToAcademicPlanAsync(
            [FromBody] AddCourseToAcademicPlanDTO dto)
        {
            var course = await academicPlanService.AddCourseToAcademicPlanAsync(dto);

            return Ok(
                ApiResponse<AcademicPlanCourseDTO>
                    .SuccessResponse(course, "Course added to Academic Plan successfully")
            );
        }

        // =========================
        // Remove Course From Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{academicPlanId}/courses/{courseId}")]
        public async Task<IActionResult> RemoveCourseFromAcademicPlanAsync(
            string academicPlanId, int courseId)
        {
            var result = await academicPlanService.RemoveCourseFromAcademicPlanAsync(academicPlanId, courseId);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse(result)
            );
        }

        // =========================
        // Assign Student To Academic Plan (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost("assign-student")]
        public async Task<IActionResult> AssignStudentToAcademicPlanAsync(
            [FromQuery] int studentId,
            [FromQuery] string academicPlanId)
        {
            await academicPlanService.AssignStudentToAcademicPlanAsync(studentId, academicPlanId);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse("Student assigned to Academic Plan successfully")
            );
        }
    }
}
