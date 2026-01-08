using System;
using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public class AnnouncementRecipient
    {
        [Key]
        public int AnnouncementRecipientId { get; set; }

        [Required]
        public int AnnouncementId { get; set; }

        [Required]
        public string RecipientUserId { get; set; } = null!;

        public DateTime DeliveredAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }

        // Navigation
        public Announcement Announcement { get; set; } = null!;
        public BaseUser RecipientUser { get; set; } = null!;
    }
}
