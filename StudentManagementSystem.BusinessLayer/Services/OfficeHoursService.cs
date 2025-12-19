using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.OfficeHoursDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class OfficeHoursService : IOfficeHoursService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<OfficeHour> officeHoursRepository;
        private readonly IBaseRepository<InstructorProfile> instructorsRepository;
        public OfficeHoursService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            this.officeHoursRepository = unitOfWork.OfficeHours;
            this.instructorsRepository = unitOfWork.InstructorProfiles;
        }
        //READ
        //Used by admin to see office hours of any instructor, based on their instructor ID
        public async Task<List<ViewOfficeHoursDTO>> GetOfficeHoursByInstructorIdAsync(int instructorId)
        {
            List<ViewOfficeHoursDTO>? officeHours = await officeHoursRepository
                .GetFilteredAndProjected(
                filter: oh => oh.InstructorId == instructorId,
                projection: oh => new ViewOfficeHoursDTO
                {
                    OfficeHoursId = oh.OfficeHourId,
                    RoomId = oh.RoomId,
                    InstructorId = oh.InstructorId,
                    Date = oh.Date,
                    StartTime = oh.StartTime.ToString(@"hh\:mm"),
                    EndTime = oh.EndTime.ToString(@"hh\:mm"),
                    Status = oh.Status.ToString(),
                    Notes = oh.Notes,
                    IsRecurring = oh.IsRecurring,
                    Room = oh.Room != null ? $"{oh.Room.Building} - {oh.Room.RoomNumber}" : null,
                    InstructorName = oh.Instructor.User.FullName
                }
                )
                .ToListAsync();
            if (officeHours == null || officeHours.Count == 0)
            {
                throw new Exception($"No office hours found for instructor with ID {instructorId}.");
            }
            return officeHours;
        }
        //CREATE
        public async Task<ViewOfficeHoursDTO> CreateOfficeHours(CreateOfficeHoursDTO hoursDTO)
        {
            InstructorProfile? instructor = await instructorsRepository.GetByIdAsync(hoursDTO.InstructorId);
            if (instructor == null)
                throw new KeyNotFoundException($"Instructor with ID {hoursDTO.InstructorId} not found.");

            if (hoursDTO.RoomId.HasValue)
            {
                var room = await unitOfWork.Rooms.GetByIdAsync(hoursDTO.RoomId.Value)
                   ?? throw new Exception($"Room with ID {hoursDTO.RoomId} not found.");
            }

            if (!TimeSpan.TryParse(hoursDTO.StartTime, out var startTime) ||
                !TimeSpan.TryParse(hoursDTO.EndTime, out var endTime))
            {
                throw new Exception("Invalid time format. Use HH:mm");
            }

            //all are valid, create office hour
            OfficeHour officeHour = new OfficeHour
            {
                InstructorId = hoursDTO.InstructorId,
                RoomId = hoursDTO.RoomId,
                Date = hoursDTO.Date.Date,
                StartTime = startTime,
                EndTime = endTime,
                IsRecurring = hoursDTO.IsRecurring,
                RecurringDay = hoursDTO.IsRecurring ? hoursDTO.Date.DayOfWeek : null,
                Notes = hoursDTO.Notes,
                Status = OfficeHourStatus.Available
            };
            await officeHoursRepository.AddAsync(officeHour);
            await unitOfWork.SaveChangesAsync();
            return new ViewOfficeHoursDTO
            {
                OfficeHoursId = officeHour.OfficeHourId,
                RoomId = officeHour.RoomId,
                InstructorId = officeHour.InstructorId,
                Date = officeHour.Date,
                StartTime = officeHour.StartTime.ToString(@"hh\:mm"),
                EndTime = officeHour.EndTime.ToString(@"hh\:mm"),
                Status = officeHour.Status.ToString(),
                Notes = officeHour.Notes,
                IsRecurring = officeHour.IsRecurring,
                Room = officeHour.Room != null ? $"{officeHour.Room.Building} - {officeHour.Room.RoomNumber}" : null,
                InstructorName = instructor.User?.FullName ?? "Unknown"
            };
        }
        //DELETE
        public async Task DeleteOfficeHour(int id)
        {
            bool deleted = await officeHoursRepository.DeleteAsync(id);
            if (!deleted)
                throw new Exception($"Office hours with ID {id} do not exist.");
        }


    }
}
