using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.DAL.Entities.Integrations
{
    public class GoogleCalendarOAuthStateNonce
    {
        public int GoogleCalendarOAuthStateNonceId { get; set; }

        // SHA-256 hex of the raw state value sent to Google.
        public string StateHash { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public BaseUser? User { get; set; }

        public DateTime IssuedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        public bool IsUsed { get; set; }
        public DateTime? UsedAtUtc { get; set; }

        public string? ReturnUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
