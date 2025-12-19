using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    // Enum for Instructor Academic Degree/Title
    public enum InstructorDegree
    {
        TeachingAssistant = 0,
        AssistantLecturer = 1,
        Lecturer = 2,
        AssistantProfessor = 3,
        AssociateProfessor = 4,
        Professor = 5
    }

    public class InstructorProfile
    {
        [Key] public int InstructorId { get; set; }

        [Required] public string Title { get; set; } = null!;

        // Academic degree/rank
        public InstructorDegree Degree { get; set; } = InstructorDegree.TeachingAssistant;

        // Department
        public string? Department { get; set; }

        private ICollection<Section> Sections { get; set; } = new List<Section>();
        private ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();
        private ICollection<OfficeHour> OfficeHours { get; set; } = new List<OfficeHour>();
        public string UserId { get; set; } = null!; //fk
        public BaseUser User { get; set; } = null!;
    }
}