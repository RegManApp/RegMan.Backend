using RegMan.Backend.BusinessLayer.DTOs.Integrations;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IGoogleCalendarIntegrationService
    {
        string CreateAuthorizationUrl(string userId, string? returnUrl, string oauthFlowBinding);
        GoogleCalendarOAuthState UnprotectState(string protectedState);
        Task<string?> HandleOAuthCallbackAsync(string code, string state, string oauthFlowBinding, CancellationToken cancellationToken);
        Task<bool> IsConnectedAsync(string userId, CancellationToken cancellationToken);
        Task TryUpsertOfficeHourBookingEventAsync(OfficeHourBooking booking, CancellationToken cancellationToken);
        Task TryDeleteOfficeHourBookingEventAsync(int bookingId, CancellationToken cancellationToken);
        Task DisconnectAsync(string userId, CancellationToken cancellationToken);
    }
}
