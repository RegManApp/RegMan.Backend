using RegMan.Backend.BusinessLayer.DTOs.Integrations;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IGoogleCalendarIntegrationService
    {
        string CreateAuthorizationUrl(string userId, string? returnUrl);
        GoogleCalendarOAuthState UnprotectState(string protectedState);
        Task<string?> HandleOAuthCallbackAsync(string code, string protectedState, CancellationToken cancellationToken);
        Task TryCreateOfficeHourBookingEventAsync(OfficeHourBooking booking, CancellationToken cancellationToken);
    }
}
