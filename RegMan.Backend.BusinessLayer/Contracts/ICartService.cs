using RegMan.Backend.BusinessLayer.DTOs.CartDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface ICartService
    {
        Task<ViewCartDTO> RemoveFromCartAsync(string studentId, int cartItemId);
        Task AddToCartAsync(string studentId, int scheduleSlotId);
        Task AddToCartByCourseAsync(string studentId, int courseId);
        Task<ViewCartDTO> ViewCartAsync(string studentId);
    }
}
