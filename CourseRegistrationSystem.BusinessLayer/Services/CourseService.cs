using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.Entities;
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
                AvailableSeats = courseDTO.AvailableSeats,
                CourseCode = courseDTO.CourseCode,
                CourseCategory = (CourseCategory)courseDTO.CourseCategoryId
            };

            // 2. Save entity
            await unitOfWork.Courses.AddAsync(course);
            unitOfWork.SaveChanges();

            // 3. Map Entity -> View DTO
            return new ViewCourseDetailsDTO
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                CreditHours = course.CreditHours,
                AvailableSeats = course.AvailableSeats,
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

            unitOfWork.SaveChanges();

            return $"Course with ID {id} deleted successfully.";
        }


        public Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync()
        {
            throw new NotImplementedException();
        }

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
                AvailableSeats = c.AvailableSeats,
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

        public Task<ViewCourseDetailsDTO> UpdateCourse(UpdateCourseDTO courseDTO)
        {
            throw new NotImplementedException();
        }
    }
}
