using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.DAL.Entities
{
    public class AcademicPlan
    {
        [Key]
        public string AcademicPlanId { get; set; } = string.Empty;

        [Required]
        public string MajorName { get; set; } = string.Empty;

        [Required]
        public int TotalCreditsRequired { get; set; }

        public string? Description { get; set; }

        public int ExpectedYearsToComplete { get; set; } = 4;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public ICollection<AcademicPlanCourse> AcademicPlanCourses { get; set; } = new HashSet<AcademicPlanCourse>();
        public ICollection<StudentProfile> Students { get; set; } = new HashSet<StudentProfile>();
    }

    public class AcademicPlanCourse
    {
        [Key]
        public int AcademicPlanCourseId { get; set; }

        [Required]
        public string AcademicPlanId { get; set; } = string.Empty;

        [Required]
        public int CourseId { get; set; }

        public int RecommendedSemester { get; set; } // 1-8 for typical 4-year plan

        public int RecommendedYear { get; set; } // 1-4

        public bool IsRequired { get; set; } = true;

        public CourseType CourseType { get; set; } = CourseType.Core;

        // Navigation Properties
        public AcademicPlan AcademicPlan { get; set; } = null!;
        public Course Course { get; set; } = null!;
    }

    public enum CourseType
    {
        Core,           // Required core courses
        Elective,       // Elective courses
        GeneralEducation, // General education requirements
        Major,          // Major-specific courses
        Minor           // Minor courses
    }
}





