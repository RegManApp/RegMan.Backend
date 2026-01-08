using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Common;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Announcements;
using RegMan.Backend.BusinessLayer.Exceptions;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class AnnouncementsService : IAnnouncementsService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly INotificationService notificationService;
        private readonly IChatService chatService;
        private readonly IChatRealtimePublisher chatRealtimePublisher;
        private readonly IAnnouncementRealtimePublisher announcementRealtimePublisher;

        private DbContext Db => unitOfWork.Context;

        public AnnouncementsService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IChatService chatService,
            IChatRealtimePublisher chatRealtimePublisher,
            IAnnouncementRealtimePublisher announcementRealtimePublisher)
        {
            this.unitOfWork = unitOfWork;
            this.notificationService = notificationService;
            this.chatService = chatService;
            this.chatRealtimePublisher = chatRealtimePublisher;
            this.announcementRealtimePublisher = announcementRealtimePublisher;
        }

        public async Task<CreateAnnouncementResultDTO> CreateAnnouncementAsync(string senderUserId, string senderRole, CreateAnnouncementRequestDTO dto)
        {
            if (string.IsNullOrWhiteSpace(senderUserId))
                throw new UnauthorizedException("Unauthorized");

            if (!string.Equals(senderRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(senderRole, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Only Admins and Instructors can send announcements");
            }

            var title = (dto.Title ?? string.Empty).Trim();
            var content = (dto.Content ?? string.Empty).Trim();

            if (title.Length == 0)
                throw new BadRequestException("Title is required");
            if (title.Length > 200)
                throw new BadRequestException("Title is too long");
            if (content.Length == 0)
                throw new BadRequestException("Content is required");

            // Instructor scope enforcement.
            if (string.Equals(senderRole, "Instructor", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.TargetType == AnnouncementTargetType.AllUsers || dto.TargetType == AnnouncementTargetType.Roles)
                    throw new ForbiddenException("Instructors can only send announcements to their own courses/sections");

                var instructorId = await Db.Set<InstructorProfile>()
                    .AsNoTracking()
                    .Where(i => i.UserId == senderUserId)
                    .Select(i => (int?)i.InstructorId)
                    .FirstOrDefaultAsync();

                if (!instructorId.HasValue)
                    throw new NotFoundException("Instructor profile not found");

                if (dto.TargetType == AnnouncementTargetType.Section)
                {
                    if (!dto.SectionId.HasValue)
                        throw new BadRequestException("SectionId is required");

                    var owns = await Db.Set<Section>()
                        .AsNoTracking()
                        .AnyAsync(s => s.SectionId == dto.SectionId.Value && s.InstructorId == instructorId.Value);
                    if (!owns)
                        throw new ForbiddenException("You can only announce to your own sections");
                }

                if (dto.TargetType == AnnouncementTargetType.Course)
                {
                    if (!dto.CourseId.HasValue)
                        throw new BadRequestException("CourseId is required");

                    var owns = await Db.Set<Section>()
                        .AsNoTracking()
                        .AnyAsync(s => s.CourseId == dto.CourseId.Value && s.InstructorId == instructorId.Value);
                    if (!owns)
                        throw new ForbiddenException("You can only announce to your own courses");
                }
            }

            // Determine recipients.
            var recipientUserIds = await ResolveRecipientUserIdsAsync(senderUserId, dto);
            if (recipientUserIds.Count == 0)
                throw new BadRequestException("No recipients matched the selected scope");

            var announcement = new Announcement
            {
                Title = title,
                Content = content,
                TargetType = dto.TargetType,
                TargetRoles = dto.TargetType == AnnouncementTargetType.Roles
                    ? NormalizeRoles(dto.TargetRoles)
                    : null,
                CourseId = dto.TargetType == AnnouncementTargetType.Course ? dto.CourseId : null,
                SectionId = dto.TargetType == AnnouncementTargetType.Section ? dto.SectionId : null,
                CreatedByUserId = senderUserId,
                CreatedByRole = senderRole,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false
            };

            Db.Set<Announcement>().Add(announcement);
            await unitOfWork.SaveChangesAsync();

            var recipientRows = recipientUserIds.Select(uid => new AnnouncementRecipient
            {
                AnnouncementId = announcement.AnnouncementId,
                RecipientUserId = uid,
                DeliveredAt = DateTime.UtcNow,
                ReadAt = null
            }).ToList();

            Db.Set<AnnouncementRecipient>().AddRange(recipientRows);
            await unitOfWork.SaveChangesAsync();

            var senderName = await Db.Set<BaseUser>()
                .AsNoTracking()
                .Where(u => u.Id == senderUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "Unknown";

            // Fan-out delivery: notification + chat message + realtime.
            foreach (var recipientUserId in recipientUserIds)
            {
                // Notification (source of truth persists in DB).
                await notificationService.CreateNotificationAsync(
                    recipientUserId,
                    NotificationType.Announcement,
                    "New Announcement",
                    $"{senderName}: {title}",
                    entityType: "Announcement",
                    entityId: announcement.AnnouncementId);

                // Read-only announcements chat (System -> user direct convo named "Announcements").
                var announcementsConversation = await chatService.GetOrCreateAnnouncementsConversationAsync(recipientUserId);

                // Ensure group join when online.
                await chatRealtimePublisher.PublishConversationCreatedAsync(recipientUserId, announcementsConversation.ConversationId);

                var chatText = $"📣 Announcement\nFrom: {senderName}\nTitle: {title}\n\n{content}";
                var chatMessage = await chatService.SendSystemMessageToConversationAsync(
                    announcementsConversation.ConversationId,
                    chatText,
                    clientMessageId: $"announcement:{announcement.AnnouncementId}");

                await chatRealtimePublisher.PublishSystemMessageCreatedAsync(announcementsConversation.ConversationId, chatMessage);

                await announcementRealtimePublisher.PublishAnnouncementSentAsync(recipientUserId, new
                {
                    announcementId = announcement.AnnouncementId,
                    title = title,
                    createdAt = announcement.CreatedAt,
                });
            }

            return new CreateAnnouncementResultDTO
            {
                AnnouncementId = announcement.AnnouncementId,
                RecipientCount = recipientUserIds.Count
            };
        }

        public async Task<List<MyAnnouncementListItemDTO>> GetMyAnnouncementsAsync(string userId, bool includeArchived = false)
        {
            var query = Db.Set<AnnouncementRecipient>()
                .AsNoTracking()
                .Include(r => r.Announcement)
                    .ThenInclude(a => a.CreatedByUser)
                .Where(r => r.RecipientUserId == userId);

            if (!includeArchived)
                query = query.Where(r => !r.Announcement.IsArchived);

            return await query
                .OrderByDescending(r => r.Announcement.CreatedAt)
                .Select(r => new MyAnnouncementListItemDTO
                {
                    AnnouncementId = r.AnnouncementId,
                    Title = r.Announcement.Title,
                    Content = r.Announcement.Content,
                    TargetType = r.Announcement.TargetType,
                    CourseId = r.Announcement.CourseId,
                    SectionId = r.Announcement.SectionId,
                    CreatedByUserId = r.Announcement.CreatedByUserId,
                    CreatedByName = r.Announcement.CreatedByUser.FullName,
                    CreatedByRole = r.Announcement.CreatedByRole,
                    CreatedAt = r.Announcement.CreatedAt,
                    IsArchived = r.Announcement.IsArchived,
                    ReadAt = r.ReadAt
                })
                .ToListAsync();
        }

        public async Task<int> GetMyUnreadCountAsync(string userId)
        {
            return await Db.Set<AnnouncementRecipient>()
                .AsNoTracking()
                .Include(r => r.Announcement)
                .Where(r => r.RecipientUserId == userId && r.ReadAt == null && !r.Announcement.IsArchived)
                .CountAsync();
        }

        public async Task MarkAnnouncementReadAsync(string userId, int announcementId)
        {
            var row = await Db.Set<AnnouncementRecipient>()
                .Include(r => r.Announcement)
                .FirstOrDefaultAsync(r => r.RecipientUserId == userId && r.AnnouncementId == announcementId);

            if (row == null)
                throw new NotFoundException("Announcement not found");

            if (row.ReadAt.HasValue)
                return;

            row.ReadAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync();

            await announcementRealtimePublisher.PublishAnnouncementReadAsync(userId, new
            {
                announcementId = announcementId,
                readAt = row.ReadAt
            });
        }

        public async Task<AnnouncementScopesDTO> GetMyScopesAsync(string userId, string userRole)
        {
            if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Admin can pick from all sections/courses.
                var sections = await Db.Set<Section>()
                    .AsNoTracking()
                    .Include(s => s.Course)
                    .OrderByDescending(s => s.SectionId)
                    .Take(500)
                    .Select(s => new ScopeSectionDTO
                    {
                        SectionId = s.SectionId,
                        SectionName = s.SectionName,
                        CourseId = s.CourseId,
                        CourseName = s.Course.CourseName,
                        CourseCode = s.Course.CourseCode
                    })
                    .ToListAsync();

                var courses = await Db.Set<Course>()
                    .AsNoTracking()
                    .OrderByDescending(c => c.CourseId)
                    .Take(500)
                    .Select(c => new ScopeCourseDTO
                    {
                        CourseId = c.CourseId,
                        CourseName = c.CourseName,
                        CourseCode = c.CourseCode
                    })
                    .ToListAsync();

                return new AnnouncementScopesDTO { Sections = sections, Courses = courses };
            }

            if (!string.Equals(userRole, "Instructor", StringComparison.OrdinalIgnoreCase))
                return new AnnouncementScopesDTO();

            var instructorId = await Db.Set<InstructorProfile>()
                .AsNoTracking()
                .Where(i => i.UserId == userId)
                .Select(i => (int?)i.InstructorId)
                .FirstOrDefaultAsync();

            if (!instructorId.HasValue)
                throw new NotFoundException("Instructor profile not found");

            var mySections = await Db.Set<Section>()
                .AsNoTracking()
                .Include(s => s.Course)
                .Where(s => s.InstructorId == instructorId.Value)
                .OrderByDescending(s => s.SectionId)
                .Select(s => new ScopeSectionDTO
                {
                    SectionId = s.SectionId,
                    SectionName = s.SectionName,
                    CourseId = s.CourseId,
                    CourseName = s.Course.CourseName,
                    CourseCode = s.Course.CourseCode
                })
                .ToListAsync();

            var myCourses = mySections
                .GroupBy(s => s.CourseId)
                .Select(g => new ScopeCourseDTO
                {
                    CourseId = g.First().CourseId,
                    CourseName = g.First().CourseName,
                    CourseCode = g.First().CourseCode
                })
                .OrderByDescending(c => c.CourseId)
                .ToList();

            return new AnnouncementScopesDTO { Sections = mySections, Courses = myCourses };
        }

        public async Task<List<AdminAnnouncementAuditItemDTO>> GetAuditAsync(DateTime? fromUtc, DateTime? toUtc, string? createdByUserId, bool? isArchived, int? courseId, int? sectionId, string? targetRole)
        {
            var query = Db.Set<Announcement>()
                .AsNoTracking()
                .Include(a => a.CreatedByUser)
                .AsQueryable();

            if (fromUtc.HasValue)
                query = query.Where(a => a.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue)
                query = query.Where(a => a.CreatedAt <= toUtc.Value);
            if (!string.IsNullOrWhiteSpace(createdByUserId))
                query = query.Where(a => a.CreatedByUserId == createdByUserId);
            if (isArchived.HasValue)
                query = query.Where(a => a.IsArchived == isArchived.Value);
            if (courseId.HasValue)
                query = query.Where(a => a.CourseId == courseId.Value);
            if (sectionId.HasValue)
                query = query.Where(a => a.SectionId == sectionId.Value);
            if (!string.IsNullOrWhiteSpace(targetRole))
                query = query.Where(a => a.TargetRoles != null && a.TargetRoles.Contains(targetRole));

            var baseRows = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(500)
                .Select(a => new
                {
                    a.AnnouncementId,
                    a.Title,
                    a.TargetType,
                    a.TargetRoles,
                    a.CourseId,
                    a.SectionId,
                    a.CreatedAt,
                    a.CreatedByUserId,
                    CreatedByName = a.CreatedByUser.FullName,
                    a.CreatedByRole,
                    a.IsArchived,
                    a.ArchivedAt,
                    a.ArchivedByUserId
                })
                .ToListAsync();

            var ids = baseRows.Select(r => r.AnnouncementId).ToList();
            var stats = await Db.Set<AnnouncementRecipient>()
                .AsNoTracking()
                .Where(r => ids.Contains(r.AnnouncementId))
                .GroupBy(r => r.AnnouncementId)
                .Select(g => new
                {
                    AnnouncementId = g.Key,
                    RecipientCount = g.Count(),
                    ReadCount = g.Count(x => x.ReadAt != null)
                })
                .ToListAsync();

            var statsById = stats.ToDictionary(x => x.AnnouncementId, x => x);

            return baseRows.Select(r =>
            {
                statsById.TryGetValue(r.AnnouncementId, out var s);
                return new AdminAnnouncementAuditItemDTO
                {
                    AnnouncementId = r.AnnouncementId,
                    Title = r.Title,
                    TargetType = r.TargetType,
                    TargetRoles = r.TargetRoles,
                    CourseId = r.CourseId,
                    SectionId = r.SectionId,
                    CreatedAt = r.CreatedAt,
                    CreatedByUserId = r.CreatedByUserId,
                    CreatedByName = r.CreatedByName,
                    CreatedByRole = r.CreatedByRole,
                    IsArchived = r.IsArchived,
                    ArchivedAt = r.ArchivedAt,
                    ArchivedByUserId = r.ArchivedByUserId,
                    RecipientCount = s?.RecipientCount ?? 0,
                    ReadCount = s?.ReadCount ?? 0
                };
            }).ToList();
        }

        public async Task ArchiveAnnouncementAsync(string adminUserId, int announcementId)
        {
            var row = await Db.Set<Announcement>().FirstOrDefaultAsync(a => a.AnnouncementId == announcementId);
            if (row == null)
                throw new NotFoundException("Announcement not found");

            if (row.IsArchived)
                return;

            row.IsArchived = true;
            row.ArchivedAt = DateTime.UtcNow;
            row.ArchivedByUserId = adminUserId;
            await unitOfWork.SaveChangesAsync();
        }

        private async Task<List<string>> ResolveRecipientUserIdsAsync(string senderUserId, CreateAnnouncementRequestDTO dto)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            switch (dto.TargetType)
            {
                case AnnouncementTargetType.AllUsers:
                    {
                        var users = await Db.Set<BaseUser>()
                            .AsNoTracking()
                            .Where(u => u.Id != senderUserId && u.Id != SystemUserConstants.SystemUserId)
                            .Select(u => u.Id)
                            .ToListAsync();
                        foreach (var id in users) ids.Add(id);
                        break;
                    }
                case AnnouncementTargetType.Roles:
                    {
                        var roles = (dto.TargetRoles ?? new List<string>())
                            .Select(r => (r ?? string.Empty).Trim())
                            .Where(r => r.Length > 0)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        if (roles.Count == 0)
                            throw new BadRequestException("TargetRoles is required");

                        var users = await Db.Set<BaseUser>()
                            .AsNoTracking()
                            .Where(u => u.Id != senderUserId && u.Id != SystemUserConstants.SystemUserId && roles.Contains(u.Role))
                            .Select(u => u.Id)
                            .ToListAsync();
                        foreach (var id in users) ids.Add(id);
                        break;
                    }
                case AnnouncementTargetType.Section:
                    {
                        if (!dto.SectionId.HasValue)
                            throw new BadRequestException("SectionId is required");

                        var studentUserIds = await Db.Set<Enrollment>()
                            .AsNoTracking()
                            .Include(e => e.Student)
                            .Where(e => e.SectionId == dto.SectionId.Value && e.Status == Status.Enrolled)
                            .Select(e => e.Student!.UserId)
                            .ToListAsync();

                        foreach (var id in studentUserIds) if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
                        break;
                    }
                case AnnouncementTargetType.Course:
                    {
                        if (!dto.CourseId.HasValue)
                            throw new BadRequestException("CourseId is required");

                        var sectionIds = await Db.Set<Section>()
                            .AsNoTracking()
                            .Where(s => s.CourseId == dto.CourseId.Value)
                            .Select(s => s.SectionId)
                            .ToListAsync();

                        var studentUserIds = await Db.Set<Enrollment>()
                            .AsNoTracking()
                            .Include(e => e.Student)
                            .Where(e => sectionIds.Contains(e.SectionId) && e.Status == Status.Enrolled)
                            .Select(e => e.Student!.UserId)
                            .ToListAsync();

                        foreach (var id in studentUserIds) if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
                        break;
                    }
                default:
                    throw new BadRequestException("Invalid target type");
            }

            // Sender should not receive their own announcement via fanout.
            ids.Remove(senderUserId);
            ids.Remove(SystemUserConstants.SystemUserId);
            return ids.ToList();
        }

        private static string? NormalizeRoles(List<string>? roles)
        {
            if (roles == null || roles.Count == 0) return null;
            return string.Join(",", roles.Select(r => (r ?? string.Empty).Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
