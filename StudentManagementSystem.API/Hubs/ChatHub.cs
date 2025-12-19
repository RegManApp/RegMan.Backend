using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO;

namespace StudentManagementSystem.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService chatService;

        public ChatHub(IChatService chatService)
        {
            this.chatService = chatService;
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
