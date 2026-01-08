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
        private readonly IChatService chatService;
        private readonly IChatRealtimePublisher chatRealtimePublisher;
        private readonly IGoogleCalendarIntegrationService googleCalendarIntegrationService;
        private readonly ICalendarReminderEngine calendarReminderEngine;
        private readonly ILogger<OfficeHoursService> logger;
        private readonly IBaseRepository<OfficeHour> officeHoursRepository;
        private readonly IBaseRepository<InstructorProfile> instructorsRepository;
        public OfficeHoursService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IChatService chatService,
            IChatRealtimePublisher chatRealtimePublisher,
            IGoogleCalendarIntegrationService googleCalendarIntegrationService,
            ICalendarReminderEngine calendarReminderEngine,
            ILogger<OfficeHoursService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.notificationService = notificationService;
            this.chatService = chatService;
            this.chatRealtimePublisher = chatRealtimePublisher;
            this.googleCalendarIntegrationService = googleCalendarIntegrationService;
            this.calendarReminderEngine = calendarReminderEngine;
            this.logger = logger;
            this.officeHoursRepository = unitOfWork.OfficeHours;
            this.instructorsRepository = unitOfWork.InstructorProfiles;
        }

        private static string BuildBookingSystemMessage(string titleLine, OfficeHour officeHour)
        {
            var date = officeHour.Date.ToString("yyyy-MM-dd");
            var time = $"{officeHour.StartTime:hh\\:mm} - {officeHour.EndTime:hh\\:mm}";
            var location = officeHour.Room != null
                ? $"{officeHour.Room.Building} - {officeHour.Room.RoomNumber}"
                : "(no location)";

            return $"{titleLine}\nDate: {date}\nTime: {time}\nLocation: {location}";
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
                    InstructorId = oh.InstructorId!.Value,
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
                    .ThenInclude(b => b.BookerUser)
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
                        Student = new AdminStudentInfoDTO
                        {
                            StudentId = b.StudentId ?? 0,
                            FullName = b.BookerUser.FullName
                        }
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
                OwnerUserId = instructor.UserId,
                OwnerRole = "Instructor",
                Capacity = 1,
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
                InstructorId = officeHour.InstructorId!.Value,
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
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot manage office hours");

            var query = Db.Set<OfficeHour>()
                .Include(oh => oh.Room)
                .Include(oh => oh.Bookings)
                    .ThenInclude(b => b.BookerUser)
                .Where(oh => oh.OwnerUserId == instructorUserId);

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
                    Bookings = oh.Bookings.Select(b => new ProviderBookingListItemDTO
                    {
                        BookingId = b.BookingId,
                        Status = b.Status,
                        Purpose = b.Purpose,
                        BookerNotes = b.BookerNotes,
                        ProviderNotes = b.ProviderNotes,
                        BookedAt = b.BookedAt,
                        Booker = new UserInfoDTO
                        {
                            UserId = b.BookerUserId,
                            FullName = b.BookerUser.FullName,
                            Email = b.BookerUser.Email,
                            Role = b.BookerUser.Role
                        }
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<int> CreateOfficeHourAsync(string instructorUserId, CreateInstructorOfficeHourDTO dto)
        {
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot create office hours");

            int? instructorId = null;
            if (string.Equals(owner.Role, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                instructorId = await Db.Set<InstructorProfile>()
                    .Where(i => i.UserId == instructorUserId)
                    .Select(i => (int?)i.InstructorId)
                    .FirstOrDefaultAsync();

                if (instructorId == null)
                    throw new NotFoundException("Instructor profile not found");
            }

            if (!TimeSpan.TryParse(dto.StartTime, out var startTime) ||
                !TimeSpan.TryParse(dto.EndTime, out var endTime))
            {
                throw new BadRequestException("Invalid time format. Use HH:mm");
            }

            if (endTime <= startTime)
                throw new BadRequestException("End time must be after start time");

            var hasOverlap = await Db.Set<OfficeHour>()
                .AnyAsync(oh => oh.OwnerUserId == instructorUserId &&
                               oh.Date.Date == dto.Date.Date &&
                               oh.Status != OfficeHourStatus.Cancelled &&
                               ((startTime >= oh.StartTime && startTime < oh.EndTime) ||
                                (endTime > oh.StartTime && endTime <= oh.EndTime) ||
                                (startTime <= oh.StartTime && endTime >= oh.EndTime)));

            if (hasOverlap)
                throw new BadRequestException("This time slot overlaps with an existing office hour");

            var officeHour = new OfficeHour
            {
                OwnerUserId = instructorUserId,
                OwnerRole = owner.Role,
                Capacity = 1,
                InstructorId = instructorId,
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
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot create office hours");

            int? instructorId = null;
            if (string.Equals(owner.Role, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                instructorId = await Db.Set<InstructorProfile>()
                    .Where(i => i.UserId == instructorUserId)
                    .Select(i => (int?)i.InstructorId)
                    .FirstOrDefaultAsync();

                if (instructorId == null)
                    throw new NotFoundException("Instructor profile not found");
            }

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
                    OwnerUserId = instructorUserId,
                    OwnerRole = owner.Role,
                    Capacity = 1,
                    InstructorId = instructorId,
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
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot update office hours");

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId && oh.OwnerUserId == instructorUserId);

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
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot delete office hours");

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.Bookings)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId && oh.OwnerUserId == instructorUserId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            foreach (var booking in officeHour.Bookings.Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationReason = "Office hour was cancelled by provider";
                booking.CancelledBy = "Provider";
                booking.CancelledAt = DateTime.UtcNow;

                try
                {
                    await calendarReminderEngine.CancelOfficeHourRemindersAsync(booking.BookingId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Reminder cancellation failed for BookingId={BookingId}", booking.BookingId);
                }

                await notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.BookerUserId,
                    cancelledBy: owner.FullName,
                    date: officeHour.Date,
                    startTime: officeHour.StartTime,
                    reason: booking.CancellationReason
                );

                try
                {
                    await googleCalendarIntegrationService.TryDeleteOfficeHourBookingEventAsync(booking.BookingId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Google Calendar delete failed for BookingId={BookingId}", booking.BookingId);
                }
            }

            Db.Set<OfficeHour>().Remove(officeHour);
            await unitOfWork.SaveChangesAsync();
        }

        public async Task ConfirmBookingAsync(string instructorUserId, int bookingId)
        {
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot confirm bookings");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.OwnerUser)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Include(b => b.BookerUser)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.OwnerUserId == instructorUserId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            if (booking.Status != BookingStatus.Pending)
                throw new BadRequestException("Booking is not in pending status");

            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedAt = DateTime.UtcNow;
            booking.OfficeHour.Status = OfficeHourStatus.Booked;

            await unitOfWork.SaveChangesAsync();

            await notificationService.CreateOfficeHourConfirmedNotificationAsync(
                studentUserId: booking.BookerUserId,
                instructorName: booking.OfficeHour.OwnerUser.FullName,
                date: booking.OfficeHour.Date,
                startTime: booking.OfficeHour.StartTime
            );

            try
            {
                // Best-effort: schedule in-app reminders for both participants.
                await calendarReminderEngine.EnsurePlannedForUserAsync(booking.BookerUserId, DateTime.UtcNow, CancellationToken.None);
                await calendarReminderEngine.EnsurePlannedForUserAsync(booking.OfficeHour.OwnerUserId, DateTime.UtcNow, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder planning failed for BookingId={BookingId}", booking.BookingId);
            }

            try
            {
                // Best-effort: never fail booking if Google integration fails.
                await googleCalendarIntegrationService.TryUpsertOfficeHourBookingEventAsync(booking, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar integration failed for BookingId={BookingId}", booking.BookingId);
            }

            var conversation = await chatService.GetOrCreateDirectConversationAsync(booking.BookerUserId, booking.OfficeHour.OwnerUserId, pageNumber: 1, pageSize: 1);
            await chatRealtimePublisher.PublishConversationCreatedAsync(booking.BookerUserId, conversation.ConversationId);
            await chatRealtimePublisher.PublishConversationCreatedAsync(booking.OfficeHour.OwnerUserId, conversation.ConversationId);

            var messageText = BuildBookingSystemMessage("✅ Office hour booking confirmed", booking.OfficeHour);
            var systemMessage = await chatService.SendSystemMessageToConversationAsync(
                conversationId: conversation.ConversationId,
                textMessage: messageText,
                clientMessageId: $"officehour-booking:{booking.BookingId}:confirmed");

            await chatRealtimePublisher.PublishSystemMessageCreatedAsync(conversation.ConversationId, systemMessage);
        }

        public async Task AddInstructorNotesAsync(string instructorUserId, int bookingId, string? notes)
        {
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot add notes");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.OwnerUserId == instructorUserId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            booking.ProviderNotes = notes;
            await unitOfWork.SaveChangesAsync();
        }

        public async Task CompleteBookingAsync(string instructorUserId, int bookingId)
        {
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot complete bookings");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.OwnerUserId == instructorUserId);

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
            var owner = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == instructorUserId);

            if (owner == null)
                throw new NotFoundException("User not found");

            if (string.Equals(owner.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Students cannot mark no-show");

            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.OfficeHour.OwnerUserId == instructorUserId);

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

        public async Task<List<StudentProvidersWithOfficeHoursDTO>> GetProvidersWithOfficeHoursAsync(string? role, int? courseId, int? sectionId)
        {
            var todayUtc = DateTime.UtcNow.Date;

            var counts = await Db.Set<OfficeHour>()
                .AsNoTracking()
                .Where(oh => oh.Status == OfficeHourStatus.Available && oh.Date >= todayUtc)
                .GroupBy(oh => oh.OwnerUserId)
                .Select(g => new { OwnerUserId = g.Key, AvailableSlots = g.Count() })
                .ToListAsync();

            var countByUserId = counts.ToDictionary(c => c.OwnerUserId, c => c.AvailableSlots);

            var providersQuery = Db.Set<BaseUser>()
                .AsNoTracking()
                .Where(u => !string.Equals(u.Role, "Student", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(role))
            {
                var normalizedRole = role.Trim();
                providersQuery = providersQuery.Where(u => u.Role == normalizedRole);
            }

            if (courseId.HasValue || sectionId.HasValue)
            {
                var sectionsQuery = Db.Set<Section>().AsNoTracking();
                if (sectionId.HasValue)
                    sectionsQuery = sectionsQuery.Where(s => s.SectionId == sectionId.Value);
                if (courseId.HasValue)
                    sectionsQuery = sectionsQuery.Where(s => s.CourseId == courseId.Value);

                var instructorIds = await sectionsQuery
                    .Where(s => s.InstructorId.HasValue)
                    .Select(s => s.InstructorId!.Value)
                    .Distinct()
                    .ToListAsync();

                var instructorUserIds = await Db.Set<InstructorProfile>()
                    .AsNoTracking()
                    .Where(i => instructorIds.Contains(i.InstructorId))
                    .Select(i => i.UserId)
                    .Distinct()
                    .ToListAsync();

                providersQuery = providersQuery.Where(u => instructorUserIds.Contains(u.Id));
            }

            var providers = await providersQuery
                .Select(u => new { u.Id, u.FullName, u.Role })
                .ToListAsync();

            var providerIds = providers.Select(p => p.Id).ToList();
            var instructors = await Db.Set<InstructorProfile>()
                .AsNoTracking()
                .Where(i => providerIds.Contains(i.UserId))
                .Select(i => new { i.UserId, i.Title, i.Degree, i.Department })
                .ToListAsync();

            var instructorByUserId = instructors.ToDictionary(i => i.UserId, i => i);

            return providers
                .Select(p =>
                {
                    instructorByUserId.TryGetValue(p.Id, out var inst);
                    countByUserId.TryGetValue(p.Id, out var slots);

                    return new StudentProvidersWithOfficeHoursDTO
                    {
                        AvailableSlots = slots,
                        Provider = new ProviderInfoDTO
                        {
                            UserId = p.Id,
                            FullName = p.FullName,
                            Role = p.Role,
                            Title = inst?.Title,
                            Degree = inst?.Degree,
                            Department = inst?.Department
                        }
                    };
                })
                .OrderBy(p => p.Provider.FullName)
                .ToList();
        }

        public async Task<List<StudentAvailableOfficeHourV2DTO>> GetAvailableOfficeHoursV2Async(string? providerUserId, DateTime? fromDate, DateTime? toDate)
        {
            var query = Db.Set<OfficeHour>()
                .AsNoTracking()
                .Include(oh => oh.OwnerUser)
                .Include(oh => oh.Instructor)
                .Include(oh => oh.Room)
                .Where(oh => oh.Status == OfficeHourStatus.Available && oh.Date >= DateTime.UtcNow.Date);

            if (!string.IsNullOrWhiteSpace(providerUserId))
                query = query.Where(oh => oh.OwnerUserId == providerUserId);
            if (fromDate.HasValue)
                query = query.Where(oh => oh.Date >= fromDate.Value.Date);
            if (toDate.HasValue)
                query = query.Where(oh => oh.Date <= toDate.Value.Date);

            return await query
                .OrderBy(oh => oh.Date)
                .ThenBy(oh => oh.StartTime)
                .Select(oh => new StudentAvailableOfficeHourV2DTO
                {
                    OfficeHourId = oh.OfficeHourId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Notes = oh.Notes,
                    Room = oh.Room != null
                        ? new RoomInfoDTO { RoomId = oh.Room.RoomId, RoomNumber = oh.Room.RoomNumber, Building = oh.Room.Building }
                        : null,
                    Provider = new ProviderInfoDTO
                    {
                        UserId = oh.OwnerUserId,
                        FullName = oh.OwnerUser.FullName,
                        Role = oh.OwnerRole,
                        Title = oh.Instructor != null ? oh.Instructor.Title : null,
                        Degree = oh.Instructor != null ? oh.Instructor.Degree : null,
                        Department = oh.Instructor != null ? oh.Instructor.Department : null
                    }
                })
                .ToListAsync();
        }

        public async Task<BookOfficeHourResultDTO> BookOfficeHourAsync(string bookerUserId, int officeHourId, BookOfficeHourRequestDTO dto)
        {
            var booker = await Db.Set<BaseUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == bookerUserId);

            if (booker == null)
                throw new NotFoundException("User not found");

            int? studentId = null;
            if (string.Equals(booker.Role, "Student", StringComparison.OrdinalIgnoreCase))
            {
                studentId = await Db.Set<StudentProfile>()
                    .AsNoTracking()
                    .Where(s => s.UserId == bookerUserId)
                    .Select(s => (int?)s.StudentId)
                    .FirstOrDefaultAsync();

                if (studentId == null)
                    throw new NotFoundException("Student profile not found");
            }

            var officeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.OwnerUser)
                .Include(oh => oh.Instructor)
                    .ThenInclude(i => i.User)
                .Include(oh => oh.Room)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == officeHourId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            if (officeHour.OwnerUserId == bookerUserId)
                throw new BadRequestException("Cannot book your own office hours");

            if (officeHour.Status != OfficeHourStatus.Available)
                throw new BadRequestException("This office hour is no longer available");

            if (officeHour.Date.Date < DateTime.UtcNow.Date)
                throw new BadRequestException("Cannot book past office hours");

            var existingBooking = await Db.Set<OfficeHourBooking>()
                .AnyAsync(b => b.OfficeHourId == officeHourId &&
                              b.BookerUserId == bookerUserId &&
                              (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (existingBooking)
                throw new BadRequestException("You already have a booking for this office hour");

            var activeCount = await Db.Set<OfficeHourBooking>()
                .CountAsync(b => b.OfficeHourId == officeHourId &&
                               (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (officeHour.Capacity < 1)
                throw new BadRequestException("This office hour is not bookable");

            if (activeCount >= officeHour.Capacity)
                throw new BadRequestException("This office hour is fully booked");

            var booking = new OfficeHourBooking
            {
                OfficeHourId = officeHourId,
                BookerUserId = bookerUserId,
                BookerRole = booker.Role,
                StudentId = studentId,
                Purpose = dto.Purpose,
                BookerNotes = dto.BookerNotes,
                Status = BookingStatus.Pending
            };

            Db.Set<OfficeHourBooking>().Add(booking);

            var newActiveCount = activeCount + 1;
            officeHour.Status = newActiveCount >= officeHour.Capacity
                ? OfficeHourStatus.Booked
                : OfficeHourStatus.Available;
            await unitOfWork.SaveChangesAsync();

            await notificationService.CreateOfficeHourBookedNotificationAsync(
                bookingId: booking.BookingId,
                instructorUserId: officeHour.OwnerUserId,
                studentName: booker.FullName,
                date: officeHour.Date,
                startTime: officeHour.StartTime
            );

            // Ensure/reuse a direct conversation between student and provider.
            var conversation = await chatService.GetOrCreateDirectConversationAsync(bookerUserId, officeHour.OwnerUserId, pageNumber: 1, pageSize: 1);

            // Best-effort: make sure both sides join the group immediately if online.
            await chatRealtimePublisher.PublishConversationCreatedAsync(bookerUserId, conversation.ConversationId);
            await chatRealtimePublisher.PublishConversationCreatedAsync(officeHour.OwnerUserId, conversation.ConversationId);

            var messageText = BuildBookingSystemMessage("📅 A new office hour booking has been created", officeHour);
            var systemMessage = await chatService.SendSystemMessageToConversationAsync(
                conversationId: conversation.ConversationId,
                textMessage: messageText,
                clientMessageId: $"officehour-booking:{booking.BookingId}:created");

            await chatRealtimePublisher.PublishSystemMessageCreatedAsync(conversation.ConversationId, systemMessage);

            return new BookOfficeHourResultDTO
            {
                BookingId = booking.BookingId,
                ConversationId = conversation.ConversationId,
                SystemMessageId = systemMessage.MessageId
            };
        }

        public async Task<List<UserBookingListItemDTO>> GetMyBookingsAsync(string bookerUserId, string? status)
        {
            var userExists = await Db.Set<BaseUser>()
                .AsNoTracking()
                .AnyAsync(u => u.Id == bookerUserId);

            if (!userExists)
                throw new NotFoundException("User not found");

            var query = Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.OwnerUser)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Instructor)
                        .ThenInclude(i => i.User)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Where(b => b.BookerUserId == bookerUserId);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
                query = query.Where(b => b.Status == bookingStatus);

            return await query
                .OrderByDescending(b => b.OfficeHour.Date)
                .ThenByDescending(b => b.OfficeHour.StartTime)
                .Select(b => new UserBookingListItemDTO
                {
                    BookingId = b.BookingId,
                    Status = b.Status,
                    Purpose = b.Purpose,
                    BookerNotes = b.BookerNotes,
                    ProviderNotes = b.ProviderNotes,
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
                    Provider = new ProviderInfoDTO
                    {
                        UserId = b.OfficeHour.OwnerUserId,
                        FullName = b.OfficeHour.OwnerUser.FullName,
                        Role = b.OfficeHour.OwnerUser.Role,
                        Title = b.OfficeHour.Instructor != null ? b.OfficeHour.Instructor.Title : null,
                        Degree = b.OfficeHour.Instructor != null ? b.OfficeHour.Instructor.Degree : null,
                        Department = b.OfficeHour.Instructor != null ? b.OfficeHour.Instructor.Department : null
                    },
                    Instructor = b.OfficeHour.Instructor != null
                        ? new InstructorInfoDTO
                        {
                            InstructorId = b.OfficeHour.Instructor.InstructorId,
                            FullName = b.OfficeHour.Instructor.User.FullName,
                            Title = b.OfficeHour.Instructor.Title,
                            Degree = b.OfficeHour.Instructor.Degree,
                            Department = b.OfficeHour.Instructor.Department
                        }
                        : null
                })
                .ToListAsync();
        }

        public async Task CancelBookingAsync(string userId, string userRole, int bookingId, string? reason)
        {
            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.OwnerUser)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Include(b => b.BookerUser)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            var isBooker = booking.BookerUserId == userId;
            var isProvider = booking.OfficeHour.OwnerUserId == userId;
            if (!isBooker && !isProvider)
                throw new ForbiddenException("Forbidden");

            if (booking.Status == BookingStatus.Cancelled)
                throw new BadRequestException("Booking is already cancelled");

            if (booking.Status == BookingStatus.Completed)
                throw new BadRequestException("Cannot cancel a completed booking");

            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = reason;
            booking.CancelledBy = isProvider ? "Provider" : "Booker";
            booking.CancelledAt = DateTime.UtcNow;

            // Re-open the slot if it was fully booked.
            var remainingActiveCount = await Db.Set<OfficeHourBooking>()
                .CountAsync(b => b.OfficeHourId == booking.OfficeHourId &&
                               b.BookingId != booking.BookingId &&
                               (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            booking.OfficeHour.Status = remainingActiveCount >= booking.OfficeHour.Capacity
                ? OfficeHourStatus.Booked
                : OfficeHourStatus.Available;

            await unitOfWork.SaveChangesAsync();

            try
            {
                await calendarReminderEngine.CancelOfficeHourRemindersAsync(booking.BookingId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder cancellation failed for BookingId={BookingId}", booking.BookingId);
            }

            try
            {
                await googleCalendarIntegrationService.TryDeleteOfficeHourBookingEventAsync(booking.BookingId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar delete failed for BookingId={BookingId}", booking.BookingId);
            }

            if (isProvider)
            {
                await notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.BookerUserId,
                    cancelledBy: booking.OfficeHour.OwnerUser.FullName,
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: reason
                );
            }
            else
            {
                await notificationService.CreateOfficeHourCancelledNotificationAsync(
                    userId: booking.OfficeHour.OwnerUserId,
                    cancelledBy: booking.BookerUser.FullName,
                    date: booking.OfficeHour.Date,
                    startTime: booking.OfficeHour.StartTime,
                    reason: reason
                );
            }

            var conversation = await chatService.GetOrCreateDirectConversationAsync(booking.BookerUserId, booking.OfficeHour.OwnerUserId, pageNumber: 1, pageSize: 1);
            await chatRealtimePublisher.PublishConversationCreatedAsync(booking.BookerUserId, conversation.ConversationId);
            await chatRealtimePublisher.PublishConversationCreatedAsync(booking.OfficeHour.OwnerUserId, conversation.ConversationId);

            var reasonLine = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\nReason: {reason}";
            var cancelledByLabel = isProvider ? "Provider" : "Booker";
            var messageText = BuildBookingSystemMessage($"❌ Office hour booking cancelled by {cancelledByLabel}", booking.OfficeHour) + reasonLine;
            var systemMessage = await chatService.SendSystemMessageToConversationAsync(
                conversationId: conversation.ConversationId,
                textMessage: messageText,
                clientMessageId: $"officehour-booking:{booking.BookingId}:cancelled");

            await chatRealtimePublisher.PublishSystemMessageCreatedAsync(conversation.ConversationId, systemMessage);
        }

        public async Task<RescheduleOfficeHourBookingResultDTO> RescheduleBookingAsync(string userId, string userRole, int bookingId, int newOfficeHourId, string? reason)
        {
            var booking = await Db.Set<OfficeHourBooking>()
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.OwnerUser)
                .Include(b => b.OfficeHour)
                    .ThenInclude(oh => oh.Room)
                .Include(b => b.BookerUser)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                throw new NotFoundException("Booking not found");

            var canReschedule = booking.BookerUserId == userId || booking.OfficeHour.OwnerUserId == userId || userRole == "Admin";
            if (!canReschedule)
                throw new ForbiddenException("Forbidden");

            if (booking.Status == BookingStatus.Cancelled)
                throw new BadRequestException("Cannot reschedule a cancelled booking");

            if (booking.Status == BookingStatus.Completed)
                throw new BadRequestException("Cannot reschedule a completed booking");

            if (booking.Status == BookingStatus.NoShow)
                throw new BadRequestException("Cannot reschedule a no-show booking");

            if (booking.OfficeHourId == newOfficeHourId)
                throw new BadRequestException("New office hour must be different from the current one");

            var newOfficeHour = await Db.Set<OfficeHour>()
                .Include(oh => oh.OwnerUser)
                .Include(oh => oh.Room)
                .FirstOrDefaultAsync(oh => oh.OfficeHourId == newOfficeHourId);

            if (newOfficeHour == null)
                throw new NotFoundException("New office hour not found");

            if (newOfficeHour.Status != OfficeHourStatus.Available)
                throw new BadRequestException("New office hour is not available");

            if (newOfficeHour.Date.Date < DateTime.UtcNow.Date)
                throw new BadRequestException("Cannot reschedule to a past office hour");

            // Keep the conversation stable: reschedule within the same provider.
            if (!string.Equals(newOfficeHour.OwnerUserId, booking.OfficeHour.OwnerUserId, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Cannot reschedule to a different provider");

            var newActiveCount = await Db.Set<OfficeHourBooking>()
                .CountAsync(b => b.OfficeHourId == newOfficeHourId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (newOfficeHour.Capacity < 1)
                throw new BadRequestException("New office hour is not bookable");

            if (newActiveCount >= newOfficeHour.Capacity)
                throw new BadRequestException("New office hour is fully booked");

            var oldOfficeHour = booking.OfficeHour;
            var oldOfficeHourId = booking.OfficeHourId;

            booking.OfficeHourId = newOfficeHourId;
            booking.OfficeHour = newOfficeHour;

            // Rescheduling requires re-confirmation.
            booking.Status = BookingStatus.Pending;
            booking.ConfirmedAt = null;

            // Update availability for both old and new office hours.
            var remainingActiveCountOld = await Db.Set<OfficeHourBooking>()
                .CountAsync(b => b.OfficeHourId == oldOfficeHourId && b.BookingId != booking.BookingId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            oldOfficeHour.Status = remainingActiveCountOld >= oldOfficeHour.Capacity
                ? OfficeHourStatus.Booked
                : OfficeHourStatus.Available;

            var updatedActiveCountNew = newActiveCount + 1;
            newOfficeHour.Status = updatedActiveCountNew >= newOfficeHour.Capacity
                ? OfficeHourStatus.Booked
                : OfficeHourStatus.Available;

            await unitOfWork.SaveChangesAsync();

            // Chat: persisted system message and realtime publish.
            var conversation = await chatService.GetOrCreateDirectConversationAsync(booking.BookerUserId, newOfficeHour.OwnerUserId, pageNumber: 1, pageSize: 1);
            await chatRealtimePublisher.PublishConversationCreatedAsync(booking.BookerUserId, conversation.ConversationId);
            await chatRealtimePublisher.PublishConversationCreatedAsync(newOfficeHour.OwnerUserId, conversation.ConversationId);

            var fromLine = BuildBookingSystemMessage("From", oldOfficeHour);
            var toLine = BuildBookingSystemMessage("To", newOfficeHour);
            var reasonLine = string.IsNullOrWhiteSpace(reason) ? string.Empty : $"\nReason: {reason}";
            var messageText = $"🔁 Office hour booking has been rescheduled\n\n{fromLine}\n\n{toLine}{reasonLine}";

            var systemMessage = await chatService.SendSystemMessageToConversationAsync(
                conversationId: conversation.ConversationId,
                textMessage: messageText,
                clientMessageId: $"officehour-booking:{booking.BookingId}:rescheduled:{newOfficeHourId}");

            await chatRealtimePublisher.PublishSystemMessageCreatedAsync(conversation.ConversationId, systemMessage);

            return new RescheduleOfficeHourBookingResultDTO
            {
                BookingId = booking.BookingId,
                ConversationId = conversation.ConversationId,
                SystemMessageId = systemMessage.MessageId
            };
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
