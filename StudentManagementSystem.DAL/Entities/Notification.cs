using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public enum NotificationType
    {
        OfficeHourBooked = 0,
        OfficeHourCancelled = 1,
        OfficeHourConfirmed = 2,
        OfficeHourReminder = 3,
        EnrollmentApproved = 4,
        EnrollmentDeclined = 5,
        General = 6
    }

    public class Notification
    {
        public int NotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = null!;

        // Optional link to related entity
        public string? EntityType { get; set; } // "OfficeHourBooking", "Enrollment", etc.
        public int? EntityId { get; set; }

        // Read status
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public BaseUser User { get; set; } = null!;
    }
}
