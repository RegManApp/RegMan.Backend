using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Calendar;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class CalendarViewService : ICalendarViewService
    {
        private readonly IUnitOfWork unitOfWork;

        private DbContext Db => unitOfWork.Context;

        public CalendarViewService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public async Task<CalendarViewResponseDTO> GetCalendarViewAsync(
            string userId,
            string userRole,
            DateTime rangeStartUtc,
            DateTime rangeEndUtc,
            CancellationToken cancellationToken)
        {
            var events = new List<CalendarViewEventDTO>();

            // Global academic timeline events (safe for all roles)
            var settings = await unitOfWork.AcademicCalendarSettings
                .GetAllAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default", cancellationToken);

            AddAcademicTimelineEvents(events, settings);

            if (userRole == "Student")
            {
                await AddStudentEventsAsync(events, userId, rangeStartUtc, rangeEndUtc, cancellationToken);
            }
            else if (userRole == "Instructor")
            {
                await AddInstructorEventsAsync(events, userId, rangeStartUtc, rangeEndUtc, cancellationToken);
            }
            else if (userRole == "Admin")
            {
                // Admin view: global academic events only (avoid exposing individual course sessions).
            }

            var conflicts = ComputeConflicts(events);

            return new CalendarViewResponseDTO
            {
                ViewRole = userRole,
                DateRange = new CalendarViewDateRangeDTO { StartUtc = rangeStartUtc, EndUtc = rangeEndUtc },
                Events = events,
                Conflicts = conflicts
            };
        }

        private static List<CalendarConflictDTO> ComputeConflicts(List<CalendarViewEventDTO> events)
        {
            // Ignore all-day academic markers for conflicts.
            static bool IsConflictRelevant(CalendarViewEventDTO e)
            {
                if (e.Type is "registration" or "withdraw")
                    return false;
                return e.EndUtc > e.StartUtc;
            }

            static bool Overlaps(CalendarViewEventDTO a, CalendarViewEventDTO b)
                => a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc;

            static (string conflictType, string severity) Classify(CalendarViewEventDTO a, CalendarViewEventDTO b)
            {
                var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { a.Type, b.Type };

                bool hasClass = types.Contains("class") || types.Contains("teaching");
                bool hasOfficeHour = types.Contains("office-hour") || types.Contains("office-hour-booking");

                if (hasClass && hasOfficeHour)
                    return ("ClassVsOfficeHour", "Critical");

                if (types.Contains("class") && types.Contains("class"))
                    return ("ClassOverlap", "Critical");

                if (types.Contains("teaching") && types.Contains("teaching"))
                    return ("TeachingOverlap", "Critical");

                if (types.Contains("office-hour-booking") && types.Contains("office-hour-booking"))
                    return ("OfficeHourBookingOverlap", "Critical");

                if (types.Contains("office-hour") && types.Contains("office-hour"))
                    return ("OfficeHourOverlap", "Warning");

                if (hasOfficeHour)
                    return ("OfficeHourOverlap", "Warning");

                return ("ScheduleConflict", "Info");
            }

            var relevant = events.Where(IsConflictRelevant)
                .OrderBy(e => e.StartUtc)
                .ThenBy(e => e.EndUtc)
                .ToList();

            var results = new List<CalendarConflictDTO>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < relevant.Count; i++)
            {
                var a = relevant[i];
                for (int j = i + 1; j < relevant.Count; j++)
                {
                    var b = relevant[j];
                    if (b.StartUtc >= a.EndUtc)
                        break;

                    if (!Overlaps(a, b))
                        continue;

                    var (conflictType, severity) = Classify(a, b);
                    var key = string.CompareOrdinal(a.Id, b.Id) < 0 ? $"{a.Id}|{b.Id}|{conflictType}" : $"{b.Id}|{a.Id}|{conflictType}";
                    if (!seen.Add(key))
                        continue;

                    results.Add(new CalendarConflictDTO
                    {
                        ConflictType = conflictType,
                        Severity = severity,
                        StartUtc = a.StartUtc > b.StartUtc ? a.StartUtc : b.StartUtc,
                        EndUtc = a.EndUtc < b.EndUtc ? a.EndUtc : b.EndUtc,
                        EventIdA = a.Id,
                        EventIdB = b.Id
                    });
                }
            }

            return results;
        }

        private static void AddAcademicTimelineEvents(List<CalendarViewEventDTO> events, AcademicCalendarSettings? settings)
        {
            if (settings?.RegistrationStartDateUtc != null)
            {
                var d = settings.RegistrationStartDateUtc.Value.Date;
                events.Add(new CalendarViewEventDTO
                {
                    Id = "academic-registration-start",
                    TitleKey = "calendar.special.registrationStarts",
                    StartUtc = d,
                    EndUtc = d,
                    Type = "registration"
                });
            }

            if (settings?.RegistrationEndDateUtc != null)
            {
                var d = settings.RegistrationEndDateUtc.Value.Date;
                events.Add(new CalendarViewEventDTO
                {
                    Id = "academic-registration-end",
                    TitleKey = "calendar.special.registrationEnds",
                    StartUtc = d,
                    EndUtc = d,
                    Type = "registration"
                });
            }

            if (settings?.WithdrawStartDateUtc != null)
            {
                var d = settings.WithdrawStartDateUtc.Value.Date;
                events.Add(new CalendarViewEventDTO
                {
                    Id = "academic-withdraw-start",
                    TitleKey = "calendar.special.withdrawStarts",
                    StartUtc = d,
                    EndUtc = d,
                    Type = "withdraw"
                });
            }

            if (settings?.WithdrawEndDateUtc != null)
            {
                var d = settings.WithdrawEndDateUtc.Value.Date;
                events.Add(new CalendarViewEventDTO
                {
                    Id = "academic-withdraw-end",
                    TitleKey = "calendar.special.withdrawEnds",
                    StartUtc = d,
                    EndUtc = d,
                    Type = "withdraw"
                });
            }
        }

        private async Task AddStudentEventsAsync(
            List<CalendarViewEventDTO> events,
            string userId,
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken cancellationToken)
        {
            var student = await Db.Set<StudentProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

            if (student == null)
                return;

            // Office hour bookings (pending/confirmed)
            var bookings = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .AsNoTracking()
                .Where(b => b.StudentId == student.StudentId
                            && b.OfficeHour.Date >= startUtc
                            && b.OfficeHour.Date <= endUtc
                            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .ToListAsync(cancellationToken);

            foreach (var booking in bookings)
            {
                if (booking.OfficeHour?.Instructor?.User == null)
                    continue;

                var start = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.StartTime);
                var end = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.EndTime);

                events.Add(new CalendarViewEventDTO
                {
                    Id = $"booking-{booking.BookingId}",
                    Title = $"Office Hour with {booking.OfficeHour.Instructor.User.FullName}",
                    StartUtc = start,
                    EndUtc = end,
                    Type = "office-hour-booking",
                    Status = booking.Status.ToString(),
                    ExtendedProps = new OfficeHourBookingPropsDTO
                    {
                        BookingId = booking.BookingId,
                        InstructorName = booking.OfficeHour.Instructor.User.FullName,
                        Room = booking.OfficeHour.Room != null
                            ? $"{booking.OfficeHour.Room.RoomNumber} ({booking.OfficeHour.Room.Building})"
                            : "TBD",
                        Purpose = booking.Purpose,
                        Notes = booking.BookerNotes
                    }
                });
            }

            // Registered class sessions (expanded across requested date range)
            var enrollments = await Db.Set<Enrollment>()
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Course)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Slots)
                        .ThenInclude(sl => sl.TimeSlot)
                .Include(e => e.Section)
                    .ThenInclude(s => s!.Slots)
                        .ThenInclude(sl => sl.Room)
                .AsNoTracking()
                .Where(e => e.StudentId == student.StudentId
                            && (e.Status == Status.Enrolled || e.Status == Status.Pending))
                .ToListAsync(cancellationToken);

            foreach (var enrollment in enrollments)
            {
                if (enrollment.Section?.Slots == null)
                    continue;

                foreach (var slot in enrollment.Section.Slots)
                {
                    if (slot.TimeSlot == null)
                        continue;

                    var currentDate = startUtc.Date;
                    var endDate = endUtc.Date;
                    while (currentDate <= endDate)
                    {
                        if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                        {
                            var start = currentDate.Date.Add(slot.TimeSlot.StartTime);
                            var end = currentDate.Date.Add(slot.TimeSlot.EndTime);

                            events.Add(new CalendarViewEventDTO
                            {
                                Id = $"class-{enrollment.EnrollmentId}-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                                Title = $"{enrollment.Section.Course?.CourseName ?? "Unknown"} ({enrollment.Section.SectionName})",
                                StartUtc = start,
                                EndUtc = end,
                                Type = "class",
                                ExtendedProps = new CourseSessionPropsDTO
                                {
                                    CourseCode = enrollment.Section.Course?.CourseCode ?? string.Empty,
                                    SectionName = enrollment.Section.SectionName,
                                    Room = slot.Room != null
                                        ? $"{slot.Room.RoomNumber} ({slot.Room.Building})"
                                        : "TBD"
                                }
                            });
                        }

                        currentDate = currentDate.AddDays(1);
                    }
                }
            }
        }

        private async Task AddInstructorEventsAsync(
            List<CalendarViewEventDTO> events,
            string userId,
            DateTime startUtc,
            DateTime endUtc,
            CancellationToken cancellationToken)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.UserId == userId, cancellationToken);

            if (instructor == null)
                return;

            // Office hours (available vs booked)
            var officeHours = await Db.Set<OfficeHour>()
                .Include(oh => oh.Room)
                .Include(oh => oh.Bookings)
                    .ThenInclude(b => b.Student)
                        .ThenInclude(s => s.User)
                .AsNoTracking()
                .Where(oh => oh.InstructorId == instructor.InstructorId
                            && oh.Date >= startUtc
                            && oh.Date <= endUtc
                            && oh.Status != OfficeHourStatus.Cancelled)
                .ToListAsync(cancellationToken);

            foreach (var oh in officeHours)
            {
                var activeBooking = oh.Bookings?.FirstOrDefault(b =>
                    b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

                var start = oh.Date.Date.Add(oh.StartTime);
                var end = oh.Date.Date.Add(oh.EndTime);

                events.Add(new CalendarViewEventDTO
                {
                    Id = $"office-hour-{oh.OfficeHourId}",
                    Title = activeBooking?.Student?.User != null
                        ? $"Office Hour: {activeBooking.Student.User.FullName}"
                        : "Office Hour (Available)",
                    StartUtc = start,
                    EndUtc = end,
                    Type = "office-hour",
                    Status = oh.Status.ToString(),
                    ExtendedProps = new InstructorOfficeHourPropsDTO
                    {
                        OfficeHourId = oh.OfficeHourId,
                        Room = oh.Room != null
                            ? $"{oh.Room.RoomNumber} ({oh.Room.Building})"
                            : "TBD",
                        Notes = oh.Notes,
                        Booking = activeBooking?.Student?.User != null
                            ? new InstructorOfficeHourActiveBookingPropsDTO
                            {
                                BookingId = activeBooking.BookingId,
                                StudentName = activeBooking.Student.User.FullName,
                                Purpose = activeBooking.Purpose,
                                Status = activeBooking.Status.ToString()
                            }
                            : null
                    }
                });
            }

            // Teaching schedule (expanded across requested date range)
            var scheduleSlots = await Db.Set<ScheduleSlot>()
                .Include(ss => ss.Section)
                    .ThenInclude(s => s.Course)
                .Include(ss => ss.TimeSlot)
                .Include(ss => ss.Room)
                .AsNoTracking()
                .Where(ss => ss.InstructorId == instructor.InstructorId)
                .ToListAsync(cancellationToken);

            foreach (var slot in scheduleSlots)
            {
                if (slot.TimeSlot == null || slot.Section?.Course == null)
                    continue;

                var currentDate = startUtc.Date;
                var endDate = endUtc.Date;
                while (currentDate <= endDate)
                {
                    if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                    {
                        var start = currentDate.Date.Add(slot.TimeSlot.StartTime);
                        var end = currentDate.Date.Add(slot.TimeSlot.EndTime);

                        events.Add(new CalendarViewEventDTO
                        {
                            Id = $"teaching-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                            Title = $"{slot.Section.Course.CourseName} ({slot.Section.SectionName})",
                            StartUtc = start,
                            EndUtc = end,
                            Type = "teaching",
                            ExtendedProps = new CourseSessionPropsDTO
                            {
                                CourseCode = slot.Section.Course.CourseCode,
                                SectionName = slot.Section.SectionName,
                                Room = slot.Room != null
                                    ? $"{slot.Room.RoomNumber} ({slot.Room.Building})"
                                    : "TBD"
                            }
                        });
                    }

                    currentDate = currentDate.AddDays(1);
                }
            }
        }
    }
}

