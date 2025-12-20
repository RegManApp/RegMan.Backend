using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.DTOs.InstructorDTOs;

public class ViewInstructorDTO
{
    public int InstructorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public InstructorDegree Degree { get; set; }
    public string DegreeDisplay => Degree.ToString();
    public string? Department { get; set; }
    public string? Address { get; set; }
}
