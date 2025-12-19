using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Entities
{
    public class ConversationParticipant
    {
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }
        public string UserId { get; set; }
        public BaseUser User { get; set; }
    }
}
