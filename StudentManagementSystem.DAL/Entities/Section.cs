using StudentManagementSystem.DAL.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class Section
    {
        [Key]
        public int SectionId { get; set; }
        [Required]
        public string Semester { get; set; } = string.Empty;
        public DateTime Year { get; set; }
        //[Required, ForeignKey("Instructor")]
        [ForeignKey("Instructor")]
        public int? InstructorId { get; set; } //changed to nullable cause sometimes an instructor may not be assigned yet
        [Required, ForeignKey("Course")]
        public int CourseId { get; set; }
        [Required]
        public int AvailableSeats { get; set; }


        //navigation properties
        public Course Course { get; set; } = null!;
        public InstructorProfile? Instructor { get; set; }
        public ICollection<ScheduleSlot> Slots { get; set; } = new HashSet<ScheduleSlot>();
        public ICollection<Enrollment> Enrollments { get; set; } = new HashSet<Enrollment>();



    }

}