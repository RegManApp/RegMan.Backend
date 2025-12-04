using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.Entities;
//Update-Database -StartupProject StudentManagementSystem.API

namespace StudentManagementSystem.DAL.DataContext
{
    public class AppDbContext : IdentityDbContext<BaseUser>
    {
        public DbSet<Course> Courses { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<BaseUser> Users { get; set; }
        public DbSet<AdminProfile> Admins { get; set; }
        public DbSet<StudentProfile> Students { get; set; }
        public DbSet<InstructorProfile> Instructors { get; set; }
        public DbSet<ScheduleSlot> ScheduleSlots { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<AcademicPlan> AcademicPlans { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Important: Always call the base implementation for IdentityDbContext
            // Important: Always call the base implementation for IdentityDbContext
            base.OnModelCreating(modelBuilder);

            // --- 1. One-to-One Relationships (Dedicated Foreign Key Pattern) ---
            // This resolves the type incompatibility error by using the new 'UserId' (string) 
            // property as the Foreign Key for all profile entities.

            // StudentProfile (Dependent) to BaseUser (Principal)
            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.User)             // StudentProfile has one BaseUser
                .WithOne(u => u.StudentProfile)    // BaseUser has one StudentProfile
                .HasForeignKey<StudentProfile>(sp => sp.UserId); // Uses the dedicated string FK

            // AdminProfile (Dependent) to BaseUser (Principal)
            modelBuilder.Entity<AdminProfile>()
                .HasOne(ap => ap.User)
                .WithOne(u => u.AdminProfile)
                .HasForeignKey<AdminProfile>(ap => ap.UserId);

            // InstructorProfile (Dependent) to BaseUser (Principal)
            modelBuilder.Entity<InstructorProfile>()
                .HasOne(ip => ip.User)
                .WithOne(u => u.InstructorProfile)
                .HasForeignKey<InstructorProfile>(ip => ip.UserId);

            // --- 2. Many-to-One Relationships ---

            // StudentProfile to AcademicPlan
            // Configures the relationship using AcademicPlanId as the FK.
            modelBuilder.Entity<StudentProfile>()
                .HasOne(sp => sp.AcademicPlan)
                .WithMany()
                .HasForeignKey(sp => sp.AcademicPlanId);

            // ScheduleSlot - Section relationship (1:M)
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.section)
                .WithMany(s => s.Slots);

            // ScheduleSlot - Room relationship (1:M)
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.room)
                .WithMany(r => r.schedule);

            // ScheduleSlot - TimeSlot relationship (1:M)
            modelBuilder.Entity<ScheduleSlot>()
                .HasOne(ss => ss.timeSlot)
                .WithMany();

            // --- 3. Unique Constraints and Indexes ---

            // Enrollment Composite Index (Prevents a student from enrolling in the same section twice)
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.SectionId })
                .IsUnique();

            // --- 4. Enum Conversions (Recommended) ---

            // Convert Enums to strings in the database for better readability and portability.
            modelBuilder.Entity<Course>()
                .Property(c => c.CourseCategory)
                .HasConversion<string>();

            modelBuilder.Entity<Enrollment>()
                .Property(e => e.Status)
                .HasConversion<string>();

            modelBuilder.Entity<TimeSlot>()
                .Property(t => t.day);
            
        }

    }
    
}
