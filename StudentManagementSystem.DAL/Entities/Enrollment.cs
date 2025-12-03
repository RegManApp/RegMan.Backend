using StudentManagementSystem.DAL.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Entities
{
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        public Status Status { get; set; }

        [Required]
        [ForeignKey("Student")]
        public int StudentId { get; set; }


        [Required]
        [ForeignKey("Section")]
        public int SectionId { get; set; }
        //Nav properties
        public StudentProfile? Student { get; set; }
        public Section? Section { get; set; }

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

public enum Status { Enrolled, Dropped, Completed };
