using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.CartDTOs
{
    public class ViewCartItemDTO
    {
        public int CartItemId { get; set; }
        public int ScheduleSlotId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public string Room { get; set; } = string.Empty;
        public string TimeSlot { get; set; } = string.Empty;
        //public TimeSpan StartTime { get; set; } 
        //public TimeSpan EndTime { get; set; } 
    }
}
