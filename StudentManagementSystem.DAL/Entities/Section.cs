using StudentManagementSystem.DAL.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.Entities
{
    public class Section
    {
        [Key]
        public int SectionId { get; set; }
        [Required]
        public string Semester { get; set; }
        public DateTime Year { get; set; }
        [Required, ForeignKey("Instructor")]
        public int InstructorId { get; set; }
        [Required, ForeignKey("Course")]
        public int CourseId { get; set; }

        //navigation properties
        public Course Course { get; set; }
        public InstructorProfile Instructor { get; set; }
        public ICollection<ScheduleSlot> Slots { get; set; } = new HashSet<ScheduleSlot>();



    }

}