using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.DAL.DataContext;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;

namespace StudentManagementSystem.API.Controllers
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

        /// <summary>
        /// Get all calendar events for the current user
        /// </summary>
        [HttpGet("events")]
        public async Task<IActionResult> GetCalendarEvents(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            var events = new List<object>();
            var startDate = fromDate ?? DateTime.UtcNow.Date.AddMonths(-1);
            var endDate = toDate ?? DateTime.UtcNow.Date.AddMonths(3);

            if (userRole == "Student")
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (student == null)
                    return NotFound(new { message = "Student profile not found" });

                // Get student's office hour bookings
                var bookings = await _context.OfficeHourBookings
                    .Include(b => b.OfficeHour)
                        .ThenInclude(oh => oh.Instructor)
                            .ThenInclude(i => i.User)
                    .Include(b => b.OfficeHour)
                        .ThenInclude(oh => oh.Room)
                    .Where(b => b.StudentId == student.StudentId &&
                               b.OfficeHour.Date >= startDate &&
                               b.OfficeHour.Date <= endDate &&
                               (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                    .ToListAsync();

                foreach (var booking in bookings)
                {
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
                        .ThenInclude(s => s.Course)
                    .Include(e => e.Section)
                        .ThenInclude(s => s.Slots)
                            .ThenInclude(sl => sl.TimeSlot)
                    .Include(e => e.Section)
                        .ThenInclude(s => s.Slots)
                            .ThenInclude(sl => sl.Room)
                    .Where(e => e.StudentId == student.StudentId &&
                               (e.Status == Status.Enrolled || e.Status == Status.Pending))
                    .ToListAsync();

                // Generate class events for the date range
                foreach (var enrollment in enrollments)
                {
                    foreach (var slot in enrollment.Section.Slots)
                    {
                        // Generate recurring class events
                        var currentDate = startDate;
                        while (currentDate <= endDate)
                        {
                            if (currentDate.DayOfWeek == slot.TimeSlot.Day)
                            {
                                events.Add(new
                                {
                                    id = $"class-{enrollment.EnrollmentId}-{slot.ScheduleSlotId}-{currentDate:yyyyMMdd}",
                                    title = $"{enrollment.Section.Course.CourseName} ({enrollment.Section.SectionName})",
                                    start = currentDate.Date.Add(slot.TimeSlot.StartTime),
                                    end = currentDate.Date.Add(slot.TimeSlot.EndTime),
                                    type = "class",
                                    color = "#3b82f6",
                                    extendedProps = new
                                    {
                                        courseCode = enrollment.Section.Course.CourseCode,
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
                    return NotFound(new { message = "Instructor profile not found" });

                // Get instructor's office hours
                var officeHours = await _context.OfficeHours
                    .Include(oh => oh.Room)
                    .Include(oh => oh.Bookings)
                        .ThenInclude(b => b.Student)
                            .ThenInclude(s => s.User)
                    .Where(oh => oh.InstructorId == instructor.InstructorId &&
                                oh.Date >= startDate &&
                                oh.Date <= endDate &&
                                oh.Status != OfficeHourStatus.Cancelled)
                    .ToListAsync();

                foreach (var oh in officeHours)
                {
                    var activeBooking = oh.Bookings.FirstOrDefault(b =>
                        b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

                    events.Add(new
                    {
                        id = $"office-hour-{oh.OfficeHourId}",
                        title = activeBooking != null
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
                            booking = activeBooking != null ? new
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
                    var currentDate = startDate;
                    while (currentDate <= endDate)
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
                    .Where(oh => oh.Date >= startDate &&
                                oh.Date <= endDate &&
                                oh.Status != OfficeHourStatus.Cancelled)
                    .ToListAsync();

                foreach (var oh in officeHours)
                {
                    var activeBooking = oh.Bookings.FirstOrDefault(b =>
                        b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

                    events.Add(new
                    {
                        id = $"office-hour-{oh.OfficeHourId}",
                        title = $"{oh.Instructor.User.FullName}: " + (activeBooking != null
                            ? $"with {activeBooking.Student.User.FullName}"
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
                            booking = activeBooking != null ? new
                            {
                                studentName = activeBooking.Student.User.FullName,
                                purpose = activeBooking.Purpose
                            } : null
                        }
                    });
                }
            }

            return Ok(new
            {
                events,
                dateRange = new { start = startDate, end = endDate }
            });
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
