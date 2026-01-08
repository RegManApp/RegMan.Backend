using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public enum AnnouncementTargetType
    {
        AllUsers = 0,
        Roles = 1,
        Course = 2,
        Section = 3
    }

    public class Announcement
    {
        [Key]
        public int AnnouncementId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        public string Content { get; set; } = null!;

        [Required]
        public AnnouncementTargetType TargetType { get; set; }

        // Comma-separated role names (e.g. "Student,Instructor")
        public string? TargetRoles { get; set; }

        public int? CourseId { get; set; }
        public int? SectionId { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = null!;

        [MaxLength(50)]
        public string? CreatedByRole { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsArchived { get; set; } = false;
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserId { get; set; }

        // Navigation
        public BaseUser CreatedByUser { get; set; } = null!;
        public Course? Course { get; set; }
        public Section? Section { get; set; }
        public ICollection<AnnouncementRecipient> Recipients { get; set; } = new HashSet<AnnouncementRecipient>();
    }
}
