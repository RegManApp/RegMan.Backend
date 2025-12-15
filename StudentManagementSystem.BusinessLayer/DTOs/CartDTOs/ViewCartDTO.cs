using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.CartDTOs
{
    public class ViewCartDTO
    {
        public List<ViewCartItemDTO> CartItems { get; set; } = new List<ViewCartItemDTO>();
        public int CartId { get; set; }
    }
}
