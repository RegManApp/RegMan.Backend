using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO
{
    public class ViewConversationsDTO
    {
        public List<ViewConversationSummaryDTO> Conversations { get; set; } = new List<ViewConversationDTO>();
        public string? ErrorMessage { get; set; } = string.Empty;
    }
}
