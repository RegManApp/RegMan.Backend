namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ICalendarReminderEngine
    {
        Task EnsurePlannedForUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken);
        Task DispatchDueAsync(DateTime nowUtc, CancellationToken cancellationToken);
        Task CancelOfficeHourRemindersAsync(int bookingId, CancellationToken cancellationToken);
    }
}
