using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.OfficeHoursDTOs;
using RegMan.Backend.BusinessLayer.Exceptions;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class OfficeHoursService : IOfficeHoursService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly INotificationService notificationService;
        private readonly IGoogleCalendarIntegrationService googleCalendarIntegrationService;
        private readonly ILogger<OfficeHoursService> logger;
        private readonly IBaseRepository<OfficeHour> officeHoursRepository;
        private readonly IBaseRepository<InstructorProfile> instructorsRepository;
        public OfficeHoursService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IGoogleCalendarIntegrationService googleCalendarIntegrationService,
            ILogger<OfficeHoursService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.notificationService = notificationService;
            this.googleCalendarIntegrationService = googleCalendarIntegrationService;
            this.logger = logger;
            this.officeHoursRepository = unitOfWork.OfficeHours;
            this.instructorsRepository = unitOfWork.InstructorProfiles;
        }

        private DbContext Db => unitOfWork.Context;
        //READ
        //Used by admin to see office hours of any instructor, based on their instructor ID
        public async Task<List<ViewOfficeHoursDTO>> GetOfficeHoursByInstructorIdAsync(int instructorId)
        {
            List<ViewOfficeHoursDTO>? officeHours = await officeHoursRepository
                .GetFilteredAndProjected(
                filter: oh => oh.InstructorId == instructorId,
                projection: oh => new ViewOfficeHoursDTO
                {
                    OfficeHoursId = oh.OfficeHourId,
                    RoomId = oh.RoomId,
                    InstructorId = oh.InstructorId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Status = oh.Status.ToString(),
                    Notes = oh.Notes,
                    IsRecurring = oh.IsRecurring,
                    Room = oh.Room != null ? $"{oh.Room.Building} - {oh.Room.RoomNumber}" : null,
                    InstructorName = oh.Instructor.User.FullName
                }
                )
                .ToListAsync();
            if (officeHours == null || officeHours.Count == 0)
            {
                throw new Exception($"No office hours found for instructor with ID {instructorId}.");
            }
            return officeHours;
        }

        public async Task<List<AdminOfficeHourListItemDTO>> GetAllOfficeHoursAsync(int? instructorId, DateTime? fromDate, DateTime? toDate, string? status)
        {
            var query = Db.Set<OfficeHour>()
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

            return await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new AdminOfficeHourListItemDTO
                {
                    OfficeHourId = oh.OfficeHourId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Status = oh.Status,
                    Notes = oh.Notes,
                    Room = oh.Room != null ? new RoomInfoDTO { RoomId = oh.Room.RoomId, RoomNumber = oh.Room.RoomNumber, Building = oh.Room.Building } : null,
                    Instructor = new AdminInstructorInfoDTO
                    {
                        InstructorId = oh.Instructor.InstructorId,
                        FullName = oh.Instructor.User.FullName,
                        Title = oh.Instructor.Title,
                        Degree = oh.Instructor.Degree
                    },
                    Bookings = oh.Bookings.Select(b => new AdminBookingListItemDTO
                    {
                        BookingId = b.BookingId,
                        Status = b.Status,
                        Purpose = b.Purpose,
                        Student = new AdminStudentInfoDTO { StudentId = b.Student.StudentId, FullName = b.Student.User.FullName }
                    }).ToList()
                })
                .ToListAsync();
        }
        //CREATE
        public async Task<ViewOfficeHoursDTO> CreateOfficeHours(CreateOfficeHoursDTO hoursDTO)
        {
            InstructorProfile? instructor = await instructorsRepository.GetByIdAsync(hoursDTO.InstructorId);
            if (instructor == null)
                throw new KeyNotFoundException($"Instructor with ID {hoursDTO.InstructorId} not found.");

            if (hoursDTO.RoomId.HasValue)
            {
                var room = await unitOfWork.Rooms.GetByIdAsync(hoursDTO.RoomId.Value)
                   ?? throw new Exception($"Room with ID {hoursDTO.RoomId} not found.");
            }

            if (!TimeSpan.TryParse(hoursDTO.StartTime, out var startTime) ||
                !TimeSpan.TryParse(hoursDTO.EndTime, out var endTime))
            {
                throw new Exception("Invalid time format. Use HH:mm");
            }

            //all are valid, create office hour
            OfficeHour officeHour = new OfficeHour
            {
                InstructorId = hoursDTO.InstructorId,
                RoomId = hoursDTO.RoomId,
                Date = hoursDTO.Date.Date,
                StartTime = startTime,
                EndTime = endTime,
                IsRecurring = hoursDTO.IsRecurring,
                RecurringDay = hoursDTO.IsRecurring ? hoursDTO.Date.DayOfWeek : null,
                Notes = hoursDTO.Notes,
                Status = OfficeHourStatus.Available
            };
            await officeHoursRepository.AddAsync(officeHour);
            await unitOfWork.SaveChangesAsync();
            return new ViewOfficeHoursDTO
            {
                OfficeHoursId = officeHour.OfficeHourId,
                RoomId = officeHour.RoomId,
                InstructorId = officeHour.InstructorId,
                Date = officeHour.Date,
                StartTime = officeHour.StartTime.ToString(@"hh\:mm"),
                EndTime = officeHour.EndTime.ToString(@"hh\:mm"),
                Status = officeHour.Status.ToString(),
                Notes = officeHour.Notes,
                IsRecurring = officeHour.IsRecurring,
                Room = officeHour.Room != null ? $"{officeHour.Room.Building} - {officeHour.Room.RoomNumber}" : null,
                InstructorName = instructor.User?.FullName ?? "Unknown"
            };
        }

        public async Task<List<InstructorOfficeHourListItemDTO>> GetMyOfficeHoursAsync(string instructorUserId, DateTime? fromDate, DateTime? toDate)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var query = Db.Set<OfficeHour>()
                .Include(oh => oh.Room)
                .Include(oh => oh.Bookings)
                    .ThenInclude(b => b.Student)
                        .ThenInclude(s => s.User)
                .Where(oh => oh.InstructorId == instructor.InstructorId);

            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            return await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new InstructorOfficeHourListItemDTO
                {
                    OfficeHourId = oh.OfficeHourId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Status = oh.Status,
                    Notes = oh.Notes,
                    IsRecurring = oh.IsRecurring,
                    RecurringDay = oh.RecurringDay,
                    Room = oh.Room != null ? new RoomInfoDTO { RoomId = oh.Room.RoomId, RoomNumber = oh.Room.RoomNumber, Building = oh.Room.Building } : null,
                    Bookings = oh.Bookings.Select(b => new InstructorBookingListItemDTO
                    {
                        BookingId = b.BookingId,
                        Status = b.Status,
                        Purpose = b.Purpose,
                        StudentNotes = b.StudentNotes,
                        InstructorNotes = b.InstructorNotes,
                        BookedAt = b.BookedAt,
                        Student = new StudentInfoDTO
                        {
                            StudentId = b.Student.StudentId,
                            FullName = b.Student.User.FullName,
                            Email = b.Student.User.Email
                        }
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<int> CreateOfficeHourAsync(string instructorUserId, CreateInstructorOfficeHourDTO dto)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            if (!TimeSpan.TryParse(dto.StartTime, out var startTime) ||
                !TimeSpan.TryParse(dto.EndTime, out var endTime))
            {
                throw new BadRequestException("Invalid time format. Use HH:mm");
            }

            if (endTime <= startTime)
                throw new BadRequestException("End time must be after start time");

            var hasOverlap = await Db.Set<OfficeHour>()
                .AnyAsync(oh => oh.InstructorId == instructor.InstructorId &&
                               oh.Date.Date == dto.Date.Date &&
                               oh.Status != OfficeHourStatus.Cancelled &&
                               ((startTime >= oh.StartTime && startTime < oh.EndTime) ||
                                (endTime > oh.StartTime && endTime <= oh.EndTime) ||
                                (startTime <= oh.StartTime && endTime >= oh.EndTime)));

            if (hasOverlap)
                throw new BadRequestException("This time slot overlaps with an existing office hour");

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

            Db.Set<OfficeHour>().Add(officeHour);
            await unitOfWork.SaveChangesAsync();
            return officeHour.OfficeHourId;
        }

        public async Task<(List<int> createdIds, List<string> errors)> CreateBatchOfficeHoursAsync(string instructorUserId, List<CreateInstructorOfficeHourDTO> dtos)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

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

                Db.Set<OfficeHour>().Add(officeHour);
                await unitOfWork.SaveChangesAsync();
                createdIds.Add(officeHour.OfficeHourId);
            }

            return (createdIds, errors);
        }

        public async Task UpdateOfficeHourAsync(string instructorUserId, int officeHourId, UpdateInstructorOfficeHourDTO dto)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId && oh.InstructorId == instructor.InstructorId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            if (officeHour.Bookings.Any(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending))
                throw new BadRequestException("Cannot modify office hour with active bookings");

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
            await unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteOfficeHourAsync(string instructorUserId, int officeHourId)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId && oh.InstructorId == instructor.InstructorId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            foreach (var booking in officeHour.Bookings.Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = "Office hour was cancelled by instructor";
                booking.CancelledBy = "Instructor";
                booking.CancelledAt = DateTime.UtcNow;

                var student = await Db.Set<StudentProfile>()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.StudentId == booking.StudentId);

                if (student != null)
                {
                    await notificationService.CreateOfficeHourCancelledNotificationAsync(
                        userId: student.UserId,
                        cancelledBy: "Instructor",
                        date: officeHour.Date,
                        startTime: officeHour.StartTime,
                        reason: booking.CancellationReason
                    );
                }
            }

            Db.Set<OfficeHour>().Remove(officeHour);
            await unitOfWork.SaveChangesAsync();
        }

        public async Task ConfirmBookingAsync(string instructorUserId, int bookingId)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Include(b => b.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            if (booking.Status != BookingStatus.Pending)
                throw new BadRequestException("Booking is not in pending status");

            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedAt = DateTime.UtcNow;
            booking.OfficeHour.Status = OfficeHourStatus.Booked;

            await unitOfWork.SaveChangesAsync();

            await notificationService.CreateOfficeHourConfirmedNotificationAsync(
                studentUserId: booking.Student.UserId,
                instructorName: booking.OfficeHour.Instructor.User.FullName,
                date: booking.OfficeHour.Date,
                startTime: booking.OfficeHour.StartTime
            );

            try
            {
                // Best-effort: never fail booking if Google integration fails.
                await googleCalendarIntegrationService.TryCreateOfficeHourBookingEventAsync(booking, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar integration failed for BookingId={BookingId}", booking.BookingId);
            }
        }

        public async Task AddInstructorNotesAsync(string instructorUserId, int bookingId, string? notes)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            booking.InstructorNotes = notes;
            await unitOfWork.SaveChangesAsync();
        }

        public async Task CompleteBookingAsync(string instructorUserId, int bookingId)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            if (booking.Status != BookingStatus.Confirmed)
                throw new BadRequestException("Booking must be confirmed before completing");

            booking.Status = BookingStatus.Completed;
            booking.CompletedAt = DateTime.UtcNow;
            booking.OfficeHour.Status = OfficeHourStatus.Available;

            await unitOfWork.SaveChangesAsync();
        }

        public async Task MarkNoShowAsync(string instructorUserId, int bookingId)
        {
            var instructor = await Db.Set<InstructorProfile>()
                .FirstOrDefaultAsync(i => i.UserId == instructorUserId);

            if (instructor == null)
                throw new NotFoundException("Instructor profile not found");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.InstructorId == instructor.InstructorId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            booking.Status = BookingStatus.NoShow;
            booking.OfficeHour.Status = OfficeHourStatus.Available;

            await unitOfWork.SaveChangesAsync();
        }

        public async Task<List<StudentAvailableOfficeHourDTO>> GetAvailableOfficeHoursAsync(int? instructorId, DateTime? fromDate, DateTime? toDate)
        {
            var query = Db.Set<OfficeHour>()
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .Include(oh => oh.Room)
                .Where(oh => oh.Status == OfficeHourStatus.Available && oh.Date >= DateTime.UtcNow.Date);

            if (instructorId.HasValue)
                query = query.Where(oh => oh.InstructorId == instructorId.Value);
            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            return await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new StudentAvailableOfficeHourDTO
                {
                    OfficeHourId = oh.OfficeHourId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Notes = oh.Notes,
                    Room = oh.Room != null ? new RoomInfoDTO { RoomId = oh.Room.RoomId, RoomNumber = oh.Room.RoomNumber, Building = oh.Room.Building } : null,
                    Instructor = new InstructorInfoDTO
                    {
                        InstructorId = oh.Instructor.InstructorId,
                        FullName = oh.Instructor.User.FullName,
                        Title = oh.Instructor.Title,
                        Degree = oh.Instructor.Degree,
                        Department = oh.Instructor.Department
                    }
                })
                .ToListAsync();
        }

        public async Task<List<StudentInstructorsWithOfficeHoursDTO>> GetInstructorsWithOfficeHoursAsync()
        {
            return await Db.Set<InstructorProfile>()
                .Include(i => i.User)
                .Select(i => new StudentInstructorsWithOfficeHoursDTO
                {
                    InstructorId = i.InstructorId,
                    FullName = i.User.FullName,
                    Title = i.Title,
                    Degree = i.Degree,
                    Department = i.Department,
                    AvailableSlots = Db.Set<OfficeHour>().Count(oh =>
                        oh.InstructorId == i.InstructorId &&
                        oh.Status == OfficeHourStatus.Available &&
                        oh.Date >= DateTime.UtcNow.Date)
                })
                .Where(i => i.AvailableSlots > 0)
                .OrderBy(i => i.FullName)
                .ToListAsync();
        }

        public async Task<int> BookOfficeHourAsync(string studentUserId, int officeHourId, BookOfficeHourRequestDTO dto)
        {
            var student = await Db.Set<StudentProfile>()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == studentUserId);

            if (student == null)
                throw new NotFoundException("Student profile not found");

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            if (officeHour.Status != OfficeHourStatus.Available)
                throw new BadRequestException("This office hour is no longer available");

            if (officeHour.Date.Date < DateTime.UtcNow.Date)
                throw new BadRequestException("Cannot book past office hours");

            var existingBooking = await Db.Set<OfficeHourBooking>()
                .AnyAsync(b => b.OfficeHourId == officeHourId &&
                              b.StudentId == student.StudentId &&
                              (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (existingBooking)
                throw new BadRequestException("You already have a booking for this office hour");

            var booking = new OfficeHourBooking
            {
                OfficeHourId = officeHourId,
                StudentId = student.StudentId,
                Purpose = dto.Purpose,
                StudentNotes = dto.StudentNotes,
                Status = BookingStatus.Pending
            };

            Db.Set<OfficeHourBooking>().Add(booking);
            officeHour.Status = OfficeHourStatus.Booked;
            await unitOfWork.SaveChangesAsync();

            await notificationService.CreateOfficeHourBookedNotificationAsync(
                bookingId: booking.BookingId,
                instructorUserId: officeHour.Instructor.UserId,
                studentName: student.User.FullName,
                date: officeHour.Date,
                startTime: officeHour.StartTime
            );

            return booking.BookingId;
        }

        public async Task<List<StudentBookingListItemDTO>> GetMyBookingsAsync(string studentUserId, string? status)
        {
            var student = await Db.Set<StudentProfile>()
                .FirstOrDefaultAsync(s => s.UserId == studentUserId);

            if (student == null)
                throw new NotFoundException("Student profile not found");

            var query = Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Where(b => b.StudentId == student.StudentId);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
                query = query.Where(b => b.Status == bookingStatus);

            return await query
                .OrderByDescending(b => b.OfficeHour.Date)
                .ThenByDescending(b => b.OfficeHour.StartTime)
                .Select(b => new StudentBookingListItemDTO
                {
                    BookingId = b.BookingId,
                    Status = b.Status,
                    Purpose = b.Purpose,
                    StudentNotes = b.StudentNotes,
                    InstructorNotes = b.InstructorNotes,
                    BookedAt = b.BookedAt,
                    ConfirmedAt = b.ConfirmedAt,
                    CancelledAt = b.CancelledAt,
                    CancellationReason = b.CancellationReason,
                    CancelledBy = b.CancelledBy,
                    OfficeHour = new StudentBookingOfficeHourDTO
                    {
                        OfficeHourId = b.OfficeHour.OfficeHourId,
                        Date = b.OfficeHour.Date,
                        StartTime = b.OfficeHour.StartTime.ToString(@"hh\:mm"),
                        EndTime = b.OfficeHour.EndTime.ToString(@"hh\:mm"),
                        Notes = b.OfficeHour.Notes,
                        Room = b.OfficeHour.Room != null ? new RoomInfoDTO { RoomId = b.OfficeHour.Room.RoomId, RoomNumber = b.OfficeHour.Room.RoomNumber, Building = b.OfficeHour.Room.Building } : null
                    },
                    Instructor = new InstructorInfoDTO
                    {
                        InstructorId = b.OfficeHour.Instructor.InstructorId,
                        FullName = b.OfficeHour.Instructor.User.FullName,
                        Title = b.OfficeHour.Instructor.Title,
                        Degree = b.OfficeHour.Instructor.Degree,
                        Department = b.OfficeHour.Instructor.Department
                    }
                })
                .ToListAsync();
        }

        public async Task CancelBookingAsync(string userId, string userRole, int bookingId, string? reason)
        {
            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.Student)
                    .ThenInclude(s => s.User)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            if (userRole == "Student")
            {
                var student = await Db.Set<StudentProfile>().FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null || booking.StudentId != student.StudentId)
                    throw new ForbiddenException("Forbidden");
            }
            else if (userRole == "Instructor")
            {
                var instructor = await Db.Set<InstructorProfile>().FirstOrDefaultAsync(i => i.UserId == userId);
                if (instructor == null || booking.OfficeHour.InstructorId != instructor.InstructorId)
                    throw new ForbiddenException("Forbidden");
            }

            if (booking.Status == BookingStatus.Cancelled)
                throw new BadRequestException("Booking is already cancelled");

            if (booking.Status == BookingStatus.Completed)
                throw new BadRequestException("Cannot cancel a completed booking");

            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = reason;
            booking.CancelledBy = userRole;
            booking.CancelledAt = DateTime.UtcNow;
            booking.OfficeHour.Status = OfficeHourStatus.Available;

            await unitOfWork.SaveChangesAsync();

            if (userRole == "Student")
            {
                await notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.OfficeHour.Instructor.UserId,
                    cancelledBy: booking.Student.User.FullName,
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: reason
                );
            }
            else
            {
                await notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.Student.UserId,
                    cancelledBy: "Instructor",
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: reason
                );
            }
        }

        //DELETE (legacy - keep existing contract behavior for any older usages)
        public async Task DeleteOfficeHour(int id)
        {
            bool deleted = await officeHoursRepository.DeleteAsync(id);
            if (!deleted)
                throw new NotFoundException($"Office hours with ID {id} do not exist.");
        }


    }
}
