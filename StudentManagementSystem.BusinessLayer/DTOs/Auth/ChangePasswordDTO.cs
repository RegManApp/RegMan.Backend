using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.BusinessLayer.DTOs.AuthDTOs
{
    public class ChangePasswordDTO
    {
        [Required]
        public string CurrentPassword { get; set; } = null!;

        [Required]
        [MinLength(6, ErrorMessage = "New password must be at least 6 characters")]
        public string NewPassword { get; set; } = null!;

        [Required]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}
