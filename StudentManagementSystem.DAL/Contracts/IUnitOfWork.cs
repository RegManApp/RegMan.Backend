using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.DAL.Contracts
{
    public interface IUnitOfWork
    {
        IBaseRepository<Course> Courses { get; }
        IBaseRepository<Room> Rooms { get; }
        IBaseRepository<TimeSlot> TimeSlots { get; }
        IBaseRepository<Section> Sections { get; }
        IBaseRepository<ScheduleSlot> ScheduleSlots { get; }
        IBaseRepository<Enrollment> Enrollments { get; }

        IBaseRepository<AdminProfile> AdminProfiles { get; }
        IBaseRepository<InstructorProfile> InstructorProfiles { get; }
        IBaseRepository<StudentProfile> StudentProfiles { get; }
        IBaseRepository<AcademicPlan> AcademicPlans { get; }

        public IBaseRepository<Cart> Carts { get; }
        public IBaseRepository<CartItem> CartItems { get; }

        IBaseRepository<RefreshToken> RefreshTokens { get; }


        Task SaveChangesAsync();
    }
}
