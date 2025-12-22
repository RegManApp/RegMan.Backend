using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.TimeSlotDTOs;

namespace RegMan.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // أي request لازم يكون عامل Login
    public class TimeSlotController(ITimeSlotService timeSlotService) : ControllerBase
    {
        private readonly ITimeSlotService timeSlotService = timeSlotService;

        // =========================
        // GET BY ROOM
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("room/{roomId:int}")]
        public async Task<IActionResult> GetByRoom(int roomId)
        {
            var slots = (await timeSlotService.GetAllTimeSlotsAsync()).Where(s => s.RoomId == roomId);
            return Ok(ApiResponse<IEnumerable<ViewTimeSlotDTO>>.SuccessResponse(slots));
        }

        // =========================
        // GET ALL
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var slots = await timeSlotService.GetAllTimeSlotsAsync();
            return Ok(ApiResponse<IEnumerable<ViewTimeSlotDTO>>.SuccessResponse(slots));
        }

        // =========================
        // CREATE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTimeSlotDTO dto)
        {
            var slot = await timeSlotService.CreateTimeSlotAsync(dto);
            return Ok(ApiResponse<ViewTimeSlotDTO>.SuccessResponse(slot));
        }

        // =========================
        // DELETE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted = await timeSlotService.DeleteTimeSlotAsync(id);
            if (!deleted)
            {
                return NotFound(ApiResponse<string>.FailureResponse(
                    "TimeSlot not found",
                    StatusCodes.Status404NotFound
                ));
            }

            return Ok(ApiResponse<string>.SuccessResponse("TimeSlot deleted successfully"));
        }

        // =========================
        // UPDATE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTimeSlotDTO dto)
        {
            if (id != dto.TimeSlotId)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "ID in URL does not match ID in body.",
                    StatusCodes.Status400BadRequest
                ));
            }
            try
            {
                var updated = await timeSlotService.UpdateTimeSlotAsync(dto);
                return Ok(ApiResponse<ViewTimeSlotDTO>.SuccessResponse(updated));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    ex.Message,
                    StatusCodes.Status400BadRequest
                ));
            }
        }
    }
}
