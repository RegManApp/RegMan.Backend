using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class ScheduleSlotService : IScheduleSlotService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IAuditLogService auditLogService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public ScheduleSlotService(
            IUnitOfWork unitOfWork,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor)
        {
            this.unitOfWork = unitOfWork;
            this.auditLogService = auditLogService;
            this.httpContextAccessor = httpContextAccessor;
        }

        // =========================
        // Helpers
        // =========================
        private (string userId, string email) GetUserInfo()
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new Exception("User context not found.");

            return (
                user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new Exception("UserId not found."),
                user.FindFirstValue(ClaimTypes.Email)
                    ?? throw new Exception("User email not found.")
            );
        }

        // =========================
        // Create ScheduleSlot
        // =========================
        public async Task<ViewScheduleSlotDTO> CreateAsync(CreateScheduleSlotDTO dto)
        {
            // =========================
            // Validate existence
            // =========================
            var section = await unitOfWork.Sections
                .GetAllAsQueryable()
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.SectionId == dto.SectionId)
                ?? throw new Exception("Section not found.");

            var room = await unitOfWork.Rooms.GetByIdAsync(dto.RoomId)
                ?? throw new Exception("Room not found.");

            var timeSlot = await unitOfWork.TimeSlots.GetByIdAsync(dto.TimeSlotId)
                ?? throw new Exception("TimeSlot not found.");

            //var instructor = await unitOfWork.InstructorProfiles.GetByIdAsync(dto.InstructorId)
            //    ?? throw new Exception("Instructor not found.");
            var instructor = await unitOfWork
                .InstructorProfiles
                .GetAllAsQueryable()
                .AsNoTracking()
                .Include(ip => ip.User)
                .FirstOrDefaultAsync(ip => ip.InstructorId == dto.InstructorId)
                ?? throw new Exception("Instructor not found.");

            // =========================
            // Conflict Detection
            // =========================
            bool conflictExists = await unitOfWork.ScheduleSlots
                .GetAllAsQueryable()
                .AnyAsync(ss =>
                    ss.TimeSlotId == dto.TimeSlotId &&
                    (
                        ss.RoomId == dto.RoomId ||
                        ss.InstructorId == dto.InstructorId ||
                        ss.SectionId == dto.SectionId
                    )
                );

            if (conflictExists)
                throw new Exception("Schedule conflict detected.");

            // =========================
            // Create Entity
            // =========================
            var scheduleSlot = new ScheduleSlot
            {
                SectionId = dto.SectionId,
                RoomId = dto.RoomId,
                TimeSlotId = dto.TimeSlotId,
                InstructorId = dto.InstructorId,
                SlotType = dto.SlotType
            };

            await unitOfWork.ScheduleSlots.AddAsync(scheduleSlot);
            await unitOfWork.SaveChangesAsync();

            // =========================
            // Audit Log
            // =========================
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "CREATE",
                "ScheduleSlot",
                scheduleSlot.ScheduleSlotId.ToString()
            );

            // =========================
            // Return DTO
            // =========================
            return new ViewScheduleSlotDTO
            {
                ScheduleSlotId = scheduleSlot.ScheduleSlotId,

                SectionId = section.SectionId,
                SectionName = $"{section.Course.CourseName} - Section {section.SectionId}",

                RoomId = room.RoomId,
                Room = $"{room.Building} - {room.RoomNumber}",

                TimeSlotId = timeSlot.TimeSlotId,
                TimeSlot = $"{timeSlot.Day} {timeSlot.StartTime}-{timeSlot.EndTime}",

                InstructorId = instructor.InstructorId,
                InstructorName = instructor.User.FullName,

                SlotType = scheduleSlot.SlotType.ToString()
            };
        }

        // =========================
        // Queries
        // =========================
        public async Task<IEnumerable<ViewScheduleSlotDTO>> GetAllAsync()
        {
            return await BuildQuery().ToListAsync();
        }

        public async Task<IEnumerable<ViewScheduleSlotDTO>> GetBySectionAsync(int sectionId)
        {
            return await BuildQuery()
                .Where(s => s.SectionId == sectionId)
                .ToListAsync();
        }

        public async Task<IEnumerable<ViewScheduleSlotDTO>> GetByInstructorAsync(int instructorId)
        {
            return await BuildQuery()
                .Where(s => s.InstructorId == instructorId)
                .ToListAsync();
        }

        public async Task<IEnumerable<ViewScheduleSlotDTO>> GetByRoomAsync(int roomId)
        {
            return await BuildQuery()
                .Where(s => s.RoomId == roomId)
                .ToListAsync();
        }

        // =========================
        // Shared Query Projection
        // =========================
        private IQueryable<ViewScheduleSlotDTO> BuildQuery()
        {
            return unitOfWork.ScheduleSlots
                .GetAllAsQueryable()
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Course)
                .Include(s => s.Room)
                .Include(s => s.TimeSlot)
                .Include(s => s.Instructor)
                    .ThenInclude(i => i.User)
                .Select(s => new ViewScheduleSlotDTO
                {
                    ScheduleSlotId = s.ScheduleSlotId,

                    SectionId = s.SectionId,
                    SectionName =
                        s.Section.Course.CourseName +
                        " - Section " +
                        s.Section.SectionId,

                    RoomId = s.RoomId,
                    Room = s.Room.Building + " - " + s.Room.RoomNumber,

                    TimeSlotId = s.TimeSlotId,
                    TimeSlot =
                        s.TimeSlot.Day + " " +
                        s.TimeSlot.StartTime + "-" +
                        s.TimeSlot.EndTime,

                    InstructorId = s.InstructorId,
                    InstructorName = s.Instructor.User.FullName,

                    SlotType = s.SlotType.ToString()
                });
        }
    }
}
