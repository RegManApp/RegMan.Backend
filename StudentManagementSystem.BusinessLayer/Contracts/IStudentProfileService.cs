using StudentManagementSystem.BusinessLayer.DTOs.StudentDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface IStudentProfileService
    {
        Task<ViewStudentProfileDTO> CreateProfileAsync(CreateStudentDTO studentDTO);
        Task<ViewStudentProfileDTO> GetProfileByIdAsync(int id);
        Task<List<ViewStudentProfileDTO>> GetAllStudentsAsync(int? GPA, int? CompletedCredits, string? AcademicPlanId);
    }
}
