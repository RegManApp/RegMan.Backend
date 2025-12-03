using StudentManagementSystem.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs
{
    public class ViewCourseSummaryDTO
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public int CreditHours { get; set; }
        public string CourseCode { get; set; }
    }
}
