using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.BusinessLayer.DTOs.ChattingDTO;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.BusinessLayer.Services
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
        private async Task<Conversation> CreateConversationAsync( List<string> UserIds)
        {
            var distinctUserIds = UserIds.Distinct().ToList();

            if (distinctUserIds.Count < 2)
                throw new ArgumentException("A conversation must have at least two users.");

            var users = await userManager.Users
                .Where(u => UserIds.Contains(u.Id))
                .ToListAsync();

            if (users.Count != UserIds.Distinct().Count())
                throw new KeyNotFoundException("One or more user IDs are invalid.");
            Conversation conversation = new Conversation
            {
                Participants = users.Select(u => new ConversationParticipant
                {
                    UserId = u.Id
                }).ToList()
            };
            foreach( var participant in conversation.Participants)
            {
                await participantRepository.AddAsync(participant);
            }

            await convoRepository.AddAsync(conversation);
            await unitOfWork.SaveChangesAsync();
            return conversation;
        }
        //send a message to a user
        public async Task<ViewConversationDTO> SendMessageAsync(string senderId, string? recieverId, int? conversationId,string textMessage)
        {
            if (string.IsNullOrWhiteSpace(textMessage))
                throw new ArgumentException("Message text cannot be empty.", nameof(textMessage));
            Conversation? conversation = null;
            if (conversationId.HasValue) //existing convo or group chat
            {
                conversation = await convoRepository.GetByIdAsync(conversationId.Value);
                if (conversation is null)
                    throw new KeyNotFoundException("Conversation not found.");
                var participants = await convoRepository.GetConversationParticipantsAsync(conversationId.Value);
                if (!participants.Any(p => p.UserId == senderId))
                    throw new UnauthorizedAccessException("Sender is not a participant of the conversation.");
            }
            else if(!string.IsNullOrEmpty(recieverId))//convo is null, but receiver ID provided (1 to 1 new chat)
            {
                conversation = await CreateConversationAsync(new List<string> { senderId, recieverId });
            }
            else if (string.IsNullOrWhiteSpace(recieverId))
                throw new ArgumentException("Receiver ID must be provided when conversation ID is not specified.", nameof(recieverId));

            //Conversation? conversation = await convoRepository.GetConversationByParticipantsAsync(senderId, recieverId);
            //if (conversation is null) 
            //    conversation = await CreateConversationAsync(new List<string> { senderId, recieverId });
            var message = new Message
            {
                SenderId = senderId,
                ConversationId = conversation.ConversationId,
                TextMessage = textMessage,
                SentAt = DateTime.UtcNow,
                Status = MsgStatus.Sent
            };
            await messageRepository.AddAsync(message);
            await unitOfWork.SaveChangesAsync();
            return await ViewConversationAsync(senderId, conversation.ConversationId, 1, 20);
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
        public async Task<ViewConversationDTO> ViewConversationAsync(string userId,int conversationId, int pageNumber, int pageSize=20)
        {
            string validationMessage = null;
            var conversation = await convoRepository.GetByIdAsync(conversationId);
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
                    Timestamp = m.SentAt
                }
                ).ToListAsync();
            if(Messages.Count == 0)
            {
                validationMessage = "No previous messages.";
            }
            string displayName=string.Empty;
            var participants = await convoRepository.GetConversationParticipantsAsync(conversationId);
            if (participants.Distinct().Count()>2) {
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
                displayName = participants.Where(p => p.UserId != userId).Select(p => p.User.FullName).FirstOrDefault();

            return new ViewConversationDTO
            {
                ConversationId = conversationId,
                Messages = Messages,
                DisplayName = displayName,
                ValidationMessage = validationMessage ?? null
            };
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
