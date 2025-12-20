using RegMan.Backend.BusinessLayer.DTOs.InstructorDTOs;
using RegMan.Backend.BusinessLayer.DTOs.ScheduleSlotDTOs;

namespace RegMan.Backend.BusinessLayer.Contracts;

public interface IInstructorService
{
    Task<ViewInstructorDTO> CreateAsync(CreateInstructorDTO dto);
    Task<IEnumerable<ViewInstructorDTO>> GetAllAsync();
    Task<ViewInstructorDTO?> GetByIdAsync(int id);
    Task<ViewInstructorDTO?> GetByUserIdAsync(string userId);
    Task<ViewInstructorDTO?> UpdateAsync(int id, UpdateInstructorDTO dto);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<ViewScheduleSlotDTO>> GetScheduleAsync(int instructorId);
}
