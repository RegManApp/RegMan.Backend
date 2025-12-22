using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.InstructorDTOs;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers;

[ApiController]
[Route("api/instructor")]
[Authorize]
public class InstructorController : ControllerBase
{
    private readonly IInstructorService instructorService;

    public InstructorController(IInstructorService instructorService)
    {
        this.instructorService = instructorService;
    }

    // =========================
    // Create Instructor
    // Admin only
    // =========================
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateInstructorDTO dto)
    {
        var result = await instructorService.CreateAsync(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "Instructor created successfully"));
    }

    // =========================
    // Get All Instructors
    // Admin, Student (for booking office hours)
    // =========================
    [Authorize(Roles = "Admin,Student,Instructor")]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var instructors = await instructorService.GetAllAsync();
        return Ok(ApiResponse<object>.SuccessResponse(instructors));
    }

    // =========================
    // Get Instructor By Id
    // Admin, Student (for viewing instructor info)
    // =========================
    [Authorize(Roles = "Admin,Student,Instructor")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var instructor = await instructorService.GetByIdAsync(id);
        if (instructor == null)
            return NotFound(ApiResponse<string>.FailureResponse("Instructor not found", 404));
        return Ok(ApiResponse<object>.SuccessResponse(instructor));
    }

    // =========================
    // Update Instructor
    // Admin only
    // =========================
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInstructorDTO dto)
    {
        var result = await instructorService.UpdateAsync(id, dto);
        if (result == null)
            return NotFound(ApiResponse<string>.FailureResponse("Instructor not found", 404));
        return Ok(ApiResponse<object>.SuccessResponse(result, "Instructor updated successfully"));
    }

    // =========================
    // Delete Instructor
    // Admin only
    // =========================
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await instructorService.DeleteAsync(id);
        if (!success)
            return NotFound(ApiResponse<string>.FailureResponse("Instructor not found", 404));
        return Ok(ApiResponse<string>.SuccessResponse("Instructor deleted successfully"));
    }

    // =========================
    // Get Instructor Schedule
    // Admin OR Instructor (own schedule)
    // =========================
    [Authorize(Roles = "Admin,Instructor")]
    [HttpGet("{id}/schedule")]
    public async Task<IActionResult> GetSchedule(int id)
    {
        if (User.IsInRole("Instructor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "Unauthorized",
                    StatusCodes.Status401Unauthorized
                ));

            var instructor = await instructorService.GetByIdAsync(id);
            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse("Instructor not found", 404));
        }

        var schedule = await instructorService.GetScheduleAsync(id);
        return Ok(ApiResponse<object>.SuccessResponse(schedule));
    }

    // =========================
    // Get My Schedule (for logged in instructor)
    // =========================
    [Authorize(Roles = "Instructor")]
    [HttpGet("my-schedule")]
    public async Task<IActionResult> GetMySchedule()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized(ApiResponse<string>.FailureResponse(
                "Unauthorized",
                StatusCodes.Status401Unauthorized
            ));

        var instructor = await instructorService.GetByUserIdAsync(userId);
        if (instructor == null)
            return NotFound(ApiResponse<string>.FailureResponse("Instructor profile not found", 404));

        var schedule = await instructorService.GetScheduleAsync(instructor.InstructorId);
        return Ok(ApiResponse<object>.SuccessResponse(schedule));
    }
}
