using Microsoft.Extensions.DependencyInjection;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer
{
    public static class BusinessLayerExtension
    {
        public static IServiceCollection AddBusinessServices(this IServiceCollection services)
        {
            services.AddScoped<ICourseService, CourseService>();
            services.AddScoped<IScheduleSlotService, ScheduleSlotService>();
            services.AddScoped<ISectionService, SectionService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<ITimeSlotService, TimeSlotService>();
            services.AddScoped<IInstructorService, InstructorService>();
            services.AddScoped<IEnrollmentService, EnrollmentService>();
            services.AddScoped<ITranscriptService, TranscriptService>();
            services.AddScoped<IAcademicPlanService, AcademicPlanService>();
            services.AddScoped<IStudentProfileService, StudentProfileService>();
            services.AddScoped<IOfficeHoursService, OfficeHoursService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAnnouncementsService, AnnouncementsService>();
            services.AddScoped<IGoogleCalendarIntegrationService, GoogleCalendarIntegrationService>();

            services.AddScoped<ICalendarViewService, CalendarViewService>();

            services.AddScoped<ICalendarPreferencesService, CalendarPreferencesService>();
            services.AddScoped<IReminderRulesService, ReminderRulesService>();
            services.AddScoped<ICalendarReminderEngine, CalendarReminderEngine>();
            services.AddScoped<IAcademicCalendarAuditService, AcademicCalendarAuditService>();



            return services;
        }
    }
}

