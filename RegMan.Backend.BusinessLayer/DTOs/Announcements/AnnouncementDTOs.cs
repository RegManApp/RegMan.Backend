using RegMan.Backend.DAL.Entities;
using System;
using System.Collections.Generic;

namespace RegMan.Backend.BusinessLayer.DTOs.Announcements
{
    public sealed class CreateAnnouncementRequestDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public AnnouncementTargetType TargetType { get; set; }

        // Used when TargetType == Roles
        public List<string>? TargetRoles { get; set; }

        // Used when TargetType == Course
        public int? CourseId { get; set; }

        // Used when TargetType == Section
        public int? SectionId { get; set; }
    }

    public sealed class CreateAnnouncementResultDTO
    {
        public int AnnouncementId { get; set; }
        public int RecipientCount { get; set; }
    }

    public sealed class MyAnnouncementListItemDTO
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public AnnouncementTargetType TargetType { get; set; }
        public int? CourseId { get; set; }
        public int? SectionId { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? CreatedByRole { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }

        public DateTime? ReadAt { get; set; }
        public bool IsRead => ReadAt.HasValue;
    }

    public sealed class AdminAnnouncementAuditItemDTO
    {
        public int AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public AnnouncementTargetType TargetType { get; set; }
        public string? TargetRoles { get; set; }
        public int? CourseId { get; set; }
        public int? SectionId { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? CreatedByRole { get; set; }

        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        public int RecipientCount { get; set; }
        public int ReadCount { get; set; }
    }

    public sealed class AnnouncementScopesDTO
    {
        public List<ScopeSectionDTO> Sections { get; set; } = new();
        public List<ScopeCourseDTO> Courses { get; set; } = new();
    }

    public sealed class ScopeSectionDTO
    {
        public int SectionId { get; set; }
        public string? SectionName { get; set; }
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
    }

    public sealed class ScopeCourseDTO
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
    }
}
