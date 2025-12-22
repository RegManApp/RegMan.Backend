using System;
using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.DAL.Entities;

public class WithdrawRequest
{
    [Key]
    public int WithdrawRequestId { get; set; }

    [Required]
    public string StudentUserId { get; set; } = string.Empty;

    [Required]
    public int EnrollmentId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public WithdrawRequestStatus Status { get; set; } = WithdrawRequestStatus.Pending;

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
}

public enum WithdrawRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}
