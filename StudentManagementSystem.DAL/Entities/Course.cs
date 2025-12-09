using System;
using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.DAL.Entities
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }
        [Required]
        public string CourseName { get; set; }
        public int CreditHours { get; set; }
        public int AvailableSeats { get; set; }
        public string CourseCode { get; set; }
        public CourseCategory CourseCategory { get; set; }

    }
    public enum CourseCategory
    {
        Elective,
        ITCS,
        ENG,
        BA,
        BT
    }
}
