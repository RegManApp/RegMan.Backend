using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.Entities
{
    public class BaseUser : IdentityUser
    {
        [Required] public string Address { get; set; } = null!;
        [Required] public string Role { get; set; } = null!;
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = null!;
        

        public StudentProfile? StudentProfile { get; set; }
        public AdminProfile? AdminProfile { get; set; }
        public InstructorProfile? InstructorProfile { get; set; }
    }
}