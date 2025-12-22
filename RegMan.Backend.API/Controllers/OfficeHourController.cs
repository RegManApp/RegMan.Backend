using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.API.Common;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OfficeHourController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public OfficeHourController(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        #region DTOs

        public class CreateOfficeHourDTO
        {
            public DateTime Date { get; set; }
            public string StartTime { get; set; } = null!; // "HH:mm" format
            public string EndTime { get; set; } = null!;
            public int? RoomId { get; set; }
            public bool IsRecurring { get; set; } = false;
            public string? Notes { get; set; }
        }

        public class UpdateOfficeHourDTO
        {
            public DateTime? Date { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public int? RoomId { get; set; }
            public string? Notes { get; set; }
        }

        public class BookOfficeHourDTO
        {
            public string? Purpose { get; set; }
            public string? StudentNotes { get; set; }
        }

        public class CancelBookingDTO
        {
            public string? Reason { get; set; }
        }

        public class InstructorNotesDTO
        {
            public string? Notes { get; set; }
        }

        #endregion

        // =============================================
        // INSTRUCTOR ENDPOINTS - Manage Office Hours
        // =============================================

        /// <summary>
        /// Get all office hours for the current instructor
        /// </summary>
        [HttpGet("my-office-hours")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetMyOfficeHours([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var query = _context.OfficeHours
                .Include(oh => oh.Room)
                .Include(oh => oh.Bookings)
                    .ThenInclude(b => b.Student)
                        .ThenInclude(s => s.User)
                .Where(oh => oh.InstructorId == instructor.InstructorId);

            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            var officeHours = await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new
                {
                    oh.OfficeHourId,
                    oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    oh.Status,
                    oh.Notes,
                    oh.IsRecurring,
                    oh.RecurringDay,
                    Room = oh.Room != null ? new { oh.Room.RoomId, oh.Room.RoomNumber, oh.Room.Building } : null,
                    Bookings = oh.Bookings.Select(b => new
                    {
                        b.BookingId,
                        b.Status,
                        b.Purpose,
                        b.StudentNotes,
                        b.InstructorNotes,
                        b.BookedAt,
                        Student = new
                        {
                            b.Student.StudentId,
                            b.Student.User.FullName,
                            b.Student.User.Email
                        }
                    }).ToList()
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }

        /// <summary>
        /// Create a new office hour slot
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CreateOfficeHour([FromBody] CreateOfficeHourDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            if (!TimeSpan.TryParse(dto.StartTime, out var startTime) ||
                !TimeSpan.TryParse(dto.EndTime, out var endTime))
            {
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Invalid time format. Use HH:mm",
                    StatusCodes.Status400BadRequest));
            }

            if (endTime <= startTime)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "End time must be after start time",
                    StatusCodes.Status400BadRequest));

            // Check for overlapping office hours
            var hasOverlap = await _context.OfficeHours
                .AnyAsync(oh => oh.InstructorId == instructor.InstructorId &&
                               oh.Date.Date == dto.Date.Date &&
                               oh.Status != OfficeHourStatus.Cancelled &&
                               ((startTime >= oh.StartTime && startTime < oh.EndTime) ||
                                (endTime > oh.StartTime && endTime <= oh.EndTime) ||
                                (startTime <= oh.StartTime && endTime >= oh.EndTime)));

            if (hasOverlap)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "This time slot overlaps with an existing office hour",
                    StatusCodes.Status400BadRequest));

            var officeHour = new OfficeHour
            {
                InstructorId = instructor.InstructorId,
                Date = dto.Date.Date,
                StartTime = startTime,
                EndTime = endTime,
                RoomId = dto.RoomId,
                IsRecurring = dto.IsRecurring,
                RecurringDay = dto.IsRecurring ? dto.Date.DayOfWeek : null,
                Notes = dto.Notes,
                Status = OfficeHourStatus.Available
            };

            _context.OfficeHours.Add(officeHour);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.SuccessResponse(
                new { officeHourId = officeHour.OfficeHourId },
                "Office hour created successfully"));
        }

        /// <summary>
        /// Create multiple office hours at once (batch create)
        /// </summary>
        [HttpPost("batch")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CreateBatchOfficeHours([FromBody] List<CreateOfficeHourDTO> dtos)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var createdIds = new List<int>();
            var errors = new List<string>();

            foreach (var dto in dtos)
            {
                if (!TimeSpan.TryParse(dto.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(dto.EndTime, out var endTime))
                {
                    errors.Add($"Invalid time format for {dto.Date:yyyy-MM-dd}");
                    continue;
                }

                if (endTime <= startTime)
                {
                    errors.Add($"End time must be after start time for {dto.Date:yyyy-MM-dd}");
                    continue;
                }

                var officeHour = new OfficeHour
                {
                    InstructorId = instructor.InstructorId,
                    Date = dto.Date.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    RoomId = dto.RoomId,
                    IsRecurring = dto.IsRecurring,
                    RecurringDay = dto.IsRecurring ? dto.Date.DayOfWeek : null,
                    Notes = dto.Notes,
                    Status = OfficeHourStatus.Available
                };

                _context.OfficeHours.Add(officeHour);
                await _context.SaveChangesAsync();
                createdIds.Add(officeHour.OfficeHourId);
            }

            return Ok(ApiResponse<object>.SuccessResponse(
                new { createdIds, errors },
                $"Created {createdIds.Count} office hours"));
        }

        /// <summary>
        /// Update an office hour
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> UpdateOfficeHour(int id, [FromBody] UpdateOfficeHourDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var officeHour = await _context.OfficeHours
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == id && oh.InstructorId == instructor.InstructorId);

            if (officeHour == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Office hour not found",
                    StatusCodes.Status404NotFound));

            if (officeHour.Bookings.Any(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending))
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Cannot modify office hour with active bookings",
                    StatusCodes.Status400BadRequest));

            if (dto.Date.HasValue)
                officeHour.Date = dto.Date.Value.Date;

            if (!string.IsNullOrEmpty(dto.StartTime) && TimeSpan.TryParse(dto.StartTime, out var startTime))
                officeHour.StartTime = startTime;

            if (!string.IsNullOrEmpty(dto.EndTime) && TimeSpan.TryParse(dto.EndTime, out var endTime))
                officeHour.EndTime = endTime;

            if (dto.RoomId.HasValue)
                officeHour.RoomId = dto.RoomId;

            if (dto.Notes != null)
                officeHour.Notes = dto.Notes;

            officeHour.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Office hour updated successfully"));
        }

        /// <summary>
        /// Delete an office hour
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> DeleteOfficeHour(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var officeHour = await _context.OfficeHours
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == id && oh.InstructorId == instructor.InstructorId);

            if (officeHour == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Office hour not found",
                    StatusCodes.Status404NotFound));

            // Cancel any existing bookings and notify students
            foreach (var booking in officeHour.Bookings.Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = "Office hour was cancelled by instructor";
                booking.CancelledBy = "Instructor";
                booking.CancelledAt = DateTime.UtcNow;

                // Create notification for student
                var student = await _context.Students
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.StudentId == booking.StudentId);

                if (student != null)
                {
                    await _notificationService.CreateOfficeHourCancelledNotificationAsync(
                        userId: student.UserId,
                        cancelledBy: "Instructor",
                        date: officeHour.Date,
                        startTime: officeHour.StartTime,
                        reason: booking.CancellationReason
                    );
                }
            }

            _context.OfficeHours.Remove(officeHour);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Office hour deleted successfully"));
        }

        /// <summary>
        /// Confirm a booking
        /// </summary>
        [HttpPost("bookings/{bookingId}/confirm")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> ConfirmBooking(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var booking = await _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Booking not found",
                    StatusCodes.Status404NotFound));

            if (booking.Status != BookingStatus.Pending)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Booking is not in pending status",
                    StatusCodes.Status400BadRequest));

            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedAt = DateTime.UtcNow;

            // Update office hour status
            booking.OfficeHour.Status = OfficeHourStatus.Booked;
            await _context.SaveChangesAsync();

            await _notificationService.CreateOfficeHourConfirmedNotificationAsync(
                studentUserId: booking.Student.UserId,
                instructorName: booking.OfficeHour.Instructor.User.FullName,
                date: booking.OfficeHour.Date,
                startTime: booking.OfficeHour.StartTime
            );

            return Ok(ApiResponse<string>.SuccessResponse("Booking confirmed successfully"));
        }

        /// <summary>
        /// Add instructor notes to a booking
        /// </summary>
        [HttpPut("bookings/{bookingId}/notes")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> AddInstructorNotes(int bookingId, [FromBody] InstructorNotesDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var booking = await _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Booking not found",
                    StatusCodes.Status404NotFound));

            booking.InstructorNotes = dto.Notes;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Notes added successfully"));
        }

        /// <summary>
        /// Mark booking as completed
        /// </summary>
        [HttpPost("bookings/{bookingId}/complete")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CompleteBooking(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var booking = await _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Booking not found",
                    StatusCodes.Status404NotFound));

            if (booking.Status != BookingStatus.Confirmed)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Booking must be confirmed before completing",
                    StatusCodes.Status400BadRequest));

            booking.Status = BookingStatus.Completed;
            booking.CompletedAt = DateTime.UtcNow;
            booking.OfficeHour.Status = OfficeHourStatus.Available;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Booking marked as completed"));
        }

        /// <summary>
        /// Mark booking as no-show
        /// </summary>
        [HttpPost("bookings/{bookingId}/no-show")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> MarkNoShow(int bookingId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var instructor = await _context.Instructors
                .FirstOrDefaultAsync(i => i.UserId == userId);

            if (instructor == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Instructor profile not found",
                    StatusCodes.Status404NotFound));

            var booking = await _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Booking not found",
                    StatusCodes.Status404NotFound));

            booking.Status = BookingStatus.NoShow;
            booking.OfficeHour.Status = OfficeHourStatus.Available;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse("Booking marked as no-show"));
        }

        // =============================================
        // STUDENT ENDPOINTS - Book Office Hours
        // =============================================

        /// <summary>
        /// Get all available office hours for students to book
        /// </summary>
        [HttpGet("available")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetAvailableOfficeHours(
            [FromQuery] int? instructorId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var query = _context.OfficeHours
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .Include(oh => oh.Room)
                .Where(oh => oh.Status == OfficeHourStatus.Available &&
                            oh.Date >= DateTime.UtcNow.Date);

            if (instructorId.HasValue)
                query = query.Where(oh => oh.InstructorId == instructorId.Value);

            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            var officeHours = await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new
                {
                    oh.OfficeHourId,
                    oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    oh.Notes,
                    Room = oh.Room != null ? new { oh.Room.RoomId, oh.Room.RoomNumber, oh.Room.Building } : null,
                    Instructor = new
                    {
                        oh.Instructor.InstructorId,
                        oh.Instructor.User.FullName,
                        oh.Instructor.Title,
                        oh.Instructor.Degree,
                        oh.Instructor.Department
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }

        /// <summary>
        /// Get all instructors with their available office hours count
        /// </summary>
        [HttpGet("instructors")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetInstructorsWithOfficeHours()
        {
            var instructors = await _context.Instructors
                .Include(i => i.User)
                .Select(i => new
                {
                    i.InstructorId,
                    i.User.FullName,
                    i.Title,
                    i.Degree,
                    i.Department,
                    AvailableSlots = _context.OfficeHours.Count(oh =>
                        oh.InstructorId == i.InstructorId &&
                        oh.Status == OfficeHourStatus.Available &&
                        oh.Date >= DateTime.UtcNow.Date)
                })
                .Where(i => i.AvailableSlots > 0)
                .OrderBy(i => i.FullName)
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(instructors));
        }

        /// <summary>
        /// Book an office hour
        /// </summary>
        [HttpPost("{id}/book")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> BookOfficeHour(int id, [FromBody] BookOfficeHourDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _context.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Student profile not found",
                    StatusCodes.Status404NotFound));

            var officeHour = await _context.OfficeHours
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == id);

            if (officeHour == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Office hour not found",
                    StatusCodes.Status404NotFound));

            if (officeHour.Status != OfficeHourStatus.Available)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "This office hour is no longer available",
                    StatusCodes.Status400BadRequest));

            if (officeHour.Date.Date < DateTime.UtcNow.Date)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Cannot book past office hours",
                    StatusCodes.Status400BadRequest));

            // Check if student already has a booking for this slot
            var existingBooking = await _context.OfficeHourBookings
                .AnyAsync(b => b.OfficeHourId == id &&
                              b.StudentId == student.StudentId &&
                              (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (existingBooking)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "You already have a booking for this office hour",
                    StatusCodes.Status400BadRequest));

            var booking = new OfficeHourBooking
            {
                OfficeHourId = id,
                StudentId = student.StudentId,
                Purpose = dto.Purpose,
                StudentNotes = dto.StudentNotes,
                Status = BookingStatus.Pending
            };

            _context.OfficeHourBookings.Add(booking);

            // Update office hour status
            officeHour.Status = OfficeHourStatus.Booked;
            await _context.SaveChangesAsync();

            await _notificationService.CreateOfficeHourBookedNotificationAsync(
                bookingId: booking.BookingId,
                instructorUserId: officeHour.Instructor.UserId,
                studentName: student.User.FullName,
                date: officeHour.Date,
                startTime: officeHour.StartTime
            );

            return Ok(ApiResponse<object>.SuccessResponse(
                new { bookingId = booking.BookingId },
                "Office hour booked successfully"));
        }

        /// <summary>
        /// Get student's bookings
        /// </summary>
        [HttpGet("my-bookings")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyBookings([FromQuery] string? status)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Student profile not found",
                    StatusCodes.Status404NotFound));

            var query = _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Where(b => b.StudentId == student.StudentId);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
                query = query.Where(b => b.Status == bookingStatus);

            var bookings = await query
                .OrderByDescending(b => b.OfficeHour.Date)
                .ThenByDescending(b => b.OfficeHour.StartTime)
                .Select(b => new
                {
                    b.BookingId,
                    b.Status,
                    b.Purpose,
                    b.StudentNotes,
                    b.InstructorNotes,
                    b.BookedAt,
                    b.ConfirmedAt,
                    b.CancelledAt,
                    b.CancellationReason,
                    b.CancelledBy,
                    OfficeHour = new
                    {
                        b.OfficeHour.OfficeHourId,
                        b.OfficeHour.Date,
                        StartTime = b.OfficeHour.StartTime.ToString(@"hh\:mm"),
                        EndTime = b.OfficeHour.EndTime.ToString(@"hh\:mm"),
                        b.OfficeHour.Notes,
                        Room = b.OfficeHour.Room != null ? new { b.OfficeHour.Room.RoomId, b.OfficeHour.Room.RoomNumber, b.OfficeHour.Room.Building } : null
                    },
                    Instructor = new
                    {
                        b.OfficeHour.Instructor.InstructorId,
                        b.OfficeHour.Instructor.User.FullName,
                        b.OfficeHour.Instructor.Title,
                        b.OfficeHour.Instructor.Degree,
                        b.OfficeHour.Instructor.Department
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(bookings));
        }

        /// <summary>
        /// Cancel a booking (student)
        /// </summary>
        [HttpPost("bookings/{bookingId}/cancel")]
        [Authorize(Roles = "Student,Instructor")]
        public async Task<IActionResult> CancelBooking(int bookingId, [FromBody] CancelBookingDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            var booking = await _context.OfficeHourBookings
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return NotFound(ApiResponse<string>.FailureResponse(
                    "Booking not found",
                    StatusCodes.Status404NotFound));

            // Verify ownership
            if (userRole == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null || booking.StudentId != student.StudentId)
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<string>.FailureResponse("Forbidden", StatusCodes.Status403Forbidden));
            }
            else if (userRole == "Instructor")
            {
                var instructor = await _context.Instructors.FirstOrDefaultAsync(i => i.UserId == userId);
                if (instructor == null || booking.OfficeHour.InstructorId != instructor.InstructorId)
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<string>.FailureResponse("Forbidden", StatusCodes.Status403Forbidden));
            }

            if (booking.Status == BookingStatus.Cancelled)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Booking is already cancelled",
                    StatusCodes.Status400BadRequest));

            if (booking.Status == BookingStatus.Completed)
                return BadRequest(ApiResponse<string>.FailureResponse(
                    "Cannot cancel a completed booking",
                    StatusCodes.Status400BadRequest));

            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = dto.Reason;
            booking.CancelledBy = userRole;
            booking.CancelledAt = DateTime.UtcNow;

            // Make office hour available again
            booking.OfficeHour.Status = OfficeHourStatus.Available;
            await _context.SaveChangesAsync();

            if (userRole == "Student")
            {
                await _notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.OfficeHour.Instructor.UserId,
                    cancelledBy: booking.Student.User.FullName,
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: dto.Reason
                );
            }
            else
            {
                await _notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.Student.UserId,
                    cancelledBy: "Instructor",
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: dto.Reason
                );
            }

            return Ok(ApiResponse<string>.SuccessResponse("Booking cancelled successfully"));
        }

        // =============================================
        // ADMIN ENDPOINTS
        // =============================================

        /// <summary>
        /// Get all office hours (admin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOfficeHours(
            [FromQuery] int? instructorId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? status)
        {
            var query = _context.OfficeHours
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .Include(oh => oh.Room)
                .Include(oh => oh.Bookings)
                    .ThenInclude(b => b.Student)
                        .ThenInclude(s => s.User)
                .AsQueryable();

            if (instructorId.HasValue)
                query = query.Where(oh => oh.InstructorId == instructorId.Value);

            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OfficeHourStatus>(status, true, out var officeHourStatus))
                query = query.Where(oh => oh.Status == officeHourStatus);

            var officeHours = await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new
                {
                    oh.OfficeHourId,
                    oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    oh.Status,
                    oh.Notes,
                    Room = oh.Room != null ? new { oh.Room.RoomId, oh.Room.RoomNumber, oh.Room.Building } : null,
                    Instructor = new
                    {
                        oh.Instructor.InstructorId,
                        oh.Instructor.User.FullName,
                        oh.Instructor.Title,
                        oh.Instructor.Degree
                    },
                    Bookings = oh.Bookings.Select(b => new
                    {
                        b.BookingId,
                        b.Status,
                        b.Purpose,
                        Student = new
                        {
                            b.Student.StudentId,
                            b.Student.User.FullName
                        }
                    }).ToList()
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.SuccessResponse(officeHours));
        }
    }
}
