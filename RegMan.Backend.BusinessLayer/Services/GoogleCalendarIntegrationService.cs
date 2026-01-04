using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Integrations;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using RegMan.Backend.DAL.Entities.Integrations;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class GoogleCalendarIntegrationService : IGoogleCalendarIntegrationService
    {
        private static readonly string[] Scopes = new[]
        {
            CalendarService.Scope.CalendarEvents
        };

        private readonly IUnitOfWork unitOfWork;
        private readonly ILogger<GoogleCalendarIntegrationService> logger;
        private readonly IDataProtector tokenProtector;
        private readonly IDataProtector stateProtector;
        private readonly string clientId;
        private readonly string clientSecret;
        private readonly string redirectUri;
        private readonly bool isConfigured;
        private readonly string? configurationError;

        private DbContext Db => unitOfWork.Context;

        public GoogleCalendarIntegrationService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<GoogleCalendarIntegrationService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.logger = logger;

            tokenProtector = dataProtectionProvider.CreateProtector("RegMan.GoogleCalendarTokens.v1");
            stateProtector = dataProtectionProvider.CreateProtector("RegMan.GoogleCalendarOAuthState.v1");

            // MonsterASP note:
            // - Env vars may not be reliably injected at runtime.
            // - Therefore we support configuration-based secrets via appsettings.Production.json (Google:* keys).
            // Resolution order (MANDATORY):
            //   1) Environment Variables (via IConfiguration)
            //   2) Google:ClientId / Google:ClientSecret / Google:RedirectUri
            clientId = ReadSetting(configuration, envKey: "GOOGLE_CLIENT_ID", configKey: "Google:ClientId");
            clientSecret = ReadSetting(configuration, envKey: "GOOGLE_CLIENT_SECRET", configKey: "Google:ClientSecret");
            redirectUri = ReadSetting(configuration, envKey: "GOOGLE_REDIRECT_URI", configKey: "Google:RedirectUri");

            var missing = new List<string>(capacity: 3);
            if (string.IsNullOrWhiteSpace(clientId))
                missing.Add("GOOGLE_CLIENT_ID or Google:ClientId");
            if (string.IsNullOrWhiteSpace(clientSecret))
                missing.Add("GOOGLE_CLIENT_SECRET or Google:ClientSecret");
            if (string.IsNullOrWhiteSpace(redirectUri))
                missing.Add("GOOGLE_REDIRECT_URI or Google:RedirectUri");

            isConfigured = missing.Count == 0;
            configurationError = isConfigured
                ? null
                : "Google OAuth is not configured. Missing: " + string.Join(", ", missing) +
                  ". Configure via environment variables OR appsettings.Production.json under the Google section.";

            if (!isConfigured)
            {
                logger.LogWarning(configurationError);
            }
        }

        public string CreateAuthorizationUrl(string userId, string? returnUrl)
        {
            if (!isConfigured)
                throw new InvalidOperationException(configurationError ?? "Google OAuth is not configured.");

            var state = ProtectState(new GoogleCalendarOAuthState(
                UserId: userId,
                IssuedAtUtc: DateTime.UtcNow,
                ReturnUrl: string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl
            ));

            var request = new GoogleAuthorizationCodeRequestUrl(new Uri("https://accounts.google.com/o/oauth2/v2/auth"))
            {
                ClientId = clientId,
                RedirectUri = redirectUri,
                Scope = string.Join(' ', Scopes),
                State = state,
                AccessType = "offline",
                IncludeGrantedScopes = "true",
                Prompt = "consent",
                ResponseType = "code"
            };

            return request.Build().ToString();
        }

        private static string ReadSetting(IConfiguration configuration, string envKey, string configKey)
        {
            // IMPORTANT: treat empty/whitespace as missing and allow fallback.
            var envValue = configuration[envKey];
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue;

            var configValue = configuration[configKey];
            if (!string.IsNullOrWhiteSpace(configValue))
                return configValue;

            return string.Empty;
        }

        public GoogleCalendarOAuthState UnprotectState(string protectedState)
        {
            try
            {
                var json = stateProtector.Unprotect(protectedState);
                var parsed = JsonSerializer.Deserialize<GoogleCalendarOAuthState>(json);

                if (parsed == null || string.IsNullOrWhiteSpace(parsed.UserId))
                    throw new InvalidOperationException("Invalid OAuth state payload.");

                // Basic replay protection window
                if (parsed.IssuedAtUtc < DateTime.UtcNow.AddMinutes(-30))
                    throw new InvalidOperationException("OAuth state has expired. Please try connecting again.");

                return parsed;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Invalid OAuth state.", ex);
            }
        }

        public async Task<string?> HandleOAuthCallbackAsync(string code, string protectedState, CancellationToken cancellationToken)
        {
            if (!isConfigured)
                throw new InvalidOperationException("Google OAuth is not configured.");

            var state = UnprotectState(protectedState);

            var flow = CreateFlow();

            TokenResponse tokenResponse = await flow.ExchangeCodeForTokenAsync(
                userId: state.UserId,
                code: code,
                redirectUri: redirectUri,
                taskCancellationToken: cancellationToken
            );

            await UpsertTokenAsync(state.UserId, tokenResponse, cancellationToken);

            return state.ReturnUrl;
        }

        public Task<bool> IsConnectedAsync(string userId, CancellationToken cancellationToken)
        {
            return HasGoogleTokenAsync(userId, cancellationToken);
        }

        public async Task TryCreateOfficeHourBookingEventAsync(OfficeHourBooking booking, CancellationToken cancellationToken)
        {
            if (!isConfigured)
                return;

            // Prefer instructor as organizer (they confirm the booking), fallback to student.
            var instructorUserId = booking.OfficeHour.Instructor.UserId;
            var studentUserId = booking.Student.UserId;

            var organizerUserId = await HasGoogleTokenAsync(instructorUserId, cancellationToken)
                ? instructorUserId
                : (await HasGoogleTokenAsync(studentUserId, cancellationToken) ? studentUserId : null);

            if (organizerUserId == null)
                return;

            try
            {
                var calendarService = await CreateCalendarServiceAsync(organizerUserId, cancellationToken);
                if (calendarService == null)
                    return;

                var startUtc = DateTime.SpecifyKind(booking.OfficeHour.Date.Date.Add(booking.OfficeHour.StartTime), DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(booking.OfficeHour.Date.Date.Add(booking.OfficeHour.EndTime), DateTimeKind.Utc);

                var description = BuildDescription(booking);

                var newEvent = new Event
                {
                    Summary = $"Office Hour with {booking.OfficeHour.Instructor.User.FullName}",
                    Description = description,
                    Location = booking.OfficeHour.Room != null
                        ? $"{booking.OfficeHour.Room.Building} - {booking.OfficeHour.Room.RoomNumber}"
                        : null,
                    Start = new EventDateTime
                    {
                        DateTimeDateTimeOffset = new DateTimeOffset(startUtc, TimeSpan.Zero),
                        TimeZone = "UTC"
                    },
                    End = new EventDateTime
                    {
                        DateTimeDateTimeOffset = new DateTimeOffset(endUtc, TimeSpan.Zero),
                        TimeZone = "UTC"
                    },
                    Attendees = new List<EventAttendee>
                    {
                        new() { Email = booking.Student.User.Email },
                        new() { Email = booking.OfficeHour.Instructor.User.Email }
                    }
                };

                var insert = calendarService.Events.Insert(newEvent, "primary");
                insert.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;

                await insert.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar event creation failed for BookingId={BookingId}", booking.BookingId);
            }
        }

        private GoogleAuthorizationCodeFlow CreateFlow()
        {
            if (!isConfigured)
                throw new InvalidOperationException("Google OAuth is not configured.");

            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = Scopes
            });
        }

        private string ProtectState(GoogleCalendarOAuthState state)
        {
            var json = JsonSerializer.Serialize(state);
            return stateProtector.Protect(json);
        }

        private async Task<bool> HasGoogleTokenAsync(string userId, CancellationToken cancellationToken)
        {
            return await Db.Set<GoogleCalendarUserToken>()
                .AsNoTracking()
                .AnyAsync(t => t.UserId == userId, cancellationToken);
        }

        private async Task UpsertTokenAsync(string userId, TokenResponse tokenResponse, CancellationToken cancellationToken)
        {
            var existing = await Db.Set<GoogleCalendarUserToken>()
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

            var nowUtc = DateTime.UtcNow;
            var expiresAtUtc = nowUtc.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600);

            var refreshToken = tokenResponse.RefreshToken;
            if (existing != null && string.IsNullOrWhiteSpace(refreshToken))
            {
                // Google may not return refresh_token on subsequent authorizations.
                refreshToken = tokenProtector.Unprotect(existing.RefreshTokenProtected);
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new InvalidOperationException(
                    "Google did not return a refresh_token. Ensure access_type=offline and prompt=consent, then try again."
                );
            }

            if (existing == null)
            {
                var created = new GoogleCalendarUserToken
                {
                    UserId = userId,
                    AccessTokenProtected = tokenProtector.Protect(tokenResponse.AccessToken),
                    RefreshTokenProtected = tokenProtector.Protect(refreshToken),
                    AccessTokenExpiresAtUtc = expiresAtUtc,
                    Scope = tokenResponse.Scope,
                    ConnectedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                };

                Db.Set<GoogleCalendarUserToken>().Add(created);
            }
            else
            {
                existing.AccessTokenProtected = tokenProtector.Protect(tokenResponse.AccessToken);
                existing.RefreshTokenProtected = tokenProtector.Protect(refreshToken);
                existing.AccessTokenExpiresAtUtc = expiresAtUtc;
                existing.Scope = tokenResponse.Scope ?? existing.Scope;
                existing.UpdatedAtUtc = nowUtc;
            }

            await unitOfWork.SaveChangesAsync();
        }

        private async Task<CalendarService?> CreateCalendarServiceAsync(string userId, CancellationToken cancellationToken)
        {
            var tokenEntity = await Db.Set<GoogleCalendarUserToken>()
                .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

            if (tokenEntity == null)
                return null;

            TokenResponse tokenResponse;
            try
            {
                tokenResponse = new TokenResponse
                {
                    AccessToken = tokenProtector.Unprotect(tokenEntity.AccessTokenProtected),
                    RefreshToken = tokenProtector.Unprotect(tokenEntity.RefreshTokenProtected),
                    Scope = tokenEntity.Scope,
                    IssuedUtc = DateTime.UtcNow,
                    ExpiresInSeconds = Math.Max(1, (int)(tokenEntity.AccessTokenExpiresAtUtc - DateTime.UtcNow).TotalSeconds)
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decrypt Google tokens for UserId={UserId}", userId);
                return null;
            }

            // Refresh if expired (or within 60 seconds of expiry)
            if (tokenEntity.AccessTokenExpiresAtUtc <= DateTime.UtcNow.AddSeconds(60))
            {
                try
                {
                    var flow = CreateFlow();
                    var refreshed = await flow.RefreshTokenAsync(userId, tokenResponse.RefreshToken, cancellationToken);
                    await UpsertTokenAsync(userId, refreshed, cancellationToken);

                    tokenResponse.AccessToken = refreshed.AccessToken;
                    tokenResponse.ExpiresInSeconds = refreshed.ExpiresInSeconds;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Google token refresh failed for UserId={UserId}", userId);
                    return null;
                }
            }

            var credential = new UserCredential(CreateFlow(), userId, tokenResponse);

            return new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RegMan"
            });
        }

        private static string BuildDescription(OfficeHourBooking booking)
        {
            var sb = new StringBuilder();

            // Course isn't modeled on OfficeHour currently; keep description useful without inventing data.
            sb.AppendLine($"Booking Id: {booking.BookingId}");
            sb.AppendLine($"Instructor: {booking.OfficeHour.Instructor.User.FullName} ({booking.OfficeHour.Instructor.User.Email})");
            sb.AppendLine($"Student: {booking.Student.User.FullName} ({booking.Student.User.Email})");

            if (!string.IsNullOrWhiteSpace(booking.Purpose))
                sb.AppendLine($"Purpose: {booking.Purpose}");

            if (!string.IsNullOrWhiteSpace(booking.StudentNotes))
                sb.AppendLine($"Student Notes: {booking.StudentNotes}");

            if (!string.IsNullOrWhiteSpace(booking.OfficeHour.Notes))
                sb.AppendLine($"Office Hour Notes: {booking.OfficeHour.Notes}");

            return sb.ToString().Trim();
        }
    }
}
