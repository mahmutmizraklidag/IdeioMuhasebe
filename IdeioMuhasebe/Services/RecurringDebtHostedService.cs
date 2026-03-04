using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdeioMuhasebe.Services
{
    // Her ayın 1'inde (ve uygulama açılışında) yinelenen borçları üretir
    public class RecurringDebtHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public RecurringDebtHostedService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uygulama açılınca bir kere çalıştır
            await RunOnce(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Bir sonraki ayın 1'ine kadar bekle
                var now = DateTime.Now;
                var nextMonthFirst = new DateTime(now.Year, now.Month, 1).AddMonths(1);
                var delay = nextMonthFirst - now;

                if (delay < TimeSpan.FromSeconds(1))
                    delay = TimeSpan.FromSeconds(1);

                await Task.Delay(delay, stoppingToken);

                await RunOnce(stoppingToken);
            }
        }

        private async Task RunOnce(CancellationToken token)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<RecurringDebtService>();

                // ✅ FIX: artık 1 argüman alıyor
                await svc.EnsureGeneratedAsync(DateTime.Today);
            }
            catch
            {
                // İstersen burada log yazabilirsin (ILogger ekleyerek)
            }
        }
    }
}