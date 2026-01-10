using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours;

namespace RegMan.Backend.API.Services
{
    public class SignalRSmartOfficeHoursRealtimePublisher : ISmartOfficeHoursRealtimePublisher
    {
        private readonly IHubContext<SmartOfficeHoursHub> _hub;

        public SignalRSmartOfficeHoursRealtimePublisher(IHubContext<SmartOfficeHoursHub> hub)
        {
            _hub = hub;
        }

        public Task PublishProviderViewAsync(int officeHourId, SmartOfficeHoursProviderViewDto payload)
        {
            return _hub.Clients.Group(SmartOfficeHoursHubGroups.Providers(officeHourId))
                .SendAsync("ProviderViewUpdated", payload);
        }

        public Task PublishStudentViewAsync(string studentUserId, int officeHourId, SmartOfficeHoursStudentViewDto payload)
        {
            // Privacy: student view is per-user (position/ETA). Do not broadcast to other students.
            return _hub.Clients.User(studentUserId).SendAsync("StudentViewUpdated", payload);
        }
    }
}
