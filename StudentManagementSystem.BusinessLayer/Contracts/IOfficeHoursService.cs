using StudentManagementSystem.BusinessLayer.DTOs.OfficeHoursDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface IOfficeHoursService
    {
        Task<List<ViewOfficeHoursDTO>> GetOfficeHoursByInstructorIdAsync(int instructorId);
        Task<ViewOfficeHoursDTO> CreateOfficeHours(CreateOfficeHoursDTO hoursDTO);
        Task DeleteOfficeHour(int id);

    }
}
