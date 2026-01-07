using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Calendar;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities.Calendar;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class CalendarPreferencesService : ICalendarPreferencesService
    {
        private readonly AppDbContext context;

        public CalendarPreferencesService(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<CalendarPreferencesDTO> GetAsync(string userId, CancellationToken cancellationToken)
        {
            var entity = await context.UserCalendarPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (entity == null)
            {
                return new CalendarPreferencesDTO
                {
                    TimeZoneId = "UTC",
                    WeekStartDay = "Mon",
                    HideWeekends = false,
                    DefaultReminderMinutes = 15,
                    EventTypeColorMapJson = null
                };
            }

            return new CalendarPreferencesDTO
            {
                TimeZoneId = string.IsNullOrWhiteSpace(entity.TimeZoneId) ? "UTC" : entity.TimeZoneId,
                WeekStartDay = string.IsNullOrWhiteSpace(entity.WeekStartDay) ? "Mon" : entity.WeekStartDay,
                HideWeekends = entity.HideWeekends,
                DefaultReminderMinutes = entity.DefaultReminderMinutes,
                EventTypeColorMapJson = entity.EventTypeColorMapJson
            };
        }

        public async Task<CalendarPreferencesDTO> UpsertAsync(string userId, CalendarPreferencesDTO dto, CancellationToken cancellationToken)
        {
            var week = dto.WeekStartDay?.Trim();
            if (week is not ("Sun" or "Mon"))
                week = "Mon";

            var tz = string.IsNullOrWhiteSpace(dto.TimeZoneId) ? "UTC" : dto.TimeZoneId.Trim();

            var entity = await context.UserCalendarPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

            if (entity == null)
            {
                entity = new UserCalendarPreferences { UserId = userId };
                context.UserCalendarPreferences.Add(entity);
            }

            entity.TimeZoneId = tz;
            entity.WeekStartDay = week;
            entity.HideWeekends = dto.HideWeekends;
            entity.DefaultReminderMinutes = dto.DefaultReminderMinutes;
            entity.EventTypeColorMapJson = dto.EventTypeColorMapJson;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            return await GetAsync(userId, cancellationToken);
        }
    }
}
