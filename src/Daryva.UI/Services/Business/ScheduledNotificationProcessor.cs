using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Background processor that sends queued notifications at the exact 1st second of each minute (system time).
    /// </summary>
    public sealed class ScheduledNotificationProcessor : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IQueueProcessedNotifier _notifier;
        private readonly Timer _timer;
        private const int IntervalMs = 60_000;

        public ScheduledNotificationProcessor(IServiceProvider serviceProvider, IQueueProcessedNotifier notifier)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            var dueMs = MillisecondsUntilNextMinute();
            _timer = new Timer(_ => _ = ProcessAsync(), null, dueMs, IntervalMs);
        }

        private static int MillisecondsUntilNextMinute()
        {
            var now = DateTime.Now;
            var next = now.Date.AddHours(now.Hour).AddMinutes(now.Minute).AddSeconds(0);
            if (next <= now) next = next.AddMinutes(1);
            return (int)Math.Max(0, (next - now).TotalMilliseconds);
        }

        private async Task ProcessAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var sent = await notificationService.ProcessDueQueueAsync();
                if (sent > 0)
                    _notifier.NotifyProcessed(sent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduledNotificationProcessor] Error processing due queue: {ex.Message}");
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
