using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Services
{
    public class SmartOfficeHoursNoShowHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmartOfficeHoursNoShowHostedService> _logger;

        public SmartOfficeHoursNoShowHostedService(IServiceScopeFactory scopeFactory, ILogger<SmartOfficeHoursNoShowHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ISmartOfficeHoursService>();

                    await svc.AutoNoShowExpiredReadyEntriesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Smart Office Hours no-show loop failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown
                }
            }
        }
    }
}
