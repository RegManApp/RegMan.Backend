using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Entities
{
    public class Message
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; }
        public DateTime SentAt { get; set; }
        public string TextMessage { get; set; }
        public MsgStatus Status { get; set; }
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }
        public BaseUser Sender{ get; set; }
    }
    public enum MsgStatus
    {
        Sending,
        Sent,
        Delivered,
        Read
    }
}
