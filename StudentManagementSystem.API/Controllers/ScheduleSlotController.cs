using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;

namespace StudentManagementSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // أي request لازم يكون authenticated
    public class ScheduleSlotController : ControllerBase
    {
        private readonly IScheduleSlotService scheduleSlotService;

        public ScheduleSlotController(IScheduleSlotService scheduleSlotService)
        {
            this.scheduleSlotService = scheduleSlotService;
        }

        // =========================
        // Create Schedule Slot
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateScheduleSlotDTO dto)
        {
            var result = await scheduleSlotService.CreateAsync(dto);
            return Ok(result);
        }

        // =========================
        // Get All Schedule Slots
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await scheduleSlotService.GetAllAsync();
            return Ok(result);
        }

        // =========================
        // Get By Section
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("section/{sectionId:int}")]
        public async Task<IActionResult> GetBySection(int sectionId)
        {
            var result = await scheduleSlotService.GetBySectionAsync(sectionId);
            return Ok(result);
        }

        // =========================
        // Get By Instructor
        // Admin + Instructor
        // =========================
        [Authorize(Roles = "Admin,Instructor")]
        [HttpGet("instructor/{instructorId:int}")]
        public async Task<IActionResult> GetByInstructor(int instructorId)
        {
            var result = await scheduleSlotService.GetByInstructorAsync(instructorId);
            return Ok(result);
        }

        // =========================
        // Get By Room
        // Admin + Instructor + Student
        // =========================
        [Authorize(Roles = "Admin,Instructor,Student")]
        [HttpGet("room/{roomId:int}")]
        public async Task<IActionResult> GetByRoom(int roomId)
        {
            var result = await scheduleSlotService.GetByRoomAsync(roomId);
            return Ok(result);
        }

        // =========================
        // Delete Schedule Slot
        // Admin only
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await scheduleSlotService.DeleteAsync(id);
            return Ok(new { message = "Schedule slot deleted successfully." });
        }
    }
}
