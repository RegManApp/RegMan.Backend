using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.RoomDTOs;
using RegMan.Backend.BusinessLayer.DTOs.TimeSlotDTOs;
using System;

namespace RegMan.Backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // أي request لازم يكون عامل Login
    public class RoomController : ControllerBase
    {
        private readonly IRoomService roomService;
        private readonly ITimeSlotService timeSlotService;

        public RoomController(IRoomService roomService, ITimeSlotService timeSlotService)
        {
            this.roomService = roomService;
            this.timeSlotService = timeSlotService;
        }

        // =========================
        // GET ALL
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rooms = await roomService.GetAllRoomsAsync();
            return Ok(ApiResponse<IEnumerable<ViewRoomDTO>>.SuccessResponse(rooms));
        }

        // =========================
        // GET BY ID
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var room = await roomService.GetRoomByIdAsync(id);
            return Ok(ApiResponse<ViewRoomDTO>.SuccessResponse(room));
        }

        // =========================
        // CREATE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoomDTO dto)
        {
            var room = await roomService.CreateRoomAsync(dto);

            // Automatically create 12 time slots for each day (Saturday to Thursday)
            // Each slot is 1 hour, starting from 8:30 AM
            var days = new[]
            {
                DayOfWeek.Saturday,
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday
            };

            foreach (var day in days)
            {
                for (int i = 0; i < 12; i++)
                {
                    var startTime = new TimeSpan(8, 30, 0) + TimeSpan.FromHours(i);
                    var endTime = startTime + TimeSpan.FromHours(1);

                    var timeSlotDto = new CreateTimeSlotDTO
                    {
                        RoomId = room.RoomId,
                        Day = day,
                        StartTime = startTime,
                        EndTime = endTime
                    };

                    await timeSlotService.CreateTimeSlotAsync(timeSlotDto);
                }
            }

            return Ok(ApiResponse<ViewRoomDTO>.SuccessResponse(room));
        }

        // =========================
        // UPDATE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateRoomDTO dto)
        {
            var room = await roomService.UpdateRoomAsync(dto);
            return Ok(ApiResponse<ViewRoomDTO>.SuccessResponse(room));
        }

        // =========================
        // DELETE
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted = await roomService.DeleteRoomAsync(id);
            if (!deleted)
            {
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Room not found",
                    StatusCodes.Status404NotFound
                ));
            }

            return Ok(ApiResponse<string>.SuccessResponse("Room deleted successfully"));
        }
    }
}
