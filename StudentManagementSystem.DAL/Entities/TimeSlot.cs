using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.DAL.Entities
{
    public class TimeSlot
    {
        [Key]
        public int TimeSlotId { get; set; }
        [Required] public DayOfWeek Day { get; set; }
        [Required] public TimeSpan StartTime { get; set; }
        [Required] public TimeSpan EndTime { get; set; }

        // public TimeSlot() { }

        // public TimeSlot(DayOfWeek day, TimeSpan start, TimeSpan end)
        // {
        //     this.day = day;
        //     this.startTime = start;
        //     this.endTime = end;
        // }

        // public bool Overlaps(TimeSlot slot)
        // {
        //     // check days
        //     if (this.day != slot.day)
        //         return false;
        //     // check time
        //     bool isOverlapping = this.startTime < slot.endTime && this.endTime > slot.startTime;
        //     return isOverlapping;
        // }

        // // to display class data
        // public override string ToString()
        // {
        //     return $"{day} {startTime} - {endTime}";
        // }
    }
}