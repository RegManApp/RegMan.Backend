using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO
{
    public class ViewConversationSummaryDTO
    {
        public int ConversationId { get; set; }
        //public List<string> ParticipantNames { get; set; } = new List<string>();
        public string LastMessageSnippet { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        public string ConversationDisplayName { get; set; } = string.Empty;
    }
}
