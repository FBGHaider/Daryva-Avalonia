using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Builds the header notification feed from payment, document, and tenant data.
    /// Mark-as-read state is in-memory for MVP; can be replaced with API persistence later.
    /// </summary>
    public class NotificationFeedService : INotificationFeedService
    {
        private readonly IPaymentService _paymentService;
        private readonly IDocumentService _documentService;
        private readonly HashSet<Guid> _readIds = new();
        private readonly object _readLock = new();

        public NotificationFeedService(IPaymentService paymentService, IDocumentService documentService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        }

        public async Task<IReadOnlyList<NotificationItem>> GetNotificationsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<NotificationItem>();
            var now = DateTimeOffset.UtcNow;
            var today = now.Date;

            // 1) Overdue rent
            try
            {
                var overdue = await _paymentService.GetOverdueRentAsync().ConfigureAwait(false);
                foreach (var item in overdue)
                {
                    var id = StableId("overdue", item.TenancyId);
                    list.Add(new NotificationItem
                    {
                        Id = id,
                        Type = NotificationFeedType.OverdueRent,
                        Title = "Overdue rent",
                        Message = $"{item.TenantName} — {item.HouseAddress}: £{item.Amount:N2} ({item.DaysLate} days late)",
                        CreatedAt = now,
                        IsRead = IsRead(id),
                        Severity = item.DaysLate > 14 ? NotificationSeverity.Critical : NotificationSeverity.Warning,
                        NavigationTarget = new NotificationNavigationTarget(NotificationTargetType.Tenancy, null)
                    });
                }
            }
            catch
            {
                // Skip source on error
            }

            // 2) Rent due soon (next 3 days) — from ledger current + next month
            try
            {
                var endSoon = today.AddDays(3);
                var addedDueSoon = new HashSet<Guid>();
                for (var i = 0; i <= 1; i++)
                {
                    var d = today.AddMonths(i);
                    var rows = await _paymentService.GetRentLedgerForMonthAsync(d.Year, d.Month, null, null, null).ConfigureAwait(false);
                    foreach (var row in rows)
                    {
                        if (row.DueDate < today || row.DueDate > endSoon || row.Balance <= 0) continue;
                        var id = StableId("duesoon", row.TenancyId, row.DueDate.Year, row.DueDate.Month);
                        if (!addedDueSoon.Add(id)) continue;
                        list.Add(new NotificationItem
                        {
                            Id = id,
                            Type = NotificationFeedType.RentDueSoon,
                            Title = "Rent due soon",
                            Message = $"{row.TenantName} — £{row.Balance:N2} due {row.DueDate:dd MMM}",
                            CreatedAt = now,
                            IsRead = IsRead(id),
                            Severity = NotificationSeverity.Info,
                            NavigationTarget = new NotificationNavigationTarget(NotificationTargetType.Payment, null)
                        });
                    }
                }
            }
            catch
            {
                // Skip
            }

            // 3) Documents expiring (next 30 days)
            try
            {
                var expiring = await _documentService.GetExpiringDocumentsAsync(30).ConfigureAwait(false);
                foreach (var doc in expiring)
                {
                    var id = StableId("doc", doc.DocumentId);
                    list.Add(new NotificationItem
                    {
                        Id = id,
                        Type = NotificationFeedType.DocsExpiring,
                        Title = "Document expiring",
                        Message = $"{doc.DisplayName} — {doc.Type} expires {doc.ValidTo:dd MMM yyyy}",
                        CreatedAt = doc.ValidTo.HasValue ? new DateTimeOffset(doc.ValidTo.Value) : now,
                        IsRead = IsRead(id),
                        Severity = doc.ValidTo.HasValue && doc.ValidTo.Value < today.AddDays(7) ? NotificationSeverity.Warning : NotificationSeverity.Info,
                        NavigationTarget = new NotificationNavigationTarget(NotificationTargetType.Document, doc.ApiId)
                    });
                }
            }
            catch
            {
                // Skip
            }

            // Sort by severity (Critical first) then created
            return list
                .OrderByDescending(n => n.Severity == NotificationSeverity.Critical ? 2 : n.Severity == NotificationSeverity.Warning ? 1 : 0)
                .ThenByDescending(n => n.CreatedAt)
                .ToList();
        }

        public Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_readLock)
                _readIds.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MarkAllAsReadAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            if (ids == null) return Task.CompletedTask;
            lock (_readLock)
            {
                foreach (var id in ids)
                    _readIds.Add(id);
            }
            return Task.CompletedTask;
        }

        public void ClearForSignOut()
        {
            lock (_readLock)
                _readIds.Clear();
        }

        private bool IsRead(Guid id)
        {
            lock (_readLock)
                return _readIds.Contains(id);
        }

        private static Guid StableId(string prefix, int id1, int id2 = 0, int id3 = 0)
        {
            var s = $"{prefix}_{id1}_{id2}_{id3}";
            var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(s));
            return new Guid(bytes.AsSpan(0, 16).ToArray());
        }

        private static Guid StableId(string prefix, int id)
        {
            return StableId(prefix, id, 0, 0);
        }
    }
}
