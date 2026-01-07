using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.Services;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;
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
                    // Broadcast presence only to users who share a conversation with this user.
                    var userConvosForPresence = await chatService.GetUserConversationIds(userId);
                    foreach (var convoId in userConvosForPresence)
                    {
                        await Clients.Group(convoId.ToString())
                            .SendAsync("UserPresenceChanged", new { userId, isOnline = true });
                    }
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
                            var lastSeenAt = DateTime.UtcNow;
                            await chatService.UpdateUserLastSeenAsync(userId, lastSeenAt);
                            // Broadcast presence only to users who share a conversation with this user.
                            var userConvosForPresence = await chatService.GetUserConversationIds(userId);
                            foreach (var convoId in userConvosForPresence)
                            {
                                await Clients.Group(convoId.ToString())
                                    .SendAsync("UserPresenceChanged", new { userId, isOnline = false, lastSeenAt });
                            }
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
            // SECURITY: Only return online users who share at least one conversation with the caller.
            // (Prevents leaking global user presence to all authenticated users.)
            return GetOnlineUsersInternalAsync();
        }

        private async Task<List<string>> GetOnlineUsersInternalAsync()
        {
            var requesterUserId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(requesterUserId))
                throw new HubException("Unauthorized");

            var convoIds = await chatService.GetUserConversationIds(requesterUserId);
            var allowedUserIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var convoId in convoIds)
            {
                // ViewConversationAsync enforces membership and returns participant IDs.
                var convo = await chatService.ViewConversationAsync(requesterUserId, convoId, pageNumber: 1, pageSize: 1);
                foreach (var participantId in convo.ParticipantIds)
                {
                    if (!string.IsNullOrWhiteSpace(participantId) && participantId != requesterUserId)
                        allowedUserIds.Add(participantId);
                }
            }

            var online = OnlineConnectionCounts.Keys
                .Where(id => allowedUserIds.Contains(id))
                .ToList();

            return online;
        }

        public async Task<ViewMessageDTO> SendMessageAsync(
            string? receiverId,
            int? conversationId,
            string textMessage,
            string? clientMessageId = null)
        {
            var senderId = Context.UserIdentifier!;
            if (conversationId is null)
                throw new HubException("conversationId is required");

            var message = await chatService.SendMessageToConversationAsync(
                senderId,
                conversationId.Value,
                textMessage,
                clientMessageId
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

        public async Task TypingStarted(int conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            await chatService.ViewConversationAsync(userId, conversationId, pageNumber: 1, pageSize: 1);
            await Clients.Group(conversationId.ToString()).SendAsync("UserTyping", new { conversationId, userId, isTyping = true });
        }

        public async Task TypingStopped(int conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            await chatService.ViewConversationAsync(userId, conversationId, pageNumber: 1, pageSize: 1);
            await Clients.Group(conversationId.ToString()).SendAsync("UserTyping", new { conversationId, userId, isTyping = false });
        }

        public async Task DeleteMessageForMe(int conversationId, int messageId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            await chatService.DeleteMessageForMeAsync(userId, conversationId, messageId);
            await Clients.Caller.SendAsync("MessageDeletedForMe", new { conversationId, messageId });
        }

        public async Task DeleteMessageForEveryone(int conversationId, int messageId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrWhiteSpace(userId))
                throw new HubException("Unauthorized");

            await chatService.DeleteMessageForEveryoneAsync(userId, conversationId, messageId);
            await Clients.Group(conversationId.ToString()).SendAsync("MessageDeletedForEveryone", new { conversationId, messageId });
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
