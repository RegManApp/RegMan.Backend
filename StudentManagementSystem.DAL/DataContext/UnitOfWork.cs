using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.DAL.Repositories;

namespace StudentManagementSystem.DAL.DataContext
{
    internal class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext dbcontext;
        //private readonly UserManager<BaseUser> userManager;
        private Lazy<IBaseRepository<AcademicPlan>> academicPlans { get; set; }
        private Lazy<IBaseRepository<AcademicPlanCourse>> academicPlanCourses { get; set; }
        private Lazy<IBaseRepository<OfficeHour>> officeHours { get; set; }
        private Lazy<IBaseRepository<Transcript>> transcripts { get; set; }
        private Lazy<IBaseRepository<CartItem>> cartItems { get; set; }
        private Lazy<IBaseRepository<Cart>> carts { get; set; }
        private Lazy<IBaseRepository<AdminProfile>> adminProfiles { get; set; }
        private Lazy<IBaseRepository<InstructorProfile>> instructorProfiles { get; set; }
        private Lazy<IBaseRepository<StudentProfile>> studentProfiles { get; set; }
        private Lazy<IBaseRepository<Course>> courses { get; set; }
        private Lazy<IBaseRepository<Enrollment>> enrollments { get; set; }
        private Lazy<IBaseRepository<Room>> rooms { get; set; }
        private Lazy<IBaseRepository<ScheduleSlot>> scheduleSlots { get; set; }
        private Lazy<IBaseRepository<Section>> sections { get; set; }
        private Lazy<IBaseRepository<TimeSlot>> timeSlots { get; set; }
        private Lazy<IBaseRepository<RefreshToken>> refreshTokens;

        public UnitOfWork(AppDbContext context)
        {
            dbcontext = context;
            //this.userManager = userManager;
            officeHours = new Lazy<IBaseRepository<OfficeHour>>(() => new BaseRepository<OfficeHour>(dbcontext));
            academicPlans = new Lazy<IBaseRepository<AcademicPlan>>(() => new BaseRepository<AcademicPlan>(dbcontext));
            academicPlanCourses = new Lazy<IBaseRepository<AcademicPlanCourse>>(() => new BaseRepository<AcademicPlanCourse>(dbcontext));
            transcripts = new Lazy<IBaseRepository<Transcript>>(() => new BaseRepository<Transcript>(dbcontext));
            cartItems = new Lazy<IBaseRepository<CartItem>>(() => new BaseRepository<CartItem>(dbcontext));
            carts = new Lazy<IBaseRepository<Cart>>(() => new BaseRepository<Cart>(dbcontext));
            adminProfiles = new Lazy<IBaseRepository<AdminProfile>>(() => new BaseRepository<AdminProfile>(dbcontext));
            instructorProfiles = new Lazy<IBaseRepository<InstructorProfile>>(() => new BaseRepository<InstructorProfile>(dbcontext));
            studentProfiles = new Lazy<IBaseRepository<StudentProfile>>(() => new BaseRepository<StudentProfile>(dbcontext));
            courses = new Lazy<IBaseRepository<Course>>(() => new BaseRepository<Course>(dbcontext));
            rooms = new Lazy<IBaseRepository<Room>>(() => new BaseRepository<Room>(dbcontext));
            enrollments = new Lazy<IBaseRepository<Enrollment>>(() => new BaseRepository<Enrollment>(dbcontext));
            scheduleSlots = new Lazy<IBaseRepository<ScheduleSlot>>(() => new BaseRepository<ScheduleSlot>(dbcontext));
            sections = new Lazy<IBaseRepository<Section>>(() => new BaseRepository<Section>(dbcontext));
            timeSlots = new Lazy<IBaseRepository<TimeSlot>>(() => new BaseRepository<TimeSlot>(dbcontext));
            refreshTokens = new Lazy<IBaseRepository<RefreshToken>>(
    () => new BaseRepository<RefreshToken>(dbcontext)
);

        }

        public IBaseRepository<Course> Courses => courses.Value;
        public IBaseRepository<Cart> Carts => carts.Value;
        public IBaseRepository<CartItem> CartItems => cartItems.Value;
        public IBaseRepository<Room> Rooms => rooms.Value;
        public IBaseRepository<TimeSlot> TimeSlots => timeSlots.Value;
        public IBaseRepository<Section> Sections => sections.Value;
        public IBaseRepository<ScheduleSlot> ScheduleSlots => scheduleSlots.Value;
        public IBaseRepository<OfficeHour> OfficeHours => officeHours.Value;
        public IBaseRepository<Transcript> Transcripts => transcripts.Value;
        public IBaseRepository<Enrollment> Enrollments => enrollments.Value;
        public IBaseRepository<AdminProfile> AdminProfiles => adminProfiles.Value;
        public IBaseRepository<InstructorProfile> InstructorProfiles => instructorProfiles.Value;
        public IBaseRepository<StudentProfile> StudentProfiles => studentProfiles.Value;
        public IBaseRepository<AcademicPlan> AcademicPlans => academicPlans.Value;
        public IBaseRepository<AcademicPlanCourse> AcademicPlanCourses => academicPlanCourses.Value;
        public IBaseRepository<RefreshToken> RefreshTokens => refreshTokens.Value;


        public async Task SaveChangesAsync()
        {
            await dbcontext.SaveChangesAsync();
        }
    }
}
