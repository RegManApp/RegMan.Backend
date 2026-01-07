using RegMan.Backend.BusinessLayer.DTOs.Notifications;

namespace RegMan.Backend.BusinessLayer.Contracts
{
    public interface IReminderRulesService
    {
        Task<List<ReminderRuleDTO>> GetRulesAsync(string userId, CancellationToken cancellationToken);
        Task<List<ReminderRuleDTO>> ReplaceRulesAsync(string userId, List<ReminderRuleDTO> rules, CancellationToken cancellationToken);
    }
}
