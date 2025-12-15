using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs
{
    public class CreateCourseDTO
    {
        public string CourseName { get; set; }
        [Required]
        [Range(1, 4)]
        public int CreditHours { get; set; }
        [Required]
        [Range(30,60)]
        public int AvailableSeats { get; set; }
        [Required]
        [StringLength(7, MinimumLength = 4)]
        public string CourseCode { get; set; }
        [Required]
        public int CourseCategoryId { get; set; }
    }
}
