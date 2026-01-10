using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.OfficeHoursDTOs;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    // Explicit route to lock the public API contract (avoid accidental breaking changes from controller renames).
    [Route("api/officehour")]
    [Route("api/officehours")]
    [ApiController]
    [Authorize]
    public class OfficeHourController : ControllerBase
    {
        private readonly IOfficeHoursService officeHoursService;

        public OfficeHourController(IOfficeHoursService officeHoursService)
        {
            this.officeHoursService = officeHoursService;
        }

        private bool TryGetUserId(out string userId)
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(userId);
        }

        private string GetUserRoleOrEmpty()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }

        #region DTOs

        public class CreateOfficeHourDTO
        {
            public DateTime Date { get; set; }
            public string StartTime { get; set; } = null!; // "HH:mm" format
            public string EndTime { get; set; } = null!;
            public int? RoomId { get; set; }
            public bool IsRecurring { get; set; } = false;
            public string? Notes { get; set; }
        }

        public class UpdateOfficeHourDTO
        {
            public DateTime? Date { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public int? RoomId { get; set; }
            public string? Notes { get; set; }
        }

        public class BookOfficeHourDTO
        {
            public string? Purpose { get; set; }
            public string? BookerNotes { get; set; }
        }

        public class CancelBookingDTO
        {
            public string? Reason { get; set; }
        }

        public class InstructorNotesDTO
        {
            public string? Notes { get; set; }
        }

        #endregion

        // =============================================
        // PROVIDER ENDPOINTS - Manage Office Hours
        // =============================================

        /// <summary>
        /// Get all office hours for the current provider
        /// </summary>
        [HttpGet("my-office-hours")]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyOfficeHours([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var officeHours = await officeHoursService.GetMyOfficeHoursAsync(userId, fromDate, toDate);

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }

        /// <summary>
        /// Create a new office hour slot
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOfficeHour([FromBody] CreateOfficeHourDTO dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var officeHourId = await officeHoursService.CreateOfficeHourAsync(userId, new CreateInstructorOfficeHourDTO
            {
                Date = dto.Date,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                RoomId = dto.RoomId,
                IsRecurring = dto.IsRecurring,
                Notes = dto.Notes
            });

            return Ok(ApiResponse<object>.SuccessResponse(
                new { officeHourId },
                "Office hour created successfully"));
        }

        /// <summary>
        /// Create multiple office hours at once (batch create)
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> CreateBatchOfficeHours([FromBody] List<CreateOfficeHourDTO> dtos)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var mapped = dtos.Select(d => new CreateInstructorOfficeHourDTO
            {
                Date = d.Date,
                StartTime = d.StartTime,
                EndTime = d.EndTime,
                RoomId = d.RoomId,
                IsRecurring = d.IsRecurring,
                Notes = d.Notes
            }).ToList();

            var (createdIds, errors) = await officeHoursService.CreateBatchOfficeHoursAsync(userId, mapped);

            return Ok(ApiResponse<object>.SuccessResponse(
                new { createdIds, errors },
                $"Created {createdIds.Count} office hours"));
        }

        /// <summary>
        /// Update an office hour
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOfficeHour(int id, [FromBody] UpdateOfficeHourDTO dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.UpdateOfficeHourAsync(userId, id, new UpdateInstructorOfficeHourDTO
            {
                Date = dto.Date,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                RoomId = dto.RoomId,
                Notes = dto.Notes
            });

            return Ok(ApiResponse<string>.SuccessResponse("Office hour updated successfully"));
        }

        /// <summary>
        /// Delete an office hour
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOfficeHour(int id)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.DeleteOfficeHourAsync(userId, id);

            return Ok(ApiResponse<string>.SuccessResponse("Office hour deleted successfully"));
        }

        /// <summary>
        /// Confirm a booking
        /// </summary>
        [HttpPost("bookings/{bookingId}/confirm")]
        public async Task<IActionResult> ConfirmBooking(int bookingId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.ConfirmBookingAsync(userId, bookingId);

            return Ok(ApiResponse<string>.SuccessResponse("Booking confirmed successfully"));
        }

        /// <summary>
        /// Add instructor notes to a booking
        /// </summary>
        [HttpPut("bookings/{bookingId}/notes")]
        public async Task<IActionResult> AddInstructorNotes(int bookingId, [FromBody] InstructorNotesDTO dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.AddInstructorNotesAsync(userId, bookingId, dto.Notes);

            return Ok(ApiResponse<string>.SuccessResponse("Notes added successfully"));
        }

        /// <summary>
        /// Mark booking as completed
        /// </summary>
        [HttpPost("bookings/{bookingId}/complete")]
        public async Task<IActionResult> CompleteBooking(int bookingId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.CompleteBookingAsync(userId, bookingId);

            return Ok(ApiResponse<string>.SuccessResponse("Booking marked as completed"));
        }

        /// <summary>
        /// Mark booking as no-show
        /// </summary>
        [HttpPost("bookings/{bookingId}/no-show")]
        public async Task<IActionResult> MarkNoShow(int bookingId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            await officeHoursService.MarkNoShowAsync(userId, bookingId);

            return Ok(ApiResponse<string>.SuccessResponse("Booking marked as no-show"));
        }

        // =============================================
        // BOOKING ENDPOINTS - Book Office Hours (all roles)
        // =============================================

        /// <summary>
        /// Get all available office hours for students to book
        /// </summary>
        [HttpGet("available")]
        public async Task<IActionResult> GetAvailableOfficeHours(
            [FromQuery] int? instructorId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var officeHours = await officeHoursService.GetAvailableOfficeHoursAsync(instructorId, fromDate, toDate);

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }

        /// <summary>
        /// Get all providers with available office hours (role-agnostic)
        /// </summary>
        [HttpGet("providers")]
        public async Task<IActionResult> GetProvidersWithOfficeHours(
            [FromQuery] string? role,
            [FromQuery] int? courseId,
            [FromQuery] int? sectionId)
        {
            var providers = await officeHoursService.GetProvidersWithOfficeHoursAsync(role, courseId, sectionId);
            return Ok(ApiResponse<object>.SuccessResponse(providers));
        }

        /// <summary>
        /// Get all available office hours for students to book (role-agnostic provider)
        /// </summary>
        [HttpGet("available-v2")]
        public async Task<IActionResult> GetAvailableOfficeHoursV2(
            [FromQuery] string? providerUserId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var officeHours = await officeHoursService.GetAvailableOfficeHoursV2Async(providerUserId, fromDate, toDate);
            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }

        /// <summary>
        /// Get all instructors with their available office hours count
        /// </summary>
        [HttpGet("instructors")]
        public async Task<IActionResult> GetInstructorsWithOfficeHours()
        {
            var instructors = await officeHoursService.GetInstructorsWithOfficeHoursAsync();

            return Ok(ApiResponse<object>.SuccessResponse(instructors));
        }

        /// <summary>
        /// Book an office hour
        /// </summary>
        [HttpPost("{id}/book")]
        public async Task<IActionResult> BookOfficeHour(int id, [FromBody] BookOfficeHourDTO? dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            dto ??= new BookOfficeHourDTO();

            var result = await officeHoursService.BookOfficeHourAsync(userId, id, new BookOfficeHourRequestDTO
            {
                Purpose = dto.Purpose,
                BookerNotes = dto.BookerNotes
            });

            return Ok(ApiResponse<object>.SuccessResponse(
                new { result.BookingId, result.ConversationId, result.SystemMessageId },
                "Office hour booked successfully"));
        }

        /// <summary>
        /// Get student's bookings
        /// </summary>
        [HttpGet("my-bookings")]
        public async Task<IActionResult> GetMyBookings([FromQuery] string? status)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var bookings = await officeHoursService.GetMyBookingsAsync(userId, status);

            return Ok(ApiResponse<object>.SuccessResponse(bookings));
        }

        /// <summary>
        /// Cancel a booking (student)
        /// </summary>
        [HttpPost("bookings/{bookingId}/cancel")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromBody] CancelBookingDTO dto)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var userRole = GetUserRoleOrEmpty();
            await officeHoursService.CancelBookingAsync(userId, userRole, bookingId, dto.Reason);

            return Ok(ApiResponse<string>.SuccessResponse("Booking cancelled successfully"));
        }

        // =============================================
        // ADMIN ENDPOINTS
        // =============================================

        /// <summary>
        /// Get all office hours (admin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOfficeHours(
            [FromQuery] int? instructorId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status)
        {
            var officeHours = await officeHoursService.GetAllOfficeHoursAsync(instructorId, fromDate, toDate, status);

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }
    }
}
