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
}
