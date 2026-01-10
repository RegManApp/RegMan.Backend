using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RegMan.Backend.DAL.Entities
{
    [Table("OfficeHourQrTokens")]
    public class OfficeHourQrToken
    {
        [Key]
        public int QrTokenId { get; set; }

        [Required]
        public int QueueEntryId { get; set; }

        // Rotation state: only valid while QueueEntry is Ready
        public Guid? CurrentNonce { get; set; }
        public DateTime? IssuedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        // One-time use marker
        public DateTime? UsedAtUtc { get; set; }
        public string? UsedByUserId { get; set; }

        public OfficeHourQueueEntry QueueEntry { get; set; } = null!;
    }
}
