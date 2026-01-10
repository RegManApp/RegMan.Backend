namespace RegMan.Backend.BusinessLayer.Services
{
    public class SmartOfficeHoursOptions
    {
        public int QrRotateSeconds { get; set; } = 10;
        public int QrTokenTtlSeconds { get; set; } = 15;
        public int ReadyNoShowTimeoutSeconds { get; set; } = 120;
        public int EstimatedMinutesPerStudent { get; set; } = 10;

        public static SmartOfficeHoursOptions Default => new();
    }
}
