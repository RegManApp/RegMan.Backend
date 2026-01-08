using System;
using System.Collections.Generic;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.DTOs.OfficeHoursDTOs
{
    // Requests
    public sealed class CreateInstructorOfficeHourDTO
    {
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!; // "HH:mm"
        public string EndTime { get; set; } = null!;
        public int? RoomId { get; set; }
        public bool IsRecurring { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class UpdateInstructorOfficeHourDTO
    {
        public DateTime? Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int? RoomId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class BookOfficeHourRequestDTO
    {
        public string? Purpose { get; set; }
        public string? BookerNotes { get; set; }
    }

    public sealed class BookOfficeHourResultDTO
    {
        public int BookingId { get; set; }
        public int ConversationId { get; set; }
        public int SystemMessageId { get; set; }
    }

    public sealed class RescheduleOfficeHourBookingRequestDTO
    {
        public int NewOfficeHourId { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class RescheduleOfficeHourBookingResultDTO
    {
        public int BookingId { get; set; }
        public int ConversationId { get; set; }
        public int SystemMessageId { get; set; }
    }

    // Small shared DTOs
    public sealed class RoomInfoDTO
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = null!;
        public string Building { get; set; } = null!;
    }

    public sealed class StudentInfoDTO
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    public sealed class UserInfoDTO
    {
        public string UserId { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
    }

    public sealed class InstructorInfoDTO
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Title { get; set; }
        public InstructorDegree? Degree { get; set; }
        public string? Department { get; set; }
    }

    // Provider (role-agnostic owner) DTOs
    public sealed class ProviderInfoDTO
    {
        public string UserId { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Title { get; set; }
        public InstructorDegree? Degree { get; set; }
        public string? Department { get; set; }
    }

    // Instructor list
    public sealed class ProviderBookingListItemDTO
    {
        public int BookingId { get; set; }
        public BookingStatus Status { get; set; }
        public string? Purpose { get; set; }
        public string? BookerNotes { get; set; }
        public string? ProviderNotes { get; set; }
        public DateTime BookedAt { get; set; }
        public UserInfoDTO Booker { get; set; } = null!;
    }

    public sealed class InstructorOfficeHourListItemDTO
    {
        public int OfficeHourId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public OfficeHourStatus Status { get; set; }
        public string? Notes { get; set; }
        public bool IsRecurring { get; set; }
        public DayOfWeek? RecurringDay { get; set; }
        public RoomInfoDTO? Room { get; set; }
        public List<ProviderBookingListItemDTO> Bookings { get; set; } = new();
    }

    // Student available office hours
    public sealed class StudentAvailableOfficeHourDTO
    {
        public int OfficeHourId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public string? Notes { get; set; }
        public RoomInfoDTO? Room { get; set; }
        public InstructorInfoDTO Instructor { get; set; } = null!;
    }

    public sealed class StudentInstructorsWithOfficeHoursDTO
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Title { get; set; }
        public InstructorDegree? Degree { get; set; }
        public string? Department { get; set; }
        public int AvailableSlots { get; set; }
    }

    public sealed class StudentProvidersWithOfficeHoursDTO
    {
        public ProviderInfoDTO Provider { get; set; } = null!;
        public int AvailableSlots { get; set; }
    }

    public sealed class StudentAvailableOfficeHourV2DTO
    {
        public int OfficeHourId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public string? Notes { get; set; }
        public RoomInfoDTO? Room { get; set; }
        public ProviderInfoDTO Provider { get; set; } = null!;
    }

    public sealed class StudentBookingOfficeHourDTO
    {
        public int OfficeHourId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public string? Notes { get; set; }
        public RoomInfoDTO? Room { get; set; }
    }

    public sealed class UserBookingListItemDTO
    {
        public int BookingId { get; set; }
        public BookingStatus Status { get; set; }
        public string? Purpose { get; set; }
        public string? BookerNotes { get; set; }
        public string? ProviderNotes { get; set; }
        public DateTime BookedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public string? CancelledBy { get; set; }
        public StudentBookingOfficeHourDTO OfficeHour { get; set; } = null!;
        public ProviderInfoDTO Provider { get; set; } = null!;
        public InstructorInfoDTO? Instructor { get; set; }
    }

    // Admin list
    public sealed class AdminBookingListItemDTO
    {
        public int BookingId { get; set; }
        public BookingStatus Status { get; set; }
        public string? Purpose { get; set; }
        public AdminStudentInfoDTO Student { get; set; } = null!;
    }

    public sealed class AdminStudentInfoDTO
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = null!;
    }

    public sealed class AdminInstructorInfoDTO
    {
        public int InstructorId { get; set; }
        public string FullName { get; set; } = null!;
        public string? Title { get; set; }
        public InstructorDegree? Degree { get; set; }
    }

    public sealed class AdminOfficeHourListItemDTO
    {
        public int OfficeHourId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public OfficeHourStatus Status { get; set; }
        public string? Notes { get; set; }
        public RoomInfoDTO? Room { get; set; }
        public AdminInstructorInfoDTO Instructor { get; set; } = null!;
        public List<AdminBookingListItemDTO> Bookings { get; set; } = new();
    }
}
