using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.DAL.DataContext;
using StudentManagementSystem.DAL.Entities;

namespace StudentManagementSystem.BusinessLayer.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly AppDbContext context;

        public AuditLogService(AppDbContext context)
        {
            this.context = context;
        }

        public async Task LogAsync(
            string userId,
            string userEmail,
            string action,
            string entityName,
            string entityId)
        {
            var log = new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                EntityName = entityName,
                EntityId = entityId
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }
    }
}
