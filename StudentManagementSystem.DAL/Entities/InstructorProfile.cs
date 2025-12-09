using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class InstructorProfile
    {
        [Key] public int InstructorId { get; set; }

        [Required] public string Title { get; set; } = null!;

        private List<Section> Sections { get; set; } = new();
        public string UserId { get; set; } = null!; //fk
        public BaseUser User { get; set; } = null!;
    }
}