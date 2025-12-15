using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.DAL.Contracts
{
    public interface IUnitOfWork
    {


        public IBaseRepository<Course> Courses { get; }
        public IBaseRepository<Cart> Carts { get; }
        public IBaseRepository<Room> Rooms { get; }
        public IBaseRepository<TimeSlot> TimeSlots { get; }
        public IBaseRepository<Section> Sections { get; }
        public IBaseRepository<ScheduleSlot> ScheduleSlots { get; }
        //public IBaseRepository<Transcript> Transcripts { get; }
        public IBaseRepository<Enrollment> Enrollments { get; }
        public IBaseRepository<AdminProfile> AdminProfiles { get; }
        public IBaseRepository<InstructorProfile> InstructorProfiles { get; }
        public IBaseRepository<StudentProfile> StudentProfiles { get; }
        public IBaseRepository<AcademicPlan> AcademicPlans { get; }
        Task SaveChangesAsync();
    }
}
