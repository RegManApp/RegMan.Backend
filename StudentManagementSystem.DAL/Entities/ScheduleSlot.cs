using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class ScheduleSlot
    {
        [Key]
        public int ScheduleSlotId { get; set; }

        // =========================
        // Foreign Keys
        // =========================
        [Required]
        public int SectionId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int TimeSlotId { get; set; }

        [Required]
        [ForeignKey("Instructor")]
        public int InstructorId { get; set; }

        [Required]
        public SlotType SlotType { get; set; } = SlotType.Lecture;

        // =========================
        // Navigation Properties
        // =========================
        public Section Section { get; set; } = null!;
        public Room Room { get; set; } = null!;
        public TimeSlot TimeSlot { get; set; } = null!;
        public InstructorProfile Instructor { get; set; } = null!;
    }

    public enum SlotType
    {
        Lecture,
        Lab,
        Tutorial
    }
}
