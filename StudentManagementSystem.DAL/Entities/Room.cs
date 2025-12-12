using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.DAL.Entities
{
    public class Room
    {
        [Key]
        [Required] public int RoomId { get; set; }

        [Required] public string Building { get; set; } = null!;

        [Required] public string RoomNumber { get; set; } = null!;

        [Required] public int Capacity { get; set; }

        public List<ScheduleSlot> Schedule { get; set; } = new();

        // public Room() { }

        // public Room(int roomId, string building, string roomNumber, int capacity)
        // {
        //     this.roomId = roomId;
        //     this.building = building;
        //     this.roomNumber = roomNumber;
        //     this.capacity = capacity;
        // }
        // public bool IsAvailable(TimeSlot slot)
        // {
        //     foreach (ScheduleSlot slot in schedule)
        //     {
        //         TimeSlot existing = slot.GetTimeSlot();

        //         if (existing.GetDay() == slot.GetDay())
        //         {
        //             // if there is confilic in the time
        //             if (existing.Overlaps(slot))
        //                 return false;
        //         }
        //     }

        //     return true;
        // }

        // public void AddScheduleSlot(ScheduleSlot slot)
        // {
        //     if (slot != null)
        //     {
        //         schedule.Add(slot);
        //     }
        // }
        // // to display class data
        // public override string ToString()
        // {
        //     return $"Room ID: {roomId}, Building: {building}, Room Number: {roomNumber}, Capacity: {capacity}, Scheduled Slots: {schedule.Count}";
        // }
    }
}