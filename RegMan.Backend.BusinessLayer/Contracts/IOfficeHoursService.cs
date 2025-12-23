using RegMan.Backend.BusinessLayer.DTOs.OfficeHoursDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IOfficeHoursService
    {
        // Admin
        Task<List<ViewOfficeHoursDTO>> GetOfficeHoursByInstructorIdAsync(int instructorId);
        Task<List<AdminOfficeHourListItemDTO>> GetAllOfficeHoursAsync(int? instructorId, DateTime? fromDate, DateTime? toDate, string? status);

        // Instructor
        Task<List<InstructorOfficeHourListItemDTO>> GetMyOfficeHoursAsync(string instructorUserId, DateTime? fromDate, DateTime? toDate);
        Task<int> CreateOfficeHourAsync(string instructorUserId, CreateInstructorOfficeHourDTO dto);
        Task<(List<int> createdIds, List<string> errors)> CreateBatchOfficeHoursAsync(string instructorUserId, List<CreateInstructorOfficeHourDTO> dtos);
        Task UpdateOfficeHourAsync(string instructorUserId, int officeHourId, UpdateInstructorOfficeHourDTO dto);
        Task DeleteOfficeHourAsync(string instructorUserId, int officeHourId);
        Task ConfirmBookingAsync(string instructorUserId, int bookingId);
        Task AddInstructorNotesAsync(string instructorUserId, int bookingId, string? notes);
        Task CompleteBookingAsync(string instructorUserId, int bookingId);
        Task MarkNoShowAsync(string instructorUserId, int bookingId);

        // Student
        Task<List<StudentAvailableOfficeHourDTO>> GetAvailableOfficeHoursAsync(int? instructorId, DateTime? fromDate, DateTime? toDate);
        Task<List<StudentInstructorsWithOfficeHoursDTO>> GetInstructorsWithOfficeHoursAsync();
        Task<int> BookOfficeHourAsync(string studentUserId, int officeHourId, BookOfficeHourRequestDTO dto);
        Task<List<StudentBookingListItemDTO>> GetMyBookingsAsync(string studentUserId, string? status);

        // Student/Instructor
        Task CancelBookingAsync(string userId, string userRole, int bookingId, string? reason);

    }
}
