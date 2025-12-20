using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.EnrollmentDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services;

internal class EnrollmentService : IEnrollmentService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditLogService auditLogService;

    public EnrollmentService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService)
    {
        this.unitOfWork = unitOfWork;
        this.auditLogService = auditLogService;
    }

    // =====================================
    // Student: Enroll from Cart
    // =====================================
    public async Task EnrollFromCartAsync(string studentUserId)
    {
        var student = await unitOfWork.StudentProfiles
            .GetAllAsQueryable()
            .Include(s => s.Cart)
                .ThenInclude(c => c.CartItems)
                    .ThenInclude(ci => ci.ScheduleSlot)
                        .ThenInclude(ss => ss.Section)
            .FirstOrDefaultAsync(s => s.UserId == studentUserId)
            ?? throw new Exception("Student not found");

        if (student.Cart == null || !student.Cart.CartItems.Any())
            throw new Exception("Cart is empty");

        foreach (var item in student.Cart.CartItems)
        {
            await EnrollInternal(student, item.ScheduleSlot.SectionId);
        }

        student.Cart.CartItems.Clear();
        await unitOfWork.SaveChangesAsync();
    }

    // =====================================
    // Admin: Force Enroll
    // =====================================
    public async Task ForceEnrollAsync(string studentUserId, int sectionId)
    {
        var student = await unitOfWork.StudentProfiles
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(s => s.UserId == studentUserId)
            ?? throw new Exception("Student not found");

        await EnrollInternal(student, sectionId);

        await auditLogService.LogAsync(
            "SYSTEM",
            "admin@system",
            "FORCE_ENROLL",
            "Enrollment",
            $"{studentUserId}:{sectionId}"
        );
    }

    // =====================================
    // Shared Logic
    // =====================================
    private async Task EnrollInternal(StudentProfile student, int sectionId)
    {
        var section = await unitOfWork.Sections
            .GetAllAsQueryable()
            .Include(s => s.Enrollments)
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.SectionId == sectionId)
            ?? throw new Exception("Section not found");

        // Check if already enrolled in this section
        if (section.Enrollments.Any(e => e.StudentId == student.StudentId &&
            (e.Status == Status.Enrolled || e.Status == Status.Pending)))
            throw new Exception("Student already enrolled in this section");

        // Check if already enrolled in another section of the same course
        var existingEnrollmentInCourse = await unitOfWork.Enrollments
            .GetAllAsQueryable()
            .Include(e => e.Section)
            .AnyAsync(e => e.StudentId == student.StudentId &&
                          e.Section.CourseId == section.CourseId &&
                          (e.Status == Status.Enrolled || e.Status == Status.Pending));

        if (existingEnrollmentInCourse)
            throw new Exception($"Student is already enrolled in another section of {section.Course?.CourseName ?? "this course"}");

        if (section.AvailableSeats <= 0)
            throw new Exception("No available seats");

        var enrollment = new Enrollment
        {
            StudentId = student.StudentId,
            SectionId = sectionId,
            Status = Status.Pending, // Start as pending, admin approves
            EnrolledAt = DateTime.UtcNow
        };

        section.AvailableSeats--;

        await unitOfWork.Enrollments.AddAsync(enrollment);
        await unitOfWork.SaveChangesAsync();

        await auditLogService.LogAsync(
            student.UserId,
            "student@system",
            "ENROLL",
            "Section",
            sectionId.ToString()
        );
    }

    // =====================================
    // Count All Enrollments
    // =====================================
    public async Task<int> CountAllAsync()
    {
        return await unitOfWork.Enrollments.GetAllAsQueryable().CountAsync();
    }

    // =====================================
    // Get Student Enrollments
    // =====================================
    public async Task<IEnumerable<ViewEnrollmentDTO>> GetStudentEnrollmentsAsync(string studentUserId)
    {
        var student = await unitOfWork.StudentProfiles
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(s => s.UserId == studentUserId)
            ?? throw new Exception("Student not found");

        var enrollments = await unitOfWork.Enrollments
            .GetAllAsQueryable()
            .Include(e => e.Section)
                .ThenInclude(s => s.Course)
            .Include(e => e.Section)
                .ThenInclude(s => s.Instructor)
                    .ThenInclude(i => i.User)
            .Where(e => e.StudentId == student.StudentId)
            .Select(e => new ViewEnrollmentDTO
            {
                EnrollmentId = e.EnrollmentId,
                SectionId = e.SectionId,
                StudentId = e.StudentId,
                EnrolledAt = e.EnrolledAt,
                Grade = e.Grade,
                Status = (int)e.Status,
                CourseId = e.Section != null && e.Section.Course != null ? e.Section.Course.CourseId : 0,
                CourseName = e.Section != null && e.Section.Course != null ? e.Section.Course.CourseName : null,
                CourseCode = e.Section != null && e.Section.Course != null ? e.Section.Course.CourseCode : null,
                CreditHours = e.Section != null && e.Section.Course != null ? e.Section.Course.CreditHours : 0,
                SectionName = e.Section != null ? e.Section.SectionName : null,
                Semester = e.Section != null ? e.Section.Semester : null,
                InstructorName = e.Section != null && e.Section.Instructor != null && e.Section.Instructor.User != null
                    ? e.Section.Instructor.User.FullName
                    : null,
                DeclineReason = e.DeclineReason,
                ApprovedBy = e.ApprovedBy,
                ApprovedAt = e.ApprovedAt
            })
            .ToListAsync();

        return enrollments;
    }
}
