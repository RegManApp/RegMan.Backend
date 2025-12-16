using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class RefreshToken
    {
        [Key]
        public int RefreshTokenId { get; set; }

        [Required]
        public string TokenHash { get; set; } = null!;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        // For multi-device support
        public string? Device { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [ForeignKey(nameof(UserId))]
        public BaseUser User { get; set; } = null!;
    }
}
