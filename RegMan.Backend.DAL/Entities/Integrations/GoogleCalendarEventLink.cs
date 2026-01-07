using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities.Integrations
{
    public class GoogleCalendarEventLink
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!; // organizer account used in Google

        [Required]
        [MaxLength(64)]
        public string SourceEntityType { get; set; } = null!; // e.g., OfficeHourBooking

        [Required]
        public int SourceEntityId { get; set; }

        [Required]
        [MaxLength(128)]
        public string GoogleCalendarId { get; set; } = "primary";

        [Required]
        [MaxLength(256)]
        public string GoogleEventId { get; set; } = null!;

        public DateTime LastSyncedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public RegMan.Backend.DAL.Entities.BaseUser User { get; set; } = null!;
    }
}
