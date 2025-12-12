using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs
{
    public class UpdateCourseDTO
    {        
        [Required]
        public int CourseId { get; set; }
        [Required]
        [Range(1, 4)]
        public int CreditHours { get; set; }
        //[Required]
        //[Range(30, 60)]
        //public int AvailableSeats { get; set; }
        [Required]
        [StringLength(7, MinimumLength = 4)]
        public string CourseCode { get; set; }
        [Required]
        public int CourseCategoryId { get; set; }
        public string Description { get; set; }

    }
}
