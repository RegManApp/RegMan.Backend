using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public enum RoomType
    {
        LectureHall,
        Lab,
        Tutorial
    }

    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Building { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string RoomNumber { get; set; } = null!;

        [Required]
        public int Capacity { get; set; }

        [Required]
        public RoomType Type { get; set; }

        // Navigation (used later in Scheduling)
        public ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();

        // Navigation: Room-specific time slots
        public ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    }
}
