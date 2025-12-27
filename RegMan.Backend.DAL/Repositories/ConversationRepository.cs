using Microsoft.EntityFrameworkCore;
using RegMan.Backend.DAL.Contracts;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegMan.Backend.DAL.Repositories
{
    internal class ConversationRepository : BaseRepository<Conversation>, IConversationRepository
    {
        private readonly AppDbContext dbContext;
        private DbSet<Conversation> dbSet;
        public ConversationRepository(AppDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
            this.dbSet = dbContext.Set<Conversation>();
        }
        public async Task<IEnumerable<ConversationParticipant>> GetConversationParticipantsAsync(int conversationId)
        {
            var participants = await dbContext.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId)
                .Select(p => new ConversationParticipant
                {
                    Conversation = p.Conversation,
                    ConversationId = p.ConversationId,
                    User = p.User,
                    UserId = p.UserId
                })
                .ToListAsync();
            return participants;
        }
        public async Task<Conversation?> GetConversationByParticipantsAsync(string firstUserId, string secondUserId)
        {
            if (string.IsNullOrWhiteSpace(firstUserId) || string.IsNullOrWhiteSpace(secondUserId))
                return null;

            if (firstUserId == secondUserId)
                return null;

            // Match ONLY direct conversations with exactly 2 participants total.
            var conversation = await dbContext.ConversationParticipants
                .GroupBy(cp => cp.ConversationId)
                .Where(g => g.Count() == 2
                    && g.Any(cp => cp.UserId == firstUserId)
                    && g.Any(cp => cp.UserId == secondUserId))
                .Select(g => g.First().Conversation)
                .FirstOrDefaultAsync();

            return conversation;
        }
        public async Task<IEnumerable<Conversation>>? GetConversationsByUserIdAsync(string userId)
        {
            var conversations = await dbContext.ConversationParticipants
                .Where(cp => cp.UserId == userId)
                .Select(cp => cp.Conversation)
                .ToListAsync();
            return conversations;
        }
        public async Task<Conversation?> GetSpecificUserConversationAsync(string userId, int conversationId)
        {
            var conversation = await dbContext.ConversationParticipants
                .Where(cp => cp.UserId == userId && cp.ConversationId == conversationId)
                .Select(cp => cp.Conversation)
                .FirstOrDefaultAsync();
            return conversation;
        }

    }
}
