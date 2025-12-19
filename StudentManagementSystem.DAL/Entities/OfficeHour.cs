using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Entities
{
    public class OfficeHour
    {
        public int OfficeHourId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int TimeSlotId { get; set; }

        [Required]
        [ForeignKey("Instructor")]
        public int InstructorId { get; set; }
        //navigation properties
        public Room Room { get; set; } = null!;
        public TimeSlot TimeSlot { get; set; } = null!;
        public InstructorProfile Instructor { get; set; } = null!;
    }
}
