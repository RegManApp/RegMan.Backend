using RegMan.Backend.BusinessLayer.DTOs.Notifications;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface INotificationRealtimePublisher
    {
        Task PublishAsync(string userId, RealtimeNotificationDTO notification);
    }
}
