namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CalendarAuditEntryDTO
    {
        public int CalendarAuditEntryId { get; set; }
        public string ActorUserId { get; set; } = string.Empty;
        public string ActorEmail { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string BeforeJson { get; set; } = string.Empty;
        public string AfterJson { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
