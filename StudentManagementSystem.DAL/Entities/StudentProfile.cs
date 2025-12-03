using StudentManagementSystem.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities;

public class StudentProfile
{
    [Key][ForeignKey("User")] public int StudentId { get; set; }

    [Required] public string FamilyContact { get; set; } = null!;

    public int CompletedCredits { get; set; } = 0;
    public int RegisteredCredits { get; set; } = 0;
    public double GPA { get; set; } = 0.0;
    [ForeignKey("AcademicPlan")]
    public BaseUser User { get; set; } = null!;
    public AcademicPlan AcademicPlan { get; set; } = null!;
}