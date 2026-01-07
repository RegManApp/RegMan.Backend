using RegMan.Backend.BusinessLayer.DTOs.Calendar;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ICalendarPreferencesService
    {
        Task<CalendarPreferencesDTO> GetAsync(string userId, CancellationToken cancellationToken);
        Task<CalendarPreferencesDTO> UpsertAsync(string userId, CalendarPreferencesDTO dto, CancellationToken cancellationToken);
    }
}
