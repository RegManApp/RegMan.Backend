using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class CartService : ICartService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<StudentProfile> studentRepository;
        private readonly IBaseRepository<Cart> cartRepository;
        public CartService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            studentRepository = unitOfWork.StudentProfiles;
            cartRepository = unitOfWork.Carts;
        }

        public async Task AddToCart(int studentId, int scheduleSlotId)
        {
            if (studentId == null )
            {
                throw new ArgumentNullException("Invalid student ID.", nameof(studentId));
            }
            if (scheduleSlotId == null )
            {
                throw new ArgumentNullException("Invalid student ID.", nameof(scheduleSlotId));
            }
            //check if valid in db
            StudentProfile? student = await studentRepository.GetByIdAsync(studentId);
            if (student == null)
            {
                throw new InvalidOperationException($"Student with ID {studentId} does not exist.");
            }
            Cart? cart = await cartRepository.GetByIdAsync(student.CartId);
            if (cart == null)
            {
                throw new InvalidOperationException($"Cart for student ID {studentId} does not exist.");
            }
            ScheduleSlot? slot = await unitOfWork.ScheduleSlots.GetByIdAsync(scheduleSlotId);
            if (slot == null)
            {
                throw new InvalidOperationException($"Schedule slot with ID {scheduleSlotId} does not exist.");
            }
            //await cartRepository.AddAsync();


            // Implementation for adding to cart
        }
    }
}
