namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface IAuditLogService
    {
        Task LogAsync(
            string userId,
            string userEmail,
            string action,
            string entityName,
            string entityId
        );
    }
}
