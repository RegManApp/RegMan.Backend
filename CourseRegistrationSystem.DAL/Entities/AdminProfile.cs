using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities;

public class AdminProfile
{
    [Key] [ForeignKey("User")] public int StaffId { get; set; }

    [Required] public string Title { get; set; } = null!;

    public BaseUser User { get; set; } = null!;
}