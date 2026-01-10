using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Hubs
{
    [Authorize]
    public class SmartOfficeHoursHub : Hub
    {
        private readonly ISmartOfficeHoursService _service;

        public SmartOfficeHoursHub(ISmartOfficeHoursService service)
        {
            _service = service;
        }

        public async Task JoinAsStudent(int officeHourId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            await Groups.AddToGroupAsync(Context.ConnectionId, SmartOfficeHoursHubGroups.Students(officeHourId));

            // Push initial view to this caller via user-targeted event (no polling)
            var payload = await _service.GetMyStatusAsync(userId, officeHourId);
            await Clients.Caller.SendAsync("StudentViewUpdated", payload);
        }

        public async Task JoinAsProvider(int officeHourId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            // Enforces ownership + role
            var payload = await _service.GetProviderViewAsync(userId, officeHourId);

            await Groups.AddToGroupAsync(Context.ConnectionId, SmartOfficeHoursHubGroups.Providers(officeHourId));
            await Clients.Caller.SendAsync("ProviderViewUpdated", payload);
        }
    }
}
