using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities.Integrations
{
    public class GoogleCalendarUserToken
    {
        [Key]
        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public string AccessTokenProtected { get; set; } = null!;

        [Required]
        public string RefreshTokenProtected { get; set; } = null!;

        [Required]
        public DateTime AccessTokenExpiresAtUtc { get; set; }

        public string? Scope { get; set; }

        public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public RegMan.Backend.DAL.Entities.BaseUser User { get; set; } = null!;
    }
}
