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
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        public string TimeSlot { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
    }
}
