using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities.Calendar
{
    public class CalendarAuditEntry
    {
        [Key]
        public int CalendarAuditEntryId { get; set; }

        [Required]
        public string ActorUserId { get; set; } = null!;

        [Required]
        public string ActorEmail { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string TargetType { get; set; } = "AcademicCalendarSettings";

        [Required]
        [MaxLength(64)]
        public string TargetKey { get; set; } = "default";

        [Required]
        [MaxLength(16)]
        public string Action { get; set; } = null!; // UPDATE | RESTORE

        [Required]
        public string BeforeJson { get; set; } = null!;

        [Required]
        public string AfterJson { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
