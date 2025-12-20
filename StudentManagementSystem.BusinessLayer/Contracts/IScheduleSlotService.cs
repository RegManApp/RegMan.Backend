using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface IScheduleSlotService
    {
        Task<ViewScheduleSlotDTO> CreateAsync(CreateScheduleSlotDTO dto);

        Task<IEnumerable<ViewScheduleSlotDTO>> GetBySectionAsync(int sectionId);

        Task<IEnumerable<ViewScheduleSlotDTO>> GetByInstructorAsync(int instructorId);

        Task<IEnumerable<ViewScheduleSlotDTO>> GetByRoomAsync(int roomId);

        Task<IEnumerable<ViewScheduleSlotDTO>> GetAllAsync();

        Task DeleteAsync(int scheduleSlotId);
    }
}
