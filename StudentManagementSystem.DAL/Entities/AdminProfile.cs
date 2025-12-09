using StudentManagementSystem.DAL.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class AdminProfile
    {
        [Key] public int StaffId { get; set; }

        [Required] public string Title { get; set; } = null!;
        public string UserId { get; set; } = null!; //fk
        public BaseUser User { get; set; } = null!;
    }
}