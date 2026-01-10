using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities
{
    public enum OfficeHourSessionStatus
    {
        Active = 0,
        Closed = 1
    }

    [Table("OfficeHourSessions")]
    public class OfficeHourSession
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public int OfficeHourId { get; set; }

        [Required]
        public string ProviderUserId { get; set; } = null!;

        public OfficeHourSessionStatus Status { get; set; } = OfficeHourSessionStatus.Active;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAtUtc { get; set; }

        public OfficeHour OfficeHour { get; set; } = null!;
        public ICollection<OfficeHourQueueEntry> QueueEntries { get; set; } = new List<OfficeHourQueueEntry>();
    }
}
