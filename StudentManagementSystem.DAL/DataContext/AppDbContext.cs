using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.DAL.DataContext
{
    public class AppDbContext : IdentityDbContext<BaseUser>
    {
        public DbSet<Course> Courses { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<OfficeHour> OfficeHours { get; set; }
        public DbSet<OfficeHourBooking> OfficeHourBookings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Section> Sections { get; set; }

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

            // ============================
            // 1. ONE-TO-ONE RELATIONSHIPS
            // ============================

            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.User)
                .WithOne(u => u.StudentProfile)
                .HasForeignKey<StudentProfile>(sp => sp.UserId);

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
            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Section)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

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
            // CartItem → ScheduleSlot (MANY-TO-ONE)
            // ============================

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.ScheduleSlot)
                .WithMany()
                .HasForeignKey(ci => ci.ScheduleSlotId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .HasIndex(ohb => new { ohb.OfficeHourId, ohb.StudentId })
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
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OfficeHour>()
                .HasOne(oh => oh.Instructor)
                .WithMany(i => i.OfficeHours)
                .HasForeignKey(oh => oh.InstructorId)
                .OnDelete(DeleteBehavior.Cascade);

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
