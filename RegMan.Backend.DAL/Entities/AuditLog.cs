using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string UserEmail { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty; // CREATE / UPDATE / DELETE

        [Required]
        public string EntityName { get; set; } = string.Empty; // Course, Student, ...

        [Required]
        public string EntityId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
