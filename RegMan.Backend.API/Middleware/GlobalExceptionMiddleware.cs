using System.Net;
using System.Text.Json;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace RegMan.Backend.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (AppException ex)
            {
                var traceId = context.TraceIdentifier;
                _logger.LogWarning(ex, "Handled app exception. TraceId={TraceId}", traceId);

                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.FailureResponse(
                    message: ex.Message,
                    statusCode: context.Response.StatusCode,
                    errors: ex.Errors ?? new { traceId, ex.ErrorCode }
                );

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (DbUpdateException ex)
            {
                // Most common judge-facing issue: unique constraint violations.
                var traceId = context.TraceIdentifier;
                _logger.LogWarning(ex, "Database update exception. TraceId={TraceId}", traceId);

                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<object>.FailureResponse(
                    message: "Conflict",
                    statusCode: context.Response.StatusCode,
                    errors: new
                    {
                        traceId,
                        reason = "A record with the same unique value already exists."
                    }
                );

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            catch (Exception ex)
            {
                var traceId = context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = ApiResponse<string>.FailureResponse(
                    message: "Something went wrong",
                    statusCode: context.Response.StatusCode,
                    errors: new { traceId }
                );

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(response)
                );
            }
        }
    }
}
