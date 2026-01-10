using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RegMan.Backend.BusinessLayer.Contracts;

namespace RegMan.Backend.API.Services
{
    public class SmartOfficeHoursQrRotationHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SmartOfficeHoursQrRotationHostedService> _logger;

        public SmartOfficeHoursQrRotationHostedService(IServiceScopeFactory scopeFactory, ILogger<SmartOfficeHoursQrRotationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Rotate on a fixed cadence; defaults live in SmartOfficeHoursOptions.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<ISmartOfficeHoursService>();

                    await svc.RotateReadyQrTokensAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Smart Office Hours QR rotation loop failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // shutdown
                }
            }
        }
    }
}
