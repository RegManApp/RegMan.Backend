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
        private readonly IBaseRepository<MessageUserDeletion> messageUserDeletionRepository;
        private readonly UserManager<BaseUser> userManager;
        public ChatService(IUnitOfWork unitOfWork, UserManager<BaseUser> userManager)
        {
            this.unitOfWork = unitOfWork;
            this.userManager = userManager;
            this.convoRepository = unitOfWork.Conversations;
            this.messageRepository = unitOfWork.Messages;
            this.participantRepository = unitOfWork.ConversationParticipants;
            this.messageUserDeletionRepository = unitOfWork.MessageUserDeletions;
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
            var now = DateTime.UtcNow;
            var message = new Message
            {
                SenderId = senderId,
                ConversationId = conversation.ConversationId,
                TextMessage = textMessage,
                SentAt = now,
                ServerReceivedAt = now,
                Status = MsgStatus.Sent,
                IsRead = false,
                ReadAt = null
            };
            await messageRepository.AddAsync(message);
            await unitOfWork.SaveChangesAsync();

            conversation.LastActivityAt = now;
            conversation.LastMessageId = message.MessageId;
            await unitOfWork.SaveChangesAsync();
            return await ViewConversationAsync(senderId, conversation.ConversationId, 1, 20);
        }

        public async Task<ViewMessageDTO> SendMessageToConversationAsync(string senderId, int conversationId, string textMessage, string? clientMessageId = null)
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

            clientMessageId = string.IsNullOrWhiteSpace(clientMessageId) ? null : clientMessageId.Trim();
            if (!string.IsNullOrWhiteSpace(clientMessageId))
            {
                var existing = await messageRepository.GetAllAsQueryable()
                    .AsNoTracking()
                    .Where(m => m.ConversationId == conversationId)
                    .Where(m => m.SenderId == senderId)
                    .Where(m => m.ClientMessageId == clientMessageId)
                    .Select(m => new ViewMessageDTO
                    {
                        MessageId = m.MessageId,
                        ConversationId = m.ConversationId,
                        ClientMessageId = m.ClientMessageId,
                        ServerReceivedAt = m.ServerReceivedAt,
                        Content = m.IsDeletedForEveryone ? "[deleted]" : m.TextMessage,
                        SenderId = m.SenderId,
                        SenderName = m.Sender.FullName,
                        Status = m.Status,
                        Timestamp = m.SentAt,
                        IsDeletedForEveryone = m.IsDeletedForEveryone,
                        DeletedAt = m.DeletedAt,
                        DeletedByUserId = m.DeletedByUserId,
                        IsRead = m.IsRead,
                        ReadAt = m.ReadAt
                    })
                    .FirstOrDefaultAsync();

                if (existing != null)
                    return existing;
            }

            var now = DateTime.UtcNow;

            var message = new Message
            {
                SenderId = senderId,
                ConversationId = conversationId,
                TextMessage = textMessage,
                SentAt = now,
                ServerReceivedAt = now,
                ClientMessageId = clientMessageId,
                Status = MsgStatus.Sent,
                IsRead = false,
                ReadAt = null
            };

            try
            {
                await messageRepository.AddAsync(message);
                await unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (!string.IsNullOrWhiteSpace(clientMessageId))
                {
                    var dup = await messageRepository.GetAllAsQueryable()
                        .AsNoTracking()
                        .Where(m => m.ConversationId == conversationId)
                        .Where(m => m.SenderId == senderId)
                        .Where(m => m.ClientMessageId == clientMessageId)
                        .Select(m => new ViewMessageDTO
                        {
                            MessageId = m.MessageId,
                            ConversationId = m.ConversationId,
                            ClientMessageId = m.ClientMessageId,
                            ServerReceivedAt = m.ServerReceivedAt,
                            Content = m.IsDeletedForEveryone ? "[deleted]" : m.TextMessage,
                            SenderId = m.SenderId,
                            SenderName = m.Sender.FullName,
                            Status = m.Status,
                            Timestamp = m.SentAt,
                            IsDeletedForEveryone = m.IsDeletedForEveryone,
                            DeletedAt = m.DeletedAt,
                            DeletedByUserId = m.DeletedByUserId,
                            IsRead = m.IsRead,
                            ReadAt = m.ReadAt
                        })
                        .FirstOrDefaultAsync();

                    if (dup != null)
                        return dup;
                }

                throw;
            }

            conversation.LastActivityAt = now;
            conversation.LastMessageId = message.MessageId;
            await unitOfWork.SaveChangesAsync();

            // Load sender name via navigation (may require lazy-loading not enabled) - query explicitly.
            var dto = await messageRepository.GetAllAsQueryable()
                .AsNoTracking()
                .Where(m => m.MessageId == message.MessageId)
                .Select(m => new ViewMessageDTO
                {
                    MessageId = m.MessageId,
                    ConversationId = m.ConversationId,
                    ClientMessageId = m.ClientMessageId,
                    ServerReceivedAt = m.ServerReceivedAt,
                    Content = m.IsDeletedForEveryone ? "[deleted]" : m.TextMessage,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    Status = m.Status,
                    Timestamp = m.SentAt
                    ,
                    IsDeletedForEveryone = m.IsDeletedForEveryone,
                    DeletedAt = m.DeletedAt,
                    DeletedByUserId = m.DeletedByUserId,
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

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            var conversations = await participantRepository.GetAllAsQueryable()
                .AsNoTracking()
                .Where(cp => cp.UserId == userId)
                .Select(cp => new ViewConversationSummaryDTO
                {
                    ConversationId = cp.Conversation.ConversationId,
                    LastMessageSnippet = cp.Conversation.Messages
                        .Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                            .Any(d => d.UserId == userId && d.MessageId == m.MessageId))
                        .OrderByDescending(m => m.MessageId)
                        .Select(m =>
                            m.IsDeletedForEveryone
                                ? "[deleted]"
                                : (m.TextMessage.Length > 30 ? m.TextMessage.Substring(0, 30) + "..." : m.TextMessage))
                        .FirstOrDefault() ?? string.Empty,
                    LastMessageTime = (cp.Conversation.LastActivityAt ?? cp.Conversation.Messages
                        .Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                            .Any(d => d.UserId == userId && d.MessageId == m.MessageId))
                        .OrderByDescending(m => m.MessageId)
                        .Select(m => (DateTime?)m.SentAt)
                        .FirstOrDefault()) ?? DateTime.MinValue,
                    ConversationDisplayName = cp.Conversation.Participants
                        .Where(p => p.UserId != userId)
                        .Select(p => p.User.FullName)
                        .FirstOrDefault() ?? "No Participants",
                    UnreadCount = cp.Conversation.Messages
                        .Where(m => m.SenderId != userId)
                        .Where(m => !m.IsDeletedForEveryone)
                        .Where(m => m.MessageId > (cp.LastReadMessageId ?? 0))
                        .Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                            .Any(d => d.UserId == userId && d.MessageId == m.MessageId))
                        .Count()
                })
                .OrderByDescending(c => c.LastMessageTime)
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
            msgsQuery = msgsQuery.Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                .Any(d => d.UserId == userId && d.MessageId == m.MessageId));
            msgsQuery = msgsQuery.OrderByDescending(m => m.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .OrderBy(m => m.SentAt);
            List<ViewMessageDTO> Messages = await msgsQuery.Select(
                m => new ViewMessageDTO
                {
                    Content = m.IsDeletedForEveryone ? "[deleted]" : m.TextMessage,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    MessageId = m.MessageId,
                    ClientMessageId = m.ClientMessageId,
                    ServerReceivedAt = m.ServerReceivedAt,
                    Status = m.Status,
                    Timestamp = m.SentAt,
                    IsDeletedForEveryone = m.IsDeletedForEveryone,
                    DeletedAt = m.DeletedAt,
                    DeletedByUserId = m.DeletedByUserId,
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

        public async Task<ViewConversationDTO> ViewConversationByCursorAsync(string userId, int conversationId, int? beforeMessageId, int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            if (conversationId <= 0)
                throw new BadRequestException("Conversation ID is required.");

            if (pageSize <= 0 || pageSize > 100)
                pageSize = 20;

            var allowedConversation = await convoRepository.GetSpecificUserConversationAsync(userId, conversationId);
            if (allowedConversation is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var msgsQuery = messageRepository.GetAllAsQueryable()
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                    .Any(d => d.UserId == userId && d.MessageId == m.MessageId));

            if (beforeMessageId.HasValue)
            {
                msgsQuery = msgsQuery.Where(m => m.MessageId < beforeMessageId.Value);
            }

            var page = await msgsQuery
                .OrderByDescending(m => m.MessageId)
                .Take(pageSize)
                .OrderBy(m => m.MessageId)
                .Select(m => new ViewMessageDTO
                {
                    MessageId = m.MessageId,
                    ConversationId = m.ConversationId,
                    ClientMessageId = m.ClientMessageId,
                    ServerReceivedAt = m.ServerReceivedAt,
                    Content = m.IsDeletedForEveryone ? "[deleted]" : m.TextMessage,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.FullName,
                    Status = m.Status,
                    Timestamp = m.SentAt,
                    IsDeletedForEveryone = m.IsDeletedForEveryone,
                    DeletedAt = m.DeletedAt,
                    DeletedByUserId = m.DeletedByUserId,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt
                })
                .ToListAsync();

            var participants = await convoRepository.GetConversationParticipantsAsync(conversationId);

            string displayName = string.Empty;
            if (participants.Distinct().Count() > 2)
            {
                if (!string.IsNullOrWhiteSpace(allowedConversation.ConversationName))
                    displayName = allowedConversation.ConversationName;
                else
                {
                    displayName = string.Join(", ",
                        allowedConversation.Participants
                            .Where(p => p.UserId != userId)
                            .Select(p => p.User.FullName)
                            .Take(3));
                }
            }
            else
            {
                displayName = participants.Where(p => p.UserId != userId).Select(p => p.User.FullName).FirstOrDefault() ?? string.Empty;
            }

            return new ViewConversationDTO
            {
                ConversationId = conversationId,
                Messages = page,
                DisplayName = displayName ?? string.Empty,
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

            var participant = await participantRepository.GetAllAsQueryable()
                .Where(cp => cp.ConversationId == conversationId && cp.UserId == readerUserId)
                .FirstOrDefaultAsync();

            if (participant is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var previousLastRead = participant.LastReadMessageId ?? 0;

            var newlyRead = await messageRepository.GetAllAsQueryable()
                .Where(m => m.ConversationId == conversationId)
                .Where(m => m.SenderId != readerUserId)
                .Where(m => !m.IsDeletedForEveryone)
                .Where(m => m.MessageId > previousLastRead)
                .Where(m => !messageUserDeletionRepository.GetAllAsQueryable()
                    .Any(d => d.UserId == readerUserId && d.MessageId == m.MessageId))
                .Select(m => new { m.MessageId, m.SenderId })
                .ToListAsync();

            var maxMessageId = await messageRepository.GetAllAsQueryable()
                .Where(m => m.ConversationId == conversationId)
                .Select(m => (int?)m.MessageId)
                .MaxAsync();

            participant.LastReadMessageId = Math.Max(previousLastRead, maxMessageId ?? previousLastRead);
            participant.LastReadAt = readAt;

            // Back-compat for current UI (best-effort): mark messages as read.
            if (newlyRead.Count > 0)
            {
                var ids = newlyRead.Select(x => x.MessageId).ToList();
                var toUpdate = await messageRepository.GetAllAsQueryable()
                    .Where(m => ids.Contains(m.MessageId))
                    .ToListAsync();

                foreach (var m in toUpdate)
                {
                    if (!m.IsRead)
                    {
                        m.IsRead = true;
                        m.ReadAt = readAt;
                        m.Status = MsgStatus.Read;
                    }
                }
            }

            await unitOfWork.SaveChangesAsync();

            return newlyRead
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
        }
        public async Task<List<int>> GetUserConversationIds(string userId)
        {
            var conversationIds = await participantRepository.GetAllAsQueryable()
                .Where(cp => cp.UserId == userId)
                .Select(cp => cp.ConversationId)
                .ToListAsync();
            return conversationIds;
        }

        public async Task UpdateUserLastSeenAsync(string userId, DateTime lastSeenAtUtc)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
                return;

            user.LastSeenAt = lastSeenAtUtc;
            await userManager.UpdateAsync(user);
        }

        public async Task DeleteMessageForMeAsync(string userId, int conversationId, int messageId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            if (conversationId <= 0 || messageId <= 0)
                throw new BadRequestException("Invalid conversation/message.");

            var allowedConversation = await convoRepository.GetSpecificUserConversationAsync(userId, conversationId);
            if (allowedConversation is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var exists = await messageRepository.GetAllAsQueryable()
                .AsNoTracking()
                .AnyAsync(m => m.MessageId == messageId && m.ConversationId == conversationId);

            if (!exists)
                throw new NotFoundException("Message not found.");

            var already = await messageUserDeletionRepository.GetAllAsQueryable()
                .AsNoTracking()
                .AnyAsync(d => d.UserId == userId && d.MessageId == messageId);

            if (already)
                return;

            await messageUserDeletionRepository.AddAsync(new MessageUserDeletion
            {
                MessageId = messageId,
                UserId = userId,
                DeletedAt = DateTime.UtcNow
            });

            await unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteMessageForEveryoneAsync(string userId, int conversationId, int messageId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException();

            if (conversationId <= 0 || messageId <= 0)
                throw new BadRequestException("Invalid conversation/message.");

            var allowedConversation = await convoRepository.GetSpecificUserConversationAsync(userId, conversationId);
            if (allowedConversation is null)
                throw new ForbiddenException("You are not a participant of this conversation.");

            var message = await messageRepository.GetAllAsQueryable()
                .Where(m => m.MessageId == messageId && m.ConversationId == conversationId)
                .FirstOrDefaultAsync();

            if (message is null)
                throw new NotFoundException("Message not found.");

            if (message.SenderId != userId)
                throw new ForbiddenException("Only the sender can delete for everyone.");

            if (message.IsDeletedForEveryone)
                return;

            message.IsDeletedForEveryone = true;
            message.DeletedAt = DateTime.UtcNow;
            message.DeletedByUserId = userId;
            message.TextMessage = string.Empty;

            await unitOfWork.SaveChangesAsync();
        }

    }
}
