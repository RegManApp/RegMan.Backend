namespace RegMan.Backend.API.DTOs.DevTools
{
    public sealed class DemoUserInfoDto
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public sealed class LoginAsRequestDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class SeedResultDto
    {
        public string Message { get; set; } = string.Empty;
        public List<DemoUserInfoDto> Users { get; set; } = new();
    }
}
