using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.DAL.Entities;
using RegMan.Backend.DAL.Entities.Calendar;
using RegMan.Backend.DAL.Entities.Integrations;

namespace RegMan.Backend.DAL.DataContext
{
    public class AppDbContext : IdentityDbContext<BaseUser>
    {
        public DbSet<Course> Courses { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<OfficeHour> OfficeHours { get; set; }
        public DbSet<OfficeHourBooking> OfficeHourBookings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AcademicCalendarSettings> AcademicCalendarSettings { get; set; }
        public DbSet<WithdrawRequest> WithdrawRequests { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<MessageUserDeletion> MessageUserDeletions { get; set; }
        public DbSet<GoogleCalendarUserToken> GoogleCalendarUserTokens { get; set; }

        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<AnnouncementRecipient> AnnouncementRecipients { get; set; }

        public DbSet<GoogleCalendarEventLink> GoogleCalendarEventLinks { get; set; }

        public DbSet<UserCalendarPreferences> UserCalendarPreferences { get; set; }
        public DbSet<UserReminderRule> UserReminderRules { get; set; }
        public DbSet<ScheduledNotification> ScheduledNotifications { get; set; }
        public DbSet<CalendarAuditEntry> CalendarAuditEntries { get; set; }

        // public DbSet<BaseUser> Users { get; set; }

        public DbSet<AdminProfile> Admins { get; set; }
        public DbSet<StudentProfile> Students { get; set; }
        public DbSet<InstructorProfile> Instructors { get; set; }

        public DbSet<ScheduleSlot> ScheduleSlots { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<AcademicPlan> AcademicPlans { get; set; }
        public DbSet<AcademicPlanCourse> AcademicPlanCourses { get; set; }
        public DbSet<Transcript> Transcripts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BaseUser>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // ============================
            // 1. ONE-TO-ONE RELATIONSHIPS
            // ============================

            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.User)
                .WithOne(u => u.StudentProfile)
                .HasForeignKey<StudentProfile>(sp => sp.UserId);

            modelBuilder.Entity<GoogleCalendarUserToken>()
                .HasOne(t => t.User)
                .WithOne()
                .HasForeignKey<GoogleCalendarUserToken>(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserCalendarPreferences>()
                .HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserCalendarPreferences>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GoogleCalendarEventLink>()
                .HasIndex(x => new { x.UserId, x.SourceEntityType, x.SourceEntityId })
                .IsUnique();

            modelBuilder.Entity<ScheduledNotification>()
                .HasIndex(x => new { x.UserId, x.TriggerType, x.SourceEntityType, x.SourceEntityId, x.ScheduledAtUtc })
                .IsUnique(false);

            modelBuilder.Entity<AdminProfile>()
                .HasOne(ap => ap.User)
                .WithOne(u => u.AdminProfile)
                .HasForeignKey<AdminProfile>(ap => ap.UserId);

            modelBuilder.Entity<InstructorProfile>()
                .HasOne(ip => ip.User)
                .WithOne(u => u.InstructorProfile)
                .HasForeignKey<InstructorProfile>(ip => ip.UserId);

            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.AcademicPlan)
                .WithMany(ap => ap.Students)
                .HasForeignKey(sp => sp.AcademicPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConversationParticipant>()
              .HasKey(cp => new { cp.ConversationId, cp.UserId });

            modelBuilder.Entity<ConversationParticipant>()
                .HasOne(cp => cp.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => cp.ConversationId);
            // ============================
            // COURSE → SECTION CASCADE DELETE
            // ============================

            // Course → Section (ONE-TO-MANY with CASCADE)
            modelBuilder.Entity<Section>()
                .HasOne(s => s.Course)
                .WithMany()
                .HasForeignKey(s => s.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================
            // 2. ONE-TO-MANY RELATIONSHIPS
            // ============================

            modelBuilder.Entity<ConversationParticipant>()
                .HasOne(cp => cp.User)
                .WithMany()
                .HasForeignKey(cp => cp.UserId);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .Property(m => m.ServerReceivedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ConversationId, m.SentAt, m.MessageId });

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.ConversationId, m.SenderId, m.ClientMessageId })
                .IsUnique();

            modelBuilder.Entity<Conversation>()
                .HasIndex(c => c.LastActivityAt);

            modelBuilder.Entity<ConversationParticipant>()
                .HasIndex(cp => new { cp.UserId, cp.ConversationId });

            modelBuilder.Entity<MessageUserDeletion>()
                .HasKey(x => new { x.MessageId, x.UserId });

            modelBuilder.Entity<MessageUserDeletion>()
                .HasOne(x => x.Message)
                .WithMany()
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MessageUserDeletion>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MessageUserDeletion>()
                .HasIndex(x => new { x.UserId, x.MessageId });

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.CreatedByUser)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Course)
                .WithMany()
                .HasForeignKey(a => a.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Section)
                .WithMany()
                .HasForeignKey(a => a.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AnnouncementRecipient>()
                .HasOne(r => r.Announcement)
                .WithMany(a => a.Recipients)
                .HasForeignKey(r => r.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnnouncementRecipient>()
                .HasOne(r => r.RecipientUser)
                .WithMany()
                .HasForeignKey(r => r.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnnouncementRecipient>()
                .HasIndex(r => new { r.AnnouncementId, r.RecipientUserId })
                .IsUnique();

            modelBuilder.Entity<Announcement>()
                .HasIndex(a => new { a.CreatedAt, a.IsArchived });

            // ============================
            // 2. ONE-TO-MANY RELATIONSHIPS
            // ============================

            // Section → ScheduleSlot
            modelBuilder.Entity<ScheduleSlot>()
                    .HasOne(ss => ss.Section)
                    .WithMany(s => s.Slots)
                    .HasForeignKey(ss => ss.SectionId)
                    .OnDelete(DeleteBehavior.Cascade);

            // Room → ScheduleSlot
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.Room)
                .WithMany(r => r.ScheduleSlots)
                .HasForeignKey(ss => ss.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // TimeSlot → ScheduleSlot
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.TimeSlot)
                .WithMany()
                .HasForeignKey(ss => ss.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);

            // Instructor → ScheduleSlot  ✅ (Feature 11)
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.Instructor)
                .WithMany()
                .HasForeignKey(ss => ss.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Section → Enrollment
            // Section → Enrollment (CASCADE - when section deleted, enrollments are deleted)
            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Section)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            // StudentProfile → Enrollment
            modelBuilder.Entity<Enrollment>()
             .HasOne(e => e.Student)
             .WithMany(s => s.Enrollments)
             .HasForeignKey(e => e.StudentId)
             .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Cart>()
             .HasOne(c => c.StudentProfile)
             .WithOne(sp => sp.Cart)
             .HasForeignKey<Cart>(c => c.StudentProfileId)
             .OnDelete(DeleteBehavior.Cascade);
            // ============================
            // Cart → CartItem (ONE-TO-MANY)
            // ============================

            modelBuilder.Entity<Cart>()
                .HasMany(c => c.CartItems)
                .WithOne(ci => ci.Cart)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================
            // CartItem → ScheduleSlot (MANY-TO-ONE with CASCADE)
            // ============================

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.ScheduleSlot)
                .WithMany()
                .HasForeignKey(ci => ci.ScheduleSlotId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================
            // TRANSCRIPT RELATIONSHIPS
            // ============================

            // StudentProfile → Transcript (ONE-TO-MANY)
            modelBuilder.Entity<Transcript>()
                .HasOne(t => t.Student)
                .WithMany(s => s.Transcripts)
                .HasForeignKey(t => t.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Course → Transcript (ONE-TO-MANY)
            modelBuilder.Entity<Transcript>()
                .HasOne(t => t.Course)
                .WithMany()
                .HasForeignKey(t => t.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Section → Transcript (ONE-TO-MANY)
            modelBuilder.Entity<Transcript>()
                .HasOne(t => t.Section)
                .WithMany()
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================
            // ACADEMIC PLAN COURSE RELATIONSHIPS
            // ============================

            // AcademicPlan → AcademicPlanCourse (ONE-TO-MANY)
            modelBuilder.Entity<AcademicPlanCourse>()
                .HasOne(apc => apc.AcademicPlan)
                .WithMany(ap => ap.AcademicPlanCourses)
                .HasForeignKey(apc => apc.AcademicPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Course → AcademicPlanCourse (ONE-TO-MANY)
            modelBuilder.Entity<AcademicPlanCourse>()
                .HasOne(apc => apc.Course)
                .WithMany()
                .HasForeignKey(apc => apc.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================
            // 3. UNIQUE CONSTRAINTS
            // ============================

            // Unique constraint: CourseCode must be unique
            modelBuilder.Entity<Course>()
                .HasIndex(c => c.CourseCode)
                .IsUnique();

            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.SectionId })
                .IsUnique();
            // Prevent same ScheduleSlot from being added twice to same Cart
            modelBuilder.Entity<CartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ScheduleSlotId })
                .IsUnique();

            // Unique constraint: Student can only have one transcript entry per course
            modelBuilder.Entity<Transcript>()
                .HasIndex(t => new { t.StudentId, t.CourseId, t.SectionId })
                .IsUnique();

            // Unique constraint: Each course in academic plan should be unique
            modelBuilder.Entity<AcademicPlanCourse>()
                .HasIndex(apc => new { apc.AcademicPlanId, apc.CourseId })
                .IsUnique();

            // Unique constraint: Student can only book same office hour once
            modelBuilder.Entity<OfficeHourBooking>()
                .HasIndex(ohb => new { ohb.OfficeHourId, ohb.BookerUserId })
                .IsUnique();

            // Ensure there is only one active calendar settings row (by convention we use SettingsId=1)
            modelBuilder.Entity<AcademicCalendarSettings>()
                .HasIndex(s => s.SettingsKey)
                .IsUnique();

            // ============================
            // 4. OFFICE HOUR RELATIONSHIPS
            // ============================

            modelBuilder.Entity<OfficeHourBooking>()
                .HasOne(ohb => ohb.OfficeHour)
                .WithMany(oh => oh.Bookings)
                .HasForeignKey(ohb => ohb.OfficeHourId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OfficeHourBooking>()
                .HasOne(ohb => ohb.Student)
                .WithMany(s => s.OfficeHourBookings)
                .HasForeignKey(ohb => ohb.StudentId)
                .OnDelete(DeleteBehavior.Restrict); // Keep optional for back-compat

            modelBuilder.Entity<OfficeHourBooking>()
                .HasOne(ohb => ohb.BookerUser)
                .WithMany()
                .HasForeignKey(ohb => ohb.BookerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OfficeHour>()

                .HasOne(oh => oh.OwnerUser)
                .WithMany()
                .HasForeignKey(oh => oh.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OfficeHour>()
                .HasOne(oh => oh.Instructor)
                .WithMany(i => i.OfficeHours)
                .HasForeignKey(oh => oh.InstructorId)
                .OnDelete(DeleteBehavior.SetNull);

            // ============================
            // 5. NOTIFICATION RELATIONSHIPS
            // ============================

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================
            // 6. ENUM CONVERSIONS
            // ============================

            modelBuilder.Entity<Course>()
                .Property(c => c.CourseCategory)
                .HasConversion<string>();

            modelBuilder.Entity<Enrollment>()
                .Property(e => e.Status)
                .HasConversion<string>();

            modelBuilder.Entity<TimeSlot>()
                .Property(t => t.Day)
                .HasConversion<string>();

            modelBuilder.Entity<AcademicPlanCourse>()
                .Property(apc => apc.CourseType)
                .HasConversion<string>();

            modelBuilder.Entity<OfficeHour>()
                .Property(oh => oh.Status)
                .HasConversion<string>();

            modelBuilder.Entity<OfficeHourBooking>()
                .Property(ohb => ohb.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>();

        }
    }
}

