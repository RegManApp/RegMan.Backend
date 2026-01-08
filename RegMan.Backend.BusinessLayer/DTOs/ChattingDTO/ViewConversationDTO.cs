using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.BusinessLayer.DTOs.ChattingDTO
{
    public class ViewConversationDTO
    {
        public int ConversationId { get; set; }
        public required string DisplayName { get; set; }
        public string? ValidationMessage { get; set; }
        // IDs of participants in this conversation (useful for frontend to determine receiver)
        public List<string> ParticipantIds { get; set; } = new List<string>();
        //public List<string> ParticipantNames { get; set; } = new List<string>();
        public List<ViewMessageDTO> Messages { get; set; } = new List<ViewMessageDTO>();
    }
}
