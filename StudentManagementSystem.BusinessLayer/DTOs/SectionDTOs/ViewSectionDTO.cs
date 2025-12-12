using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs
{
    public class ViewSectionDTO
    {
        public int SectionId { get; set; }
        public string Semester { get; set; }
        public DateTime Year { get; set; }
        public int InstructorId { get; set; }
        public string InstructorName { get; set; }
        public int AvailableSeats { get; set; }

        //public int CourseId { get; set; }
        //public string CourseName { get; set; }
        //public string Description { get; set; }
        //public int CreditHours { get; set; }
        //public string CourseCode { get; set; }
        public ViewCourseSummaryDTO CourseSummary { get; set; }
        public IEnumerable<ViewScheduleSlotDTO> ScheduleSlots { get; set; }

    }
}
