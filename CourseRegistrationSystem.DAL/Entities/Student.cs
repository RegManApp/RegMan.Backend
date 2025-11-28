using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseRegistrationSystem.DAL.Entities;

public class StudentProfile
{
    [Key] [ForeignKey("User")] public string StudentId { get; set; } = null!;

    [Required] public string FamilyContact { get; set; } = null!;

    public int CompletedCredits { get; set; } = 0;
    public int RegisteredCredits { get; set; } = 0;
    public double GPA { get; set; } = 0.0;

    public BaseUser User { get; set; } = null!;
    public AcademicPlan AcademicPlan { get; set; } = null!;
}