using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YourProject.DAL.Entities
{
    public class Enrollment
    {
        
        public Status Status {get;set;}
        
        [Key] [ForeignKey('Student')]
        public Student StudentId{ get; set; }

        [Key] [ForeignKey('Section')]
        public Section SectionId{ get; set; }

        // Operations
        
        public void ChangeStatus(Status newStatus)
        {
            Status = newStatus;
        }


        public bool IsActive()
        {
            return Status == Status.Enrolled;
        }


    public void Drop()
    {
        Status = Status.Dropped;
    }

    }
}
