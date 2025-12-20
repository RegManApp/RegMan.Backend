using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.InstructorDTOs;
using RegMan.Backend.BusinessLayer.DTOs.ScheduleSlotDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services;

internal class InstructorService : IInstructorService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly UserManager<BaseUser> userManager;

    public InstructorService(
        IUnitOfWork unitOfWork,
        UserManager<BaseUser> userManager)
    {
        this.unitOfWork = unitOfWork;
        this.userManager = userManager;
    }

    // ======================
    // Create Instructor
    // ======================
    public async Task<ViewInstructorDTO> CreateAsync(CreateInstructorDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new Exception("Instructor title is required.");

        var user = new BaseUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            Address = "N/A",
            Role = "Instructor"
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            throw new Exception("Failed to create instructor user: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, "Instructor");

        var instructor = new InstructorProfile
        {
            UserId = user.Id,
            Title = dto.Title,
            Degree = InstructorDegree.Lecturer,
            Department = "General"
        };

        await unitOfWork.InstructorProfiles.AddAsync(instructor);
        await unitOfWork.SaveChangesAsync();

        return new ViewInstructorDTO
        {
            InstructorId = instructor.InstructorId,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Title = instructor.Title,
            Degree = instructor.Degree,
            Department = instructor.Department,
            Address = user.Address
        };
    }

    // ======================
    // Get All
    // ======================
    public async Task<IEnumerable<ViewInstructorDTO>> GetAllAsync()
    {
        return await unitOfWork.InstructorProfiles
            .GetAllAsQueryable()
            .Include(i => i.User)
            .Select(i => new ViewInstructorDTO
            {
                InstructorId = i.InstructorId,
                UserId = i.UserId,
                FullName = i.User.FullName,
                Email = i.User.Email!,
                Title = i.Title,
                Degree = i.Degree,
                Department = i.Department,
                Address = i.User.Address
            })
            .ToListAsync();
    }

    // ======================
    // Get By Id
    // ======================
    public async Task<ViewInstructorDTO?> GetByIdAsync(int id)
    {
        var instructor = await unitOfWork.InstructorProfiles
            .GetAllAsQueryable()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.InstructorId == id);

        if (instructor == null)
            return null;

        return new ViewInstructorDTO
        {
            InstructorId = instructor.InstructorId,
            UserId = instructor.UserId,
            FullName = instructor.User.FullName,
            Email = instructor.User.Email!,
            Title = instructor.Title,
            Degree = instructor.Degree,
            Department = instructor.Department,
            Address = instructor.User.Address
        };
    }

    // ======================
    // Get By User Id
    // ======================
    public async Task<ViewInstructorDTO?> GetByUserIdAsync(string userId)
    {
        var instructor = await unitOfWork.InstructorProfiles
            .GetAllAsQueryable()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.UserId == userId);

        if (instructor == null)
            return null;

        return new ViewInstructorDTO
        {
            InstructorId = instructor.InstructorId,
            UserId = instructor.UserId,
            FullName = instructor.User.FullName,
            Email = instructor.User.Email!,
            Title = instructor.Title,
            Degree = instructor.Degree,
            Department = instructor.Department,
            Address = instructor.User.Address
        };
    }

    // ======================
    // Update Instructor
    // ======================
    public async Task<ViewInstructorDTO?> UpdateAsync(int id, UpdateInstructorDTO dto)
    {
        var instructor = await unitOfWork.InstructorProfiles
            .GetAllAsQueryable()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.InstructorId == id);

        if (instructor == null)
            return null;

        // Update User fields
        if (!string.IsNullOrWhiteSpace(dto.FullName))
            instructor.User.FullName = dto.FullName;

        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != instructor.User.Email)
        {
            var existingUser = await userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null && existingUser.Id != instructor.UserId)
                throw new Exception("Email already in use by another user");

            instructor.User.Email = dto.Email;
            instructor.User.UserName = dto.Email;
            instructor.User.NormalizedEmail = dto.Email.ToUpper();
            instructor.User.NormalizedUserName = dto.Email.ToUpper();
        }

        if (!string.IsNullOrWhiteSpace(dto.Address))
            instructor.User.Address = dto.Address;

        // Update InstructorProfile fields
        if (!string.IsNullOrWhiteSpace(dto.Title))
            instructor.Title = dto.Title;

        if (dto.Degree.HasValue)
            instructor.Degree = dto.Degree.Value;

        if (dto.Department != null)
            instructor.Department = dto.Department;

        await userManager.UpdateAsync(instructor.User);
        await unitOfWork.SaveChangesAsync();

        return new ViewInstructorDTO
        {
            InstructorId = instructor.InstructorId,
            UserId = instructor.UserId,
            FullName = instructor.User.FullName,
            Email = instructor.User.Email!,
            Title = instructor.Title,
            Degree = instructor.Degree,
            Department = instructor.Department,
            Address = instructor.User.Address
        };
    }

    // ======================
    // Delete Instructor
    // ======================
    public async Task<bool> DeleteAsync(int id)
    {
        var instructor = await unitOfWork.InstructorProfiles
            .GetAllAsQueryable()
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.InstructorId == id);

        if (instructor == null)
            return false;

        // Delete the user (cascade should handle the profile)
        await userManager.DeleteAsync(instructor.User);

        return true;
    }

    // ======================
    // Instructor Schedule
    // ======================
    public async Task<IEnumerable<ViewScheduleSlotDTO>> GetScheduleAsync(int instructorId)
    {
        return await unitOfWork.ScheduleSlots
            .GetAllAsQueryable()
            .Where(s => s.InstructorId == instructorId)
            .Include(s => s.Section)
                .ThenInclude(sec => sec.Course)
            .Include(s => s.Room)
            .Include(s => s.TimeSlot)
            .Include(s => s.Instructor)
                .ThenInclude(i => i.User)
            .Select(s => new ViewScheduleSlotDTO
            {
                ScheduleSlotId = s.ScheduleSlotId,

                SectionId = s.SectionId,
                SectionName = s.Section.Course.CourseName,

                RoomId = s.RoomId,
                Room = s.Room.Building + " - " + s.Room.RoomNumber,

                TimeSlotId = s.TimeSlotId,
                TimeSlot = s.TimeSlot.Day + " " +
                           s.TimeSlot.StartTime + "-" +
                           s.TimeSlot.EndTime,

                InstructorId = s.InstructorId,
                InstructorName = s.Instructor.User.FullName,

                SlotType = s.SlotType.ToString()
            })
            .ToListAsync();
    }
}
