using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.DTOs.Notifications
{
    public class RealtimeNotificationDTO
    {
        public int NotificationId { get; set; }
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
