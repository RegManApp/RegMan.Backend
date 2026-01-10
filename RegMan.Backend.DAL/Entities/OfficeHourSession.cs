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
        [Column("OfficeHourSessionId")]
        public int SessionId { get; set; }

        [Column("OfficeHourId")]
        public int? OfficeHourIdDb { get; set; }

        [NotMapped]
        public int OfficeHourId
        {
            get => OfficeHourIdDb ?? 0;
            set => OfficeHourIdDb = value;
        }

        [Required]
        [MaxLength(450)]
        public string ProviderUserId { get; set; } = null!;

        public OfficeHourSessionStatus Status { get; set; } = OfficeHourSessionStatus.Active;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAtUtc { get; set; }

        public OfficeHour OfficeHour { get; set; } = null!;
        public ICollection<OfficeHourQueueEntry> QueueEntries { get; set; } = new List<OfficeHourQueueEntry>();
    }
}
