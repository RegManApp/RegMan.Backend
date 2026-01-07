using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities.Calendar
{
    public class UserCalendarPreferences
    {
        [Key]
        [Required]
        public string UserId { get; set; } = null!;

        [MaxLength(64)]
        public string TimeZoneId { get; set; } = "UTC";

        [MaxLength(3)]
        public string WeekStartDay { get; set; } = "Mon"; // Sun | Mon

        public bool HideWeekends { get; set; } = false;

        public int? DefaultReminderMinutes { get; set; }

        public string? EventTypeColorMapJson { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public RegMan.Backend.DAL.Entities.BaseUser User { get; set; } = null!;
    }
}
