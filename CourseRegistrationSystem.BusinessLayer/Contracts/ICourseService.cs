using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface ICourseService
    {
        //Read
        Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync(string? courseName, int? creditHours, int? availableSeats, string? courseCode, int? courseCategoryId);
        Task<ViewCourseDetailsDTO> GetCourseById(int id);
        //Create
        Task<ViewCourseDetailsDTO> CreateCourse(CreateCourseDTO courseDTO);
        //Update
        Task<ViewCourseDetailsDTO> UpdateCourse(UpdateCourseDTO courseDTO);
        //Delete
        Task<string> DeleteCourse(int id);
    }
}
