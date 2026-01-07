using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.DAL.Entities
{
    public class BaseUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastSeenAt { get; set; }

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
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new HashSet<RefreshToken>();
        public ICollection<Notification> Notifications { get; set; } = new HashSet<Notification>();

    }
}