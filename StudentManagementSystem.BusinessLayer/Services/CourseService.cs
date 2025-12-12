using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class CourseService : ICourseService
    {
        private readonly IUnitOfWork unitOfWork;
        private IBaseRepository<Course> courseRepository;
        public CourseService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            courseRepository = unitOfWork.Courses;
        }
        public async Task<ViewCourseDetailsDTO> CreateCourse(CreateCourseDTO courseDTO)
        {
            if (courseDTO == null)
                throw new ArgumentNullException(nameof(courseDTO));

            // 1. Map DTO -> Entity
            var course = new Course
            {
                CourseName = courseDTO.CourseName,
                CreditHours = courseDTO.CreditHours,
                //AvailableSeats = courseDTO.AvailableSeats,
                CourseCode = courseDTO.CourseCode,
                CourseCategory = (CourseCategory)courseDTO.CourseCategoryId,
                Description = courseDTO.Description
            };

            // 2. Save entity
            await unitOfWork.Courses.AddAsync(course);
            await unitOfWork.SaveChangesAsync(); // updated
            // 3. Map Entity -> View DTO
            return new ViewCourseDetailsDTO
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                CreditHours = course.CreditHours,
                Description= course.Description,
                //AvailableSeats = course.AvailableSeats,
                CourseCode = course.CourseCode,
                CourseCategoryId = (int)course.CourseCategory,
                CourseCategoryName = course.CourseCategory.ToString()
            };
        }


        public async Task<string> DeleteCourse(int id)
        {
            bool deleted = await unitOfWork.Courses.DeleteAsync(id);

            if (!deleted)
                throw new Exception($"Course with ID {id} not found.");

            await unitOfWork.SaveChangesAsync();   // updated

            return $"Course with ID {id} deleted successfully.";
        }

        public async Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync(string? courseName, int? creditHours, string? courseCode, int? courseCategoryId)
        {
            var query = unitOfWork.Courses.GetAllAsQueryable();

            if (!string.IsNullOrWhiteSpace(courseName))
                query = query.Where(c => c.CourseName.Contains(courseName));

            if (creditHours.HasValue)
                query = query.Where(c => c.CreditHours == creditHours.Value);

            if (!string.IsNullOrWhiteSpace(courseCode))
                query = query.Where(c => c.CourseCode.Contains(courseCode));

            if (courseCategoryId.HasValue)
                query = query.Where(c => (int)c.CourseCategory == courseCategoryId.Value);

            var result = await query
                .Select(c => new ViewCourseSummaryDTO
                {
                    CourseId = c.CourseId,
                    CourseName = c.CourseName,
                    CreditHours = c.CreditHours,
                    CourseCode = c.CourseCode
                })
                .ToListAsync();

            return result;
        }
        // Removed AvailableSeats filter from GetAllCoursesAsync method, this is the old version with AvailableSeats filter:
        //public async Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync(string? courseName, int? creditHours, int? availableSeats, string? courseCode, int? courseCategoryId)
        //{
        //    var query = unitOfWork.Courses.GetAllAsQueryable();

        //    if (!string.IsNullOrWhiteSpace(courseName))
        //        query = query.Where(c => c.CourseName.Contains(courseName));

        //    if (creditHours.HasValue)
        //        query = query.Where(c => c.CreditHours == creditHours.Value);

        //    if (availableSeats.HasValue)
        //        query = query.Where(c => c.AvailableSeats == availableSeats.Value);

        //    if (!string.IsNullOrWhiteSpace(courseCode))
        //        query = query.Where(c => c.CourseCode.Contains(courseCode));

        //    if (courseCategoryId.HasValue)
        //        query = query.Where(c => (int)c.CourseCategory == courseCategoryId.Value);

        //    var result = await query
        //        .Select(c => new ViewCourseSummaryDTO
        //        {
        //            CourseId = c.CourseId,
        //            CourseName = c.CourseName,
        //            CreditHours = c.CreditHours,
        //            CourseCode = c.CourseCode
        //        })
        //        .ToListAsync();

        //    return result;
        //}


        public async Task<ViewCourseDetailsDTO> GetCourseById(int id)
        {
            // 1.The WHERE clause
            Expression<Func<Course, bool>> filterExpression = course =>
                course.CourseId == id;

            // 2.The SELECT clause, used for projection
            Expression<Func<Course, ViewCourseDetailsDTO>> projectionExpression = c => new ViewCourseDetailsDTO
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                CreditHours = c.CreditHours,
                //AvailableSeats = c.AvailableSeats,
                Description= c.Description,
                CourseCode = c.CourseCode,
                CourseCategoryId = (int)c.CourseCategory,
                CourseCategoryName = c.CourseCategory.ToString()

            };

            // 3. Call GetFilteredAndProjected method defined previously in the base repository and pass the expressions
            var projectedQuery = courseRepository.GetFilteredAndProjected(
                filter: filterExpression,
                projection: projectionExpression
            );

            // 4. Execute the query using SingleOrDefaultAsync to get a single result
            // when getting all courses (a list), use ToListAsync to execute query
            ViewCourseDetailsDTO? courseDTO = await projectedQuery.SingleOrDefaultAsync(); //will return null if not found
            if (courseDTO == null)
                throw new Exception($"Course with ID: {id} not found.");
            return courseDTO;
        }

        public async Task<ViewCourseDetailsDTO> UpdateCourse(UpdateCourseDTO courseDTO)
        {
            if (courseDTO == null)
                throw new ArgumentNullException(nameof(courseDTO));

            var existingCourse = await unitOfWork.Courses.GetByIdAsync(courseDTO.CourseId);

            if (existingCourse == null)
                throw new Exception($"Course with ID {courseDTO.CourseId} not found.");

            existingCourse.CreditHours = courseDTO.CreditHours;
            //existingCourse.AvailableSeats = courseDTO.AvailableSeats;
            existingCourse.CourseCode = courseDTO.CourseCode;
            existingCourse.CourseCategory = (CourseCategory)courseDTO.CourseCategoryId;

            unitOfWork.Courses.Update(existingCourse);
            await unitOfWork.SaveChangesAsync();   // updated
            return new ViewCourseDetailsDTO
            {
                CourseId = existingCourse.CourseId,
                CourseName = existingCourse.CourseName,
                CreditHours = existingCourse.CreditHours,
                //AvailableSeats = existingCourse.AvailableSeats,
                Description= existingCourse.Description,
                CourseCode = existingCourse.CourseCode,
                CourseCategoryId = (int)existingCourse.CourseCategory,
                CourseCategoryName = existingCourse.CourseCategory.ToString()
            };
        }

    }
}
