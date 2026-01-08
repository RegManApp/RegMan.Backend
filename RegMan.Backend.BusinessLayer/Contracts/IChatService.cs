using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IChatService
    {
        Task<ViewConversationDTO> SendMessageAsync(string senderId, string? recieverId, int? conversationId, string textMessage);
        Task<ViewMessageDTO> SendMessageToConversationAsync(string senderId, int conversationId, string textMessage, string? clientMessageId = null);
        Task<ViewMessageDTO> SendSystemMessageToConversationAsync(int conversationId, string textMessage, string? clientMessageId = null);
        Task<ViewConversationsDTO> GetUserConversationsAsync(string userId);
        Task<ViewConversationDTO> ViewConversationAsync(string userId, int conversationId, int pageNumber, int pageSize = 20);
        Task<ViewConversationDTO> ViewConversationByCursorAsync(string userId, int conversationId, int? beforeMessageId, int pageSize = 20);
        Task<List<int>> GetUserConversationIds(string userId);

        Task<ViewConversationDTO> GetOrCreateDirectConversationAsync(string userId, string otherUserId, int pageNumber = 1, int pageSize = 20);
        Task<ViewConversationDTO> GetOrCreateAnnouncementsConversationAsync(string userId, int pageNumber = 1, int pageSize = 20);
        Task<List<ChatUserSearchResultDTO>> SearchUsersAsync(string requesterUserId, string query, int limit = 20);

        Task<List<MessageReadReceiptDTO>> MarkConversationMessagesReadAsync(string readerUserId, int conversationId);
        Task UpdateUserLastSeenAsync(string userId, DateTime lastSeenAtUtc);
        Task DeleteMessageForMeAsync(string userId, int conversationId, int messageId);
        Task DeleteMessageForEveryoneAsync(string userId, int conversationId, int messageId);
    }
}
