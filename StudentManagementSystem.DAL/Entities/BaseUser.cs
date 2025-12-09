using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.DAL.Entities
{
    public class BaseUser : IdentityUser
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = null!;
        
        [Required]
        [StringLength(200)]
        public string Address { get; set; } = null!;
        
        [Required]
        [StringLength(50)]
        public string Role { get; set; } = null!;

        public StudentProfile? StudentProfile { get; set; }
        public AdminProfile? AdminProfile { get; set; }
        public InstructorProfile? InstructorProfile { get; set; }
    }
}