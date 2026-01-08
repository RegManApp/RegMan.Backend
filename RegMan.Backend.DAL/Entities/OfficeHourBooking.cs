using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities
{
    public enum BookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2,
        Completed = 3,
        NoShow = 4
    }

    public class OfficeHourBooking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        public int OfficeHourId { get; set; }

        // Role-agnostic: who booked this office hour
        [Required]
        public string BookerUserId { get; set; } = null!;

        [MaxLength(50)]
        public string BookerRole { get; set; } = null!;

        // Back-compat for existing student bookings (nullable for non-student bookers)
        public int? StudentId { get; set; }

        // Booking details
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        // Reason for the meeting
        [MaxLength(500)]
        public string? Purpose { get; set; }

        // Notes from booker
        [MaxLength(1000)]
        public string? BookerNotes { get; set; }

        // Notes from provider (after meeting)
        [MaxLength(1000)]
        public string? ProviderNotes { get; set; }

        // Cancellation reason
        [MaxLength(500)]
        public string? CancellationReason { get; set; }
        public string? CancelledBy { get; set; } // e.g. "Booker" or "Provider" (or a role)

        // Timestamps
        public DateTime BookedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public OfficeHour OfficeHour { get; set; } = null!;
        public BaseUser BookerUser { get; set; } = null!;
        public StudentProfile? Student { get; set; }
    }
}
