using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.TranscriptDTOs;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TranscriptController : ControllerBase
    {
        private readonly ITranscriptService transcriptService;

        public TranscriptController(ITranscriptService transcriptService)
        {
            this.transcriptService = transcriptService;
        }

        // =========================
        // Get My Transcript (Student)
        // =========================
        [Authorize(Roles = "Student")]
        [HttpGet("my-transcript")]
        public async Task<IActionResult> GetMyTranscriptAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Unauthorized",
                    StatusCodes.Status401Unauthorized));

            var transcript = await transcriptService.GetMyTranscriptAsync(userId);

            return Ok(
                ApiResponse<StudentTranscriptSummaryDTO>
                    .SuccessResponse(transcript)
            );
        }

        // =========================
        // Get Student Full Transcript (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("student/{studentUserId}")]
        public async Task<IActionResult> GetStudentFullTranscriptAsync(string studentUserId)
        {
            var transcript = await transcriptService.GetStudentFullTranscriptAsync(studentUserId);

            return Ok(
                ApiResponse<StudentTranscriptSummaryDTO>
                    .SuccessResponse(transcript)
            );
        }

        // =========================
        // Get Transcript By ID (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTranscriptByIdAsync(int id)
        {
            var transcript = await transcriptService.GetTranscriptByIdAsync(id);

            return Ok(
                ApiResponse<ViewTranscriptDTO>
                    .SuccessResponse(transcript)
            );
        }

        // =========================
        // Get Transcripts By Student ID (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("by-student/{studentId}")]
        public async Task<IActionResult> GetTranscriptsByStudentIdAsync(int studentId)
        {
            var transcripts = await transcriptService.GetTranscriptsByStudentIdAsync(studentId);

            return Ok(
                ApiResponse<IEnumerable<ViewTranscriptDTO>>
                    .SuccessResponse(transcripts)
            );
        }

        // =========================
        // Get Transcripts By Semester (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("by-semester")]
        public async Task<IActionResult> GetTranscriptsBySemesterAsync(
            [FromQuery] string semester,
            [FromQuery] int year)
        {
            var transcripts = await transcriptService.GetTranscriptsBySemesterAsync(semester, year);

            return Ok(
                ApiResponse<IEnumerable<ViewTranscriptDTO>>
                    .SuccessResponse(transcripts)
            );
        }

        // =========================
        // Get All Transcripts (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAllTranscriptsAsync(
            [FromQuery] int? studentId,
            [FromQuery] int? courseId,
            [FromQuery] string? semester,
            [FromQuery] int? year,
            [FromQuery] string? grade)
        {
            var transcripts = await transcriptService.GetAllTranscriptsAsync(
                studentId, courseId, semester, year, grade);

            return Ok(
                ApiResponse<IEnumerable<ViewTranscriptDTO>>
                    .SuccessResponse(transcripts)
            );
        }

        // =========================
        // Create Transcript (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpPost]
        public async Task<IActionResult> CreateTranscriptAsync(
            [FromBody] CreateTranscriptDTO dto)
        {
            var transcript = await transcriptService.CreateTranscriptAsync(dto);

            return Ok(
                ApiResponse<ViewTranscriptDTO>
                    .SuccessResponse(transcript, "Transcript created successfully")
            );
        }

        // =========================
        // Update Grade (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpPut]
        public async Task<IActionResult> UpdateGradeAsync(
            [FromBody] UpdateTranscriptDTO dto)
        {
            var transcript = await transcriptService.UpdateGradeAsync(dto);

            return Ok(
                ApiResponse<ViewTranscriptDTO>
                    .SuccessResponse(transcript, "Grade updated successfully")
            );
        }

        // =========================
        // Delete Transcript (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTranscriptAsync(int id)
        {
            var result = await transcriptService.DeleteTranscriptAsync(id);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse(result)
            );
        }

        // =========================
        // Calculate Student GPA (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("gpa/{studentId}")]
        public async Task<IActionResult> CalculateStudentGPAAsync(int studentId)
        {
            var gpa = await transcriptService.CalculateStudentGPAAsync(studentId);

            return Ok(
                ApiResponse<double>
                    .SuccessResponse(gpa, "GPA calculated successfully")
            );
        }

        // =========================
        // Calculate Semester GPA (Admin/Instructor)
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("semester-gpa/{studentId}")]
        public async Task<IActionResult> CalculateSemesterGPAAsync(
            int studentId,
            [FromQuery] string semester,
            [FromQuery] int year)
        {
            var gpa = await transcriptService.CalculateSemesterGPAAsync(studentId, semester, year);

            return Ok(
                ApiResponse<double>
                    .SuccessResponse(gpa, "Semester GPA calculated successfully")
            );
        }

        // =========================
        // Recalculate Student GPA (Admin)
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost("recalculate-gpa/{studentId}")]
        public async Task<IActionResult> RecalculateStudentGPAAsync(int studentId)
        {
            await transcriptService.RecalculateAndUpdateStudentGPAAsync(studentId);

            return Ok(
                ApiResponse<string>
                    .SuccessResponse("Student GPA recalculated and updated successfully")
            );
        }
    }
}
