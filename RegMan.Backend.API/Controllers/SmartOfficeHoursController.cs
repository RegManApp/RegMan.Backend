using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/smart-office-hours")]
    [ApiController]
    [Authorize]
    public class SmartOfficeHoursController : ControllerBase
    {
        private readonly ISmartOfficeHoursService _service;

        public SmartOfficeHoursController(ISmartOfficeHoursService service)
        {
            _service = service;
        }

        private bool TryGetUserId(out string userId)
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(userId);
        }

        [HttpPost("{officeHourId:int}/queue/join")]
        public async Task<IActionResult> JoinQueue(int officeHourId, [FromBody] SmartOfficeHoursJoinQueueRequestDto request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.JoinQueueAsync(userId, officeHourId, request);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpGet("{officeHourId:int}/queue/me")]
        public async Task<IActionResult> GetMyStatus(int officeHourId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.GetMyStatusAsync(userId, officeHourId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpGet("{officeHourId:int}/provider")]
        public async Task<IActionResult> GetProviderView(int officeHourId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.GetProviderViewAsync(userId, officeHourId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpPost("{officeHourId:int}/provider/call-next")]
        public async Task<IActionResult> CallNext(int officeHourId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.CallNextAsync(userId, officeHourId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpPost("{officeHourId:int}/provider/complete")]
        public async Task<IActionResult> CompleteCurrent(int officeHourId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.CompleteCurrentAsync(userId, officeHourId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpPost("{officeHourId:int}/provider/no-show")]
        public async Task<IActionResult> NoShowCurrent(int officeHourId)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.MarkNoShowCurrentAsync(userId, officeHourId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpPost("scan")]
        public async Task<IActionResult> Scan([FromBody] SmartOfficeHoursScanRequestDto request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("Unauthorized", StatusCodes.Status401Unauthorized));

            var result = await _service.ScanQrAsync(userId, request);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }
    }
}
