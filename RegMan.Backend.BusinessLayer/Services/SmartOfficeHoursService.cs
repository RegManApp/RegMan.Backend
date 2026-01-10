using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.SmartOfficeHours;
using RegMan.Backend.BusinessLayer.Exceptions;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class SmartOfficeHoursService : ISmartOfficeHoursService
    {
        private const string AuditEntityName = "OfficeHourQueueEntry";

        private readonly AppDbContext _db;
        private readonly byte[] _qrSigningKey;
        private readonly ISmartOfficeHoursRealtimePublisher _publisher;
        private readonly IAuditLogService _auditLog;
        private readonly ILogger<SmartOfficeHoursService> _logger;
        private readonly SmartOfficeHoursOptions _options;

        public SmartOfficeHoursService(
            AppDbContext db,
            IConfiguration configuration,
            ISmartOfficeHoursRealtimePublisher publisher,
            IAuditLogService auditLog,
            ILogger<SmartOfficeHoursService> logger,
            IOptions<SmartOfficeHoursOptions>? options = null)
        {
            _db = db;
            var jwtKey = configuration["Jwt:Key"]; // supports env var Jwt__Key
            if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
                throw new InvalidOperationException("JWT signing key is missing/weak; required for Smart Office Hours QR signing");

            _qrSigningKey = Encoding.UTF8.GetBytes(jwtKey);
            _publisher = publisher;
            _auditLog = auditLog;
            _logger = logger;
            _options = options?.Value ?? SmartOfficeHoursOptions.Default;
        }

        private sealed record QrPayload(int QueueEntryId, Guid Nonce, long ExpUnixSeconds);

        private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
        private static DateTime FromUnixSeconds(long v) => DateTimeOffset.FromUnixTimeSeconds(v).UtcDateTime;

        private static string Base64UrlEncode(byte[] bytes)
        {
            var s = Convert.ToBase64String(bytes);
            s = s.Replace('+', '-').Replace('/', '_');
            return s.TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        private string SignPayload(QrPayload payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(json));

            using var hmac = new HMACSHA256(_qrSigningKey);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
            var sigB64 = Base64UrlEncode(sig);

            return $"{payloadB64}.{sigB64}";
        }

        private QrPayload VerifyAndParsePayload(string token)
        {
            try
            {
                var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    throw new BadRequestException("Invalid or expired QR token");

                var payloadB64 = parts[0];
                var sigB64 = parts[1];

                using var hmac = new HMACSHA256(_qrSigningKey);
                var expectedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadB64));
                var providedSig = Base64UrlDecode(sigB64);

                if (providedSig.Length != expectedSig.Length || !CryptographicOperations.FixedTimeEquals(providedSig, expectedSig))
                    throw new BadRequestException("Invalid or expired QR token");

                var jsonBytes = Base64UrlDecode(payloadB64);
                var json = Encoding.UTF8.GetString(jsonBytes);
                var payload = JsonSerializer.Deserialize<QrPayload>(json);
                return payload ?? throw new BadRequestException("Invalid token payload");
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch
            {
                throw new BadRequestException("Invalid or expired QR token");
            }
        }

        private async Task<string> GetUserEmailOrFallbackAsync(string userId)
        {
            var email = await _db.Set<BaseUser>()
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(email) ? "unknown@user.com" : email;
        }

        private async Task TryAuditAsync(string actorUserId, string action, string entityId)
        {
            try
            {
                var email = await GetUserEmailOrFallbackAsync(actorUserId);
                await _auditLog.LogAsync(actorUserId, email, action, AuditEntityName, entityId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit logging failed. ActorUserId={ActorUserId} Action={Action} EntityId={EntityId}", actorUserId, action, entityId);
            }
        }

        private static bool IsProvider(BaseUser user) => !string.Equals(user.Role, "Student", StringComparison.OrdinalIgnoreCase);

        private async Task<OfficeHour> GetOfficeHourOrThrowAsync(int officeHourId)
        {
            var officeHour = await _db.OfficeHours
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OfficeHourId == officeHourId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            return officeHour;
        }

        private async Task<(OfficeHourSession session, OfficeHour officeHour)> GetOrCreateSessionAsync(int officeHourId)
        {
            var officeHour = await _db.OfficeHours
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OfficeHourId == officeHourId);

            if (officeHour == null)
                throw new NotFoundException("Office hour not found");

            var session = await _db.OfficeHourSessions
                .FirstOrDefaultAsync(s => s.OfficeHourId == officeHourId);

            if (session != null)
                return (session, officeHour);

            session = new OfficeHourSession
            {
                OfficeHourId = officeHourId,
                ProviderUserId = officeHour.OwnerUserId,
                Status = OfficeHourSessionStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.OfficeHourSessions.Add(session);
            await _db.SaveChangesAsync();

            return (session, officeHour);
        }

        private static void Transition(OfficeHourQueueEntry entry, OfficeHourQueueEntryStatus to, string actorUserId, DateTime nowUtc)
        {
            entry.Status = to;
            entry.LastStateChangedByUserId = actorUserId;
            entry.LastStateChangedAtUtc = nowUtc;

            if (to == OfficeHourQueueEntryStatus.Waiting)
            {
                entry.IsActive = true;
            }
            else if (to == OfficeHourQueueEntryStatus.Ready)
            {
                entry.IsActive = true;
                entry.ReadyAtUtc = nowUtc;
            }
            else if (to == OfficeHourQueueEntryStatus.InProgress)
            {
                entry.IsActive = true;
                entry.InProgressAtUtc = nowUtc;
            }
            else if (to == OfficeHourQueueEntryStatus.Done)
            {
                entry.IsActive = false;
                entry.DoneAtUtc = nowUtc;
            }
            else if (to == OfficeHourQueueEntryStatus.NoShow)
            {
                entry.IsActive = false;
                entry.NoShowAtUtc = nowUtc;
            }
        }

        private async Task<(string token, DateTime expiresAtUtc)> IssueOrRotateQrAsync(OfficeHourQueueEntry entry, DateTime nowUtc, CancellationToken ct = default)
        {
            if (entry.Status != OfficeHourQueueEntryStatus.Ready)
                throw new BadRequestException("QR can only be issued when status is Ready");

            var expiresAtUtc = nowUtc.AddSeconds(_options.QrTokenTtlSeconds);
            var nonce = Guid.NewGuid();

            var tokenEntity = await _db.OfficeHourQrTokens
                .FirstOrDefaultAsync(t => t.QueueEntryId == entry.QueueEntryId, ct);

            if (tokenEntity == null)
            {
                tokenEntity = new OfficeHourQrToken
                {
                    QueueEntryId = entry.QueueEntryId
                };
                _db.OfficeHourQrTokens.Add(tokenEntity);
            }

            tokenEntity.CurrentNonce = nonce;
            tokenEntity.IssuedAtUtc = nowUtc;
            tokenEntity.ExpiresAtUtc = expiresAtUtc;
            tokenEntity.UsedAtUtc = null;
            tokenEntity.UsedByUserId = null;

            var payload = new QrPayload(entry.QueueEntryId, nonce, ToUnixSeconds(expiresAtUtc));
            return (SignPayload(payload), expiresAtUtc);
        }

        private async Task<(int sessionId, OfficeHourQueueEntry? current, List<OfficeHourQueueEntry> queue, string? currentQr, DateTime? currentQrExpires)> BuildProviderStateAsync(int officeHourId, CancellationToken ct = default)
        {
            var (session, _) = await GetOrCreateSessionAsync(officeHourId);

            var entries = await _db.OfficeHourQueueEntries
                .Include(e => e.StudentUser)
                .Include(e => e.QrToken)
                .Where(e => e.SessionId == session.SessionId)
                .OrderBy(e => e.EnqueuedAtUtc)
                .ToListAsync(ct);

            var current = entries
                .FirstOrDefault(e => e.Status == OfficeHourQueueEntryStatus.InProgress)
                ?? entries.FirstOrDefault(e => e.Status == OfficeHourQueueEntryStatus.Ready);

            string? currentQr = null;
            DateTime? currentQrExpires = null;

            if (current != null && current.Status == OfficeHourQueueEntryStatus.Ready && current.QrToken?.CurrentNonce != null && current.QrToken.ExpiresAtUtc != null)
            {
                var payload = new QrPayload(current.QueueEntryId, current.QrToken.CurrentNonce.Value, ToUnixSeconds(current.QrToken.ExpiresAtUtc.Value));
                currentQr = SignPayload(payload);
                currentQrExpires = current.QrToken.ExpiresAtUtc;
            }

            var activeQueue = entries
                .Where(e => e.Status == OfficeHourQueueEntryStatus.Waiting || e.Status == OfficeHourQueueEntryStatus.Ready || e.Status == OfficeHourQueueEntryStatus.InProgress)
                .OrderBy(e => e.EnqueuedAtUtc)
                .ToList();

            return (session.SessionId, current, activeQueue, currentQr, currentQrExpires);
        }

        private SmartOfficeHoursQueueEntryDto MapQueueEntry(OfficeHourQueueEntry e, int? position)
        {
            int? eta = position.HasValue ? Math.Max(0, (position.Value - 1) * _options.EstimatedMinutesPerStudent) : null;

            return new SmartOfficeHoursQueueEntryDto
            {
                QueueEntryId = e.QueueEntryId,
                StudentUserId = e.StudentUserId,
                StudentFullName = e.StudentUser?.FullName,
                Status = e.Status.ToString(),
                EnqueuedAtUtc = e.EnqueuedAtUtc,
                ReadyAtUtc = e.ReadyAtUtc,
                InProgressAtUtc = e.InProgressAtUtc,
                ReadyExpiresAtUtc = e.ReadyExpiresAtUtc,
                Position = position,
                EstimatedWaitMinutes = eta
            };
        }

        private async Task PublishViewsAsync(int officeHourId, string? affectedStudentUserId = null, CancellationToken ct = default)
        {
            var providerView = await GetProviderViewInternalAsync(officeHourId, ct);
            await _publisher.PublishProviderViewAsync(officeHourId, providerView);

            if (!string.IsNullOrWhiteSpace(affectedStudentUserId))
            {
                var studentView = await GetStudentViewInternalAsync(affectedStudentUserId, officeHourId, ct);
                await _publisher.PublishStudentViewAsync(affectedStudentUserId, officeHourId, studentView);
            }
        }

        private async Task<SmartOfficeHoursProviderViewDto> GetProviderViewInternalAsync(int officeHourId, CancellationToken ct)
        {
            var (session, officeHour) = await GetOrCreateSessionAsync(officeHourId);

            var (sessionId, current, queue, currentQr, currentQrExpires) = await BuildProviderStateAsync(officeHourId, ct);

            var queueDtos = queue
                .Select((e, idx) => MapQueueEntry(e, idx + 1))
                .ToList();

            SmartOfficeHoursQueueEntryDto? currentDto = null;
            if (current != null)
            {
                var pos = queue.FindIndex(x => x.QueueEntryId == current.QueueEntryId);
                currentDto = MapQueueEntry(current, pos >= 0 ? pos + 1 : null);
            }

            return new SmartOfficeHoursProviderViewDto
            {
                OfficeHourId = officeHourId,
                SessionId = sessionId,
                ProviderUserId = officeHour.OwnerUserId,
                SessionStatus = session.Status.ToString(),
                ServerTimeUtc = DateTime.UtcNow,
                Queue = queueDtos,
                CurrentReadyOrInProgress = currentDto,
                CurrentQrToken = currentQr,
                CurrentQrExpiresAtUtc = currentQrExpires
            };
        }

        private async Task<SmartOfficeHoursStudentViewDto> GetStudentViewInternalAsync(string userId, int officeHourId, CancellationToken ct)
        {
            var (session, _) = await GetOrCreateSessionAsync(officeHourId);

            var entries = await _db.OfficeHourQueueEntries
                .AsNoTracking()
                .Where(e => e.SessionId == session.SessionId && e.IsActive)
                .OrderBy(e => e.EnqueuedAtUtc)
                .ToListAsync(ct);

            var mine = entries.FirstOrDefault(e => e.StudentUserId == userId);
            var position = mine != null ? entries.FindIndex(e => e.QueueEntryId == mine.QueueEntryId) + 1 : (int?)null;
            int? eta = position.HasValue ? Math.Max(0, (position.Value - 1) * _options.EstimatedMinutesPerStudent) : null;

            return new SmartOfficeHoursStudentViewDto
            {
                OfficeHourId = officeHourId,
                SessionId = session.SessionId,
                ServerTimeUtc = DateTime.UtcNow,
                QueueEntryId = mine?.QueueEntryId,
                Status = mine?.Status.ToString(),
                Position = position,
                EstimatedWaitMinutes = eta,
                ReadyExpiresAtUtc = mine?.ReadyExpiresAtUtc
            };
        }

        public async Task<SmartOfficeHoursStudentViewDto> JoinQueueAsync(string studentUserId, int officeHourId, SmartOfficeHoursJoinQueueRequestDto request)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentUserId)
                ?? throw new NotFoundException("User not found");

            if (!string.Equals(user.Role, "Student", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Only students can join the queue");

            var (session, _) = await GetOrCreateSessionAsync(officeHourId);

            var existing = await _db.OfficeHourQueueEntries
                .FirstOrDefaultAsync(e => e.SessionId == session.SessionId && e.StudentUserId == studentUserId && e.IsActive);

            if (existing == null)
            {
                existing = new OfficeHourQueueEntry
                {
                    SessionId = session.SessionId,
                    StudentUserId = studentUserId,
                    Purpose = request.Purpose,
                    Status = OfficeHourQueueEntryStatus.Waiting,
                    IsActive = true,
                    EnqueuedAtUtc = DateTime.UtcNow,
                    LastStateChangedAtUtc = DateTime.UtcNow,
                    LastStateChangedByUserId = studentUserId
                };
                _db.OfficeHourQueueEntries.Add(existing);
                await _db.SaveChangesAsync();

                await TryAuditAsync(studentUserId, "QUEUE_JOIN", existing.QueueEntryId.ToString());
            }

            var view = await GetStudentViewInternalAsync(studentUserId, officeHourId, CancellationToken.None);
            await PublishViewsAsync(officeHourId, studentUserId);
            return view;
        }

        public async Task<SmartOfficeHoursStudentViewDto> GetMyStatusAsync(string userId, int officeHourId)
        {
            return await GetStudentViewInternalAsync(userId, officeHourId, CancellationToken.None);
        }

        public async Task<SmartOfficeHoursProviderViewDto> GetProviderViewAsync(string providerUserId, int officeHourId)
        {
            await EnsureProviderAccessAsync(providerUserId, officeHourId);
            return await GetProviderViewInternalAsync(officeHourId, CancellationToken.None);
        }

        private async Task EnsureProviderAccessAsync(string providerUserId, int officeHourId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == providerUserId)
                ?? throw new NotFoundException("User not found");

            if (!IsProvider(user))
                throw new ForbiddenException("Students cannot provide office hours");

            var officeHour = await GetOfficeHourOrThrowAsync(officeHourId);
            if (!string.Equals(officeHour.OwnerUserId, providerUserId, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("You do not own this office hour");
        }

        public async Task<SmartOfficeHoursProviderViewDto> CallNextAsync(string providerUserId, int officeHourId)
        {
            await EnsureProviderAccessAsync(providerUserId, officeHourId);

            var (session, _) = await GetOrCreateSessionAsync(officeHourId);
            var nowUtc = DateTime.UtcNow;

            using var tx = await _db.Database.BeginTransactionAsync();

            var hasActive = await _db.OfficeHourQueueEntries
                .AnyAsync(e => e.SessionId == session.SessionId && (e.Status == OfficeHourQueueEntryStatus.Ready || e.Status == OfficeHourQueueEntryStatus.InProgress));

            if (hasActive)
                throw new BadRequestException("A student is already Ready or InProgress");

            var next = await _db.OfficeHourQueueEntries
                .Where(e => e.SessionId == session.SessionId && e.Status == OfficeHourQueueEntryStatus.Waiting && e.IsActive)
                .OrderBy(e => e.EnqueuedAtUtc)
                .FirstOrDefaultAsync();

            if (next == null)
                throw new NotFoundException("No waiting students");

            Transition(next, OfficeHourQueueEntryStatus.Ready, providerUserId, nowUtc);
            next.ReadyExpiresAtUtc = nowUtc.AddSeconds(_options.ReadyNoShowTimeoutSeconds);

            var (token, expiresAtUtc) = await IssueOrRotateQrAsync(next, nowUtc);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await TryAuditAsync(providerUserId, "QUEUE_WAITING_TO_READY", next.QueueEntryId.ToString());

            var view = await GetProviderViewInternalAsync(officeHourId, CancellationToken.None);
            // Ensure provider view contains up-to-date current QR
            view.CurrentQrToken = token;
            view.CurrentQrExpiresAtUtc = expiresAtUtc;

            await PublishViewsAsync(officeHourId, next.StudentUserId);
            return view;
        }

        public async Task<SmartOfficeHoursStudentViewDto> ScanQrAsync(string studentUserId, SmartOfficeHoursScanRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                throw new BadRequestException("Token is required");

            var payload = VerifyAndParsePayload(request.Token);

            var nowUtc = DateTime.UtcNow;
            var expUtc = FromUnixSeconds(payload.ExpUnixSeconds);
            if (expUtc < nowUtc)
                throw new BadRequestException("Invalid or expired QR token");

            using var tx = await _db.Database.BeginTransactionAsync();

            var entry = await _db.OfficeHourQueueEntries
                .Include(e => e.Session)
                .Include(e => e.QrToken)
                .FirstOrDefaultAsync(e => e.QueueEntryId == payload.QueueEntryId);

            if (entry == null)
                throw new NotFoundException("Queue entry not found");

            if (!string.Equals(entry.StudentUserId, studentUserId, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("This QR is not for you");

            if (entry.Status != OfficeHourQueueEntryStatus.Ready)
                throw new BadRequestException("Entry is not Ready");

            if (entry.QrToken == null || entry.QrToken.CurrentNonce == null)
                throw new BadRequestException("QR is not active");

            if (entry.QrToken.UsedAtUtc != null)
                throw new BadRequestException("QR already used");

            if (entry.QrToken.ExpiresAtUtc == null || entry.QrToken.ExpiresAtUtc < nowUtc)
                throw new BadRequestException("Invalid or expired QR token");

            if (entry.QrToken.CurrentNonce.Value != payload.Nonce)
                throw new BadRequestException("Invalid or expired QR token");

            // One-time use
            entry.QrToken.UsedAtUtc = nowUtc;
            entry.QrToken.UsedByUserId = studentUserId;
            entry.QrToken.CurrentNonce = null;

            Transition(entry, OfficeHourQueueEntryStatus.InProgress, studentUserId, nowUtc);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await TryAuditAsync(studentUserId, "QUEUE_READY_TO_IN_PROGRESS", entry.QueueEntryId.ToString());

            var officeHourId = (await _db.OfficeHourSessions.AsNoTracking().FirstAsync(s => s.SessionId == entry.SessionId)).OfficeHourId;
            await PublishViewsAsync(officeHourId, studentUserId);

            return await GetStudentViewInternalAsync(studentUserId, officeHourId, CancellationToken.None);
        }

        public async Task<SmartOfficeHoursProviderViewDto> CompleteCurrentAsync(string providerUserId, int officeHourId)
        {
            await EnsureProviderAccessAsync(providerUserId, officeHourId);
            var (session, _) = await GetOrCreateSessionAsync(officeHourId);
            var nowUtc = DateTime.UtcNow;

            var current = await _db.OfficeHourQueueEntries
                .Where(e => e.SessionId == session.SessionId && e.Status == OfficeHourQueueEntryStatus.InProgress && e.IsActive)
                .OrderBy(e => e.InProgressAtUtc)
                .FirstOrDefaultAsync();

            if (current == null)
                throw new NotFoundException("No in-progress student");

            Transition(current, OfficeHourQueueEntryStatus.Done, providerUserId, nowUtc);
            await _db.SaveChangesAsync();

            await TryAuditAsync(providerUserId, "QUEUE_IN_PROGRESS_TO_DONE", current.QueueEntryId.ToString());

            var view = await GetProviderViewInternalAsync(officeHourId, CancellationToken.None);
            await PublishViewsAsync(officeHourId, current.StudentUserId);
            return view;
        }

        public async Task<SmartOfficeHoursProviderViewDto> MarkNoShowCurrentAsync(string providerUserId, int officeHourId)
        {
            await EnsureProviderAccessAsync(providerUserId, officeHourId);
            var (session, _) = await GetOrCreateSessionAsync(officeHourId);
            var nowUtc = DateTime.UtcNow;

            var current = await _db.OfficeHourQueueEntries
                .Where(e => e.SessionId == session.SessionId && e.Status == OfficeHourQueueEntryStatus.Ready && e.IsActive)
                .OrderBy(e => e.ReadyAtUtc)
                .FirstOrDefaultAsync();

            if (current == null)
                throw new NotFoundException("No ready student");

            Transition(current, OfficeHourQueueEntryStatus.NoShow, providerUserId, nowUtc);
            await _db.SaveChangesAsync();

            await TryAuditAsync(providerUserId, "QUEUE_READY_TO_NO_SHOW", current.QueueEntryId.ToString());

            var view = await GetProviderViewInternalAsync(officeHourId, CancellationToken.None);
            await PublishViewsAsync(officeHourId, current.StudentUserId);
            return view;
        }

        // Background jobs
        public async Task<int> RotateReadyQrTokensAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddSeconds(-Math.Max(1, _options.QrRotateSeconds));

            var readyEntries = await _db.OfficeHourQueueEntries
                .Include(e => e.QrToken)
                .Include(e => e.Session)
                .Where(e => e.Status == OfficeHourQueueEntryStatus.Ready && e.IsActive)
                .ToListAsync(cancellationToken);

            var rotatedOfficeHours = new HashSet<int>();
            var rotatedCount = 0;

            foreach (var entry in readyEntries)
            {
                if (entry.QrToken?.IssuedAtUtc != null && entry.QrToken.IssuedAtUtc > cutoff)
                    continue;

                // Ensure token exists + rotate
                await IssueOrRotateQrAsync(entry, nowUtc, cancellationToken);
                rotatedCount += 1;

                var officeHourId = await _db.OfficeHourSessions
                    .AsNoTracking()
                    .Where(s => s.SessionId == entry.SessionId)
                    .Select(s => s.OfficeHourId)
                    .FirstAsync(cancellationToken);

                rotatedOfficeHours.Add(officeHourId);
            }

            if (rotatedCount > 0)
                await _db.SaveChangesAsync(cancellationToken);

            foreach (var officeHourId in rotatedOfficeHours)
            {
                try
                {
                    var providerView = await GetProviderViewInternalAsync(officeHourId, cancellationToken);
                    await _publisher.PublishProviderViewAsync(officeHourId, providerView);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish rotated QR update. OfficeHourId={OfficeHourId}", officeHourId);
                }
            }

            return rotatedCount;
        }

        public async Task<int> AutoNoShowExpiredReadyEntriesAsync(CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;

            var expired = await _db.OfficeHourQueueEntries
                .Include(e => e.Session)
                .Where(e => e.Status == OfficeHourQueueEntryStatus.Ready && e.IsActive && e.ReadyExpiresAtUtc != null && e.ReadyExpiresAtUtc < nowUtc)
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
                return 0;

            var touchedOfficeHours = new HashSet<int>();

            foreach (var entry in expired)
            {
                Transition(entry, OfficeHourQueueEntryStatus.NoShow, "system", nowUtc);

                try
                {
                    await _auditLog.LogAsync("system", "system@regman", "QUEUE_READY_TO_NO_SHOW_TIMEOUT", AuditEntityName, entry.QueueEntryId.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Audit logging failed for auto no-show. QueueEntryId={QueueEntryId}", entry.QueueEntryId);
                }

                var officeHourId = await _db.OfficeHourSessions
                    .AsNoTracking()
                    .Where(s => s.SessionId == entry.SessionId)
                    .Select(s => s.OfficeHourId)
                    .FirstAsync(cancellationToken);

                touchedOfficeHours.Add(officeHourId);

                try
                {
                    var studentView = await GetStudentViewInternalAsync(entry.StudentUserId, officeHourId, cancellationToken);
                    await _publisher.PublishStudentViewAsync(entry.StudentUserId, officeHourId, studentView);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish student auto no-show update. QueueEntryId={QueueEntryId}", entry.QueueEntryId);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            foreach (var officeHourId in touchedOfficeHours)
            {
                try
                {
                    var providerView = await GetProviderViewInternalAsync(officeHourId, cancellationToken);
                    await _publisher.PublishProviderViewAsync(officeHourId, providerView);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish provider auto no-show update. OfficeHourId={OfficeHourId}", officeHourId);
                }
            }

            return expired.Count;
        }
    }
}
