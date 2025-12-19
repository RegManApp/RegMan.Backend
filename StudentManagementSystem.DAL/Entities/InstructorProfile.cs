using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class InstructorProfile
    {
        [Key] public int InstructorId { get; set; }

        [Required] public string Title { get; set; } = null!;

        private ICollection<Section> Sections { get; set; } = new List<Section>();
        private ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();
        private ICollection<OfficeHour> OfficeHours { get; set; } = new List<OfficeHour>();
        public string UserId { get; set; } = null!; //fk
        public BaseUser User { get; set; } = null!;
    }
}