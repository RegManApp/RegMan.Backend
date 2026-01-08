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
using System.Threading;

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
        private readonly string clientId = string.Empty;
        private readonly string clientSecret = string.Empty;
        private readonly string redirectUri = string.Empty;
        private readonly bool isConfigured;
        private readonly string? configurationError;

        private static int hasLoggedConfiguration;

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
            var (resolvedClientId, clientIdSource) = ResolveSecret(
                configuration,
                settingName: "ClientId",
                envKeys: new[] { "GOOGLE_CLIENT_ID", "Google__ClientId" },
                configKeys: new[] { "Google:ClientId" }
            );

            var (resolvedClientSecret, clientSecretSource) = ResolveSecret(
                configuration,
                settingName: "ClientSecret",
                envKeys: new[] { "GOOGLE_CLIENT_SECRET", "Google__ClientSecret" },
                configKeys: new[] { "Google:ClientSecret" }
            );

            var (resolvedRedirectUri, redirectUriSource) = ResolveSecret(
                configuration,
                settingName: "RedirectUri",
                envKeys: new[] { "GOOGLE_REDIRECT_URI", "Google__RedirectUri" },
                configKeys: new[] { "Google:RedirectUri" }
            );

            clientId = resolvedClientId;
            clientSecret = resolvedClientSecret;
            redirectUri = resolvedRedirectUri;

            var missing = new List<string>(capacity: 3);
            if (string.IsNullOrWhiteSpace(clientId))
                missing.Add("GOOGLE_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(clientSecret))
                missing.Add("GOOGLE_CLIENT_SECRET");
            if (string.IsNullOrWhiteSpace(redirectUri))
                missing.Add("GOOGLE_REDIRECT_URI");

            isConfigured = missing.Count == 0;
            configurationError = isConfigured
                ? null
                : "Google Calendar OAuth misconfigured: " + string.Join(", ", missing) + " is missing. " +
                  "Supported keys: GOOGLE_CLIENT_ID/GOOGLE_CLIENT_SECRET/GOOGLE_REDIRECT_URI (env) or Google:ClientId/Google:ClientSecret/Google:RedirectUri (config / appsettings / web.config).";

            // Log once per process to help diagnose hosting config issues (MonsterASP).
            if (Interlocked.Exchange(ref hasLoggedConfiguration, 1) == 0)
            {
                logger.LogInformation(
                    "Google OAuth config snapshot: ClientId={ClientIdPresent} (Source={ClientIdSource}, Len={ClientIdLen}), ClientSecret={ClientSecretPresent} (Source={ClientSecretSource}, Len={ClientSecretLen}), RedirectUri={RedirectUriPresent} (Source={RedirectUriSource}, Value={RedirectUriValue})",
                    !string.IsNullOrWhiteSpace(clientId),
                    clientIdSource,
                    clientId?.Length ?? 0,
                    !string.IsNullOrWhiteSpace(clientSecret),
                    clientSecretSource,
                    clientSecret?.Length ?? 0,
                    !string.IsNullOrWhiteSpace(redirectUri),
                    redirectUriSource,
                    string.IsNullOrWhiteSpace(redirectUri) ? "<missing>" : redirectUri
                );

                if (!isConfigured)
                {
                    logger.LogError(configurationError);
                }
            }
        }

        public string CreateAuthorizationUrl(string userId, string? returnUrl)
        {
            if (!isConfigured)
                throw new InvalidOperationException(configurationError ?? "Google OAuth is not configured.");

            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException(
                    "Google Calendar OAuth misconfigured: GOOGLE_REDIRECT_URI is not a valid absolute URL. " +
                    "Make sure it exactly matches the Google Cloud Console redirect URI."
                );
            }

            string state;
            try
            {
                state = ProtectState(new GoogleCalendarOAuthState(
                    UserId: userId,
                    IssuedAtUtc: DateTime.UtcNow,
                    ReturnUrl: string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl
                ));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to generate OAuth state. This is usually a server crypto (Data Protection) configuration issue. Try again later.",
                    ex
                );
            }

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

        private static (string Value, string Source) ResolveSecret(
            IConfiguration configuration,
            string settingName,
            IReadOnlyList<string> envKeys,
            IReadOnlyList<string> configKeys)
        {
            // 1) Environment.GetEnvironmentVariable (direct OS env vars)
            foreach (var key in envKeys)
            {
                var raw = Environment.GetEnvironmentVariable(key);
                var trimmed = raw?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    return (trimmed, $"Environment:{key}");
            }

            // 2) IConfiguration direct lookup (covers appsettings, web.config providers, and env var providers)
            foreach (var key in envKeys)
            {
                var raw = configuration[key];
                var trimmed = raw?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    return (trimmed, $"Configuration:{key}");
            }

            // 3) IConfiguration hierarchical keys (e.g., Google:ClientId; also supports env var mapping Google__ClientId)
            foreach (var key in configKeys)
            {
                var raw = configuration[key];
                var trimmed = raw?.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    return (trimmed, $"Configuration:{key}");
            }

            return (string.Empty, $"Missing:{settingName}");
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

        public async Task DisconnectAsync(string userId, CancellationToken cancellationToken)
        {
            if (!isConfigured)
                return;

            try
            {
                var token = await Db.Set<GoogleCalendarUserToken>()
                    .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
                if (token != null)
                {
                    // Best-effort: revoke the token at Google as part of disconnect.
                    // Google docs recommend programmatic revocation for unsubscribe/removal flows.
                    // https://developers.google.com/identity/protocols/oauth2/web-server#tokenrevoke
                    try
                    {
                        var refreshToken = tokenProtector.Unprotect(token.RefreshTokenProtected);
                        if (!string.IsNullOrWhiteSpace(refreshToken))
                        {
                            var flow = CreateFlow();
                            await flow.RevokeTokenAsync(userId, refreshToken, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Never block disconnect on revocation failures.
                        logger.LogWarning(ex, "Google token revocation failed for UserId={UserId}", userId);
                    }

                    Db.Set<GoogleCalendarUserToken>().Remove(token);
                }

                var links = await Db.Set<GoogleCalendarEventLink>()
                    .Where(l => l.UserId == userId)
                    .ToListAsync(cancellationToken);
                if (links.Count > 0)
                {
                    Db.Set<GoogleCalendarEventLink>().RemoveRange(links);
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar disconnect failed for UserId={UserId}", userId);
            }
        }

        public async Task TryUpsertOfficeHourBookingEventAsync(OfficeHourBooking booking, CancellationToken cancellationToken)
        {
            if (!isConfigured)
                return;

            // Prefer provider (the one offering the slot) as organizer, fallback to student.
            var providerUserId = booking.OfficeHour.OwnerUserId;
            var bookerUserId = booking.BookerUserId;

            var organizerUserId = await HasGoogleTokenAsync(providerUserId, cancellationToken)
                ? providerUserId
                : (await HasGoogleTokenAsync(bookerUserId, cancellationToken) ? bookerUserId : null);

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

                var providerName = booking.OfficeHour.Instructor?.User.FullName
                                   ?? booking.OfficeHour.OwnerUser?.FullName
                                   ?? "Provider";

                var providerEmail = booking.OfficeHour.Instructor?.User.Email
                                    ?? booking.OfficeHour.OwnerUser?.Email;

                var studentEmail = booking.Student?.User?.Email;
                if (string.IsNullOrWhiteSpace(studentEmail))
                {
                    logger.LogError(
                        "Google Calendar upsert skipped for BookingId={BookingId}: missing student email.",
                        booking.BookingId);
                    return;
                }

                var attendees = new List<EventAttendee>
                {
                    new() { Email = studentEmail }
                };

                if (!string.IsNullOrWhiteSpace(providerEmail))
                {
                    attendees.Add(new EventAttendee { Email = providerEmail });
                }

                var newEvent = new Event
                {
                    Summary = $"Office Hour with {providerName}",
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
                    Attendees = attendees
                };

                var link = await Db.Set<GoogleCalendarEventLink>()
                    .FirstOrDefaultAsync(l => l.UserId == organizerUserId
                                              && l.SourceEntityType == "OfficeHourBooking"
                                              && l.SourceEntityId == booking.BookingId,
                        cancellationToken);

                if (link == null)
                {
                    var insert = calendarService.Events.Insert(newEvent, "primary");
                    insert.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;
                    var created = await insert.ExecuteAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(created?.Id))
                    {
                        Db.Set<GoogleCalendarEventLink>().Add(new GoogleCalendarEventLink
                        {
                            UserId = organizerUserId,
                            SourceEntityType = "OfficeHourBooking",
                            SourceEntityId = booking.BookingId,
                            GoogleCalendarId = "primary",
                            GoogleEventId = created.Id,
                            LastSyncedAtUtc = DateTime.UtcNow,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                        await unitOfWork.SaveChangesAsync();
                    }
                }
                else
                {
                    var update = calendarService.Events.Update(newEvent, link.GoogleCalendarId, link.GoogleEventId);
                    update.SendUpdates = EventsResource.UpdateRequest.SendUpdatesEnum.All;
                    await update.ExecuteAsync(cancellationToken);

                    link.LastSyncedAtUtc = DateTime.UtcNow;
                    await unitOfWork.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar upsert failed for BookingId={BookingId}", booking.BookingId);
            }
        }

        public async Task TryDeleteOfficeHourBookingEventAsync(int bookingId, CancellationToken cancellationToken)
        {
            if (!isConfigured)
                return;

            try
            {
                var link = await Db.Set<GoogleCalendarEventLink>()
                    .FirstOrDefaultAsync(l => l.SourceEntityType == "OfficeHourBooking" && l.SourceEntityId == bookingId, cancellationToken);

                if (link == null)
                    return;

                var calendarService = await CreateCalendarServiceAsync(link.UserId, cancellationToken);
                if (calendarService == null)
                    return;

                var delete = calendarService.Events.Delete(link.GoogleCalendarId, link.GoogleEventId);
                delete.SendUpdates = EventsResource.DeleteRequest.SendUpdatesEnum.All;
                await delete.ExecuteAsync(cancellationToken);

                Db.Set<GoogleCalendarEventLink>().Remove(link);
                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Google Calendar delete failed for BookingId={BookingId}", bookingId);
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

            var providerName = booking.OfficeHour.Instructor?.User.FullName
                               ?? booking.OfficeHour.OwnerUser?.FullName
                               ?? "Provider";

            var providerEmail = booking.OfficeHour.Instructor?.User.Email
                                ?? booking.OfficeHour.OwnerUser?.Email
                                ?? "";

            sb.AppendLine($"Provider: {providerName} ({providerEmail})");

            var bookerName = booking.BookerUser?.FullName
                            ?? booking.Student?.User?.FullName
                            ?? "Booker";

            var bookerEmail = booking.BookerUser?.Email
                             ?? booking.Student?.User?.Email
                             ?? "";

            sb.AppendLine($"Booked By: {bookerName} ({bookerEmail})");

            if (!string.IsNullOrWhiteSpace(booking.Purpose))
                sb.AppendLine($"Purpose: {booking.Purpose}");


            if (!string.IsNullOrWhiteSpace(booking.BookerNotes))
                sb.AppendLine($"Booker Notes: {booking.BookerNotes}");

            if (!string.IsNullOrWhiteSpace(booking.OfficeHour.Notes))
                sb.AppendLine($"Office Hour Notes: {booking.OfficeHour.Notes}");

            return sb.ToString().Trim();
        }
    }
}
