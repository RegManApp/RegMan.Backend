using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.OfficeHoursDTOs
{
    public class CreateOfficeHoursDTO
    {
        public int InstructorId { get; set; }
        public int? RoomId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = null!; // "HH:mm" format
        public string EndTime { get; set; } = null!;
        public bool IsRecurring { get; set; } = false;
        public string? Notes { get; set; }
    }
}
