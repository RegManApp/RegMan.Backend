namespace RegMan.Backend.BusinessLayer.DTOs.Calendar
{
    public class CourseSessionPropsDTO
    {
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
    }

    public class OfficeHourBookingPropsDTO
    {
        public int BookingId { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
    }

    public class InstructorOfficeHourPropsDTO
    {
        public int OfficeHourId { get; set; }
        public string Room { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public InstructorOfficeHourActiveBookingPropsDTO? Booking { get; set; }
    }

    public class InstructorOfficeHourActiveBookingPropsDTO
    {
        public int BookingId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
