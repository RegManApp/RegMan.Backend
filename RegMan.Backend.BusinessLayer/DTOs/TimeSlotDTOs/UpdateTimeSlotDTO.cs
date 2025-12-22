using System.ComponentModel.DataAnnotations;

namespace RegMan.Backend.BusinessLayer.DTOs.TimeSlotDTOs
{
    public class UpdateTimeSlotDTO
    {
        [Required]
        public int TimeSlotId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public DayOfWeek Day { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }
    }
}
