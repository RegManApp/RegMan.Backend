using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class Transcript
    {
        [Key]
        public int TranscriptId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        public int SectionId { get; set; }

        [Required]
        [StringLength(10)]
        public string Grade { get; set; } = string.Empty; // A, A-, B+, B, B-, C+, C, C-, D+, D, F

        public double GradePoints { get; set; } // 4.0, 3.7, 3.3, etc.

        [Required]
        public string Semester { get; set; } = string.Empty; // Fall, Spring, Summer

        [Required]
        public int Year { get; set; }

        public int CreditHours { get; set; }

        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("StudentId")]
        public StudentProfile Student { get; set; } = null!;

        [ForeignKey("CourseId")]
        public Course Course { get; set; } = null!;

        [ForeignKey("SectionId")]
        public Section Section { get; set; } = null!;
    }

    public static class GradeHelper
    {
        public static double GetGradePoints(string grade)
        {
            return grade.ToUpper() switch
            {
                "A" => 4.0,
                "A-" => 3.7,
                "B+" => 3.3,
                "B" => 3.0,
                "B-" => 2.7,
                "C+" => 2.3,
                "C" => 2.0,
                "C-" => 1.7,
                "D+" => 1.3,
                "D" => 1.0,
                "F" => 0.0,
                _ => 0.0
            };
        }

        public static bool IsValidGrade(string grade)
        {
            var validGrades = new[] { "A", "A-", "B+", "B", "B-", "C+", "C", "C-", "D+", "D", "F" };
            return validGrades.Contains(grade.ToUpper());
        }

        public static bool IsPassing(string grade)
        {
            return GetGradePoints(grade) >= 1.0; // D or above
        }
    }
}