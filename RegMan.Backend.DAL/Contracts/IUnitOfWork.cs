using Microsoft.EntityFrameworkCore;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.DAL.Contracts
{
    public interface IUnitOfWork
    {
        DbContext Context { get; }

        IBaseRepository<Course> Courses { get; }
        IBaseRepository<Room> Rooms { get; }
        IBaseRepository<TimeSlot> TimeSlots { get; }
        IBaseRepository<Section> Sections { get; }
        IBaseRepository<ScheduleSlot> ScheduleSlots { get; }
        IBaseRepository<OfficeHour> OfficeHours { get; }
        IBaseRepository<Enrollment> Enrollments { get; }
        IMessageRepository Messages { get; }
        IConversationRepository Conversations { get; }
        IBaseRepository<ConversationParticipant> ConversationParticipants { get; }
        IBaseRepository<MessageUserDeletion> MessageUserDeletions { get; }
        IBaseRepository<AdminProfile> AdminProfiles { get; }
        IBaseRepository<InstructorProfile> InstructorProfiles { get; }
        IBaseRepository<StudentProfile> StudentProfiles { get; }
        IBaseRepository<AcademicPlan> AcademicPlans { get; }
        IBaseRepository<AcademicPlanCourse> AcademicPlanCourses { get; }
        IBaseRepository<Transcript> Transcripts { get; }

        public IBaseRepository<Cart> Carts { get; }
        public IBaseRepository<CartItem> CartItems { get; }

        IBaseRepository<AcademicCalendarSettings> AcademicCalendarSettings { get; }
        IBaseRepository<WithdrawRequest> WithdrawRequests { get; }

        IBaseRepository<RefreshToken> RefreshTokens { get; }


        Task SaveChangesAsync();
    }
}
