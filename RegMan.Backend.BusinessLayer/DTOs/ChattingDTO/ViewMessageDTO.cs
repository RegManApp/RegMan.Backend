using RegMan.Backend.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.DTOs.ChattingDTO
{
    public class ViewMessageDTO
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public string? ClientMessageId { get; set; }
        public DateTime? ServerReceivedAt { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderRole { get; set; }
        public bool IsSystem { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public MsgStatus Status { get; set; }
        public bool IsDeletedForEveryone { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
