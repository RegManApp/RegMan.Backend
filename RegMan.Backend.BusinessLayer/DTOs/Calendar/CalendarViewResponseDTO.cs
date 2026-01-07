namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CalendarViewResponseDTO
    {
        public string ViewRole { get; set; } = string.Empty;

        public CalendarViewDateRangeDTO DateRange { get; set; } = new();

        public List<CalendarViewEventDTO> Events { get; set; } = new();

        public List<CalendarConflictDTO> Conflicts { get; set; } = new();
    }

    public class CalendarViewDateRangeDTO
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }
}

