using System;

namespace StudentManagementSystem.Models
{
    public class ScheduleSlot
    {
        private int scheduleSlotId;
        private Section section;
        private Room room;
        private TimeSlot timeSlot;

        public ScheduleSlot() { }

        public ScheduleSlot(int id, Section section, Room room, TimeSlot timeSlot)
        {
            this.scheduleSlotId = id;
            this.section = section;
            this.room = room;
            this.timeSlot = timeSlot;
        }


        // Setters
        public void SetScheduleSlotId(int value)
        {
            scheduleSlotId = value;
        }
        public void SetSection(Section value)
        {
            section = value;
        }
        public void SetRoom(Room value)
        {
            room = value;
        }
        public void SetTimeSlot(TimeSlot value)
        {
            timeSlot = value;
        }

        // Getters
        public int GetScheduleSlotId()
        {
            return scheduleSlotId;
        }
        public Section GetSection()
        {
            return section;
        }
        public Room GetRoom()
        {
            return room;
        }
        public TimeSlot GetTimeSlot()
        {
            return timeSlot;
        }

        public bool ConflictWith(ScheduleSlot slot)
        {
            if (this.room.GetRoomId() != slot.GetRoom().GetRoomId())
                return false;

            return this.timeSlot.Overlaps(slot.GetTimeSlot());
        }

        // to display class info
        public override string ToString()
        {
            string sectionInfo;
            string roomInfo;
            string timeInfo;

            if (Section != null)
            {
                sectionInfo = "Section ID: " + Section.GetSectionId();
            }
            else
            {
                sectionInfo = "Section: N/A";
            }

            if (Room != null)
            {
                roomInfo = "Room ID: " + Room.GetRoomId();
            }
            else
            {
                roomInfo = "Room: N/A";
            }

            if (TimeSlot != null)
            {
                timeInfo = "Time: " + TimeSlot.ToString();
            }
            else
            {
                timeInfo = "Time: N/A";
            }

            return sectionInfo + ", " + roomInfo + ", " + timeInfo;
        }

    }
}
