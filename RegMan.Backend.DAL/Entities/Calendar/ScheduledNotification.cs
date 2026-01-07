using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities.Calendar
{
    public enum ScheduledNotificationStatus
    {
        Pending = 0,
        Sent = 1,
        Cancelled = 2,
        Failed = 3
    }

    public class ScheduledNotification
    {
        [Key]
        public int ScheduledNotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public ReminderTriggerType TriggerType { get; set; }

        [MaxLength(64)]
        public string? SourceEntityType { get; set; }

        public int? SourceEntityId { get; set; }

        [Required]
        public DateTime ScheduledAtUtc { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = null!;

        public string? EntityType { get; set; }
        public int? EntityId { get; set; }

        public ScheduledNotificationStatus Status { get; set; } = ScheduledNotificationStatus.Pending;
        public int AttemptCount { get; set; } = 0;
        public DateTime? LastAttemptAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
