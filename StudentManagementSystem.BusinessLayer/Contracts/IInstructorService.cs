using StudentManagementSystem.BusinessLayer.DTOs.InstructorDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;

namespace StudentManagementSystem.BusinessLayer.Contracts;

public interface IInstructorService
{
    Task<ViewInstructorDTO> CreateAsync(CreateInstructorDTO dto);
    Task<IEnumerable<ViewInstructorDTO>> GetAllAsync();
    Task<ViewInstructorDTO> GetByIdAsync(int id);

    Task<IEnumerable<ViewScheduleSlotDTO>> GetScheduleAsync(int instructorId);
}
