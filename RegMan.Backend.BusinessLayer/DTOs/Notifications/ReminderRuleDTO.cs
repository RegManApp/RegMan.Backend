using RegMan.Backend.DAL.Entities.Calendar;

namespace RegMan.Backend.BusinessLayer.DTOs.Notifications
{
    public class ReminderRuleDTO
    {
        public int RuleId { get; set; }
        public ReminderTriggerType TriggerType { get; set; }
        public int MinutesBefore { get; set; }
        public ReminderChannel ChannelMask { get; set; }
        public bool IsEnabled { get; set; }
    }
}
