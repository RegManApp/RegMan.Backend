using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO
{
    public class ViewMessageDTO
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public MsgStatus Status { get; set; }
    }
}
