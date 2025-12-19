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
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
    }
}
