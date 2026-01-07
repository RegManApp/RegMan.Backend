namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CalendarViewEventDTO
    {
        public string Id { get; set; } = string.Empty;

        public string? Title { get; set; }

        // Optional localization key for frontend i18n (preferred for system events)
        public string? TitleKey { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        // e.g. registration, withdraw, class, teaching, office-hour, office-hour-booking
        public string Type { get; set; } = string.Empty;

        // Optional (booking status, office hour status, etc.)
        public string? Status { get; set; }

        // Strongly-typed object per event kind (serialized as JSON)
        public object? ExtendedProps { get; set; }
    }
}
