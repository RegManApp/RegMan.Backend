using StudentManagementSystem.DAL.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public enum Status { Enrolled, Dropped, Completed }

    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        public Status Status { get; set; }

        [Required]
        public int StudentId { get; set; }


        [Required]
        public int SectionId { get; set; }

        // Grade (A, B, C, D, F, etc.)
        public string? Grade { get; set; }

        //Nav properties
        public StudentProfile? Student { get; set; }
        public Section? Section { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;


        // Operations

        public void ChangeStatus(Status newStatus)
        {
            Status = newStatus;
        }


        public bool IsActive()
        {
            return Status == Status.Enrolled;
        }


        public void Drop()
        {
            Status = Status.Dropped;
        }

    }
}
