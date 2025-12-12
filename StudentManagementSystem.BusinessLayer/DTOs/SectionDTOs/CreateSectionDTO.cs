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
    public class CreateSectionDTO
    {
       
        [Required]
        public string Semester { get; set; }
        public DateTime Year { get; set; }
        public int InstructorId { get; set; }
        [Required]
        public int CourseId { get; set; }
        [Required]
        [Range(30, 60)]
        public int AvailableSeats { get; set; }


    }
}
