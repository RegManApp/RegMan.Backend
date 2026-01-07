using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using RegMan.Backend.DAL.Entities.Calendar;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class CalendarReminderEngine : ICalendarReminderEngine
    {
        private readonly AppDbContext context;
        private readonly INotificationService notificationService;
        private readonly IReminderRulesService rulesService;
        private readonly ICalendarPreferencesService preferencesService;

        public CalendarReminderEngine(
            AppDbContext context,
            INotificationService notificationService,
            IReminderRulesService rulesService,
            ICalendarPreferencesService preferencesService)
        {
            this.context = context;
            this.notificationService = notificationService;
            this.rulesService = rulesService;
            this.preferencesService = preferencesService;
        }

        public async Task EnsurePlannedForUserAsync(string userId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            // Deterministic + lightweight: plan only a short horizon, and only for this user.
            var horizonDays = 7;
            var horizonUtc = nowUtc.Date.AddDays(horizonDays);

            var rules = await rulesService.GetRulesAsync(userId, cancellationToken);
            var prefs = await preferencesService.GetAsync(userId, cancellationToken);

            int GetMinutesBefore(ReminderTriggerType trigger)
            {
                var enabled = rules.FirstOrDefault(r => r.TriggerType == trigger && r.IsEnabled);
                if (enabled != null)
                    return enabled.MinutesBefore;
                return prefs.DefaultReminderMinutes ?? 15;
            }

            // 1) Office hour bookings (pending/confirmed)
            var student = await context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
            if (student != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.OfficeHour);
                var bookings = await context.OfficeHourBookings
                    .Include(b => b.OfficeHour)
                        .ThenInclude(oh => oh.Instructor)
                            .ThenInclude(i => i.User)
                    .AsNoTracking()
                    .Where(b => b.StudentId == student.StudentId
                                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                                && b.OfficeHour.Date >= nowUtc.Date
                                && b.OfficeHour.Date <= horizonUtc)
                    .ToListAsync(cancellationToken);

                foreach (var booking in bookings)
                {
                    var startUtc = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.StartTime);
                    var scheduledAt = startUtc.AddMinutes(-mins);
                    if (scheduledAt <= nowUtc.AddMinutes(-1))
                        continue;

                    await UpsertScheduledAsync(
                        userId,
                        ReminderTriggerType.OfficeHour,
                        sourceEntityType: "OfficeHourBooking",
                        sourceEntityId: booking.BookingId,
                        scheduledAtUtc: scheduledAt,
                        title: "Upcoming Office Hour",
                        message: $"Office hour with {booking.OfficeHour.Instructor.User.FullName} starts at {startUtc:HH:mm} UTC.",
                        notificationEntityType: "OfficeHourBooking",
                        notificationEntityId: booking.BookingId,
                        cancellationToken);
                }
            }

            // Instructor: office hour bookings (pending/confirmed)
            var instructor = await context.Instructors.AsNoTracking().FirstOrDefaultAsync(i => i.UserId == userId, cancellationToken);
            if (instructor != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.OfficeHour);
                var bookings = await context.OfficeHourBookings
                    .Include(b => b.OfficeHour)
                        .ThenInclude(oh => oh.Instructor)
                            .ThenInclude(i => i.User)
                    .Include(b => b.Student)
                        .ThenInclude(s => s.User)
                    .AsNoTracking()
                    .Where(b => b.OfficeHour.InstructorId == instructor.InstructorId
                                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                                && b.OfficeHour.Date >= nowUtc.Date
                                && b.OfficeHour.Date <= horizonUtc)
                    .ToListAsync(cancellationToken);

                foreach (var booking in bookings)
                {
                    var startUtc = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.StartTime);
                    var scheduledAt = startUtc.AddMinutes(-mins);
                    if (scheduledAt <= nowUtc.AddMinutes(-1))
                        continue;

                    await UpsertScheduledAsync(
                        userId,
                        ReminderTriggerType.OfficeHour,
                        sourceEntityType: "OfficeHourBooking",
                        sourceEntityId: booking.BookingId,
                        scheduledAtUtc: scheduledAt,
                        title: "Upcoming Office Hour",
                        message: $"Office hour with {booking.Student.User.FullName} starts at {startUtc:HH:mm} UTC.",
                        notificationEntityType: "OfficeHourBooking",
                        notificationEntityId: booking.BookingId,
                        cancellationToken);
                }
            }

            // 2) Class reminders (based on enrollments schedule; short horizon)
            if (student != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.Class);
                var enrollments = await context.Enrollments
                    .Include(e => e.Section)
                        .ThenInclude(s => s!.Course)
                    .Include(e => e.Section)
                        .ThenInclude(s => s!.Slots)
                            .ThenInclude(sl => sl.TimeSlot)
                    .AsNoTracking()
                    .Where(e => e.StudentId == student.StudentId
                                && (e.Status == Status.Enrolled || e.Status == Status.Pending))
                    .ToListAsync(cancellationToken);

                var startDate = nowUtc.Date;
                var endDate = horizonUtc.Date;

                foreach (var enrollment in enrollments)
                {
                    if (enrollment.Section?.Slots == null)
                        continue;

                    foreach (var slot in enrollment.Section.Slots)
                    {
                        if (slot.TimeSlot == null)
                            continue;

                        var current = startDate;
                        while (current <= endDate)
                        {
                            if (current.DayOfWeek == slot.TimeSlot.Day)
                            {
                                var classStartUtc = current.Add(slot.TimeSlot.StartTime);
                                var scheduledAt = classStartUtc.AddMinutes(-mins);
                                if (scheduledAt > nowUtc.AddMinutes(-1))
                                {
                                    var sessionKey = $"{enrollment.EnrollmentId}-{slot.ScheduleSlotId}-{current:yyyyMMdd}";
                                    await UpsertScheduledAsync(
                                        userId,
                                        ReminderTriggerType.Class,
                                        sourceEntityType: "ClassSession",
                                        sourceEntityId: null,
                                        scheduledAtUtc: scheduledAt,
                                        title: "Upcoming Class",
                                        message: $"{enrollment.Section.Course?.CourseName ?? "Class"} starts at {classStartUtc:HH:mm} UTC.",
                                        notificationEntityType: "ClassSession",
                                        notificationEntityId: null,
                                        cancellationToken,
                                        dedupeKey: sessionKey);
                                }
                            }

                            current = current.AddDays(1);
                        }
                    }
                }
            }

            // Instructor: teaching reminders (based on ScheduleSlots; short horizon)
            if (instructor != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.Class);
                var scheduleSlots = await context.ScheduleSlots
                    .Include(ss => ss.Section)
                        .ThenInclude(s => s.Course)
                    .Include(ss => ss.TimeSlot)
                    .AsNoTracking()
                    .Where(ss => ss.InstructorId == instructor.InstructorId)
                    .ToListAsync(cancellationToken);

                var startDate = nowUtc.Date;
                var endDate = horizonUtc.Date;

                foreach (var slot in scheduleSlots)
                {
                    if (slot.TimeSlot == null || slot.Section?.Course == null)
                        continue;

                    var current = startDate;
                    while (current <= endDate)
                    {
                        if (current.DayOfWeek == slot.TimeSlot.Day)
                        {
                            var classStartUtc = current.Add(slot.TimeSlot.StartTime);
                            var scheduledAt = classStartUtc.AddMinutes(-mins);
                            if (scheduledAt > nowUtc.AddMinutes(-1))
                            {
                                var sessionKey = $"teaching-{slot.ScheduleSlotId}-{current:yyyyMMdd}";
                                await UpsertScheduledAsync(
                                    userId,
                                    ReminderTriggerType.Class,
                                    sourceEntityType: "TeachingSession",
                                    sourceEntityId: null,
                                    scheduledAtUtc: scheduledAt,
                                    title: "Upcoming Class",
                                    message: $"{slot.Section.Course.CourseName} ({slot.Section.SectionName}) starts at {classStartUtc:HH:mm} UTC.",
                                    notificationEntityType: "TeachingSession",
                                    notificationEntityId: null,
                                    cancellationToken,
                                    dedupeKey: sessionKey);
                            }
                        }

                        current = current.AddDays(1);
                    }
                }
            }

            // 3) Registration/withdraw deadlines (per-user planning)
            var settings = await context.AcademicCalendarSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default", cancellationToken);

            if (settings?.RegistrationEndDateUtc != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.RegistrationDeadline);
                var deadline = settings.RegistrationEndDateUtc.Value.Date.AddDays(1).AddMinutes(-1); // inclusive end-of-day
                var scheduledAt = deadline.AddMinutes(-mins);
                if (scheduledAt > nowUtc.AddMinutes(-1) && scheduledAt <= horizonUtc.AddDays(30))
                {
                    await UpsertScheduledAsync(
                        userId,
                        ReminderTriggerType.RegistrationDeadline,
                        sourceEntityType: "AcademicCalendarSettings",
                        sourceEntityId: null,
                        scheduledAtUtc: scheduledAt,
                        title: "Registration Deadline",
                        message: $"Registration closes on {deadline:yyyy-MM-dd} (UTC).",
                        notificationEntityType: null,
                        notificationEntityId: null,
                        cancellationToken,
                        dedupeKey: "registration-deadline");
                }
            }

            if (settings?.WithdrawEndDateUtc != null)
            {
                var mins = GetMinutesBefore(ReminderTriggerType.WithdrawDeadline);
                var deadline = settings.WithdrawEndDateUtc.Value.Date.AddDays(1).AddMinutes(-1);
                var scheduledAt = deadline.AddMinutes(-mins);
                if (scheduledAt > nowUtc.AddMinutes(-1) && scheduledAt <= horizonUtc.AddDays(30))
                {
                    await UpsertScheduledAsync(
                        userId,
                        ReminderTriggerType.WithdrawDeadline,
                        sourceEntityType: "AcademicCalendarSettings",
                        sourceEntityId: null,
                        scheduledAtUtc: scheduledAt,
                        title: "Withdraw Deadline",
                        message: $"Withdraw period ends on {deadline:yyyy-MM-dd} (UTC).",
                        notificationEntityType: null,
                        notificationEntityId: null,
                        cancellationToken,
                        dedupeKey: "withdraw-deadline");
                }
            }
        }

        public async Task DispatchDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            var due = await context.ScheduledNotifications
                .Where(n => n.Status == ScheduledNotificationStatus.Pending
                            && n.ScheduledAtUtc <= nowUtc
                            && n.AttemptCount < 3)
                .OrderBy(n => n.ScheduledAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (var item in due)
            {
                item.AttemptCount += 1;
                item.LastAttemptAtUtc = nowUtc;

                try
                {
                    var type = item.TriggerType == ReminderTriggerType.Class
                        ? NotificationType.ClassReminder
                        : NotificationType.DeadlineReminder;

                    if (item.TriggerType == ReminderTriggerType.OfficeHour)
                        type = NotificationType.OfficeHourReminder;

                    await notificationService.CreateNotificationAsync(
                        item.UserId,
                        type,
                        item.Title,
                        item.Message,
                        item.EntityType,
                        item.EntityId);

                    item.Status = ScheduledNotificationStatus.Sent;
                }
                catch
                {
                    item.Status = item.AttemptCount >= 3
                        ? ScheduledNotificationStatus.Failed
                        : ScheduledNotificationStatus.Pending;
                }
            }

            if (due.Count > 0)
                await context.SaveChangesAsync(cancellationToken);
        }

        public async Task CancelOfficeHourRemindersAsync(int bookingId, CancellationToken cancellationToken)
        {
            var scheduled = await context.ScheduledNotifications
                .Where(n => n.SourceEntityType == "OfficeHourBooking" && n.SourceEntityId == bookingId && n.Status == ScheduledNotificationStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var s in scheduled)
                s.Status = ScheduledNotificationStatus.Cancelled;

            if (scheduled.Count > 0)
                await context.SaveChangesAsync(cancellationToken);
        }

        private async Task UpsertScheduledAsync(
            string userId,
            ReminderTriggerType trigger,
            string? sourceEntityType,
            int? sourceEntityId,
            DateTime scheduledAtUtc,
            string title,
            string message,
            string? notificationEntityType,
            int? notificationEntityId,
            CancellationToken cancellationToken,
            string? dedupeKey = null)
        {
            // We keep it simple: dedupe by a stable key in SourceEntityType when needed.
            var effectiveSourceType = string.IsNullOrWhiteSpace(dedupeKey) ? sourceEntityType : $"{sourceEntityType}:{dedupeKey}";

            var exists = await context.ScheduledNotifications
                .AnyAsync(n => n.UserId == userId
                              && n.TriggerType == trigger
                              && n.SourceEntityType == effectiveSourceType
                              && n.SourceEntityId == sourceEntityId
                              && n.ScheduledAtUtc == scheduledAtUtc
                              && n.Status != ScheduledNotificationStatus.Cancelled,
                    cancellationToken);

            if (exists)
                return;

            context.ScheduledNotifications.Add(new ScheduledNotification
            {
                UserId = userId,
                TriggerType = trigger,
                SourceEntityType = effectiveSourceType,
                SourceEntityId = sourceEntityId,
                ScheduledAtUtc = scheduledAtUtc,
                Title = title,
                Message = message,
                EntityType = notificationEntityType,
                EntityId = notificationEntityId,
                Status = ScheduledNotificationStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
