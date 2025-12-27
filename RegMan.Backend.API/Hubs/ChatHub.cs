using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.DAL.Entities;
using System.Collections.Concurrent;

namespace RegMan.Backend.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, int> OnlineConnectionCounts = new();

        private readonly IChatService chatService;
        private readonly INotificationService notificationService;
        private readonly ILogger<ChatHub> logger;

        public ChatHub(IChatService chatService, INotificationService notificationService, ILogger<ChatHub> logger)
        {
            this.chatService = chatService;
            this.notificationService = notificationService;
            this.logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.UserIdentifier;
                if (userId == null)
                {
                    throw new HubException("Unauthorized");
                }

                // Presence tracking (per-connection)
                var newCount = OnlineConnectionCounts.AddOrUpdate(userId, 1, (_, existing) => existing + 1);
                if (newCount == 1)
                {
                    await Clients.All.SendAsync("UserPresenceChanged", new { userId, isOnline = true });
                }

                // get all conversation IDs this user belongs to
                var userConvos = await chatService.GetUserConversationIds(userId);

                foreach (var id in userConvos)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, id.ToString());
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in ChatHub.OnConnectedAsync");
                throw;
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userId = Context.UserIdentifier;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    if (OnlineConnectionCounts.TryGetValue(userId, out var existing))
                    {
                        if (existing <= 1)
                        {
                            OnlineConnectionCounts.TryRemove(userId, out _);
                            await Clients.All.SendAsync("UserPresenceChanged", new { userId, isOnline = false });
                        }
                        else
                        {
                            OnlineConnectionCounts[userId] = existing - 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error in ChatHub.OnDisconnectedAsync");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public Task<List<string>> GetOnlineUsers()
        {
            var users = OnlineConnectionCounts.Keys.ToList();
            return Task.FromResult(users);
        }

        public async Task<ViewMessageDTO> SendMessageAsync(
            string? receiverId,
            int? conversationId,
            string textMessage)
        {
            var senderId = Context.UserIdentifier!;
            if (conversationId is null)
                throw new HubException("conversationId is required");

            var message = await chatService.SendMessageToConversationAsync(
                senderId,
                conversationId.Value,
                textMessage
            );

            var conversationIdStr = conversationId.Value.ToString();

            // Ensure the sender is in the group (important for new conversations created via REST after connect)
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationIdStr);

            // Broadcast to all participants in this conversation
            await Clients.Group(conversationIdStr).SendAsync("ReceiveMessage", message);

            // Receiver notifications: for 1:1 chats receiverId is available in client, but we don't rely on it.
            // If receiverId is present, we keep notifications minimal and safe.
            if (!string.IsNullOrWhiteSpace(receiverId) && receiverId != senderId)
            {
                await notificationService.CreateNotificationAsync(
                    userId: receiverId,
                    type: NotificationType.General,
                    title: "New message",
                    message: $"New message from {message.SenderName}: {message.Content}",
                    entityType: "Conversation",
                    entityId: message.ConversationId
                );
            }

            return message;
        }

        public async Task JoinConversationGroup(int conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            // This will throw if user isn't allowed to view the conversation.
            await chatService.ViewConversationAsync(userId, conversationId, pageNumber: 1, pageSize: 1);

            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
        }

        public async Task<ViewConversationDTO> ViewConversation(int conversationId, int pageNumber, int pageSize = 20)
        {
            var userId = Context.UserIdentifier!;
            var conversation = await chatService.ViewConversationAsync(userId, conversationId, pageNumber, pageSize);
            return conversation;
        }

        public async Task<ViewConversationsDTO> GetAllConversations()
        {
            var userId = Context.UserIdentifier!;
            var conversations = await chatService.GetUserConversationsAsync(userId);
            return conversations;
        }
    }
}
