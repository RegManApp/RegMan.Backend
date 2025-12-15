using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System.Linq.Expressions;
using static System.Collections.Specialized.BitVector32;
using Section = StudentManagementSystem.DAL.Entities.Section;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class SectionService : ISectionService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<Course> courseRepository;
        private readonly IBaseRepository<Section> sectionRepository;
        private readonly IBaseRepository<ScheduleSlot> scheduleSlotsRepository;
        private readonly IBaseRepository<InstructorProfile> instructorRepository;
        private readonly ICourseService courseService;
        public SectionService(IUnitOfWork unitOfWork, ICourseService courseService)
        {
            this.unitOfWork = unitOfWork;
            this.courseRepository = unitOfWork.Courses;
            this.sectionRepository = unitOfWork.Sections;
            this.scheduleSlotsRepository = unitOfWork.ScheduleSlots;
            this.instructorRepository = unitOfWork.InstructorProfiles;
            this.courseService = courseService;
        }
        public async Task<ViewSectionDTO> CreateSectionAsync(CreateSectionDTO sectionDTO)
        {
            if (sectionDTO == null)
                throw new ArgumentNullException(nameof(sectionDTO));
            //check all inputs are not null

            //if (sectionDTO.InstructorId == null) 
            //{
            //    throw new ArgumentNullException(nameof(sectionDTO.InstructorId));

            //}

            if (sectionDTO.CourseId == null)
            {
                throw new ArgumentNullException(nameof(sectionDTO.CourseId));

            }
            if (sectionDTO.RoomId == null)
            {
                throw new ArgumentNullException(nameof(sectionDTO.RoomId));

            }
            if (sectionDTO.TimeSlotId == null)
            {
                throw new ArgumentNullException(nameof(sectionDTO.TimeSlotId));

            }

            //if the instructor ID is not null, check if they exist in DB

            //InstructorProfile? instructor = await instructorRepository.GetAllAsQueryable().AsNoTracking().Where(i=>i.InstructorId==sectionDTO.InstructorId).Include(i => i.User.FullName).SingleOrDefaultAsync();
            //if (instructor == null) //if not found & not exist in DB
            //{
            //    throw new ArgumentNullException(nameof(sectionDTO.InstructorId));
            //}

            //if the course ID is not null, check if it exists in DB
            Course? course = await courseRepository.GetAllAsQueryable().AsNoTracking().Where(c => c.CourseId == sectionDTO.CourseId).SingleOrDefaultAsync();
            if (course == null) //if not found & not exist in DB
            {
                throw new ArgumentNullException(nameof(sectionDTO.CourseId));
            }


            //no issues, map to entity

            Section section = new Section
            {
                Semester = sectionDTO.Semester,
                Year = sectionDTO.Year,
                //InstructorId= sectionDTO.InstructorId,
                CourseId = sectionDTO.CourseId,
                AvailableSeats = sectionDTO.AvailableSeats
            };

            //add and save
            await sectionRepository.AddAsync(section);
            await unitOfWork.SaveChangesAsync();

            ScheduleSlot scheduleSlot = new ScheduleSlot //default lecture slot, admins can enter details later
            {
                RoomId = sectionDTO.RoomId,
                TimeSlotId = sectionDTO.TimeSlotId,
                Section = section,
                SectionId = section.SectionId,
                SlotType = SlotType.Lecture
            };
            await scheduleSlotsRepository.AddAsync(scheduleSlot);
            await unitOfWork.SaveChangesAsync();

            //return mapped dto
            return new ViewSectionDTO
            {
                SectionId = section.SectionId,
                Semester = section.Semester,
                Year = section.Year,
                InstructorId = section.InstructorId,
                AvailableSeats = section.AvailableSeats,
                InstructorName = section.Instructor.User.FullName,
                CourseSummary = await courseService.GetCourseSummaryByIdAsync(section.CourseId),
                ScheduleSlots = section.Slots?.Select(slot => new ViewScheduleSlotDTO
                {
                    ScheduleSlotId = slot.ScheduleSlotId,
                    SlotType = slot.SlotType,
                    SectionId = slot.SectionId,
                    RoomId = slot.RoomId,
                    RoomNumber = slot.Room.RoomNumber,
                    TimeSlotId = slot.TimeSlotId,
                    Day = slot.TimeSlot.Day,
                    StartTime = slot.TimeSlot.StartTime,
                    EndTime = slot.TimeSlot.EndTime,
                }) ?? Enumerable.Empty<ViewScheduleSlotDTO>()

            };

        } //need to modify it to check if the roomid exists as well as timeslot id

        //public async Task<ViewSectionDTO> GetSectionByIdAsync(int id) 
        //{

        //    //find it first
        //    var courseId = await sectionRepository.GetAllAsQueryable().AsNoTracking()
        //        .Where(s => s.SectionId == id)
        //        .Select(s => s.CourseId)
        //        .FirstOrDefaultAsync();
        //    if (courseId == 0) // Assuming CourseId is never 0 and section was not found
        //    {
        //        // If courseId is 0, the section likely doesn't exist
        //        throw new Exception($"Section with ID {id} was not found.");
        //    }
        //    var section = await sectionRepository.GetAllAsQueryable().AsNoTracking().Where(s => s.SectionId == id).Select(
        //         s => new ViewSectionDTO
        //        {
        //            SectionId=s.SectionId,
        //            Semester=s.Semester,
        //            Year=s.Year,
        //            InstructorId=s.InstructorId,
        //            InstructorName=s.Instructor.User.FullName,
        //            AvailableSeats=s.AvailableSeats,
        //            ScheduleSlots= s.Slots.Select(slot=> new ViewScheduleSlotDTO {
        //                            ScheduleSlotId= slot.ScheduleSlotId,
        //                            SlotType = slot.SlotType,
        //                            SectionId = slot.SectionId,
        //                            RoomId = slot.RoomId,
        //                            RoomNumber = slot.Room.RoomNumber,
        //                            TimeSlotId = slot.TimeSlotId,
        //                            Day = slot.TimeSlot.Day,
        //                            StartTime = slot.TimeSlot.StartTime,
        //                            EndTime = slot.TimeSlot.EndTime
        //                }.ToList()
        //        }
        //        ).FirstOrDefaultAsync();
        //    if (section == null)
        //    {
        //        throw new Exception($"section with ID {id} was not found. ");
        //    }
        //    section.CourseSummary = await courseService.GetCourseSummaryByIdAsync(courseId);
        //    return section;
        //}
        public async Task<ViewSectionDTO> GetSectionByIdAsync(int id)
        {
            var section = await sectionRepository.GetAllAsQueryable().AsNoTracking().Where(s => s.SectionId == id).Select(
                 s => new ViewSectionDTO
                 {
                     SectionId = s.SectionId,
                     Semester = s.Semester,
                     Year = s.Year,
                     InstructorId = s.InstructorId,
                     InstructorName = s.Instructor.User.FullName,
                     AvailableSeats = s.AvailableSeats,
                     ScheduleSlots = s.Slots.Select(slot => new ViewScheduleSlotDTO
                     {
                         ScheduleSlotId = slot.ScheduleSlotId,
                         SlotType = slot.SlotType,
                         SectionId = slot.SectionId,
                         RoomId = slot.RoomId,
                         RoomNumber = slot.Room.RoomNumber,
                         TimeSlotId = slot.TimeSlotId,
                         Day = slot.TimeSlot.Day,
                         StartTime = slot.TimeSlot.StartTime,
                         EndTime = slot.TimeSlot.EndTime
                     }).ToList(),
                     CourseSummary = new ViewCourseSummaryDTO
                     {
                         CourseCode = s.Course.CourseCode,
                         CourseId = s.Course.CourseId,
                         CourseName = s.Course.CourseName,
                         CreditHours = s.Course.CreditHours

                     }
                 }
                ).FirstOrDefaultAsync();
            if (section == null)
            {
                throw new Exception($"section with ID {id} was not found. ");
            }
            return section;
        }
        public async Task<ViewSectionDTO> UpdateSectionAsync(UpdateSectionDTO sectionDTO)
        {
            if (sectionDTO == null)
                throw new ArgumentNullException(nameof(sectionDTO));
            Section? section = await sectionRepository.GetByIdAsync(sectionDTO.SectionId);
            if (section is null)
                throw new KeyNotFoundException($"Section with ID {sectionDTO.SectionId} does not exist or not found.");
            //otherwise, update
            if (sectionDTO.Year >= DateTime.Now)
                section.Year = sectionDTO.Year;

            if (sectionDTO.AvailableSeats >= 30 && sectionDTO.AvailableSeats <= 60) //i need to count the enrollments in this section and ensure that the admin doesnt redue the number to be less than the current enrollments
                section.AvailableSeats = sectionDTO.AvailableSeats;

            if (sectionDTO.InstructorId.HasValue) //if it has a value, check if instructor exists
            {
                if (await instructorRepository.GetByIdAsync(sectionDTO.InstructorId ?? 0) is null)
                    throw new KeyNotFoundException($"Instructor with ID {sectionDTO.InstructorId} does not exist or not found.");
                section.InstructorId = sectionDTO.InstructorId.Value;

            }
            else
                section.InstructorId = null;
            section.Semester = sectionDTO.Semester;
            sectionRepository.Update(section);
            await unitOfWork.SaveChangesAsync();
            return await GetSectionByIdAsync(sectionDTO.SectionId);
        }
        public async Task DeleteSectionAsync(int id) //should be cascade delete and delete all related schedule slots as well
        {
            var deleted = await sectionRepository.DeleteAsync(id);
            if (!deleted)
                throw new KeyNotFoundException($"Section with ID {id} does not exist or not found.");
            await unitOfWork.SaveChangesAsync();


        }
    }
}
