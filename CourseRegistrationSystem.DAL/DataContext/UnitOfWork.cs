using CourseRegistrationSystem.DAL.DataContext;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.DAL.Repositories;
using StudentManagementSystem.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.DataContext
{
    internal class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext dbcontext;
        private Lazy<IBaseRepository<AcademicPlan>> academicPlans { get; set; }
        private Lazy<IBaseRepository<AdminProfile>> adminProfiles { get; set; }
        private Lazy<IBaseRepository<InstructorProfile>> instructorProfiles { get; set; }
        private Lazy<IBaseRepository<StudentProfile>> studentProfiles { get; set; }
        private Lazy<IBaseRepository<Course>> courses { get; set; }
        private Lazy<IBaseRepository<Enrollment>> enrollments { get; set; }
        public Lazy<IBaseRepository<Room>> rooms { get; set; }
        public Lazy<IBaseRepository<ScheduleSlot>> scheduleSlots { get; set; }
        public Lazy<IBaseRepository<Section>> sections { get; set; }
        public Lazy<IBaseRepository<TimeSlot>> timeSlots { get; set; }
        public Lazy<IBaseRepository<Transcript>> transcripts { get; set; }
        public UnitOfWork(AppDbContext context)
        {
            dbcontext = context;
            academicPlans = new Lazy<IBaseRepository<AcademicPlan>>(() => new BaseRepository<AcademicPlan>(dbcontext));
            adminProfiles = new Lazy<IBaseRepository<AdminProfile>>(() => new BaseRepository<AdminProfile>(dbcontext));
            instructorProfiles = new Lazy<IBaseRepository<InstructorProfile>>(() => new BaseRepository<InstructorProfile>(dbcontext));
            studentProfiles = new Lazy<IBaseRepository<StudentProfile>>(() => new BaseRepository<StudentProfile>(dbcontext));
            courses = new Lazy<IBaseRepository<Course>>(() => new BaseRepository<Course>(dbcontext));
            rooms = new Lazy<IBaseRepository<Room>>(()=> new BaseRepository<Room>(dbcontext)));
            enrollments = new Lazy<IBaseRepository<Enrollment>>(() => new BaseRepository<Enrollment>(dbcontext));
            scheduleSlots = new Lazy<IBaseRepository<ScheduleSlot>>(() => new BaseRepository<ScheduleSlot>(dbcontext));
            sections = new Lazy<IBaseRepository<Section>>(() => new BaseRepository<Section>(dbcontext));
            timeSlots = new Lazy<IBaseRepository<TimeSlot>>(() => new BaseRepository<TimeSlot>(dbcontext));
            transcripts = new Lazy<IBaseRepository<Transcript>>(() => new BaseRepository<Transcript>(dbcontext));
        }
        public IBaseRepository<Course> Courses => courses.Value;
        public IBaseRepository<Room> Rooms => rooms.Value;
        public IBaseRepository<TimeSlot> TimeSlots => timeSlots.Value;
        public IBaseRepository<Section> Sections => sections.Value;
        public IBaseRepository<ScheduleSlot> ScheduleSlots => scheduleSlots.Value;
        public IBaseRepository<Transcript> Transcripts => transcripts.Value;
        public IBaseRepository<Enrollment> Enrollments => enrollments.Value; 
        public IBaseRepository<AdminProfile> AdminProfiles => adminProfiles.Value;
        public IBaseRepository<InstructorProfile> InstructorProfiles => instructorProfiles.Value;
        public IBaseRepository<StudentProfile> StudentProfiles => studentProfiles.Value;
        public IBaseRepository<AcademicPlan> AcademicPlans => academicPlans.Value;

        public void SaveChanges()
        {
            dbcontext.SaveChanges();
        }
    }
}
