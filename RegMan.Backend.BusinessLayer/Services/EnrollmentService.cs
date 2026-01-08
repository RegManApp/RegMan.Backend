using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.CartDTOs;
using RegMan.Backend.BusinessLayer.DTOs.EnrollmentDTOs;
using RegMan.Backend.BusinessLayer.Exceptions;
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
        var student = await LoadStudentWithCartAsync(studentUserId);
        var cartItems = student.Cart?.CartItems;

        if (cartItems == null || cartItems.Count == 0)
            throw new BadRequestException("Cart is empty");

        // Ensure enroll and checkout run the same validations.
        await ValidateCartOrThrowAsync(student, cartItems);

        foreach (var item in cartItems)
            await EnrollInternalAsync(student, item.ScheduleSlot.SectionId);

        student.Cart!.CartItems.Clear();
        await unitOfWork.SaveChangesAsync();
    }

    // =====================================
    // Student: Checkout (validation only)
    // =====================================
    public async Task<CartCheckoutValidationDTO> ValidateCheckoutFromCartAsync(string studentUserId)
    {
        var student = await LoadStudentWithCartAsync(studentUserId);
        var cart = student.Cart;
        var cartItems = cart?.CartItems;

        if (cart == null)
            throw new NotFoundException("Cart not found");

        if (cartItems == null || cartItems.Count == 0)
            throw new BadRequestException("Cart is empty");

        await ValidateCartOrThrowAsync(student, cartItems);

        var dto = new CartCheckoutValidationDTO
        {
            CartId = cart.CartId,
            ItemCount = cartItems.Count,
            ValidatedAtUtc = DateTime.UtcNow,
            Items = cartItems.Select(ci => new CartCheckoutValidationItemDTO
            {
                CartItemId = ci.CartItemId,
                ScheduleSlotId = ci.ScheduleSlotId,
                SectionId = ci.ScheduleSlot.SectionId,
                CourseId = ci.ScheduleSlot.Section.CourseId,
                CourseCode = ci.ScheduleSlot.Section.Course?.CourseCode ?? string.Empty,
                CourseName = ci.ScheduleSlot.Section.Course?.CourseName ?? string.Empty,
                Semester = ci.ScheduleSlot.Section.Semester,
                SeatsAvailable = ci.ScheduleSlot.Section.AvailableSeats > 0
            }).ToList()
        };

        return dto;
    }

    // =====================================
    // Admin: Force Enroll
    // =====================================
    public async Task ForceEnrollAsync(string studentUserId, int sectionId)
    {
        var student = await unitOfWork.StudentProfiles
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(s => s.UserId == studentUserId)
            ?? throw new NotFoundException("Student profile not found");

        await EnrollInternalAsync(student, sectionId);

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
    private async Task EnrollInternalAsync(StudentProfile student, int sectionId)
    {
        var section = await unitOfWork.Sections
            .GetAllAsQueryable()
            .Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.SectionId == sectionId)
            ?? throw new NotFoundException("Section not found");

        // Unique index exists on (StudentId, SectionId). Re-enroll MUST reuse the existing row.
        // Do NOT rely solely on navigation loading; query directly to avoid DbUpdateException -> generic 409.
        var existingEnrollmentInSection = await unitOfWork.Enrollments
            .GetAllAsQueryable()
            .FirstOrDefaultAsync(e => e.StudentId == student.StudentId && e.SectionId == sectionId);

        if (existingEnrollmentInSection != null)
        {
            if (existingEnrollmentInSection.Status == Status.Enrolled || existingEnrollmentInSection.Status == Status.Pending)
                throw new ConflictException("Student already enrolled in this section");

            if (existingEnrollmentInSection.Status == Status.Dropped || existingEnrollmentInSection.Status == Status.Declined)
            {
                if (section.AvailableSeats <= 0)
                    throw new ConflictException("No available seats");

                // Reactivate the same enrollment record.
                existingEnrollmentInSection.Status = Status.Pending;
                existingEnrollmentInSection.EnrolledAt = DateTime.UtcNow;
                existingEnrollmentInSection.Grade = null;
                existingEnrollmentInSection.DeclineReason = null;
                existingEnrollmentInSection.ApprovedBy = null;
                existingEnrollmentInSection.ApprovedAt = null;

                section.AvailableSeats--;
                await unitOfWork.SaveChangesAsync();

                await auditLogService.LogAsync(
                    student.UserId,
                    "student@system",
                    "REENROLL",
                    "Section",
                    sectionId.ToString()
                );

                return;
            }

            // Completed (or other terminal states) shouldn't silently DB-conflict.
            throw new ConflictException("Student already has an enrollment record for this section");
        }

        // Check if already enrolled in another section of the same course
        var db = unitOfWork.Context;
        var existingEnrollmentInCourse = await (
            from e in unitOfWork.Enrollments.GetAllAsQueryable().AsNoTracking()
            join s in db.Set<Section>().AsNoTracking() on e.SectionId equals s.SectionId
            where e.StudentId == student.StudentId
                  && s.CourseId == section.CourseId
                  && (e.Status == Status.Enrolled || e.Status == Status.Pending)
            select e.EnrollmentId)
            .AnyAsync();

        if (existingEnrollmentInCourse)
            throw new ConflictException($"Student is already enrolled in another section of {section.Course?.CourseName ?? "this course"}");

        if (section.AvailableSeats <= 0)
            throw new ConflictException("No available seats");

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

    private async Task<StudentProfile> LoadStudentWithCartAsync(string studentUserId)
    {
        var student = await unitOfWork.StudentProfiles
            .GetAllAsQueryable()
            .Include(s => s.Cart)
                .ThenInclude(c => c.CartItems)
                    .ThenInclude(ci => ci.ScheduleSlot)
                        .ThenInclude(ss => ss.TimeSlot)
            .Include(s => s.Cart)
                .ThenInclude(c => c.CartItems)
                    .ThenInclude(ci => ci.ScheduleSlot)
                        .ThenInclude(ss => ss.Section)
                            .ThenInclude(sec => sec.Course)
            .FirstOrDefaultAsync(s => s.UserId == studentUserId);

        if (student == null)
            throw new NotFoundException("Student profile not found");

        return student;
    }

    private static bool TimeOverlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
    {
        return aStart < bEnd && bStart < aEnd;
    }

    private async Task ValidateCartOrThrowAsync(StudentProfile student, ICollection<CartItem> cartItems)
    {
        var db = unitOfWork.Context;

        // Basic existence checks
        foreach (var item in cartItems)
        {
            if (item.ScheduleSlot == null)
                throw new BadRequestException("Cart contains an invalid schedule slot");

            if (item.ScheduleSlot.Section == null)
                throw new BadRequestException("Cart contains an invalid section");

            if (item.ScheduleSlot.TimeSlot == null)
                throw new BadRequestException("Cart contains an invalid timeslot");
        }

        // Seats + active enrollment validation (409 only for seats/already-enrolled)
        foreach (var item in cartItems)
        {
            var sectionId = item.ScheduleSlot.SectionId;
            var section = item.ScheduleSlot.Section;

            if (section.AvailableSeats <= 0)
                throw new ConflictException("No available seats");

            var alreadyEnrolledInSection = await unitOfWork.Enrollments
                .GetAllAsQueryable()
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == student.StudentId
                              && e.SectionId == sectionId
                              && (e.Status == Status.Enrolled || e.Status == Status.Pending));

            if (alreadyEnrolledInSection)
                throw new ConflictException("Student already enrolled in this section");

            var alreadyEnrolledInCourse = await (
                from e in unitOfWork.Enrollments.GetAllAsQueryable().AsNoTracking()
                join s in db.Set<Section>().AsNoTracking() on e.SectionId equals s.SectionId
                where e.StudentId == student.StudentId
                      && (e.Status == Status.Enrolled || e.Status == Status.Pending)
                      && s.CourseId == section.CourseId
                select e.EnrollmentId)
                .AnyAsync();

            if (alreadyEnrolledInCourse)
                throw new ConflictException($"Student is already enrolled in another section of {section.Course?.CourseName ?? "this course"}");
        }

        // Schedule conflict validation (400)
        var activeEnrollments = await unitOfWork.Enrollments
            .GetAllAsQueryable()
            .AsNoTracking()
            .Include(e => e.Section)
                .ThenInclude(s => s!.Slots)
                    .ThenInclude(sl => sl.TimeSlot)
            .Include(e => e.Section)
                .ThenInclude(s => s!.Course)
            .Where(e => e.StudentId == student.StudentId && (e.Status == Status.Enrolled || e.Status == Status.Pending))
            .ToListAsync();

        foreach (var cartItem in cartItems)
        {
            var cartTime = cartItem.ScheduleSlot.TimeSlot;
            var cartCourse = cartItem.ScheduleSlot.Section.Course;
            var cartCourseName = cartCourse?.CourseName ?? "this course";

            // Conflicts with active enrollments (consider all slots in the enrolled section)
            foreach (var enrollment in activeEnrollments)
            {
                var sectionSlots = enrollment.Section?.Slots ?? Array.Empty<ScheduleSlot>();
                foreach (var slot in sectionSlots)
                {
                    if (slot.TimeSlot == null) continue;
                    if (slot.TimeSlot.Day != cartTime.Day) continue;

                    if (TimeOverlaps(slot.TimeSlot.StartTime, slot.TimeSlot.EndTime, cartTime.StartTime, cartTime.EndTime))
                    {
                        var enrolledCourseName = enrollment.Section?.Course?.CourseName ?? "an enrolled course";
                        throw new BadRequestException($"Schedule conflict between {cartCourseName} and {enrolledCourseName}");
                    }
                }
            }
        }

        // Conflicts within cart itself (400)
        var cartList = cartItems.ToList();
        for (var i = 0; i < cartList.Count; i++)
        {
            var a = cartList[i].ScheduleSlot.TimeSlot;
            for (var j = i + 1; j < cartList.Count; j++)
            {
                var b = cartList[j].ScheduleSlot.TimeSlot;
                if (a.Day != b.Day) continue;
                if (TimeOverlaps(a.StartTime, a.EndTime, b.StartTime, b.EndTime))
                    throw new BadRequestException("Schedule conflict within cart");
            }
        }
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
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == studentUserId)
            ;

        if (student == null)
            return Array.Empty<ViewEnrollmentDTO>();

        var db = unitOfWork.Context;

        var rows = await (
            from e in unitOfWork.Enrollments.GetAllAsQueryable().AsNoTracking()
            join s in db.Set<Section>().AsNoTracking() on e.SectionId equals s.SectionId
            join c in db.Set<Course>().AsNoTracking() on s.CourseId equals c.CourseId
            where e.StudentId == student.StudentId
            select new { Enrollment = e, Section = s, Course = c })
            .ToListAsync();

        if (rows.Count == 0)
            return Array.Empty<ViewEnrollmentDTO>();

        var instructorIds = rows
            .Select(r => r.Section.InstructorId)
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .Distinct()
            .ToList();

        Dictionary<int, string> instructorNameById = new();
        if (instructorIds.Count > 0)
        {
            instructorNameById = await db.Set<InstructorProfile>()
                .AsNoTracking()
                .Where(ip => instructorIds.Contains(ip.InstructorId))
                .Select(ip => new { ip.InstructorId, ip.User.FullName })
                .ToDictionaryAsync(x => x.InstructorId, x => x.FullName);
        }

        return rows.Select(r =>
        {
            string? instructorName = null;
            if (r.Section.InstructorId is int instructorId && instructorNameById.TryGetValue(instructorId, out var name))
            {
                instructorName = name;
            }

            return new ViewEnrollmentDTO
            {
                EnrollmentId = r.Enrollment.EnrollmentId,
                SectionId = r.Enrollment.SectionId,
                StudentId = r.Enrollment.StudentId,
                EnrolledAt = r.Enrollment.EnrolledAt,
                Grade = r.Enrollment.Grade,
                Status = (int)r.Enrollment.Status,
                CourseId = r.Course.CourseId,
                CourseName = r.Course.CourseName,
                CourseCode = r.Course.CourseCode,
                CreditHours = r.Course.CreditHours,
                SectionName = r.Section.SectionName,
                Semester = r.Section.Semester,
                InstructorName = instructorName,
                DeclineReason = r.Enrollment.DeclineReason,
                ApprovedBy = r.Enrollment.ApprovedBy,
                ApprovedAt = r.Enrollment.ApprovedAt
            };
        }).ToList();
    }
}
