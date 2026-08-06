using System;

namespace Daryva.Services.Business
{
    public sealed class QueueProcessedNotifier : IQueueProcessedNotifier
    {
        public event EventHandler<int>? QueueProcessed;

        public void NotifyProcessed(int count) => QueueProcessed?.Invoke(this, count);
    }
}
