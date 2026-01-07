using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Services
{
    internal sealed class ScheduledNotificationDispatcherHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<ScheduledNotificationDispatcherHostedService> logger;

        public ScheduledNotificationDispatcherHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ScheduledNotificationDispatcherHostedService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<ICalendarReminderEngine>();
                    await engine.DispatchDueAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Scheduled notification dispatch loop failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
            }
        }
    }
}
