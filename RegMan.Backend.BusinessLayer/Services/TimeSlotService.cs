using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.TimeSlotDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace RegMan.Backend.BusinessLayer.Services 
{
    public class TimeSlotService : ITimeSlotService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IAuditLogService auditLogService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public TimeSlotService(
            IUnitOfWork unitOfWork,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor)
        {
            this.unitOfWork = unitOfWork;
            this.auditLogService = auditLogService;
            this.httpContextAccessor = httpContextAccessor;
        }

        private (string userId, string email) GetUserInfo()
        {
            var user = httpContextAccessor.HttpContext?.User
                ?? throw new Exception("User context not found.");

            return (
                user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new Exception("UserId not found."),
                user.FindFirstValue(ClaimTypes.Email)
                    ?? "unknown@email.com"
            );
        }

        // =========================
        // Update
        // =========================
        public async Task<ViewTimeSlotDTO> UpdateTimeSlotAsync(UpdateTimeSlotDTO dto)
        {
            var slot = await unitOfWork.TimeSlots.GetByIdAsync(dto.TimeSlotId);
            if (slot == null)
                throw new Exception("TimeSlot not found.");

            // Update fields
            slot.RoomId = dto.RoomId;
            slot.Day = dto.Day;
            slot.StartTime = dto.StartTime;
            slot.EndTime = dto.EndTime;

            if (!slot.IsValid())
                throw new Exception("End time must be greater than start time.");

            // Prevent overlapping time slots for the same room and day (excluding self)
            var overlapping = await unitOfWork.TimeSlots.GetAllAsQueryable()
                .AnyAsync(ts =>
                    ts.TimeSlotId != slot.TimeSlotId &&
                    ts.RoomId == slot.RoomId &&
                    ts.Day == slot.Day &&
                    ((slot.StartTime < ts.EndTime && slot.EndTime > ts.StartTime))
                );
            if (overlapping)
                throw new Exception("This time slot overlaps with an existing slot for this room.");

            unitOfWork.TimeSlots.Update(slot);
            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "UPDATE",
                "TimeSlot",
                slot.TimeSlotId.ToString()
            );

            return new ViewTimeSlotDTO
            {
                TimeSlotId = slot.TimeSlotId,
                RoomId = slot.RoomId,
                Day = slot.Day,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            };
        }

        // =========================
        // Create
        // =========================
        public async Task<ViewTimeSlotDTO> CreateTimeSlotAsync(CreateTimeSlotDTO dto)
        {
            var slot = new TimeSlot
            {
                RoomId = dto.RoomId,
                Day = dto.Day,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime
            };

            if (!slot.IsValid())
                throw new Exception("End time must be greater than start time.");

            // Prevent overlapping time slots for the same room and day
            var overlapping = await unitOfWork.TimeSlots.GetAllAsQueryable()
                .AnyAsync(ts =>
                    ts.RoomId == slot.RoomId &&
                    ts.Day == slot.Day &&
                    ((slot.StartTime < ts.EndTime && slot.EndTime > ts.StartTime))
                );
            if (overlapping)
                throw new Exception("This time slot overlaps with an existing slot for this room.");

            await unitOfWork.TimeSlots.AddAsync(slot);
            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "CREATE",
                "TimeSlot",
                slot.TimeSlotId.ToString()
            );

            return new ViewTimeSlotDTO
            {
                TimeSlotId = slot.TimeSlotId,
                RoomId = slot.RoomId,
                Day = slot.Day,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            };
        }

        // =========================
        // Delete
        // =========================
        public async Task<bool> DeleteTimeSlotAsync(int timeSlotId)
        {
            bool deleted = await unitOfWork.TimeSlots.DeleteAsync(timeSlotId);
            if (!deleted)
                return false;

            await unitOfWork.SaveChangesAsync();

            // Audit Log
            var (userId, email) = GetUserInfo();
            await auditLogService.LogAsync(
                userId,
                email,
                "DELETE",
                "TimeSlot",
                timeSlotId.ToString()
            );

            return true;
        }

        // =========================
        // Get
        // =========================
        public async Task<IEnumerable<ViewTimeSlotDTO>> GetAllTimeSlotsAsync()
        {
            var slots = await unitOfWork.TimeSlots.GetAllAsQueryable().ToListAsync();

            return slots.Select(s => new ViewTimeSlotDTO
            {
                TimeSlotId = s.TimeSlotId,
                RoomId = s.RoomId,
                Day = s.Day,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            });
        }
    }
}
