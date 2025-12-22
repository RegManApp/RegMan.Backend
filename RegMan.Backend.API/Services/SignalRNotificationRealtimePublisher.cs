using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Notifications;

namespace RegMan.Backend.API.Services
{
    public class SignalRNotificationRealtimePublisher : INotificationRealtimePublisher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRNotificationRealtimePublisher(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public async Task PublishAsync(string userId, RealtimeNotificationDTO notification)
        {
            await _hub.Clients.User(userId).SendAsync("NotificationReceived", notification);
        }
    }
}
