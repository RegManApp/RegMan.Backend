using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities
{
    public enum OfficeHourQueueEntryStatus
    {
        Waiting = 0,
        Ready = 1,
        InProgress = 2,
        Done = 3,
        NoShow = 4
    }

    [Table("OfficeHourQueueEntries")]
    public class OfficeHourQueueEntry
    {
        [Key]
        public int QueueEntryId { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public string StudentUserId { get; set; } = null!;

        [MaxLength(500)]
        public string? Purpose { get; set; }

        public OfficeHourQueueEntryStatus Status { get; set; } = OfficeHourQueueEntryStatus.Waiting;

        public bool IsActive { get; set; } = true;

        public DateTime EnqueuedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReadyAtUtc { get; set; }
        public DateTime? InProgressAtUtc { get; set; }
        public DateTime? DoneAtUtc { get; set; }
        public DateTime? NoShowAtUtc { get; set; }

        public DateTime? ReadyExpiresAtUtc { get; set; }

        public string? LastStateChangedByUserId { get; set; }
        public DateTime LastStateChangedAtUtc { get; set; } = DateTime.UtcNow;

        public OfficeHourSession Session { get; set; } = null!;
        public BaseUser StudentUser { get; set; } = null!;
        public OfficeHourQrToken? QrToken { get; set; }
    }
}
