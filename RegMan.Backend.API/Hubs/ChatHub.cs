using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService chatService;
        private readonly INotificationService notificationService;

        public ChatHub(IChatService chatService, INotificationService notificationService)
        {
            this.chatService = chatService;
            this.notificationService = notificationService;
        }
        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.UserIdentifier;
                if (userId == null)
                {
                    throw new Exception("UserIdentifier is null");
                }
                Console.WriteLine($"Connected: {userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectedAsync: {ex.Message}");
                throw;
            }
            await base.OnConnectedAsync();
        }

        public async Task<ViewConversationDTO> SendMessage(
         string receiverId,
         string textMessage)
        {
            var senderId = Context.UserIdentifier!;
            var conversation = await chatService.SendMessageAsync(
                senderId,
                receiverId,
                textMessage
            );

            //push message to receiver (real-time)
            var lastMessage = conversation.Messages.Last();

            await Clients.User(receiverId)
                .SendAsync("ReceiveMessage", lastMessage);

            // Also create a notification for the receiver (demo-critical)
            await notificationService.CreateNotificationAsync(
                userId: receiverId,
                type: NotificationType.General,
                title: "New message",
                message: $"New message from {lastMessage.SenderName}: {lastMessage.Content}",
                entityType: "Conversation",
                entityId: conversation.ConversationId
            );

            //return full conversation to sender
            return conversation;
        }
        // get the specific conversation
        public async Task<ViewConversationDTO> ViewConversation(int conversationId, int pageNumber, int pageSize = 20)
        {
            var userId = Context.UserIdentifier!;
            var conversation = await chatService.ViewConversationAsync(userId, conversationId, pageNumber, pageSize);
            return conversation;
        }
        //get all conversations (chats) for the user
        public async Task<ViewConversationsDTO> GetAllConversations()
        {
            var userId = Context.UserIdentifier!;
            var conversations = await chatService.GetUserConversationsAsync(userId);
            return conversations;
        }
    }
}
