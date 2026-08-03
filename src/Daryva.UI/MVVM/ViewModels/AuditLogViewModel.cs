using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Read-only audit trail viewer. Backed by GET /api/audit-logs, which enforces visibility
    /// itself (Landlords see their own org, platform admins see any org) -- a Tenant opening this
    /// page gets a 403 that AuditLogApiService turns into a friendly ErrorMessage, since Tenants
    /// don't hold the Audit.View permission at all.
    /// </summary>
    public class AuditLogViewModel : BaseViewModel
    {
        private const int PageSize = 50;

        private readonly IAuditLogApiService _auditLogApiService;
        private readonly IDialogService _dialogService;

        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private string _selectedEventType = AllEventTypesOption;
        private DateTimeOffset? _fromDate;
        private DateTimeOffset? _toDate;
        private int _page = 1;
        private int _totalCount;

        public const string AllEventTypesOption = "All events";

        public AuditLogViewModel(IAuditLogApiService auditLogApiService, IDialogService dialogService)
        {
            _auditLogApiService = auditLogApiService ?? throw new ArgumentNullException(nameof(auditLogApiService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            Entries = new ObservableCollection<AuditLogEntryVm>();
            EventTypeOptions = new ObservableCollection<string>(BuildEventTypeOptions());

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            ClearFiltersCommand = new RelayCommand(async _ => await ClearFiltersAsync());
            NextPageCommand = new RelayCommand(async _ => await GoToPageAsync(Page + 1), _ => Page < TotalPages);
            PreviousPageCommand = new RelayCommand(async _ => await GoToPageAsync(Page - 1), _ => Page > 1);
            ViewDetailsCommand = new RelayCommand(async p => await ViewDetailsAsync(p as AuditLogEntryVm));

            _ = LoadAsync();
        }

        public ObservableCollection<AuditLogEntryVm> Entries { get; }
        public ObservableCollection<string> EventTypeOptions { get; }

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasEntries => Entries.Count > 0;
        public bool IsEmpty => !IsLoading && !HasError && Entries.Count == 0;

        public string SelectedEventType
        {
            get => _selectedEventType;
            set
            {
                var v = string.IsNullOrWhiteSpace(value) ? AllEventTypesOption : value;
                if (SetProperty(ref _selectedEventType, v))
                    _ = GoToPageAsync(1);
            }
        }

        public DateTimeOffset? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                    _ = GoToPageAsync(1);
            }
        }

        public DateTimeOffset? ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value))
                    _ = GoToPageAsync(1);
            }
        }

        public int Page
        {
            get => _page;
            private set => SetProperty(ref _page, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (SetProperty(ref _totalCount, value))
                    OnPropertyChanged(nameof(TotalPages));
            }
        }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        public string PageSummary => TotalCount == 0
            ? "No entries"
            : $"Page {Page} of {TotalPages} ({TotalCount} total)";

        private async Task ClearFiltersAsync()
        {
            _selectedEventType = AllEventTypesOption;
            OnPropertyChanged(nameof(SelectedEventType));
            _fromDate = null;
            OnPropertyChanged(nameof(FromDate));
            _toDate = null;
            OnPropertyChanged(nameof(ToDate));
            await GoToPageAsync(1);
        }

        private async Task GoToPageAsync(int page)
        {
            Page = Math.Max(1, page);
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(IsEmpty));
            try
            {
                var query = new AuditLogQuery
                {
                    EventType = SelectedEventType == AllEventTypesOption ? null : SelectedEventType,
                    FromDate = FromDate?.UtcDateTime,
                    ToDate = ToDate?.UtcDateTime,
                    Page = Page,
                    PageSize = PageSize
                };

                var result = await _auditLogApiService.QueryAsync(query).ConfigureAwait(true);

                Entries.Clear();
                foreach (var item in result.Items)
                {
                    Entries.Add(new AuditLogEntryVm
                    {
                        Id = item.Id,
                        CreatedAtUtc = item.CreatedAt,
                        EventType = item.EventType,
                        ActorRole = item.ActorRole,
                        ActorUserId = item.ActorUserId,
                        TargetType = item.TargetType,
                        TargetId = item.TargetId,
                        IpAddress = item.IpAddress,
                        Metadata = item.Metadata
                    });
                }

                TotalCount = result.TotalCount;
                Page = result.Page;
            }
            catch (Exception ex)
            {
                Entries.Clear();
                TotalCount = 0;
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasEntries));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(PageSummary));
                NextPageCommand.RaiseCanExecuteChanged();
                PreviousPageCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task ViewDetailsAsync(AuditLogEntryVm? entry)
        {
            if (entry == null)
                return;

            var details =
                $"Event: {entry.EventTypeDisplay}\n" +
                $"When: {entry.CreatedAtDisplay}\n" +
                $"Actor: {entry.ActorDisplay}\n" +
                $"Target: {entry.TargetDisplay}\n" +
                $"IP address: {entry.IpAddressDisplay}\n\n" +
                $"Metadata:\n{FormatMetadata(entry.Metadata)}";

            await _dialogService.ShowMessageAsync(details, "Audit Log Entry");
        }

        private static string FormatMetadata(string? metadata)
        {
            if (string.IsNullOrWhiteSpace(metadata))
                return "(none)";

            try
            {
                using var document = JsonDocument.Parse(metadata);
                return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (JsonException)
            {
                return metadata;
            }
        }

        private static IEnumerable<string> BuildEventTypeOptions()
        {
            yield return AllEventTypesOption;
            yield return "AuthRegister";
            yield return "AuthLogin";
            yield return "AuthLoginFailed";
            yield return "AuthLoginBlocked";
            yield return "AuthPasswordResetRequested";
            yield return "AuthPasswordReset";
            yield return "PlatformAdminGranted";
            yield return "TwoFactorEnabled";
            yield return "TwoFactorChallengeIssued";
            yield return "TwoFactorVerified";
            yield return "TwoFactorFailed";
            yield return "TwoFactorRecoveryCodeUsed";
            yield return "TwoFactorDisabled";
            yield return "TwoFactorRecoveryCodesRegenerated";
            yield return "SupportSessionStarted";
            yield return "SupportSessionEnded";
            yield return "HouseArchived";
            yield return "HouseDeleted";
            yield return "TenantArchived";
            yield return "TenantUnarchived";
            yield return "TenantDeleted";
            yield return "TenancyEnded";
            yield return "TenancyReactivated";
            yield return "TenancyDeleted";
            yield return "TenanciesBulkDeleted";
            yield return "DocumentDeleted";
            yield return "ExpenseDeleted";
            yield return "PaymentRecorded";
            yield return "DepositReturnRecorded";
            yield return "PaymentDeleted";
            yield return "PaymentsBulkDeleted";
        }
    }
}
