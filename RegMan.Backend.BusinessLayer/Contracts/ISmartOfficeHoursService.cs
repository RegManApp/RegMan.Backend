using RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ISmartOfficeHoursService
    {
        Task<SmartOfficeHoursStudentViewDto> JoinQueueAsync(string studentUserId, int officeHourId, SmartOfficeHoursJoinQueueRequestDto request);
        Task<SmartOfficeHoursStudentViewDto> GetMyStatusAsync(string userId, int officeHourId);

        Task<SmartOfficeHoursProviderViewDto> GetProviderViewAsync(string providerUserId, int officeHourId);
        Task<SmartOfficeHoursProviderViewDto> CallNextAsync(string providerUserId, int officeHourId);
        Task<SmartOfficeHoursProviderViewDto> CompleteCurrentAsync(string providerUserId, int officeHourId);
        Task<SmartOfficeHoursProviderViewDto> MarkNoShowCurrentAsync(string providerUserId, int officeHourId);

        Task<SmartOfficeHoursStudentViewDto> ScanQrAsync(string studentUserId, SmartOfficeHoursScanRequestDto request);

        // Background jobs
        Task<int> RotateReadyQrTokensAsync(CancellationToken cancellationToken);
        Task<int> AutoNoShowExpiredReadyEntriesAsync(CancellationToken cancellationToken);
    }
}
