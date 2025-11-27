using System;
namespace StudentManagementSystem.Models
{
    public class TimeSlot
    {
        private DayOfWeak day;
        private TimeSpan startTime;
        private TimeSpan endTime;

        public TimeSlot() { }

        public TimeSlot(DayOfWeek day, TimeSpan start, TimeSpan end)
        {
            this.day = day;
            this.startTime = start;
            this.endTime = end;
        }

        // Setters
        public void SetDay(DayOfWeek value)
        {
            day = value;
        }
        public void SetStartTime(TimeSpan value)
        {
            startTime = value;
        }
        public void SetEndTime(TimeSpan value)
        {
            endTime = value;
        }

        // Getters
        public DayOfWeek GetDay()
        {
            return day;
        }
        public TimeSpan GetStartTime()
        {

            return startTime;
        }
        public TimeSpan GetEndTime()
        {
            return endTime;
        }

        public bool Overlaps(TimeSlot slot)
        {
            // check days
            if (this.day != slot.day)
                return false;
            // check time
            bool isOverlapping = this.startTime < slot.endTime && this.endTime > slot.startTime;
            return isOverlapping;
        }

        // to display class data
        public override string ToString()
        {
            return $"{day} {startTime} - {endTime}";
        }
    }
}