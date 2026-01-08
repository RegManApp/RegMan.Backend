using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.DAL.Entities
{
    public enum OfficeHourStatus
    {
        Available = 0,
        Booked = 1,
        Cancelled = 2
    }

    public class OfficeHour
    {
        public int OfficeHourId { get; set; }

        [Required]
        public string OwnerUserId { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string OwnerRole { get; set; } = null!;

        public int Capacity { get; set; } = 1;

        // Optional room - office hours can be virtual
        public int? RoomId { get; set; }

        [ForeignKey("Instructor")]
        public int? InstructorId { get; set; }

        // Specific date and time for the office hour
        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        // Recurrence settings
        public bool IsRecurring { get; set; } = false;
        public DayOfWeek? RecurringDay { get; set; }

        // Status
        public OfficeHourStatus Status { get; set; } = OfficeHourStatus.Available;

        // Optional notes from instructor
        public string? Notes { get; set; }

        // Created/Updated timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public Room? Room { get; set; }
        public BaseUser OwnerUser { get; set; } = null!;
        public InstructorProfile? Instructor { get; set; }
        public ICollection<OfficeHourBooking> Bookings { get; set; } = new List<OfficeHourBooking>();
    }
}
