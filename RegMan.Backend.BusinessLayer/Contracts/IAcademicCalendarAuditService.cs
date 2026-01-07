using RegMan.Backend.BusinessLayer.DTOs.Calendar;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IAcademicCalendarAuditService
    {
        Task LogChangeAsync(string actorUserId, string actorEmail, AcademicCalendarSettings before, AcademicCalendarSettings after, string action, CancellationToken cancellationToken);
        Task<List<CalendarAuditEntryDTO>> GetAuditAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
        Task<bool> RestoreAsync(int auditEntryId, string actorUserId, string actorEmail, CancellationToken cancellationToken);
    }
}
