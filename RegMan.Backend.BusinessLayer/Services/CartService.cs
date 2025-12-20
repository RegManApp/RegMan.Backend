using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.CartDTOs;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;
using System.Security.Claims;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class CartService : ICartService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<StudentProfile> studentRepository;
        private readonly IBaseRepository<Cart> cartRepository;
        private readonly IBaseRepository<CartItem> cartItemRepository;
        public CartService(IUnitOfWork unitOfWork, IHttpContextAccessor httpContextAccessor)
        {
            this.unitOfWork = unitOfWork;
            studentRepository = unitOfWork.StudentProfiles;
            cartRepository = unitOfWork.Carts;
            cartItemRepository = unitOfWork.CartItems;
        }
        //CREATE
        public async Task AddToCartAsync(string studentId, int scheduleSlotId)
        {

            //check if valid in db
            int cartId = await ValidateStudentAndCart(studentId); //method checks both student and cart existence

            bool slotExists = await unitOfWork.ScheduleSlots
            .GetAllAsQueryable()
            .AsNoTracking()
            .AnyAsync(s => s.ScheduleSlotId == scheduleSlotId);

            if (!slotExists)
                throw new InvalidOperationException($"Schedule slot with ID {scheduleSlotId} does not exist.");


            //preventing adding same schedule slot multiple times
            bool alreadyInCart = await cartItemRepository.GetAllAsQueryable().AsNoTracking().AnyAsync(ci => ci.CartId == cartId && ci.ScheduleSlotId == scheduleSlotId);
            if (alreadyInCart)
            {
                throw new InvalidOperationException($"Schedule slot with ID {scheduleSlotId} is already in the cart.");
            }
            CartItem cartItem = new CartItem
            {
                CartId = cartId,
                ScheduleSlotId = scheduleSlotId
            };
            await cartItemRepository.AddAsync(cartItem);
            await unitOfWork.SaveChangesAsync();
        }

        // Add to cart by courseId - finds first available section with scheduleSlot
        public async Task AddToCartByCourseAsync(string studentId, int courseId)
        {
            // Check if course exists
            var courseExists = await unitOfWork.Courses
                .GetAllAsQueryable()
                .AsNoTracking()
                .AnyAsync(c => c.CourseId == courseId);

            if (!courseExists)
                throw new InvalidOperationException($"Course with ID {courseId} does not exist.");

            // Find an available section with a scheduleSlot for this course
            var availableSlot = await unitOfWork.ScheduleSlots
                .GetAllAsQueryable()
                .AsNoTracking()
                .Include(ss => ss.Section)
                .Where(ss => ss.Section.CourseId == courseId && ss.Section.AvailableSeats > 0)
                .OrderBy(ss => ss.SectionId)
                .FirstOrDefaultAsync();

            if (availableSlot == null)
                throw new InvalidOperationException($"No available sections with schedule for course ID {courseId}. Please ensure the course has sections with available seats and schedules.");

            // Use existing AddToCartAsync with the found scheduleSlotId
            await AddToCartAsync(studentId, availableSlot.ScheduleSlotId);
        }
        //DELETE
        public async Task<ViewCartDTO> RemoveFromCartAsync(string studentId, int cartItemId)
        {
            //check if valid in db
            int cartId = await ValidateStudentAndCart(studentId); //method checks both student and cart existence
            bool cartItemExists = await cartItemRepository.GetAllAsQueryable().AsNoTracking().AnyAsync(ci => ci.CartId == cartId && ci.CartItemId == cartItemId);

            if (!cartItemExists)

                throw new InvalidOperationException($"Cart item with ID {cartItemId} does not exist in the cart.");

            bool deleted = await cartItemRepository.DeleteAsync(cartItemId);
            if (!deleted)
            {
                throw new Exception($"Failed to delete Cart item with ID {cartItemId}.");
            }
            await unitOfWork.SaveChangesAsync();
            return await ViewCartAsync(studentId);
        }
        //READ (view)
        public async Task<ViewCartDTO> ViewCartAsync(string studentId)
        {
            //check if valid in db
            int cartId = await ValidateStudentAndCart(studentId); //method checks both student and cart existence
            List<ViewCartItemDTO> cartItems = await cartItemRepository.GetAllAsQueryable()
                .AsNoTracking()
                .Where(ci => ci.CartId == cartId)
                .Select(ci => new ViewCartItemDTO
                {
                    CartItemId = ci.CartItemId,
                    ScheduleSlotId = ci.ScheduleSlotId,
                    SectionName = $"{ci.ScheduleSlot.Section.Course.CourseName} - Section {ci.ScheduleSlot.Section.SectionId}",
                    TimeSlot = $"{ci.ScheduleSlot.TimeSlot.StartTime} - {ci.ScheduleSlot.TimeSlot.EndTime}",
                    Room = $"{ci.ScheduleSlot.Room.Building} - {ci.ScheduleSlot.Room.RoomNumber}"

                })
                .ToListAsync();
            var viewCartDTO = new ViewCartDTO
            {
                CartId = cartId,
                CartItems = cartItems
            };
            return viewCartDTO;
        }
        private async Task<int> ValidateStudentAndCart(string userId)
        {
            StudentProfile? studentWithCart = await studentRepository.GetAllAsQueryable().AsNoTracking().Include(s => s.Cart).FirstOrDefaultAsync(s => s.UserId == userId);
            if (studentWithCart == null)
            {
                throw new InvalidOperationException($"Student with ID {userId} does not exist.");
            }
            if (studentWithCart.Cart == null)
            {
                throw new InvalidOperationException($"Cart for student ID {userId} does not exist.");
            }
            return studentWithCart.CartId;
        }
    }
}
