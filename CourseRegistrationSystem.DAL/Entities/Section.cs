using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static System.Collections.Specialized.BitVector32;

namespace StudentManagementSystem.Models
{
    public class Section
    {
        public int SectionId { get; set; }
        [Required]
        public string Semester { get; set; }
        public DateTime Year { get; set; }
        [ForeignKey("Instructor")]
        public int InstructorId { get; set; }
        [ForeignKey("Course")]
        public int CourseId { get; set; }

        //navigation properties
        public Course Course { get; set; }
        public InstructorProfile Instructor { get; set; }
        public ICollection<ScheduleSlot> Slots { get; set; } = new HashSet<ScheduleSlot>();



    }

}