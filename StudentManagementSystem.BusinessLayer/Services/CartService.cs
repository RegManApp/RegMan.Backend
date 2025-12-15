using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public void AddToCart(int studentId, int scheduleSlotId)
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
            //StudentProfile? student = 


            // Implementation for adding to cart
        }
    }
}
