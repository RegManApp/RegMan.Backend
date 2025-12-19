using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.OfficeHoursDTOs
{
    public class ViewOfficeHoursDTO
    {
        public int OfficeHoursId { get; set; }
        public int InstructorId { get; set; }
        public int? RoomId { get; set; }
        public DateTime Date { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public bool IsRecurring { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public string? Room { get; set; }
    }
}
