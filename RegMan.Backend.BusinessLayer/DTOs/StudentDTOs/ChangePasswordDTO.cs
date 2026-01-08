using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.DTOs.StudentDTOs
{
    public class ChangePasswordDTO
    {
        [Required]
        public required string Email { get; set; }
        [Required]
        public required string NewPassword { get; set; }
        [Required]
        public required string OldPassword { get; set; }
        [Required]
        public required string ConfirmPassword { get; set; }
    }
}
