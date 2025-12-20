using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.DTOs.InstructorDTOs;

public class UpdateInstructorDTO
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Title { get; set; }
    public InstructorDegree? Degree { get; set; }
    public string? Department { get; set; }
    public string? Address { get; set; }
}
