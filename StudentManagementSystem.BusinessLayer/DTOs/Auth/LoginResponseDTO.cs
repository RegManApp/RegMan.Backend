namespace StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs
{
    public class LoginResponseDTO
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string UserId { get; set; } = null!;
        public string? InstructorTitle { get; set; }
    }
}
