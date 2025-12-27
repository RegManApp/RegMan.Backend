using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.ChattingDTO;
using RegMan.Backend.BusinessLayer.Exceptions;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.Entities;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal class ChatService : IChatService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IConversationRepository convoRepository;
        private readonly IMessageRepository messageRepository;
        private readonly IBaseRepository<ConversationParticipant> participantRepository;
        private readonly UserManager<BaseUser> userManager;
        public ChatService(IUnitOfWork unitOfWork, UserManager<BaseUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.userManager = userManager;
            this.convoRepository = unitOfWork.Conversations;
            this.messageRepository = unitOfWork.Messages;
            this.participantRepository = unitOfWork.ConversationParticipants;
        }

        private async Task<Conversation> CreateConversationAsync(List<string> userIds)
        {
            var distinctUserIds = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (distinctUserIds.Count < 2)
                throw new BadRequestException("A conversation must have at least two users.");

            var users = await userManager.Users
                .Where(u => distinctUserIds.Contains(u.Id))
                .ToListAsync();

            if (users.Count != distinctUserIds.Count)
                throw new NotFoundException("One or more user IDs are invalid.");

            Conversation conversation = new Conversation
            {
                Participants = users.Select(u => new ConversationParticipant
                {
                    UserId = u.Id
                }).ToList()
            };

            await convoRepository.AddAsync(conversation);
            await unitOfWork.SaveChangesAsync();
            return conversation;
        }

        public async Task<List<ChatUserSearchResultDTO>> SearchUsersAsync(string requesterUserId, string query, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(requesterUserId))
                throw new UnauthorizedException();

            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return new List<ChatUserSearchResultDTO>();

            limit = Math.Clamp(limit, 1, 50);
            var q = query.Trim();

            // Search by: FullName, Email, or UserId. Exclude the requester.
            var results = await userManager.Users
                .AsNoTracking()
                .Where(u => u.Id != requesterUserId)
                .Where(u =>
                    u.FullName.Contains(q) ||
                    (u.Email != null && u.Email.Contains(q)) ||
                    u.Id.Contains(q))
                .OrderBy(u => u.FullName)
                .Take(limit)
                .Select(u => new ChatUserSearchResultDTO
                {
                    UserId = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    Role = u.Role
                })
                .ToListAsync();

            return results;
        }

        public async Task<ViewConversationDTO> GetOrCreateDirectConversationAsync(string userId, string otherUserId, int pageNumber = 1, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            if (string.IsNullOrWhiteSpace(otherUserId))
                throw new BadRequestException("Other user ID is required.");

            if (userId == otherUserId)
                throw new BadRequestException("Cannot start a conversation with yourself.");

            var otherExists = await userManager.Users.AsNoTracking().AnyAsync(u => u.Id == otherUserId);
            if (!otherExists)
                throw new NotFoundException("User not found.");

            var existing = await convoRepository.GetConversationByParticipantsAsync(userId, otherUserId);
            if (existing != null)
                return await ViewConversationAsync(userId, existing.ConversationId, pageNumber, pageSize);

            var created = await CreateConversationAsync(new List<string> { userId, otherUserId });
            return await ViewConversationAsync(userId, created.ConversationId, pageNumber, pageSize);
        }

        //send a message to a user
        public async Task<ViewConversationDTO> SendMessageAsync(string senderId, string? recieverId, int? conversationId, string textMessage)
        {
            if (string.IsNullOrWhiteSpace(senderId))
                throw new UnauthorizedException();

            if (string.IsNullOrWhiteSpace(textMessage))
                throw new BadRequestException("Message text cannot be empty.");

            Conversation? conversation = null;
            if (conversationId.HasValue) //existing convo or group chat
            {
                conversation = await convoRepository.GetByIdAsync(conversationId.Value);
                if (conversation is null)
                    throw new NotFoundException("Conversation not found.");
                var participants = await convoRepository.GetConversationParticipantsAsync(conversationId.Value);
                if (!participants.Any(p => p.UserId == senderId))
                    throw new ForbiddenException("Sender is not a participant of the conversation.");
            }
            else if (!string.IsNullOrEmpty(recieverId))//convo is null, but receiver ID provided (1 to 1 new chat)
            {
                // Reuse existing direct conversation if present (avoid duplicates)
                conversation = await convoRepository.GetConversationByParticipantsAsync(senderId, recieverId);
                if (conversation is null)
                {
                    conversation = await CreateConversationAsync(new List<string> { senderId, recieverId });
                }
            }
            else if (string.IsNullOrWhiteSpace(recieverId))
                throw new BadRequestException("Receiver ID must be provided when conversation ID is not specified.");

            if (conversation is null)
                throw new NotFoundException("Conversation not found.");

            //Conversation? conversation = await convoRepository.GetConversationByParticipantsAsync(senderId, recieverId);
            //if (conversation is null) 
            //    conversation = await CreateConversationAsync(new List<string> { senderId, recieverId });
            var message = new Message
            {
                SenderId = senderId,
                ConversationId = conversation.ConversationId,
                TextMessage = textMessage,
                SentAt = DateTime.UtcNow,
                Status = MsgStatus.Sent,
                IsRead = false,
                ReadAt = null
            };
            await messageRepository.AddAsync(message);
            await unitOfWork.SaveChangesAsync();
            return await ViewConversationAsync(senderId, conversation.ConversationId, 1, 20);
        }

        public async Task<ViewMessageDTO> SendMessageToConversationAsync(string senderId, int conversationId, string textMessage)
        {
            if (string.IsNullOrWhiteSpace(senderId))
                throw new UnauthorizedException();

            if (conversationId <= 0)
                throw new BadRequestException("Conversation ID is required.");

            if (string.IsNullOrWhiteSpace(textMessage))
                throw new BadRequestException("Message content is required.");

            var conversation = await convoRepository.GetByIdAsync(conversationId);
            if (conversation is null)
                throw new NotFoundException("Conversation not found.");

            var participants = await convoRepository.GetConversationParticipantsAsync(conversationId);
            if (!participants.Any(p => p.UserId == senderId))
                throw new ForbiddenException("Sender is not a participant of the conversation.");

            var message = new Message
            {
                SenderId = senderId,
                ConversationId = conversationId,
                TextMessage = textMessage,
                SentAt = DateTime.UtcNow,
                Status = MsgStatus.Sent,
                IsRead = false,
                ReadAt = null
            };

            await messageRepository.AddAsync(message);
            await unitOfWork.SaveChangesAsync();

            // Load sender name via navigation (may require lazy-loading not enabled) - query explicitly.
            var dto = await messageRepository.GetAllAsQueryable()
                .AsNoTracking()
                .Where(m => m.MessageId == message.MessageId)
                .Select(m => new ViewMessageDTO
                {
                    MessageId = m.MessageId,
                    ConversationId = m.ConversationId,
                    Content = m.TextMessage,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    Status = m.Status,
                    Timestamp = m.SentAt
                    ,
                    IsRead = m.IsRead
                    ,
                    ReadAt = m.ReadAt
                })
                .FirstAsync();

            return dto;
        }

        //View all user conversations
        public async Task<ViewConversationsDTO> GetUserConversationsAsync(string userId)
        {

            var conversations = await participantRepository.GetAllAsQueryable().AsNoTracking()
                .Where(cp => cp.UserId == userId)
                .Select(cp => new ViewConversationSummaryDTO
                {
                    ConversationId = cp.Conversation.ConversationId,
                    LastMessageSnippet = cp.Conversation.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.TextMessage.Length > 30 ? m.TextMessage.Substring(0, 30) + "..." : m.TextMessage)
                        .FirstOrDefault() ?? string.Empty,
                    LastMessageTime = cp.Conversation.Messages
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => m.SentAt)
                        .FirstOrDefault(),
                    ConversationDisplayName = cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.FullName)
                        .FirstOrDefault() ?? "No Participants"
                    ,
                    UnreadCount = cp.Conversation.Messages
                        .Count(m => m.SenderId != userId && !m.IsRead)
                })
                .ToListAsync();

            if (conversations == null)
            {
                ViewConversationsDTO errorResult = new ViewConversationsDTO
                {
                    Conversations = new List<ViewConversationSummaryDTO>(),
                    ErrorMessage = "No conversations found for the user."
                };
                return errorResult;
            }
            ViewConversationsDTO conversationsDTO = new ViewConversationsDTO
            {
                Conversations = conversations
            };
            return conversationsDTO;
        }
        //View specific conversation in details (view chat)
        public async Task<ViewConversationDTO> ViewConversationAsync(string userId, int conversationId, int pageNumber, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            if (conversationId <= 0)
                throw new BadRequestException("Conversation ID is required.");

            if (pageNumber <= 0)
                pageNumber = 1;

            if (pageSize <= 0 || pageSize > 100)
                pageSize = 20;

            string? validationMessage = null;

            // Ensure the requesting user is a participant.
            var allowedConversation = await convoRepository.GetSpecificUserConversationAsync(userId, conversationId);
            if (allowedConversation is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var conversation = allowedConversation;
            var msgsQuery = messageRepository.GetAllAsQueryable();
            msgsQuery = msgsQuery.Where(m => m.ConversationId == conversationId);
            msgsQuery = msgsQuery.OrderByDescending(m => m.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt);
            List<ViewMessageDTO> Messages = await msgsQuery.Select(
                m => new ViewMessageDTO
                {
                    Content = m.TextMessage,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    MessageId = m.MessageId,
                    Status = m.Status,
                    Timestamp = m.SentAt,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt
                }
                ).ToListAsync();
            if (Messages.Count == 0)
            {
                validationMessage = "No previous messages.";
            }
            string displayName = string.Empty;
            var participants = await convoRepository.GetConversationParticipantsAsync(conversationId);
            if (participants.Distinct().Count() > 2)
            {
                if (!string.IsNullOrWhiteSpace(conversation.ConversationName))
                    displayName = conversation.ConversationName;
                else
                {
                    displayName = string.Join(", ",
                        conversation.Participants
                            .Where(p => p.UserId != userId)
                            .Select(p => p.User.FullName)
                            .Take(3));
                }

            }
            else
                displayName = participants.Where(p => p.UserId != userId).Select(p => p.User.FullName).FirstOrDefault() ?? string.Empty;

            return new ViewConversationDTO
            {
                ConversationId = conversationId,
                Messages = Messages,
                DisplayName = displayName ?? string.Empty,
                ValidationMessage = validationMessage ?? null,
                ParticipantIds = participants.Select(p => p.UserId).ToList()
            };
        }

        public async Task<List<MessageReadReceiptDTO>> MarkConversationMessagesReadAsync(string readerUserId, int conversationId)
        {
            if (string.IsNullOrWhiteSpace(readerUserId))
                throw new UnauthorizedException();

            if (conversationId <= 0)
                throw new BadRequestException("Conversation ID is required.");

            // Ensure reader is a participant.
            var allowedConversation = await convoRepository.GetSpecificUserConversationAsync(readerUserId, conversationId);
            if (allowedConversation is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var readAt = DateTime.UtcNow;

            // Mark as read: messages in this conversation NOT sent by the reader.
            var unreadQuery = messageRepository.GetAllAsQueryable()
                .Where(m => m.ConversationId == conversationId)
                .Where(m => m.SenderId != readerUserId)
                .Where(m => !m.IsRead);

            // Load minimal set to update + build receipts.
            var unread = await unreadQuery
                .Select(m => new { m.MessageId, m.SenderId })
                .ToListAsync();

            if (unread.Count == 0)
                return new List<MessageReadReceiptDTO>();

            // Update entities (EF Core tracks via query; re-query tracked entities).
            var ids = unread.Select(x => x.MessageId).ToList();
            var toUpdate = await messageRepository.GetAllAsQueryable()
                .Where(m => ids.Contains(m.MessageId))
                .ToListAsync();

            foreach (var m in toUpdate)
            {
                m.IsRead = true;
                m.ReadAt = readAt;
                m.Status = MsgStatus.Read;
            }

            await unitOfWork.SaveChangesAsync();

            // Create one receipt per sender so the hub/controller can notify correct users.
            var receipts = unread
                .GroupBy(x => x.SenderId)
                .Select(g => new MessageReadReceiptDTO
                {
                    ConversationId = conversationId,
                    SenderId = g.Key,
                    ReaderId = readerUserId,
                    ReadAt = readAt,
                    MessageIds = g.Select(x => x.MessageId).ToList()
                })
                .ToList();

            return receipts;
        }
        public async Task<List<int>> GetUserConversationIds(string userId)
        {
            var conversationIds = await participantRepository.GetAllAsQueryable()
                .Where(cp => cp.UserId == userId)
                .Select(cp => cp.ConversationId)
                .ToListAsync();
            return conversationIds;
        }

    }
}
