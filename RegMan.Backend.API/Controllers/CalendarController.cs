using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CalendarController(AppDbContext context)
        {
            _context = context;
        }

        private static string FormatDate(DateTime? utcDate)
        {
            return utcDate?.ToString("yyyy-MM-dd") ?? "";
        }

        private static object ComputeTimelineStatus(AcademicCalendarSettings? settings, DateTime nowUtc)
        {
            var todayUtc = nowUtc.Date;
            var regStart = settings?.RegistrationStartDateUtc?.Date;
            var regEnd = settings?.RegistrationEndDateUtc?.Date;
            var withdrawStart = settings?.WithdrawStartDateUtc?.Date;
            var withdrawEnd = settings?.WithdrawEndDateUtc?.Date;

            string phase;
            DateTime? countdownTargetUtc = null;

            if (regStart.HasValue && todayUtc < regStart.Value)
            {
                phase = "Closed";
                countdownTargetUtc = regStart.Value;
            }
            else if (regStart.HasValue && regEnd.HasValue && todayUtc >= regStart.Value && todayUtc <= regEnd.Value)
            {
                phase = "Open";
                countdownTargetUtc = regEnd.Value.AddDays(1); // end-of-day inclusive UX
            }
            else if (regEnd.HasValue && withdrawStart.HasValue && todayUtc > regEnd.Value && todayUtc < withdrawStart.Value)
            {
                phase = "Closed";
                countdownTargetUtc = withdrawStart.Value;
            }
            else if (withdrawStart.HasValue && withdrawEnd.HasValue && todayUtc >= withdrawStart.Value && todayUtc <= withdrawEnd.Value)
            {
                phase = "Withdraw period";
                countdownTargetUtc = withdrawEnd.Value.AddDays(1); // end-of-day inclusive UX
            }
            else
            {
                phase = "Closed";
                countdownTargetUtc = null;
            }

            return new
            {
                phase,
                nowUtc,
                countdownTargetUtc,
            };
        }

        /// <summary>
        /// Get registration and withdrawal dates
        /// </summary>
        [HttpGet("registration-withdraw-dates")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRegistrationWithdrawDates()
        {
            var settings = await _context.AcademicCalendarSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            var status = ComputeTimelineStatus(settings, DateTime.UtcNow);

            var payload = new
            {
                registrationStartDate = FormatDate(settings?.RegistrationStartDateUtc),
                registrationEndDate = FormatDate(settings?.RegistrationEndDateUtc),
                withdrawStartDate = FormatDate(settings?.WithdrawStartDateUtc),
                withdrawEndDate = FormatDate(settings?.WithdrawEndDateUtc),
                status
            };

            return Ok(ApiResponse<object>.SuccessResponse(payload));
        }

        /// <summary>
        /// Public timeline endpoint (student/instructor read-only)
        /// </summary>
        [HttpGet("timeline")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTimeline()
        {
            var settings = await _context.AcademicCalendarSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingsKey == "default");

            var nowUtc = DateTime.UtcNow;
            var status = ComputeTimelineStatus(settings, nowUtc);

            var payload = new
            {
                registrationStartDate = FormatDate(settings?.RegistrationStartDateUtc),
                registrationEndDate = FormatDate(settings?.RegistrationEndDateUtc),
                withdrawStartDate = FormatDate(settings?.WithdrawStartDateUtc),
                withdrawEndDate = FormatDate(settings?.WithdrawEndDateUtc),
                status
            };

            return Ok(ApiResponse<object>.SuccessResponse(payload));
        }

        /// <summary>
        /// Get all calendar events for the current user
        /// </summary>
        [HttpGet("events")]
        public async Task<IActionResult> GetCalendarEvents(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userRole = User.FindFirstValue(ClaimTypes.Role);

                var events = new List<object>();
                // Support both parameter naming conventions
                var start = startDate ?? fromDate ?? DateTime.UtcNow.Date.AddMonths(-1);
                var end = endDate ?? toDate ?? DateTime.UtcNow.Date.AddMonths(3);

                if (userRole == "Student")
                {
                    var student = await _context.Students
                        .FirstOrDefaultAsync(s => s.UserId == userId);

                    if (student == null)
                    {
                        var payload = new { events = new List<object>(), dateRange = new { start, end }, message = "Student profile not found" };
                        return Ok(ApiResponse<object>.SuccessResponse(payload));
                    }

                    // Get student's office hour bookings
                    var bookings = await _context.OfficeHourBookings
                        .Include(b => b.OfficeHour)
                            .ThenInclude(oh => oh.Instructor)
                                .ThenInclude(i => i.User)
                        .Include(b => b.OfficeHour)
                            .ThenInclude(oh => oh.Room)
                        .Where(b => b.StudentId == student.StudentId &&
                                   b.OfficeHour.Date >= start &&
                                   b.OfficeHour.Date <= end &&
                                   (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                        .ToListAsync();

                    foreach (var booking in bookings)
                    {
                        if (booking.OfficeHour?.Instructor?.User == null) continue;

                        events.Add(new
                        {
                            id = $"booking-{booking.BookingId}",
                            title = $"Office Hour with {booking.OfficeHour.Instructor.User.FullName}",
                            start = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.StartTime),
                            end = booking.OfficeHour.Date.Date.Add(booking.OfficeHour.EndTime),
                            type = "office-hour-booking",
                            status = booking.Status.ToString(),
                            color = booking.Status == BookingStatus.Confirmed ? "#22c55e" : "#f59e0b",
                            extendedProps = new
                            {
                                bookingId = booking.BookingId,
                                instructorName = booking.OfficeHour.Instructor.User.FullName,
                                room = booking.OfficeHour.Room != null ? $"{booking.OfficeHour.Room.RoomNumber} ({booking.OfficeHour.Room.Building})" : "TBD",
                                purpose = booking.Purpose,
                                notes = booking.StudentNotes
                            }
                        });
                    }

                    // Get student's enrolled sections schedule
                    var enrollments = await _context.Enrollments
                        .Include(e => e.Section)
                            .ThenInclude(s => s!.Course)
                        .Include(e => e.Section)
                            .ThenInclude(s => s!.Slots)
                                .ThenInclude(sl => sl.TimeSlot)
                        .Include(e => e.Section)
                            .ThenInclude(s => s!.Slots)
                                .ThenInclude(sl => sl.Room)
                        .Where(e => e.StudentId == student.StudentId &&
                                   (e.Status == Status.Enrolled || e.Status == Status.Pending))
                        .ToListAsync();

                    // Generate class events for the date range
                    foreach (var enrollment in enrollments)
                    {
                        if (enrollment.Section?.Slots == null) continue;

                        foreach (var slot in enrollment.Section.Slots)
                        {
                            if (slot.TimeSlot == null) continue;

                            // Generate recurring class events
                            var currentDate = start;
                            while (currentDate <= end)
                            {
                                if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                                {
                                    events.Add(new
                                    {
                                        id = $"class-{enrollment.EnrollmentId}-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                                        title = $"{enrollment.Section.Course?.CourseName ?? "Unknown"} ({enrollment.Section.SectionName})",
                                        start = currentDate.Date.Add(slot.TimeSlot.StartTime),
                                        end = currentDate.Date.Add(slot.TimeSlot.EndTime),
                                        type = "class",
                                        color = "#3b82f6",
                                        extendedProps = new
                                        {
                                            courseCode = enrollment.Section.Course?.CourseCode ?? "",
                                            sectionName = enrollment.Section.SectionName,
                                            room = slot.Room != null ? $"{slot.Room.RoomNumber} ({slot.Room.Building})" : "TBD"
                                        }
                                    });
                                }
                                currentDate = currentDate.AddDays(1);
                            }
                        }
                    }
                }
                else if (userRole == "Instructor")
                {
                    var instructor = await _context.Instructors
                        .FirstOrDefaultAsync(i => i.UserId == userId);

                    if (instructor == null)
                    {
                        var payload = new { events = new List<object>(), dateRange = new { start, end }, message = "Instructor profile not found" };
                        return Ok(ApiResponse<object>.SuccessResponse(payload));
                    }

                    // Get instructor's office hours
                    var officeHours = await _context.OfficeHours
                        .Include(oh => oh.Room)
                        .Include(oh => oh.Bookings)
                            .ThenInclude(b => b.Student)
                                .ThenInclude(s => s.User)
                        .Where(oh => oh.InstructorId == instructor.InstructorId &&
                                    oh.Date >= start &&
                                    oh.Date <= end &&
                                    oh.Status != OfficeHourStatus.Cancelled)
                        .ToListAsync();

                    foreach (var oh in officeHours)
                    {
                        var activeBooking = oh.Bookings?.FirstOrDefault(b =>
                            b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

                        events.Add(new
                        {
                            id = $"office-hour-{oh.OfficeHourId}",
                            title = activeBooking?.Student?.User != null
                                ? $"Office Hour: {activeBooking.Student.User.FullName}"
                                : "Office Hour (Available)",
                            start = oh.Date.Date.Add(oh.StartTime),
                            end = oh.Date.Date.Add(oh.EndTime),
                            type = "office-hour",
                            status = oh.Status.ToString(),
                            color = oh.Status == OfficeHourStatus.Booked ? "#22c55e" : "#94a3b8",
                            extendedProps = new
                            {
                                officeHourId = oh.OfficeHourId,
                                room = oh.Room != null ? $"{oh.Room.RoomNumber} ({oh.Room.Building})" : "TBD",
                                notes = oh.Notes,
                                booking = activeBooking?.Student?.User != null ? new
                                {
                                    bookingId = activeBooking.BookingId,
                                    studentName = activeBooking.Student.User.FullName,
                                    purpose = activeBooking.Purpose,
                                    status = activeBooking.Status.ToString()
                                } : null
                            }
                        });
                    }

                    // Get instructor's teaching schedule
                    var scheduleSlots = await _context.ScheduleSlots
                        .Include(ss => ss.Section)
                            .ThenInclude(s => s.Course)
                        .Include(ss => ss.TimeSlot)
                        .Include(ss => ss.Room)
                        .Where(ss => ss.InstructorId == instructor.InstructorId)
                        .ToListAsync();

                    // Generate class events for the date range
                    foreach (var slot in scheduleSlots)
                    {
                        if (slot.TimeSlot == null || slot.Section?.Course == null) continue;

                        var currentDate = start;
                        while (currentDate <= end)
                        {
                            if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                            {
                                events.Add(new
                                {
                                    id = $"teaching-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                                    title = $"{slot.Section.Course.CourseName} ({slot.Section.SectionName})",
                                    start = currentDate.Date.Add(slot.TimeSlot.StartTime),
                                    end = currentDate.Date.Add(slot.TimeSlot.EndTime),
                                    type = "teaching",
                                    color = "#8b5cf6",
                                    extendedProps = new
                                    {
                                        courseCode = slot.Section.Course.CourseCode,
                                        sectionName = slot.Section.SectionName,
                                        room = slot.Room != null ? $"{slot.Room.RoomNumber} ({slot.Room.Building})" : "TBD"
                                    }
                                });
                            }
                            currentDate = currentDate.AddDays(1);
                        }
                    }
                }
                else if (userRole == "Admin")
                {
                    // Admin can see all office hours
                    var officeHours = await _context.OfficeHours
                        .Include(oh => oh.Instructor)
                            .ThenInclude(i => i.User)
                        .Include(oh => oh.Room)
                        .Include(oh => oh.Bookings)
                            .ThenInclude(b => b.Student)
                                .ThenInclude(s => s.User)
                        .Where(oh => oh.Date >= start &&
                                    oh.Date <= end &&
                                    oh.Status != OfficeHourStatus.Cancelled)
                        .ToListAsync();

                    foreach (var oh in officeHours)
                    {
                        if (oh.Instructor?.User == null) continue;

                        var activeBooking = oh.Bookings?.FirstOrDefault(b =>
                            b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

                        var studentName = activeBooking?.Student?.User?.FullName;

                        events.Add(new
                        {
                            id = $"office-hour-{oh.OfficeHourId}",
                            title = $"{oh.Instructor.User.FullName}: " + (studentName != null
                                ? $"with {studentName}"
                                : "Available"),
                            start = oh.Date.Date.Add(oh.StartTime),
                            end = oh.Date.Date.Add(oh.EndTime),
                            type = "office-hour",
                            status = oh.Status.ToString(),
                            color = oh.Status == OfficeHourStatus.Booked ? "#22c55e" : "#94a3b8",
                            extendedProps = new
                            {
                                officeHourId = oh.OfficeHourId,
                                instructorName = oh.Instructor.User.FullName,
                                room = oh.Room != null ? $"{oh.Room.RoomNumber} ({oh.Room.Building})" : "TBD",
                                booking = studentName != null ? new
                                {
                                    studentName,
                                    purpose = activeBooking?.Purpose
                                } : null
                            }
                        });
                    }

                    // Admin can also see all scheduled classes
                    var scheduleSlots = await _context.ScheduleSlots
                        .Include(ss => ss.Section)
                            .ThenInclude(s => s.Course)
                        .Include(ss => ss.TimeSlot)
                        .Include(ss => ss.Room)
                        .Include(ss => ss.Instructor)
                            .ThenInclude(i => i!.User)
                        .ToListAsync();

                    foreach (var slot in scheduleSlots)
                    {
                        if (slot.TimeSlot == null || slot.Section?.Course == null) continue;

                        var currentDate = start;
                        while (currentDate <= end)
                        {
                            if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                            {
                                events.Add(new
                                {
                                    id = $"class-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                                    title = $"{slot.Section.Course.CourseName} ({slot.Section.SectionName})",
                                    start = currentDate.Date.Add(slot.TimeSlot.StartTime),
                                    end = currentDate.Date.Add(slot.TimeSlot.EndTime),
                                    type = "class",
                                    color = "#3b82f6",
                                    extendedProps = new
                                    {
                                        courseCode = slot.Section.Course.CourseCode,
                                        sectionName = slot.Section.SectionName,
                                        instructorName = slot.Instructor?.User?.FullName ?? "TBD",
                                        room = slot.Room != null ? $"{slot.Room.RoomNumber} ({slot.Room.Building})" : "TBD"
                                    }
                                });
                            }
                            currentDate = currentDate.AddDays(1);
                        }
                    }
                }

                var resultPayload = new
                {
                    events,
                    dateRange = new { start, end }
                };

                return Ok(ApiResponse<object>.SuccessResponse(resultPayload));
            }
            catch
            {
                var start = startDate ?? DateTime.UtcNow.Date.AddMonths(-1);
                var end = endDate ?? DateTime.UtcNow.Date.AddMonths(3);

                var errorPayload = new
                {
                    events = new List<object>(),
                    dateRange = new { start, end },
                    error = "Failed to load calendar events"
                };

                return Ok(ApiResponse<object>.SuccessResponse(errorPayload, "Failed to load calendar events"));
            }
        }

        /// <summary>
        /// Get today's events for the current user
        /// </summary>
        [HttpGet("today")]
        public async Task<IActionResult> GetTodayEvents()
        {
            var today = DateTime.UtcNow.Date;
            return await GetCalendarEvents(today, today);
        }

        /// <summary>
        /// Get upcoming events for the current user (next 7 days)
        /// </summary>
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcomingEvents()
        {
            var today = DateTime.UtcNow.Date;
            var nextWeek = today.AddDays(7);
            return await GetCalendarEvents(today, nextWeek);
        }
    }
}
