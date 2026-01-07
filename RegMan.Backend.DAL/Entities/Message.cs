using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.DAL.Entities
{
    public class Message
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime ServerReceivedAt { get; set; }

        public string? ClientMessageId { get; set; }
        public string TextMessage { get; set; }
        public MsgStatus Status { get; set; }

        // Legacy global read tracking (kept for backward compatibility).
        // Per-user read state is derived from ConversationParticipant.LastRead*.
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }

        public bool IsDeletedForEveryone { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }
        public BaseUser Sender { get; set; }
    }
    public enum MsgStatus
    {
        Sending,
        Sent,
        Delivered,
        Read
    }
}
