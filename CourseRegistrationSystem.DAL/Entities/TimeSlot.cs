using System;
using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.Entities
{
    public class TimeSlot
    {
        [Required] public DayOfWeek day { get; set; }
        [Required] public TimeSpan startTime { get; set; }
        [Required] public TimeSpan endTime { get; set; }

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