using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities.Calendar
{
    public enum ReminderTriggerType
    {
        Class = 0,
        OfficeHour = 1,
        RegistrationDeadline = 2,
        WithdrawDeadline = 3
    }

    [Flags]
    public enum ReminderChannel
    {
        None = 0,
        InApp = 1,
        Email = 2,
        Google = 4
    }

    public class UserReminderRule
    {
        [Key]
        public int RuleId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public ReminderTriggerType TriggerType { get; set; }

        [Range(0, 7 * 24 * 60)]
        public int MinutesBefore { get; set; }

        public ReminderChannel ChannelMask { get; set; } = ReminderChannel.InApp;

        public bool IsEnabled { get; set; } = true;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
