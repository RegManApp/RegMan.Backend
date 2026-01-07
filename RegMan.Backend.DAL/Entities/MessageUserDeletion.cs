using System;

namespace RegMan.Backend.DAL.Entities
{
    public class MessageUserDeletion
    {
        public int MessageId { get; set; }
        public Message Message { get; set; }

        public string UserId { get; set; }
        public BaseUser User { get; set; }

        public DateTime DeletedAt { get; set; }
    }
}