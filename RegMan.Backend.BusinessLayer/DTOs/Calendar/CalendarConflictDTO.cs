namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CalendarConflictDTO
    {
        public string ConflictType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // Info | Warning | Critical

        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        public string EventIdA { get; set; } = string.Empty;
        public string EventIdB { get; set; } = string.Empty;
    }
}
