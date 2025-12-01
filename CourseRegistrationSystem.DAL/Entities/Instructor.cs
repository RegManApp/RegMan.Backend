using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseRegistrationSystem.DAL.Entities;

public class InstructorProfile
{
    [Key] [ForeignKey("User")] public int InstructorId { get; set; }

    [Required] public string Title { get; set; } = null!;

    private List<Section> Sections { get; set; } = new();

    public BaseUser User { get; set; } = null!;
}