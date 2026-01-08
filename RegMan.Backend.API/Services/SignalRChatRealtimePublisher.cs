using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.API.Hubs;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;

namespace RegMan.Backend.API.Services
{
    public class SignalRChatRealtimePublisher : IChatRealtimePublisher
    {
        private readonly IHubContext<ChatHub> _hub;

        public SignalRChatRealtimePublisher(IHubContext<ChatHub> hub)
        {
            _hub = hub;
        }

        public async Task PublishSystemMessageCreatedAsync(int conversationId, ViewMessageDTO message)
        {
            var group = conversationId.ToString();
            await _hub.Clients.Group(group).SendAsync("SystemMessageCreated", message);
            // Back-compat: existing frontend reload logic listens on ReceiveMessage.
            await _hub.Clients.Group(group).SendAsync("ReceiveMessage", message);
        }

        public async Task PublishConversationCreatedAsync(string userId, int conversationId)
        {
            await _hub.Clients.User(userId).SendAsync("ConversationCreated", conversationId);
        }
    }
}
