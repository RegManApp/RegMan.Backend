using System;
using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities;

public class AcademicCalendarSettings
{
    [Key]
    public int AcademicCalendarSettingsId { get; set; }

    // Single-row key to avoid any more static/in-memory state.
    [Required]
    [MaxLength(64)]
    public string SettingsKey { get; set; } = "default";

    public DateTime? RegistrationStartDateUtc { get; set; }
    public DateTime? RegistrationEndDateUtc { get; set; }

    public DateTime? WithdrawStartDateUtc { get; set; }
    public DateTime? WithdrawEndDateUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
