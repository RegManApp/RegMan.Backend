using RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ISmartOfficeHoursRealtimePublisher
    {
        Task PublishProviderViewAsync(int officeHourId, SmartOfficeHoursProviderViewDto payload);
        Task PublishStudentViewAsync(string studentUserId, int officeHourId, SmartOfficeHoursStudentViewDto payload);
    }
}
