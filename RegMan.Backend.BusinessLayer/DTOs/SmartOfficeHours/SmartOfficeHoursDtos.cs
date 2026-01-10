namespace RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours
{
    public class SmartOfficeHoursQueueEntryDto
    {
        public int QueueEntryId { get; set; }
        public string StudentUserId { get; set; } = string.Empty;
        public string? StudentFullName { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime EnqueuedAtUtc { get; set; }
        public DateTime? ReadyAtUtc { get; set; }
        public DateTime? InProgressAtUtc { get; set; }
        public DateTime? ReadyExpiresAtUtc { get; set; }
        public int? Position { get; set; }
        public int? EstimatedWaitMinutes { get; set; }
    }

    public class SmartOfficeHoursProviderViewDto
    {
        public int OfficeHourId { get; set; }
        public int SessionId { get; set; }
        public string ProviderUserId { get; set; } = string.Empty;
        public string SessionStatus { get; set; } = string.Empty;
        public DateTime ServerTimeUtc { get; set; }

        public SmartOfficeHoursQueueEntryDto? CurrentReadyOrInProgress { get; set; }
        public List<SmartOfficeHoursQueueEntryDto> Queue { get; set; } = new();

        // Present only when CurrentReadyOrInProgress is Ready
        public string? CurrentQrToken { get; set; }
        public DateTime? CurrentQrExpiresAtUtc { get; set; }
    }

    public class SmartOfficeHoursStudentViewDto
    {
        public int OfficeHourId { get; set; }
        public int SessionId { get; set; }
        public DateTime ServerTimeUtc { get; set; }

        public int? QueueEntryId { get; set; }
        public string? Status { get; set; }
        public int? Position { get; set; }
        public int? EstimatedWaitMinutes { get; set; }
        public DateTime? ReadyExpiresAtUtc { get; set; }
    }

    public class SmartOfficeHoursJoinQueueRequestDto
    {
        public string? Purpose { get; set; }
    }

    public class SmartOfficeHoursScanRequestDto
    {
        public string Token { get; set; } = string.Empty;
    }
}
