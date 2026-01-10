using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.BusinessLayer.Exceptions;
using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Hubs
{
    [Authorize]
    public class SmartOfficeHoursHub : Hub
    {
        private readonly ISmartOfficeHoursService _service;
        private readonly ILogger<SmartOfficeHoursHub> _logger;

        public SmartOfficeHoursHub(ISmartOfficeHoursService service, ILogger<SmartOfficeHoursHub> logger)
        {
            _service = service;
            _logger = logger;
        }

        public async Task JoinAsStudent(int officeHourId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, SmartOfficeHoursHubGroups.Students(officeHourId));

                // Push initial view to this caller via user-targeted event (no polling)
                var payload = await _service.GetMyStatusAsync(userId, officeHourId);
                await Clients.Caller.SendAsync("StudentViewUpdated", payload);
            }
            catch (AppException ex)
            {
                _logger.LogWarning(ex,
                    "JoinAsStudent failed. OfficeHourId={OfficeHourId} UserId={UserId} ConnectionId={ConnectionId}",
                    officeHourId,
                    userId,
                    Context.ConnectionId);
                throw new HubException(ex.Message);
            }
            catch (Exception ex)
            {
                var traceId = Context.GetHttpContext()?.TraceIdentifier;
                _logger.LogError(ex,
                    "JoinAsStudent unexpected error. TraceId={TraceId} OfficeHourId={OfficeHourId} UserId={UserId} ConnectionId={ConnectionId}",
                    traceId,
                    officeHourId,
                    userId,
                    Context.ConnectionId);

                throw new HubException(traceId != null
                    ? $"Unexpected server error. TraceId={traceId}"
                    : "Unexpected server error");
            }
        }

        public async Task JoinAsProvider(int officeHourId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            try
            {
                // Enforces ownership + role
                var payload = await _service.GetProviderViewAsync(userId, officeHourId);

                await Groups.AddToGroupAsync(Context.ConnectionId, SmartOfficeHoursHubGroups.Providers(officeHourId));
                await Clients.Caller.SendAsync("ProviderViewUpdated", payload);
            }
            catch (AppException ex)
            {
                _logger.LogWarning(ex,
                    "JoinAsProvider failed. OfficeHourId={OfficeHourId} UserId={UserId} ConnectionId={ConnectionId}",
                    officeHourId,
                    userId,
                    Context.ConnectionId);
                throw new HubException(ex.Message);
            }
            catch (Exception ex)
            {
                var traceId = Context.GetHttpContext()?.TraceIdentifier;
                _logger.LogError(ex,
                    "JoinAsProvider unexpected error. TraceId={TraceId} OfficeHourId={OfficeHourId} UserId={UserId} ConnectionId={ConnectionId}",
                    traceId,
                    officeHourId,
                    userId,
                    Context.ConnectionId);

                throw new HubException(traceId != null
                    ? $"Unexpected server error. TraceId={traceId}"
                    : "Unexpected server error");
            }
        }
    }
}
