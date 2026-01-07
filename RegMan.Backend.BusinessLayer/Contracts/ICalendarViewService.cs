using RegMan.Backend.BusinessLayer.DTOs.Calendar;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ICalendarViewService
    {
        Task<CalendarViewResponseDTO> GetCalendarViewAsync(
            string userId,
            string userRole,
            DateTime rangeStartUtc,
            DateTime rangeEndUtc,
            CancellationToken cancellationToken);
    }
}
