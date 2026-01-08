using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using System.Security.Claims;
using System.Security.Cryptography;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/integrations/google-calendar")]
    [ApiController]
    public class GoogleCalendarIntegrationController : ControllerBase
    {
        private readonly IGoogleCalendarIntegrationService googleCalendarIntegrationService;
        private readonly ILogger<GoogleCalendarIntegrationController> logger;

        public GoogleCalendarIntegrationController(
            IGoogleCalendarIntegrationService googleCalendarIntegrationService,
            ILogger<GoogleCalendarIntegrationController> logger)
        {
            this.googleCalendarIntegrationService = googleCalendarIntegrationService;
            this.logger = logger;
        }

        /// <summary>
        /// Returns a Google authorization URL for the current user.
        /// Frontend should call this with JWT auth, then navigate the browser to the returned URL.
        /// </summary>
        [HttpGet("connect-url")]
        [Authorize]
        public IActionResult GetConnectUrl([FromQuery] string? returnUrl = null)
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

            logger.LogInformation("GoogleCalendar connect-url requested for UserId={UserId} ReturnUrl={ReturnUrl}", userId, safeReturnUrl);

            try
            {
                var url = googleCalendarIntegrationService.CreateAuthorizationUrl(userId, safeReturnUrl);
                return Ok(ApiResponse<object>.SuccessResponse(new { url }));
            }
            catch (InvalidOperationException ex)
            {
                // Configuration/validation errors.
                logger.LogError(ex, "GoogleCalendar connect-url failed (likely misconfiguration) for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<string>.FailureResponse(ex.Message, StatusCodes.Status503ServiceUnavailable));
            }
            catch (CryptographicException ex)
            {
                logger.LogError(ex, "GoogleCalendar connect-url failed (crypto/DataProtection) for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<string>.FailureResponse(
                        "Google Calendar connect is temporarily unavailable due to a server crypto configuration issue. Please try again later.",
                        StatusCodes.Status503ServiceUnavailable));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GoogleCalendar connect-url failed unexpectedly for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.FailureResponse("Google Calendar connect-url failed. See server logs for details.", StatusCodes.Status500InternalServerError));
            }
        }

        /// <summary>
        /// Legacy endpoint: redirects to Google authorization URL.
        /// NOTE: Browser navigation will not include JWT, so frontend must use /connect-url.
        /// </summary>
        [HttpGet("connect")]
        [Authorize]
        public IActionResult Connect([FromQuery] string? returnUrl = null)
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

            try
            {
                var url = googleCalendarIntegrationService.CreateAuthorizationUrl(userId, safeReturnUrl);
                return Redirect(url);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "GoogleCalendar connect failed (likely misconfiguration) for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<string>.FailureResponse(ex.Message, StatusCodes.Status503ServiceUnavailable));
            }
            catch (CryptographicException ex)
            {
                logger.LogError(ex, "GoogleCalendar connect failed (crypto/DataProtection) for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<string>.FailureResponse(
                        "Google Calendar connect is temporarily unavailable due to a server crypto configuration issue. Please try again later.",
                        StatusCodes.Status503ServiceUnavailable));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "GoogleCalendar connect failed unexpectedly for UserId={UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.FailureResponse("Google Calendar connect failed. See server logs for details.", StatusCodes.Status500InternalServerError));
            }
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
        /// Disconnect current user from Google Calendar (removes stored tokens and RegMan↔Google event mappings).
        /// Best-effort; never exposes tokens.
        /// </summary>
        [HttpPost("disconnect")]
        [Authorize]
        public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(ApiResponse<string>.FailureResponse("User is not authenticated", 401));

            await googleCalendarIntegrationService.DisconnectAsync(userId, cancellationToken);
            return Ok(ApiResponse<string>.SuccessResponse("Google Calendar disconnected"));
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
                logger.LogWarning("GoogleCalendar OAuth callback returned error={Error}", error);
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
                logger.LogError(ex, "GoogleCalendar OAuth callback failed");
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
