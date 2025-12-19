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
                    TimeSlotId = oh.TimeSlotId,
                    InstructorId = oh.InstructorId,
                    Room = $"{oh.Room.Building} - {oh.Room.RoomNumber}",
                    TimeSlot = $"{oh.TimeSlot.Day} {oh.TimeSlot.StartTime}-{oh.TimeSlot.EndTime}",
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
            var room = await unitOfWork.Rooms.GetByIdAsync(hoursDTO.RoomId)
               ?? throw new Exception($"Room with ID {hoursDTO.RoomId} not found.");

            var timeSlot = await unitOfWork.TimeSlots.GetByIdAsync(hoursDTO.TimeSlotId)
                ?? throw new Exception($"Time slot with ID {hoursDTO.TimeSlotId} not found.");
            //all are valid, create office hour
            OfficeHour officeHour = new OfficeHour
            {
                InstructorId = hoursDTO.InstructorId,
                RoomId = hoursDTO.RoomId,
                TimeSlotId = hoursDTO.TimeSlotId
            };
            await officeHoursRepository.AddAsync(officeHour);
            await unitOfWork.SaveChangesAsync();
            return new ViewOfficeHoursDTO
            {
                OfficeHoursId = officeHour.OfficeHourId,
                RoomId = officeHour.RoomId,
                TimeSlotId = officeHour.TimeSlotId,
                InstructorId = officeHour.InstructorId,
                Room = $"{officeHour.Room.Building} - {officeHour.Room.RoomNumber}",
                TimeSlot = $"{officeHour.TimeSlot.Day} {officeHour.TimeSlot.StartTime}-{officeHour.TimeSlot.EndTime}",
                InstructorName = officeHour.Instructor.User.FullName
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
