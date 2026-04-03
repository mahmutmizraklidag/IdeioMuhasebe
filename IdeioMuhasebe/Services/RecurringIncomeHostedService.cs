using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdeioMuhasebe.Services
{
    public class RecurringIncomeHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public RecurringIncomeHostedService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunOnce(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
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
                var svc = scope.ServiceProvider.GetRequiredService<RecurringIncomeService>();
                await svc.EnsureGeneratedAsync(DateTime.Today);
            }
            catch
            {
            }
        }
    }
}