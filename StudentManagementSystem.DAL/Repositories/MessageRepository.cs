using Microsoft.EntityFrameworkCore;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.DataContext;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Repositories
{
    internal class MessageRepository  : BaseRepository<Message>, IMessageRepository
    {
        private readonly AppDbContext _context;
        private DbSet<Message> _dbset;
        public MessageRepository(AppDbContext context) : base(context)
        {
            _context = context;
            _dbset = context.Set<Message>();
        }

        public async Task<IEnumerable<Message>> GetByConversationIdAsync(int ConversationId)
        {
            var messages = await _dbset.Where(m => m.ConversationId == ConversationId).ToListAsync();
            return messages;
        }
    }
}
