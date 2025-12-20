using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.DTOs.Auth
{
    public class CreateUserDTO
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Password { get; set; } = null!;

        // Student-specific fields
        public string? FamilyContact { get; set; }
        public string? AcademicPlanId { get; set; }

        // Instructor-specific fields
        public string? Title { get; set; }
        public InstructorDegree? Degree { get; set; }
        public string? Department { get; set; }
    }
}
