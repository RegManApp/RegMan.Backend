using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs
{
    public class ViewCourseDetailsDTO
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public int CreditHours { get; set; }
        public int AvailableSeats { get; set; }
        public string CourseCode { get; set; }
        public int CourseCategoryId { get; set; }
        public string CourseCategoryName { get; set; }
    }
}
