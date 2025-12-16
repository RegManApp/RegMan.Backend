namespace StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs
{
    public class LoginResponseDTO
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
