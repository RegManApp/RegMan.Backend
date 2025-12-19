using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
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

        [Required]
        public int StudentId { get; set; }

        // Booking details
        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        // Reason for the meeting
        [MaxLength(500)]
        public string? Purpose { get; set; }

        // Notes from student
        [MaxLength(1000)]
        public string? StudentNotes { get; set; }

        // Notes from instructor (after meeting)
        [MaxLength(1000)]
        public string? InstructorNotes { get; set; }

        // Cancellation reason
        [MaxLength(500)]
        public string? CancellationReason { get; set; }
        public string? CancelledBy { get; set; } // "Student" or "Instructor"

        // Timestamps
        public DateTime BookedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public OfficeHour OfficeHour { get; set; } = null!;
        public StudentProfile Student { get; set; } = null!;
    }
}
