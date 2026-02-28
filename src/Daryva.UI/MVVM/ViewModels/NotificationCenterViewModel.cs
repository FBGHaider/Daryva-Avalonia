using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel for the header notification bell and slide-over drawer.
    /// </summary>
    public class NotificationCenterViewModel : BaseViewModel
    {
        private readonly INotificationFeedService _notificationFeedService;
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;

        private bool _isOpen;
        private int _unreadCount;
        private bool _isLoading;

        public NotificationCenterViewModel(
            INotificationFeedService notificationFeedService,
            INavigationService navigationService,
            IDialogService dialogService)
        {
            _notificationFeedService = notificationFeedService ?? throw new ArgumentNullException(nameof(notificationFeedService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            Items = new ObservableCollection<NotificationItemVm>();

            ToggleOpenCommand = new RelayCommand(_ => ToggleOpen());
            CloseCommand = new RelayCommand(_ => Close());
            MarkAllReadCommand = new RelayCommand(async _ => await MarkAllReadAsync(), _ => UnreadCount > 0);
            MarkReadCommand = new RelayCommand<NotificationItemVm>(async item => await MarkReadAsync(item), item => item != null && !item.IsRead);
            OpenNotificationCommand = new RelayCommand<NotificationItemVm>(item => OpenNotification(item), item => item != null);
            RefreshNotificationsCommand = new RelayCommand(async _ => await RefreshAsync());
        }

        public ObservableCollection<NotificationItemVm> Items { get; }

        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        public int UnreadCount
        {
            get => _unreadCount;
            private set
            {
                if (SetProperty(ref _unreadCount, value))
                {
                    OnPropertyChanged(nameof(ShowBadge));
                    OnPropertyChanged(nameof(BadgeText));
                }
            }
        }

        /// <summary>True when the unread badge should be visible.</summary>
        public bool ShowBadge => UnreadCount > 0;

        /// <summary>Badge label (e.g. "3" or "99+").</summary>
        public string BadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>True when the list is empty and not loading (show empty state).</summary>
        public bool ShowEmptyState => !IsLoading && Items.Count == 0;

        /// <summary>True when there are items to show.</summary>
        public bool HasItems => Items.Count > 0;

        public ICommand ToggleOpenCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand MarkAllReadCommand { get; }
        public ICommand MarkReadCommand { get; }
        public ICommand OpenNotificationCommand { get; }
        public ICommand RefreshNotificationsCommand { get; }

        private void ToggleOpen()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
                _ = RefreshAsync();
        }

        public void Close()
        {
            IsOpen = false;
        }

        private async Task MarkAllReadAsync()
        {
            try
            {
                var ids = Items.Where(i => !i.IsRead).Select(i => i.Id).ToList();
                if (ids.Count > 0)
                    await _notificationFeedService.MarkAllAsReadAsync(ids);
                foreach (var item in Items)
                    item.IsRead = true;
                UpdateUnreadCount();
                (MarkAllReadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Failed to mark all as read: {ex.Message}", "Error");
            }
        }

        private async Task MarkReadAsync(NotificationItemVm? item)
        {
            if (item == null) return;
            try
            {
                await _notificationFeedService.MarkAsReadAsync(item.Id);
                item.IsRead = true;
                UpdateUnreadCount();
                (MarkAllReadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Failed to mark as read: {ex.Message}", "Error");
            }
        }

        private void OpenNotification(NotificationItemVm? item)
        {
            if (item == null) return;

            // Mark as read when opening
            if (!item.IsRead)
                _ = MarkReadAsync(item);

            var target = item.NavigationTarget;
            if (target == null)
            {
                Close();
                return;
            }

            switch (target.TargetType)
            {
                case NotificationTargetType.Tenant:
                    _navigationService.NavigateTo<TenantsViewModel>();
                    break;
                case NotificationTargetType.House:
                    _navigationService.NavigateTo<HousesViewModel>();
                    break;
                case NotificationTargetType.Document:
                    _navigationService.NavigateTo<DocumentsViewModel>();
                    break;
                case NotificationTargetType.Payment:
                case NotificationTargetType.Tenancy:
                    _navigationService.NavigateTo<RentPaymentsViewModel>();
                    break;
                default:
                    _navigationService.NavigateTo<DashboardViewModel>();
                    break;
            }

            Close();
        }

        private void UpdateUnreadCount()
        {
            UnreadCount = Items.Count(i => !i.IsRead);
        }

        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var list = await _notificationFeedService.GetNotificationsAsync();
                Items.Clear();
                foreach (var n in list)
                    Items.Add(new NotificationItemVm(n));
                UpdateUnreadCount();
                (MarkAllReadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Failed to load notifications: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(HasItems));
            }
        }
    }
}
