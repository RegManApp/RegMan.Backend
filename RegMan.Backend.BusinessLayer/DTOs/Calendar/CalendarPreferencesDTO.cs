namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CalendarPreferencesDTO
    {
        public string TimeZoneId { get; set; } = "UTC";
        public string WeekStartDay { get; set; } = "Mon"; // Sun | Mon
        public bool HideWeekends { get; set; } = false;
        public int? DefaultReminderMinutes { get; set; }
        public string? EventTypeColorMapJson { get; set; }
    }
}
