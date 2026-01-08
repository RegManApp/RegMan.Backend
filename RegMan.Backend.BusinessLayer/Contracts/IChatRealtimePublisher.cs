using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IChatRealtimePublisher
    {
        Task PublishSystemMessageCreatedAsync(int conversationId, ViewMessageDTO message);
        Task PublishConversationCreatedAsync(string userId, int conversationId);
    }
}
