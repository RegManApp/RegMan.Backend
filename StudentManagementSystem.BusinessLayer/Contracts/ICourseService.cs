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
        Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync(string? courseName, int? creditHours, string? courseCode, int? courseCategoryId);
        Task<ViewCourseDetailsDTO> GetCourseByIdAsync(int id);
        Task<ViewCourseSummaryDTO> GetCourseSummaryByIdAsync(int id);

        //Create
        Task<ViewCourseDetailsDTO> CreateCourseAsync(CreateCourseDTO courseDTO);
        //Update
        Task<ViewCourseDetailsDTO> UpdateCourseAsync(UpdateCourseDTO courseDTO);
        //Delete
        Task<string> DeleteCourseAsync(int id);
    }
}
