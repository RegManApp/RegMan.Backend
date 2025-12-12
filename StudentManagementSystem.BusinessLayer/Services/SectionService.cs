using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.ScheduleSlotDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class SectionService:ISectionService
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

            Section section = new Section { 
                Semester= sectionDTO.Semester,
                Year= sectionDTO.Year,
                //InstructorId= sectionDTO.InstructorId,
                CourseId = sectionDTO.CourseId,
                AvailableSeats =sectionDTO.AvailableSeats
            };
            //add and save
            await sectionRepository.AddAsync(section);
            await unitOfWork.SaveChangesAsync();
            return new ViewSectionDTO
            {
                SectionId = section.SectionId,
                Semester = section.Semester,
                Year = section.Year,
                //InstructorId = section.InstructorId,
                AvailableSeats = section.AvailableSeats,
                //InstructorName = section.Instructor.User.FullName,
                CourseSummary =await courseService.GetCourseSummaryByIdAsync(section.CourseId),
                ScheduleSlots = section.Slots?.Select(slot => new ViewScheduleSlotDTO
                {
                    ScheduleSlotId = slot.ScheduleSlotId,
                    SlotType = slot.SlotType,
                    SectionId = slot.SectionId,
                    RoomId= slot.RoomId,
                    RoomNumber = slot.Room.RoomNumber,
                    TimeSlotId = slot.TimeSlotId,
                    Day = slot.TimeSlot.Day,
                    StartTime = slot.TimeSlot.StartTime,
                    EndTime = slot.TimeSlot.EndTime,
                }) ?? Enumerable.Empty<ViewScheduleSlotDTO>()

            };

        }
        

    }
}
