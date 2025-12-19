using StudentManagementSystem.DAL.Entities;
using StudentManagementSystem.DAL.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.DAL.Contracts
{
    public interface IMessageRepository : IBaseRepository<Message>
    {
        Task<IEnumerable<Message>> GetByConversationIdAsync(int ConversationId);
    }
}
