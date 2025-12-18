using Microsoft.Extensions.DependencyInjection;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer
{
    public static class BusinessLayerExtension
    {
        public static IServiceCollection AddBusinessServices(this IServiceCollection services)
        {
            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<IScheduleSlotService, ScheduleSlotService>();
            services.AddScoped<ISectionService, SectionService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ITimeSlotService, TimeSlotService>();
            services.AddScoped<IInstructorService, InstructorService>();
            services.AddScoped<IEnrollmentService, EnrollmentService>();
            services.AddScoped<ITranscriptService, TranscriptService>();
            services.AddScoped<IAcademicPlanService, AcademicPlanService>();
            services.AddScoped<IStudentProfileService, StudentProfileService>();



            return services;
        }
    }
}
