using System;

namespace RegMan.Backend.BusinessLayer.DTOs.Integrations
{
    public record GoogleCalendarOAuthState(string UserId, DateTime IssuedAtUtc, string? ReturnUrl);
}
