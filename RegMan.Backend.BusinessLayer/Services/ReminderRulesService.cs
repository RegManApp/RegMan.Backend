using Microsoft.EntityFrameworkCore;
using RegMan.Backend.BusinessLayer.Contracts;
using RegMan.Backend.BusinessLayer.DTOs.Notifications;
using RegMan.Backend.DAL.DataContext;
using RegMan.Backend.DAL.Entities.Calendar;

namespace RegMan.Backend.BusinessLayer.Services
{
    internal sealed class ReminderRulesService : IReminderRulesService
    {
        private readonly AppDbContext context;

        public ReminderRulesService(AppDbContext context)
        {
            this.context = context;
        }

        public async Task<List<ReminderRuleDTO>> GetRulesAsync(string userId, CancellationToken cancellationToken)
        {
            var rules = await context.UserReminderRules
                .AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.TriggerType)
                .ToListAsync(cancellationToken);

            if (rules.Count == 0)
            {
                // Defaults (in-app only): class + office hour reminders 15 minutes before.
                return new List<ReminderRuleDTO>
                {
                    new() { RuleId = 0, TriggerType = ReminderTriggerType.Class, MinutesBefore = 15, ChannelMask = ReminderChannel.InApp, IsEnabled = true },
                    new() { RuleId = 0, TriggerType = ReminderTriggerType.OfficeHour, MinutesBefore = 15, ChannelMask = ReminderChannel.InApp, IsEnabled = true },
                    new() { RuleId = 0, TriggerType = ReminderTriggerType.RegistrationDeadline, MinutesBefore = 24 * 60, ChannelMask = ReminderChannel.InApp, IsEnabled = true },
                    new() { RuleId = 0, TriggerType = ReminderTriggerType.WithdrawDeadline, MinutesBefore = 24 * 60, ChannelMask = ReminderChannel.InApp, IsEnabled = true }
                };
            }

            return rules.Select(r => new ReminderRuleDTO
            {
                RuleId = r.RuleId,
                TriggerType = r.TriggerType,
                MinutesBefore = r.MinutesBefore,
                ChannelMask = r.ChannelMask,
                IsEnabled = r.IsEnabled
            }).ToList();
        }

        public async Task<List<ReminderRuleDTO>> ReplaceRulesAsync(string userId, List<ReminderRuleDTO> rules, CancellationToken cancellationToken)
        {
            var existing = await context.UserReminderRules
                .Where(r => r.UserId == userId)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
                context.UserReminderRules.RemoveRange(existing);

            foreach (var r in rules)
            {
                context.UserReminderRules.Add(new UserReminderRule
                {
                    UserId = userId,
                    TriggerType = r.TriggerType,
                    MinutesBefore = Math.Max(0, r.MinutesBefore),
                    ChannelMask = r.ChannelMask,
                    IsEnabled = r.IsEnabled,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            return await GetRulesAsync(userId, cancellationToken);
        }
    }
}
