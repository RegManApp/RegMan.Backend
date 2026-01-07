using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Notifications;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/notifications/reminder-rules")]
    [ApiController]
    [Authorize]
    public class ReminderRulesController : ControllerBase
    {
        private readonly IReminderRulesService reminderRulesService;

        public ReminderRulesController(IReminderRulesService reminderRulesService)
        {
            this.reminderRulesService = reminderRulesService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            var rules = await reminderRulesService.GetRulesAsync(userId, cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(rules));
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] List<ReminderRuleDTO> rules, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            var saved = await reminderRulesService.ReplaceRulesAsync(userId, rules ?? new List<ReminderRuleDTO>(), cancellationToken);
            return Ok(ApiResponse<object>.SuccessResponse(saved));
        }
    }
}
