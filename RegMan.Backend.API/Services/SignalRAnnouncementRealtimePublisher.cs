using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Services
{
    public class SignalRAnnouncementRealtimePublisher : IAnnouncementRealtimePublisher
    {
        private readonly IHubContext<NotificationHub> _hub;

        public SignalRAnnouncementRealtimePublisher(IHubContext<NotificationHub> hub)
        {
            _hub = hub;
        }

        public Task PublishAnnouncementSentAsync(string userId, object payload)
        {
            return _hub.Clients.User(userId).SendAsync("AnnouncementSent", payload);
        }

        public Task PublishAnnouncementReadAsync(string userId, object payload)
        {
            return _hub.Clients.User(userId).SendAsync("AnnouncementRead", payload);
        }
    }
}
