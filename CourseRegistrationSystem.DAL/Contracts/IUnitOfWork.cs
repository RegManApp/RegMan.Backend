using CourseRegistrationSystem.DAL.DataContext;
using Microsoft.AspNetCore.Server.Kestrel.Core.Adapter.Internal;
using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Contracts
{
    public interface IUnitOfWork
    {
       

        public IBaseRepository<Course> Courses { get; }
        public IBaseRepository<Room> Rooms { get; }
        public IBaseRepository<TimeSlot> TimeSlots { get; }
        public IBaseRepository<Section> Sections { get; }
        public IBaseRepository<ScheduleSlot> ScheduleSlots { get; }
        public IBaseRepository<Transcript> Transcripts { get; }
        public IBaseRepository<Enrollment> Enrollments { get; }
        public IBaseRepository<AdminProfile> AdminProfiles { get; }
        public IBaseRepository<InstructorProfile> InstructorProfiles { get; }
        public IBaseRepository<StudentProfile> StudentProfiles { get; }
        public IBaseRepository<AcademicPlan> AcademicPlans { get; }
        Task SaveChangesAsync();
    }
}
