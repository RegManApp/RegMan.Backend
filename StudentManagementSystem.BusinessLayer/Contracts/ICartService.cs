using StudentManagementSystem.BusinessLayer.DTOs.CartDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface ICartService
    {
        Task<ViewCartDTO> RemoveFromCartAsync(int studentId, int cartItemId);
        Task AddToCartAsync(int studentId, int scheduleSlotId);
        Task<ViewCartDTO> ViewCartAsync(int studentId);
    }
}
