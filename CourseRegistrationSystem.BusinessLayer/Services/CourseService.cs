using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.CourseDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public Task<ViewCourseDetailsDTO> CreateCourse(CreateCourseDTO courseDTO)
        {
            throw new NotImplementedException();
        }

        public Task<string> DeleteCourse(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<ViewCourseSummaryDTO>> GetAllCoursesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ViewCourseDetailsDTO> GetCourseById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<ViewCourseDetailsDTO> UpdateCourse(UpdateCourseDTO courseDTO)
        {
            throw new NotImplementedException();
        }
    }
}
