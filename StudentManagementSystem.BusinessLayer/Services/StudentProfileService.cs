using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.AcademicPlanDTOs;
using StudentManagementSystem.BusinessLayer.DTOs.StudentDTOs;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class StudentProfileService: IStudentProfileService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<Cart> cartRepository;
        private readonly IBaseRepository<StudentProfile> studentRepository;
        private readonly IBaseRepository<AcademicPlan> academicPlanRepository;
        private readonly UserManager<BaseUser> userManager;
        const string role = "Student";
        public StudentProfileService(
            IUnitOfWork unitOfWork,
            UserManager<BaseUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.userManager = userManager;
            cartRepository = unitOfWork.Carts;
            academicPlanRepository = unitOfWork.AcademicPlans;
            studentRepository = unitOfWork.StudentProfiles;
        }
        //Create Student Profile - for admin use only
        public async Task<ViewStudentProfileDTO> CreateProfileAsync(CreateStudentDTO studentDTO) 
        {
            if (await userManager.FindByEmailAsync(studentDTO.Email) is not null)
                throw new Exception($"A student with the email {studentDTO.Email} already exists!");

            BaseUser baseUser = new BaseUser {
                FullName=studentDTO.FullName,
                Email = studentDTO.Email,
                UserName = studentDTO.Email
            };
            if (studentDTO.Address != null)
                baseUser.Address = studentDTO.Address;
    
            var result = await userManager.CreateAsync(baseUser, studentDTO.Password);
            if (!result.Succeeded) 
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception(errors);
            }
            await userManager.AddToRoleAsync(baseUser, role);
            Cart cart = new Cart { };
            await cartRepository.AddAsync(cart);

            bool validAcademicPlan = await academicPlanRepository.GetAllAsQueryable().AsNoTracking().AnyAsync(p=>p.AcademicPlanId==studentDTO.AcademicPlanId);

            StudentProfile student = new StudentProfile { 
                FamilyContact=studentDTO.FamilyContact,
                CompletedCredits=0,
                RegisteredCredits=0,
                GPA=0.0,
                UserId =baseUser.Id,
                AcademicPlanId=studentDTO.AcademicPlanId,
                CartId=cart.CartId
            };
            await studentRepository.AddAsync(student);
            await unitOfWork.SaveChangesAsync();
            ViewStudentProfileDTO viewStudent = new ViewStudentProfileDTO
            {
                StudentId = student.StudentId,
                FullName = baseUser.FullName,
                Address = baseUser.Address,
                FamilyContact = student.FamilyContact,
                CompletedCredits = student.CompletedCredits,
                RegisteredCredits = student.RegisteredCredits,
                GPA = student.GPA,
                AcademicPlanId = student.AcademicPlanId,
                RemainingCredits = await academicPlanRepository.GetAllAsQueryable().AsNoTracking().Where(ap => ap.AcademicPlanId == student.AcademicPlanId).Select(ap => ap.TotalCreditsRequired).FirstOrDefaultAsync()- (student.CompletedCredits + student.RegisteredCredits)
            };
            return viewStudent;
        }

        public async Task<ViewStudentProfileDTO> GetProfileByIdAsync(int id) 
        {
            ViewStudentProfileDTO? student = await studentRepository.GetAllAsQueryable().AsNoTracking()
                .Where(s => s.StudentId == id)
                .Select(st => new ViewStudentProfileDTO
                {
                    StudentId=st.StudentId,
                    FullName=st.User.FullName,
                    Address=st.User.Address,
                    FamilyContact = st.FamilyContact,
                    CompletedCredits = st.CompletedCredits,
                    RegisteredCredits = st.RegisteredCredits,
                    GPA=st.GPA,
                    RemainingCredits=st.AcademicPlan.TotalCreditsRequired-(st.CompletedCredits+st.RegisteredCredits)
                })
                .FirstOrDefaultAsync();
            if (student is null)
                throw new KeyNotFoundException($"Student with ID {id} does not exist.");
            return student;
        }

        public async Task<List<ViewStudentProfileDTO>> GetAllStudentsAsync(int? GPA, int? CompletedCredits, string? AcademicPlanId) 
        {
            var query = studentRepository.GetAllAsQueryable().AsNoTracking();
            if (GPA.HasValue)
            {
                query = query.Where(s => s.GPA >= GPA.Value);
            }
            if (CompletedCredits.HasValue)
            {
                query = query.Where(s => s.CompletedCredits >= CompletedCredits.Value);
            }
            if (!string.IsNullOrEmpty(AcademicPlanId))
            {
                query = query.Where(s => s.AcademicPlanId == AcademicPlanId);
            }
            var students = await query
                .Select(st => new ViewStudentProfileDTO
                {
                    StudentId = st.StudentId,
                    FullName = st.User.FullName,
                    Address = st.User.Address,
                    FamilyContact = st.FamilyContact,
                    CompletedCredits = st.CompletedCredits,
                    RegisteredCredits = st.RegisteredCredits,
                    GPA = st.GPA,
                    AcademicPlanId = st.AcademicPlanId,
                    RemainingCredits = st.AcademicPlan.TotalCreditsRequired - (st.CompletedCredits + st.RegisteredCredits)
                })
                .ToListAsync();
            if(students is null)
                throw new KeyNotFoundException("No students found with the specified criteria.");
            return students;
        }

    }
}
