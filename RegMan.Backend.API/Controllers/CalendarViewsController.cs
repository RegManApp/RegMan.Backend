using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/calendar")]
    [ApiController]
    [Authorize]
    public class CalendarViewsController : ControllerBase
    {
        private readonly ICalendarViewService calendarViewService;

        public CalendarViewsController(ICalendarViewService calendarViewService)
        {
            this.calendarViewService = calendarViewService;
        }

        /// <summary>
        /// Unified role-aware calendar view. Backend resolves filtering based on authenticated role.
        /// Supports both (startDate,endDate) and legacy (fromDate,toDate) query parameter names.
        /// </summary>
        [HttpGet("view")]
        public async Task<IActionResult> GetCalendarView(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userRole))
            {
                return Unauthorized(ApiResponse<string>.FailureResponse(
                    "User is not authenticated",
                    StatusCodes.Status401Unauthorized
                ));
            }

            var rangeStart = startDate ?? fromDate ?? DateTime.UtcNow.Date.AddMonths(-1);
            var rangeEnd = endDate ?? toDate ?? DateTime.UtcNow.Date.AddMonths(3);

            if (rangeEnd < rangeStart)
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid date range: end must be >= start",
                    StatusCodes.Status400BadRequest
                ));
            }

            var result = await calendarViewService.GetCalendarViewAsync(
                userId,
                userRole,
                rangeStart,
                rangeEnd,
                cancellationToken);

            // Best-effort: lazily plan reminders for the caller (in-app only for now).
            // Never blocks calendar view on scheduler failures.
            try
            {
                var engine = HttpContext.RequestServices.GetRequiredService<ICalendarReminderEngine>();
                await engine.EnsurePlannedForUserAsync(userId, DateTime.UtcNow, cancellationToken);
            }
            catch
            {
                // ignore
            }

            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
    }
}

