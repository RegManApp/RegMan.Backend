using System;
using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }
        [Required]
        public string CourseName { get; set; } = string.Empty;
        public int CreditHours { get; set; }
        //public int AvailableSeats { get; set; }
        [Required]
        public string CourseCode { get; set; } = string.Empty;
        public CourseCategory CourseCategory { get; set; }
        public string? Description { get; set; }

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
