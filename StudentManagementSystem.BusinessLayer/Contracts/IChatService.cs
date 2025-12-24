using StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface IChatService
    {
        Task<ViewConversationDTO> SendMessageAsync(string senderId, string? recieverId, int? conversationId, string textMessage);
        Task<ViewConversationsDTO> GetUserConversationsAsync(string userId);
        Task<ViewConversationDTO> ViewConversationAsync(string userId, int conversationId, int pageNumber, int pageSize = 20);
        Task<List<int>> GetUserConversationIds(string userId);
    }
}
