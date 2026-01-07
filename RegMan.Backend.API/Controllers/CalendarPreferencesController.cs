using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Calendar;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/calendar/preferences")]
    [ApiController]
    [Authorize]
    public class CalendarPreferencesController : ControllerBase
    {
        private readonly ICalendarPreferencesService preferencesService;

        public CalendarPreferencesController(ICalendarPreferencesService preferencesService)
        {
            this.preferencesService = preferencesService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            var prefs = await preferencesService.GetAsync(userId, cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(prefs));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] CalendarPreferencesDTO dto, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            var prefs = await preferencesService.UpsertAsync(userId, dto, cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(prefs));
        }
    }
}
