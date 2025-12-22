using RegMan.Backend.BusinessLayer.DTOs.TimeSlotDTOs;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ITimeSlotService
    {
        Task<ViewTimeSlotDTO> CreateTimeSlotAsync(CreateTimeSlotDTO dto);
        Task<ViewTimeSlotDTO> UpdateTimeSlotAsync(UpdateTimeSlotDTO dto);
        Task<bool> DeleteTimeSlotAsync(int timeSlotId);

        Task<IEnumerable<ViewTimeSlotDTO>> GetAllTimeSlotsAsync();
    }
}
