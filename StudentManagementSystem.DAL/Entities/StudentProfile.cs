using StudentManagementSystem.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities;

public class StudentProfile
{
    [Key] public int StudentId { get; set; }

    [Required] public string FamilyContact { get; set; } = null!;

    public int CompletedCredits { get; set; } = 0;
    public int RegisteredCredits { get; set; } = 0;
    public double GPA { get; set; } = 0.0;
    public string UserId { get; set; } = null!;
    public BaseUser User { get; set; } = null!;
    public string AcademicPlanId { get; set; } = null!;
    public AcademicPlan AcademicPlan { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new HashSet<Enrollment>();
}