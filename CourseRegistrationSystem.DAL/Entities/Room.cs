using System;

namespace StudentManagementSystem.Models
{
    public class Room
    {
        private int roomId;
        private string building;
        private string roomNumber;
        private int capacity;
        private List<ScheduleSlot> schedule = new List<ScheduleSlot>();

        public Room() { }
        public Room(int roomId, string building, string roomNumber, int capacity)
        {
            this.roomId = roomId;
            this.building = building;
            this.roomNumber = roomNumber;
            this.capacity = capacity;
        }

        // Setters
        public void SetRoomId(int value)
        {
            roomId = value;
        }
        public void SetBuilding(string value)
        {
            building = value;
        }
        public void SetRoomNumber(string value)
        {
            roomNumber = value;
        }
        public void SetCapacity(int value)
        {
            capacity = value;
        }
        public void SetSchedule(List<ScheduleSlot> value)
        {
            schedule = value;
        }

        // Getters
        public int GetRoomId()
        {
            return roomId;
        }
        public string GetBuilding()
        {
            return building;
        }
        public string GetRoomNumber()
        {
            return roomNumber;
        }
        public int GetCapacity()
        {
            return capacity;
        }
        public List<ScheduleSlot> GetSchedule()
        {

            return schedule;
        }

        public bool IsAvailable(TimeSlot slot)
        {
            foreach (ScheduleSlot slot in schedule)
            {
                TimeSlot existing = slot.GetTimeSlot();

                if (existing.GetDay() == slot.GetDay())
                {
                    // if there is confilic in the time
                    if (existing.Overlaps(slot))
                        return false;
                }
            }

            return true;
        }


        public void AddScheduleSlot(ScheduleSlot slot)
        {
            if (slot != null)
            {
                schedule.Add(slot);
            }
        }
        // to display class data
        public override string ToString()
        {
            return $"Room ID: {roomId}, Building: {building}, Room Number: {roomNumber}, Capacity: {capacity}, Scheduled Slots: {schedule.Count}";
        }

    }
}