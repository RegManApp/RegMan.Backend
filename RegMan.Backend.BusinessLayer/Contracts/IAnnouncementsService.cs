using RegMan.Backend.BusinessLayer.DTOs.Announcements;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IAnnouncementsService
    {
        Task<CreateAnnouncementResultDTO> CreateAnnouncementAsync(string senderUserId, string senderRole, CreateAnnouncementRequestDTO dto);

        Task<List<MyAnnouncementListItemDTO>> GetMyAnnouncementsAsync(string userId, bool includeArchived = false);
        Task MarkAnnouncementReadAsync(string userId, int announcementId);
        Task<int> GetMyUnreadCountAsync(string userId);

        Task<AnnouncementScopesDTO> GetMyScopesAsync(string userId, string userRole);

        // Admin audit
        Task<List<AdminAnnouncementAuditItemDTO>> GetAuditAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? createdByUserId,
            bool? isArchived,
            int? courseId,
            int? sectionId,
            string? targetRole);

        Task ArchiveAnnouncementAsync(string adminUserId, int announcementId);
    }
}
