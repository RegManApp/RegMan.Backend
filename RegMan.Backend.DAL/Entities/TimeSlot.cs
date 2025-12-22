using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public class TimeSlot
    {
        [Key]
        public int TimeSlotId { get; set; }

        [Required]
        public DayOfWeek Day { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        // New: Room association
        [Required]
        public int RoomId { get; set; }
        public Room Room { get; set; } = null!;

        // Simple validation helper
        public bool IsValid()
        {
            return EndTime > StartTime;
        }
    }
}
