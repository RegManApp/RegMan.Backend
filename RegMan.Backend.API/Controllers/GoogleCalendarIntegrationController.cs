using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/integrations/google-calendar")]
    [ApiController]
    public class GoogleCalendarIntegrationController : ControllerBase
    {
        private readonly IGoogleCalendarIntegrationService googleCalendarIntegrationService;

        public GoogleCalendarIntegrationController(IGoogleCalendarIntegrationService googleCalendarIntegrationService)
        {
            this.googleCalendarIntegrationService = googleCalendarIntegrationService;
        }

        /// <summary>
        /// Returns a Google authorization URL for the current user.
        /// Frontend should request this endpoint with JWT auth, then navigate the browser to the returned URL.
        /// </summary>
        [HttpGet("connect")]
        [Authorize]
        public IActionResult Connect(
            [FromQuery] string? returnUrl = null,
            [FromQuery] bool redirect = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            string? safeReturnUrl = null;
            if (!string.IsNullOrWhiteSpace(returnUrl)
                && returnUrl.StartsWith('/')
                && !returnUrl.StartsWith("//"))
            {
                safeReturnUrl = returnUrl;
            }

            var url = googleCalendarIntegrationService.CreateAuthorizationUrl(userId, safeReturnUrl);

            // Optional: allow explicit redirect when caller can authenticate the request.
            if (redirect)
                return Redirect(url);

            return Ok(ApiResponse<string>.SuccessResponse(url));
        }

        /// <summary>
        /// Returns whether the current user has a stored Google Calendar connection.
        /// Never returns tokens.
        /// </summary>
        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> Status(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            var connected = await googleCalendarIntegrationService.IsConnectedAsync(userId, cancellationToken);
            var email = User.FindFirstValue(ClaimTypes.Email);

            return Ok(ApiResponse<object>.SuccessResponse(new { connected, email }));
        }

        /// <summary>
        /// OAuth callback endpoint configured in Google Cloud Console.
        /// </summary>
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Content($"Google Calendar connection failed: {error}", "text/plain");
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return BadRequest(ApiResponse<string>.FailureResponse("Missing code/state", 400));
            }

            string? returnUrl;
            try
            {
                returnUrl = await googleCalendarIntegrationService.HandleOAuthCallbackAsync(code, state, cancellationToken);
            }
            catch (Exception ex)
            {
                return Content($"Google Calendar connection failed: {ex.Message}", "text/plain");
            }

            // Avoid open redirects; only allow local-relative return urls.
            if (!string.IsNullOrWhiteSpace(returnUrl)
                && returnUrl.StartsWith('/')
                && !returnUrl.StartsWith("//"))
            {
                return Redirect(returnUrl);
            }

            return Content("Google Calendar connected successfully. You can close this window.", "text/plain");
        }
    }
}
