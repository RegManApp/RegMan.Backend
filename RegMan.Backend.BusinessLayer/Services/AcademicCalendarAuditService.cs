using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Calendar;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using RegMan.Backend.DAL.Entities.Calendar;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class AcademicCalendarAuditService : IAcademicCalendarAuditService
    {
        private readonly IUnitOfWork unitOfWork;

        private DbContext Db => unitOfWork.Context;

        public AcademicCalendarAuditService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        private static object Snapshot(AcademicCalendarSettings s) => new
        {
            s.SettingsKey,
            s.RegistrationStartDateUtc,
            s.RegistrationEndDateUtc,
            s.WithdrawStartDateUtc,
            s.WithdrawEndDateUtc,
            s.UpdatedAtUtc
        };

        public async Task LogChangeAsync(string actorUserId, string actorEmail, AcademicCalendarSettings before, AcademicCalendarSettings after, string action, CancellationToken cancellationToken)
        {
            var entry = new CalendarAuditEntry
            {
                ActorUserId = actorUserId,
                ActorEmail = actorEmail,
                TargetType = "AcademicCalendarSettings",
                TargetKey = after.SettingsKey,
                Action = action,
                BeforeJson = JsonSerializer.Serialize(Snapshot(before)),
                AfterJson = JsonSerializer.Serialize(Snapshot(after)),
                CreatedAtUtc = DateTime.UtcNow
            };

            Db.Set<CalendarAuditEntry>().Add(entry);
            await unitOfWork.SaveChangesAsync();
        }

        public async Task<List<CalendarAuditEntryDTO>> GetAuditAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
        {
            var query = Db.Set<CalendarAuditEntry>().AsNoTracking().AsQueryable();

            if (fromUtc.HasValue)
                query = query.Where(a => a.CreatedAtUtc >= fromUtc.Value);
            if (toUtc.HasValue)
                query = query.Where(a => a.CreatedAtUtc <= toUtc.Value);

            var items = await query
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            return items.Select(a => new CalendarAuditEntryDTO
            {
                CalendarAuditEntryId = a.CalendarAuditEntryId,
                ActorUserId = a.ActorUserId,
                ActorEmail = a.ActorEmail,
                TargetType = a.TargetType,
                TargetKey = a.TargetKey,
                Action = a.Action,
                BeforeJson = a.BeforeJson,
                AfterJson = a.AfterJson,
                CreatedAtUtc = a.CreatedAtUtc
            }).ToList();
        }

        private sealed class AcademicCalendarSnapshot
        {
            public string SettingsKey { get; set; } = "default";
            public DateTime? RegistrationStartDateUtc { get; set; }
            public DateTime? RegistrationEndDateUtc { get; set; }
            public DateTime? WithdrawStartDateUtc { get; set; }
            public DateTime? WithdrawEndDateUtc { get; set; }
        }

        public async Task<bool> RestoreAsync(int auditEntryId, string actorUserId, string actorEmail, CancellationToken cancellationToken)
        {
            var entry = await Db.Set<CalendarAuditEntry>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CalendarAuditEntryId == auditEntryId, cancellationToken);

            if (entry == null)
                return false;

            var snapshot = JsonSerializer.Deserialize<AcademicCalendarSnapshot>(entry.BeforeJson);
            if (snapshot == null)
                return false;

            var settings = await unitOfWork.AcademicCalendarSettings.GetAllAsQueryable()
                .FirstOrDefaultAsync(s => s.SettingsKey == snapshot.SettingsKey, cancellationToken);

            if (settings == null)
            {
                settings = new AcademicCalendarSettings { SettingsKey = snapshot.SettingsKey };
                await unitOfWork.AcademicCalendarSettings.AddAsync(settings);
            }

            var before = new AcademicCalendarSettings
            {
                SettingsKey = settings.SettingsKey,
                RegistrationStartDateUtc = settings.RegistrationStartDateUtc,
                RegistrationEndDateUtc = settings.RegistrationEndDateUtc,
                WithdrawStartDateUtc = settings.WithdrawStartDateUtc,
                WithdrawEndDateUtc = settings.WithdrawEndDateUtc,
                UpdatedAtUtc = settings.UpdatedAtUtc
            };

            settings.RegistrationStartDateUtc = snapshot.RegistrationStartDateUtc;
            settings.RegistrationEndDateUtc = snapshot.RegistrationEndDateUtc;
            settings.WithdrawStartDateUtc = snapshot.WithdrawStartDateUtc;
            settings.WithdrawEndDateUtc = snapshot.WithdrawEndDateUtc;
            settings.UpdatedAtUtc = DateTime.UtcNow;

            await unitOfWork.SaveChangesAsync();

            await LogChangeAsync(actorUserId, actorEmail, before, settings, action: "RESTORE", cancellationToken);
            return true;
        }
    }
}
