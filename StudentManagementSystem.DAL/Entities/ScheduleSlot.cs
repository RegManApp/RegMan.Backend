using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagementSystem.DAL.Entities
{
    public class ScheduleSlot
    {
        [Key]
        [Required] public int ScheduleSlotId { get; set; }

        //[Required]
        public Section Section { get; set; } = null!;
        //[Required]
        public Room Room { get; set; } = null!;
        //[Required]
        public TimeSlot TimeSlot { get; set; } = null!;
        public int SectionId { get; set; }
        public int RoomId { get; set; }
        public int TimeSlotId { get; set; }
        [Required]
        public SlotType SlotType { get; set; } = SlotType.Lecture; //lecture by default
        // public ScheduleSlot() { }

        // public ScheduleSlot(int id, Section section, Room room, TimeSlot timeSlot)
        // {
        //     this.scheduleSlotId = id;
        //     this.section = section;
        //     this.room = room;
        //     this.timeSlot = timeSlot;
        // }

        // public bool ConflictWith(ScheduleSlot slot)
        // {
        //     if (this.room.GetRoomId() != slot.GetRoom().GetRoomId())
        //         return false;

        //     return this.timeSlot.Overlaps(slot.GetTimeSlot());
        // }

        // // to display class info
        // public override string ToString()
        // {
        //     string sectionInfo;
        //     string roomInfo;
        //     string timeInfo;

        //     if (Section != null)
        //     {
        //         sectionInfo = "Section ID: " + Section.GetSectionId();
        //     }
        //     else
        //     {
        //         sectionInfo = "Section: N/A";
        //     }

        //     if (Room != null)
        //     {
        //         roomInfo = "Room ID: " + Room.GetRoomId();
        //     }
        //     else
        //     {
        //         roomInfo = "Room: N/A";
        //     }

        //     if (TimeSlot != null)
        //     {
        //         timeInfo = "Time: " + TimeSlot.ToString();
        //     }
        //     else
        //     {
        //         timeInfo = "Time: N/A";
        //     }

        //     return sectionInfo + ", " + roomInfo + ", " + timeInfo;
        // }

    }
    public enum SlotType 
    {
        Lecture,
        Lab,
        Tutorial
    }
}
