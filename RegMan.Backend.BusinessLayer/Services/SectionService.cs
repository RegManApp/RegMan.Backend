using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.CourseDTOs;
using RegMan.Backend.BusinessLayer.DTOs.ScheduleSlotDTOs;
using RegMan.Backend.BusinessLayer.DTOs.SectionDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class SectionService : ISectionService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<Section> sectionRepository;
        private readonly IBaseRepository<Course> courseRepository;
        private readonly IBaseRepository<InstructorProfile> instructorRepository;
        private readonly ICourseService courseService;

        public SectionService(IUnitOfWork unitOfWork, ICourseService courseService)
        {
            this.unitOfWork = unitOfWork;
            this.sectionRepository = unitOfWork.Sections;
            this.courseRepository = unitOfWork.Courses;
            this.instructorRepository = unitOfWork.InstructorProfiles;
            this.courseService = courseService;
        }

        // =========================
        // Create
        // =========================
        public async Task<ViewSectionDTO> CreateSectionAsync(CreateSectionDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var course = await courseRepository.GetByIdAsync(dto.CourseId)
                ?? throw new Exception("Course not found");

            string? instructorName = null;
            if (dto.InstructorId.HasValue)
            {
                var instructor = await instructorRepository
                    .GetAllAsQueryable()
                    .Include(i => i.User)
                    .FirstOrDefaultAsync(i => i.InstructorId == dto.InstructorId);

                if (instructor == null)
                    throw new Exception("Instructor not found");

                instructorName = instructor.User.FullName;
            }

            var section = new Section
            {
                Semester = dto.Semester,
                Year = dto.Year,
                CourseId = dto.CourseId,
                InstructorId = dto.InstructorId,
                AvailableSeats = dto.AvailableSeats
            };

            await sectionRepository.AddAsync(section);
            await unitOfWork.SaveChangesAsync();

            // Create a schedule slot if RoomId and TimeSlotId are provided
            var scheduleSlots = new List<ViewScheduleSlotDTO>();
            if (dto.RoomId > 0 && dto.TimeSlotId > 0 && dto.InstructorId.HasValue)
            {
                // Validate that the time slot belongs to the selected room
                var timeSlot = await unitOfWork.TimeSlots.GetByIdAsync(dto.TimeSlotId);
                if (timeSlot == null)
                    throw new Exception("Time slot not found");
                if (timeSlot.RoomId != dto.RoomId)
                    throw new Exception("Selected time slot does not belong to the selected room.");

                var scheduleSlot = new ScheduleSlot
                {
                    SectionId = section.SectionId,
                    RoomId = dto.RoomId,
                    TimeSlotId = dto.TimeSlotId,
                    InstructorId = dto.InstructorId.Value,
                    SlotType = dto.SlotType
                };

                await unitOfWork.ScheduleSlots.AddAsync(scheduleSlot);
                await unitOfWork.SaveChangesAsync();

                // Load the slot with related data for response
                var room = await unitOfWork.Rooms.GetByIdAsync(dto.RoomId);

                scheduleSlots.Add(new ViewScheduleSlotDTO
                {
                    ScheduleSlotId = scheduleSlot.ScheduleSlotId,
                    SectionId = section.SectionId,
                    SectionName = $"{course.CourseName} - Section {section.SectionId}",
                    RoomId = dto.RoomId,
                    Room = room != null ? $"{room.Building} {room.RoomNumber}" : "",
                    TimeSlotId = dto.TimeSlotId,
                    TimeSlot = timeSlot != null ? $"{timeSlot.Day} {timeSlot.StartTime:hh\\:mm}-{timeSlot.EndTime:hh\\:mm}" : "",
                    InstructorId = dto.InstructorId.Value,
                    InstructorName = instructorName ?? "",
                    SlotType = dto.SlotType.ToString()
                });
            }

            return new ViewSectionDTO
            {
                SectionId = section.SectionId,
                Semester = section.Semester,
                Year = section.Year,
                InstructorId = section.InstructorId,
                InstructorName = instructorName,
                AvailableSeats = section.AvailableSeats,
                CourseSummary = await courseService.GetCourseSummaryByIdAsync(section.CourseId),
                ScheduleSlots = scheduleSlots
            };
        }

        // =========================
        // Get By Id
        // =========================
        public async Task<ViewSectionDTO> GetSectionByIdAsync(int id)
        {
            var section = await BuildBaseQuery()
                .FirstOrDefaultAsync(s => s.SectionId == id);

            if (section == null)
                throw new Exception($"Section with ID {id} not found");

            return section;
        }

        // =========================
        // Get All
        // =========================
        public async Task<IEnumerable<ViewSectionDTO>> GetAllSectionsAsync(
            string? semester,
            DateTime? year,
            int? instructorId,
            int? courseId,
            int? seats)
        {
            var query = sectionRepository
                .GetAllAsQueryable()
                .Include(s => s.Course)
                .Include(s => s.Instructor!)
                    .ThenInclude(i => i.User)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.Room)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.TimeSlot)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.Instructor!)
                        .ThenInclude(i => i.User)
                .AsQueryable();

            // Guard against inconsistent data (orphaned sections). Keeping the list endpoint resilient
            // prevents admin forms from failing to load due to a single bad row.
            query = query.Where(s => s.Course != null);

            if (!string.IsNullOrEmpty(semester))
                query = query.Where(s => s.Semester == semester);

            if (year.HasValue)
                query = query.Where(s => s.Year == year.Value);

            if (instructorId.HasValue)
                query = query.Where(s => s.InstructorId == instructorId.Value);

            if (courseId.HasValue)
                query = query.Where(s => s.CourseId == courseId.Value);

            if (seats.HasValue)
                query = query.Where(s => s.AvailableSeats == seats.Value);

            return await query
                .Select(s => new ViewSectionDTO
                {
                    SectionId = s.SectionId,
                    Semester = s.Semester,
                    Year = s.Year,
                    InstructorId = s.InstructorId,
                    InstructorName = s.Instructor != null
                        ? s.Instructor.User.FullName
                        : null,
                    AvailableSeats = s.AvailableSeats,

                    CourseSummary = new ViewCourseSummaryDTO
                    {
                        CourseId = s.Course.CourseId,
                        CourseName = s.Course.CourseName,
                        CourseCode = s.Course.CourseCode,
                        CreditHours = s.Course.CreditHours,
                        CourseCategoryId = (int)s.Course.CourseCategory,
                        Description = s.Course.Description
                    },

                    ScheduleSlots = s.Slots.Select(slot => new ViewScheduleSlotDTO
                    {
                        ScheduleSlotId = slot.ScheduleSlotId,
                        SectionId = slot.SectionId,
                        SectionName = (s.Course.CourseName ?? string.Empty) + " - Section " + s.SectionId,

                        RoomId = slot.RoomId,
                        Room = slot.Room != null
                            ? (slot.Room.Building + " - " + slot.Room.RoomNumber)
                            : string.Empty,

                        TimeSlotId = slot.TimeSlotId,
                        TimeSlot = slot.TimeSlot != null
                            ? (slot.TimeSlot.Day.ToString() + " " +
                               slot.TimeSlot.StartTime.ToString(@"hh\:mm") + "-" +
                               slot.TimeSlot.EndTime.ToString(@"hh\:mm"))
                            : string.Empty,

                        InstructorId = slot.InstructorId,
                        InstructorName = slot.Instructor != null
                            ? slot.Instructor.User.FullName
                            : string.Empty,
                        SlotType = slot.SlotType.ToString()
                    }).ToList()
                })
                .ToListAsync();
        }

        // =========================
        // Update
        // =========================
        public async Task<ViewSectionDTO> UpdateSectionAsync(UpdateSectionDTO dto)
        {
            var section = await sectionRepository.GetByIdAsync(dto.SectionId)
                ?? throw new Exception("Section not found");

            section.Semester = dto.Semester;
            section.Year = dto.Year;
            section.AvailableSeats = dto.AvailableSeats;
            section.InstructorId = dto.InstructorId;

            sectionRepository.Update(section);
            await unitOfWork.SaveChangesAsync();

            return await GetSectionByIdAsync(section.SectionId);
        }

        // =========================
        // Delete
        // =========================
        public async Task<bool> DeleteSectionAsync(int id)
        {
            var deleted = await sectionRepository.DeleteAsync(id);
            if (!deleted)
                throw new Exception("Section not found");

            await unitOfWork.SaveChangesAsync();
            return true;
        }

        // =========================
        // Shared Query
        // =========================
        private IQueryable<ViewSectionDTO> BuildBaseQuery()
        {
            return sectionRepository
                .GetAllAsQueryable()
                .Include(s => s.Course)
                .Include(s => s.Instructor!)
                    .ThenInclude(i => i.User)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.Room)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.TimeSlot)
                .Include(s => s.Slots)
                    .ThenInclude(sl => sl.Instructor!)
                        .ThenInclude(i => i.User)
                .Select(s => new ViewSectionDTO
                {
                    SectionId = s.SectionId,
                    Semester = s.Semester,
                    Year = s.Year,
                    InstructorId = s.InstructorId,
                    InstructorName = s.Instructor != null
                        ? s.Instructor.User.FullName
                        : null,
                    AvailableSeats = s.AvailableSeats,

                    CourseSummary = new ViewCourseSummaryDTO
                    {
                        CourseId = s.Course.CourseId,
                        CourseName = s.Course.CourseName,
                        CourseCode = s.Course.CourseCode,
                        CreditHours = s.Course.CreditHours,
                        CourseCategoryId = (int)s.Course.CourseCategory,
                        Description = s.Course.Description
                    },

                    ScheduleSlots = s.Slots.Select(slot => new ViewScheduleSlotDTO
                    {
                        ScheduleSlotId = slot.ScheduleSlotId,
                        SectionId = slot.SectionId,
                        SectionName = s.Course.CourseName + " - Section " + s.SectionId,

                        RoomId = slot.RoomId,
                        Room = slot.Room.Building + " - " + slot.Room.RoomNumber,

                        TimeSlotId = slot.TimeSlotId,
                        TimeSlot = slot.TimeSlot.Day.ToString() + " " +
                                   slot.TimeSlot.StartTime.ToString(@"hh\:mm") + "-" +
                                   slot.TimeSlot.EndTime.ToString(@"hh\:mm"),

                        InstructorId = slot.InstructorId,
                        InstructorName = slot.Instructor.User.FullName,
                        SlotType = slot.SlotType.ToString()
                    }).ToList()
                });
        }

        // =========================
        // Mapper
        // =========================
        private static ViewSectionDTO MapToViewDTO(Section s)
        {
            return new ViewSectionDTO
            {
                SectionId = s.SectionId,
                Semester = s.Semester,
                Year = s.Year,
                InstructorId = s.InstructorId,
                InstructorName = s.Instructor != null ? s.Instructor.User.FullName : null,
                AvailableSeats = s.AvailableSeats,

                CourseSummary = new ViewCourseSummaryDTO
                {
                    CourseId = s.Course.CourseId,
                    CourseName = s.Course.CourseName,
                    CourseCode = s.Course.CourseCode,
                    CreditHours = s.Course.CreditHours,
                    CourseCategoryId = (int)s.Course.CourseCategory,
                    Description = s.Course.Description
                },

                ScheduleSlots = s.Slots.Select(slot => new ViewScheduleSlotDTO
                {
                    ScheduleSlotId = slot.ScheduleSlotId,
                    SectionId = slot.SectionId,
                    SectionName = s.Course.CourseName + " - Section " + s.SectionId,
                    RoomId = slot.RoomId,
                    Room = (slot.Room?.Building ?? "Unknown") + " - " + (slot.Room?.RoomNumber ?? "Unknown"),
                    TimeSlotId = slot.TimeSlotId,
                    TimeSlot = slot.TimeSlot.Day + " " +
                               (slot.TimeSlot?.StartTime != null ? slot.TimeSlot.StartTime.ToString(@"hh\:mm") : "??") + "-" +
                               (slot.TimeSlot?.EndTime != null ? slot.TimeSlot.EndTime.ToString(@"hh\:mm") : "??"),
                    InstructorId = slot.InstructorId,
                    InstructorName = slot.Instructor?.User?.FullName ?? "Unknown Instructor",
                    SlotType = slot.SlotType.ToString()
                }).ToList()
            };
        }
    }
}
