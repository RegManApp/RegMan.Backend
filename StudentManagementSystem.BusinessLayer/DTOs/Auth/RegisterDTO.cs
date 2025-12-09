using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs
{
    public class RegisterDTO
    {
        [Required]
        public string FullName { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Address { get; set; } = null!;

        [Required]
        public string Role { get; set; } = null!;  // Admin / Student / Instructor

        [Required, MinLength(8)]
        public string Password { get; set; } = null!;
    }
}
