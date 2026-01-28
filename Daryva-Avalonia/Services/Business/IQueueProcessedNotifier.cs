using System;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Notifies when the scheduled processor has sent queued notifications. Subscribe to refresh UI (e.g. Queue table).
    /// </summary>
    public interface IQueueProcessedNotifier
    {
        event EventHandler<int>? QueueProcessed;
        void NotifyProcessed(int count);
    }

    public sealed class QueueProcessedNotifier : IQueueProcessedNotifier
    {
        public event EventHandler<int>? QueueProcessed;

        public void NotifyProcessed(int count) => QueueProcessed?.Invoke(this, count);
    }
}
