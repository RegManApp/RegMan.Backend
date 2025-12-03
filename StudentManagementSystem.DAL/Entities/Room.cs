using System.ComponentModel.DataAnnotations;

namespace StudentManagementSystem.Entities
{
    public class Room
    {
        [Key]
        [Required] public int roomId { get; set; }

        [Required] public string building { get; set; } = null!;

        [Required] public string roomNumber { get; set; } = null!;

        [Required] public int capacity { get; set; }

        public List<ScheduleSlot> schedule { get; set; } = new();

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