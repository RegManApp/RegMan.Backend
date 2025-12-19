using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Entities
{
    public class Conversation
    {
        public int ConversationId { get; set; }
        public string? ConversationName { get; set; }
        //public string FirstSenderId { get; set; }
        //public string LastSenderId { get; set; }
        //public BaseUser Firstsender { get; set; }
        //public BaseUser Lastsender { get; set; }
        public ICollection<Message> Messages { get; set; } = new HashSet<Message>();
        public ICollection<ConversationParticipant> Participants { get; set; } = new HashSet<ConversationParticipant>();
    }
}
