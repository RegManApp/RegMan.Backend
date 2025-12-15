using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Entities
{
    public class CartItem
    {
        public int CartItemId { get; set; }
        [ForeignKey("Cart")]
        public int CartId { get; set; }
        public Cart Cart { get; set; }
        [ForeignKey("ScheduleSlot")]
        public int ScheduleSlotId { get; set; }
        public ScheduleSlot ScheduleSlot { get; set; }

    }
}
