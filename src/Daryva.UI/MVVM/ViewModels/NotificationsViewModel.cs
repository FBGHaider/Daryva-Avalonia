using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class NotificationsViewModel : BaseViewModel
    {
        private readonly INotificationService _notificationService;
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IDialogService _dialogService;
        private readonly IEmailSender _emailSender;
        private readonly IQueueProcessedNotifier _queueProcessedNotifier;
        private readonly ISettingsService _settingsService;

        private int _selectedTabIndex;
        private string _targetType = "Single";
        private int? _selectedTenantId;
        private int? _selectedHouseId;
        private string _statusFilter = "Due";
        private int _selectedMonth = DateTime.Now.Month;
        private int _selectedYear = DateTime.Now.Year;
        private string _selectedChannel = "Email";
        private int? _selectedTemplateId;
        private string _subject = string.Empty;
        private string _body = string.Empty;
        private DateTimeOffset? _scheduledFor = DateTimeOffset.Now;
        private TimeSpan? _scheduledTime = new TimeSpan(15, 0, 0);
        private RecipientViewModel? _selectedRecipient;
        private NotificationRowViewModel? _selectedNotification;
        private string _queueStatusFilter = "Pending";
        private string _historyStatusFilter = "Sent";
        private bool _isLoadingRecipients;

        public NotificationsViewModel(
            INotificationService notificationService,
            IHouseService houseService,
            ITenantService tenantService,
            IDialogService dialogService,
            IEmailSender emailSender,
            IQueueProcessedNotifier queueProcessedNotifier,
            ISettingsService settingsService)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _queueProcessedNotifier = queueProcessedNotifier ?? throw new ArgumentNullException(nameof(queueProcessedNotifier));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            Recipients = new ObservableCollection<RecipientViewModel>();
            Templates = new ObservableCollection<NotificationTemplate>();
            Tenants = new ObservableCollection<Tenant>();
            Houses = new ObservableCollection<House>();
            QueueNotifications = new ObservableCollection<NotificationRowViewModel>();
            HistoryNotifications = new ObservableCollection<NotificationRowViewModel>();

            TargetTypeOptions = new ObservableCollection<string> { "Single", "All", "House" };
            StatusFilterOptions = new ObservableCollection<string> { "Due", "Overdue", "All" };
            ChannelOptions = new ObservableCollection<string> { "Email", "SMS", "WhatsApp" };
            QueueStatusOptions = new ObservableCollection<string> { "Pending", "Failed", "Cancelled", "All" };
            HistoryStatusOptions = new ObservableCollection<string> { "Sent", "Failed", "All" };
            VariableTokens = new ObservableCollection<string> { "{TenantName}", "{HouseAddress}", "{AmountDue}", "{DueDate}", "{Month}", "{PayInstructions}", "{Currency}", "{Message}" };

            LoadRecipientsCommand = new RelayCommand(async _ => await LoadRecipientsAsync());
            LoadTemplatesCommand = new RelayCommand(async _ => await LoadTemplatesAsync());
            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            LoadQueueCommand = new RelayCommand(async _ => await LoadQueueAsync());
            LoadHistoryCommand = new RelayCommand(async _ => await LoadHistoryAsync());
            PreviewMessageCommand = new RelayCommand(async _ => await PreviewMessageAsync());
            SendNowCommand = new RelayCommand(async _ => await SendNowAsync(), _ => CanSend());
            QueueCommand = new RelayCommand(async _ => await QueueAsync(), _ => CanSend());
            SendTestCommand = new RelayCommand(async _ => await SendTestAsync());
            SendQueueItemCommand = new RelayCommand(async p => await SendQueueItemAsync(p), p => (p as NotificationRowViewModel) != null || SelectedNotification != null);
            CancelQueueItemCommand = new RelayCommand(async p => await CancelQueueItemAsync(p), p => (p as NotificationRowViewModel) != null || SelectedNotification != null);
            ViewDetailsCommand = new RelayCommand(async p => await ViewDetailsAsync(p), p => (p as NotificationRowViewModel) != null || SelectedNotification != null);
            InsertVariableCommand = new RelayCommand(InsertVariable);

            _queueProcessedNotifier.QueueProcessed += OnQueueProcessed;

            LoadTemplatesCommand.Execute(null);
            LoadHousesCommand.Execute(null);
            LoadTenantsCommand.Execute(null);
            LoadRecipientsCommand.Execute(null);
            _ = LoadDefaultNotificationChannelAsync();
        }

        private async Task LoadDefaultNotificationChannelAsync()
        {
            try
            {
                var ch = await _settingsService.GetSettingAsync("DefaultNotificationChannel", "Email") ?? "Email";
                if (string.IsNullOrWhiteSpace(ch)) return;
                var v = ch.Trim();
                if (!ChannelOptions.Contains(v))
                    return;
                Dispatcher.UIThread.Post(() =>
                {
                    _selectedChannel = v;
                    OnPropertyChanged(nameof(SelectedChannel));
                    ((RelayCommand)SendNowCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)QueueCommand).RaiseCanExecuteChanged();
                });
            }
            catch { /* ignore */ }
        }

        private void OnQueueProcessed(object? sender, int count)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadQueueCommand.Execute(null);
                LoadHistoryCommand.Execute(null);
            });
        }

        public ICommand LoadRecipientsCommand { get; }
        public ICommand LoadTemplatesCommand { get; }
        public ICommand LoadTenantsCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand LoadQueueCommand { get; }
        public ICommand LoadHistoryCommand { get; }
        public ICommand PreviewMessageCommand { get; }
        public ICommand SendNowCommand { get; }
        public ICommand QueueCommand { get; }
        public ICommand SendTestCommand { get; }
        public ICommand SendQueueItemCommand { get; }
        public ICommand CancelQueueItemCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand InsertVariableCommand { get; }

        public ObservableCollection<RecipientViewModel> Recipients { get; }
        public ObservableCollection<NotificationTemplate> Templates { get; }
        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<NotificationRowViewModel> QueueNotifications { get; }
        public ObservableCollection<NotificationRowViewModel> HistoryNotifications { get; }

        public ObservableCollection<string> TargetTypeOptions { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }
        public ObservableCollection<string> ChannelOptions { get; }
        public ObservableCollection<string> QueueStatusOptions { get; }
        public ObservableCollection<string> HistoryStatusOptions { get; }
        public ObservableCollection<string> VariableTokens { get; }

        public string QueueStatusFilter
        {
            get => _queueStatusFilter;
            set { var v = value ?? "Pending"; if (SetProperty(ref _queueStatusFilter, v)) LoadQueueCommand.Execute(null); }
        }

        public string HistoryStatusFilter
        {
            get => _historyStatusFilter;
            set { var v = value ?? "Sent"; if (SetProperty(ref _historyStatusFilter, v)) LoadHistoryCommand.Execute(null); }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    if (value == 1)
                        LoadQueueCommand.Execute(null);
                    else if (value == 2)
                        LoadHistoryCommand.Execute(null);
                }
            }
        }

        public string TargetType
        {
            get => _targetType;
            set
            {
                if (SetProperty(ref _targetType, value))
                {
                    OnPropertyChanged(nameof(IsTenantAndStatusVisible));
                    OnPropertyChanged(nameof(IsHouseFilterVisible));
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

        /// <summary>True when TargetType is Single only. Use to show/hide Tenant and Status.</summary>
        public bool IsTenantAndStatusVisible => string.Equals(TargetType, "Single", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when TargetType is House. Use to show House filter.</summary>
        public bool IsHouseFilterVisible => string.Equals(TargetType, "House", StringComparison.OrdinalIgnoreCase);

        public int RecipientCount => Recipients.Count;
        public string RecipientsHeaderText => $"Recipients ({RecipientCount})";
        public bool HasQueueNotifications => QueueNotifications.Count > 0;
        public bool HasHistoryNotifications => HistoryNotifications.Count > 0;

        public int? SelectedTenantId
        {
            get => _selectedTenantId;
            set
            {
                if (SetProperty(ref _selectedTenantId, value))
                {
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

        public int? SelectedHouseId
        {
            get => _selectedHouseId;
            set
            {
                if (SetProperty(ref _selectedHouseId, value))
                {
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                var v = value ?? "Due";
                if (SetProperty(ref _statusFilter, v))
                    LoadRecipientsCommand.Execute(null);
            }
        }

        public int SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                {
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

        public string SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (SetProperty(ref _selectedChannel, value))
                {
                    LoadTemplatesCommand.Execute(null);
                }
            }
        }

        public int? SelectedTemplateId
        {
            get => _selectedTemplateId;
            set
            {
                if (SetProperty(ref _selectedTemplateId, value))
                {
                    LoadTemplateContent();
                }
            }
        }

        public string Subject
        {
            get => _subject;
            set
            {
                if (SetProperty(ref _subject, value))
                    RaiseSendCommandsCanExecuteChanged();
            }
        }

        public string Body
        {
            get => _body;
            set
            {
                if (SetProperty(ref _body, value))
                    RaiseSendCommandsCanExecuteChanged();
            }
        }

        public DateTimeOffset? ScheduledFor
        {
            get => _scheduledFor;
            set => SetProperty(ref _scheduledFor, value ?? DateTimeOffset.Now);
        }

        public TimeSpan? ScheduledTime
        {
            get => _scheduledTime;
            set => SetProperty(ref _scheduledTime, value ?? new TimeSpan(15, 0, 0));
        }

        public string PreviewText { get; set; } = string.Empty;

        /// <summary>Custom message text for General template; replaces {Message} in the body.</summary>
        public string CustomMessage
        {
            get => _customMessage;
            set
            {
                if (SetProperty(ref _customMessage, value ?? string.Empty))
                    PreviewMessageCommand.Execute(null);
            }
        }

        public RecipientViewModel? SelectedRecipient
        {
            get => _selectedRecipient;
            set
            {
                if (SetProperty(ref _selectedRecipient, value))
                {
                    PreviewMessageCommand.Execute(null);
                }
            }
        }

        public NotificationRowViewModel? SelectedNotification
        {
            get => _selectedNotification;
            set
            {
                if (SetProperty(ref _selectedNotification, value))
                {
                    ((RelayCommand)SendQueueItemCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CancelQueueItemCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ViewDetailsCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadRecipientsAsync()
        {
            if (_isLoadingRecipients) return;
            _isLoadingRecipients = true;
            try
            {
                var isHouse = string.Equals(TargetType, "House", StringComparison.OrdinalIgnoreCase);
                var isSingle = string.Equals(TargetType, "Single", StringComparison.OrdinalIgnoreCase);
                var filter = new RecipientFilter
                {
                    TargetType = TargetType,
                    TenantId = isSingle ? SelectedTenantId : null,
                    HouseId = isHouse && SelectedHouseId.HasValue && SelectedHouseId.Value > 0 ? SelectedHouseId : null,
                    StatusFilter = isSingle ? StatusFilter : null,
                    Month = SelectedMonth,
                    Year = SelectedYear
                };

                var recipients = await _notificationService.BuildRecipientsAsync(filter);
                
                Recipients.Clear();
                foreach (var recipient in recipients)
                {
                    Recipients.Add(new RecipientViewModel
                    {
                        TenantId = recipient.TenantId,
                        ApiTenantId = recipient.ApiTenantId,
                        TenantName = recipient.TenantName,
                        Email = recipient.Email,
                        PhoneNumber = recipient.PhoneNumber,
                        TenancyId = recipient.TenancyId,
                        ApiTenancyId = recipient.ApiTenancyId,
                        HouseAddress = recipient.HouseAddress,
                        HasEmail = recipient.HasEmail,
                        HasWhatsApp = recipient.HasWhatsApp,
                        AmountDue = recipient.AmountDue,
                        DueDate = recipient.DueDate,
                        IsSelected = true
                    });
                }

                if (Recipients.Any())
                {
                    SelectedRecipient = Recipients.First();
                }

                RaiseSendCommandsCanExecuteChanged();
                OnPropertyChanged(nameof(RecipientCount));
                OnPropertyChanged(nameof(RecipientsHeaderText));
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync($"Error loading recipients: {ex.Message}", "Error");
            }
            finally
            {
                _isLoadingRecipients = false;
                OnPropertyChanged(nameof(RecipientCount));
                OnPropertyChanged(nameof(RecipientsHeaderText));
            }
        }

        private void InsertVariable(object? parameter)
        {
            var token = parameter as string;
            if (string.IsNullOrEmpty(token)) return;
            Body = Body + token;
        }

        private async Task LoadTemplatesAsync()
        {
            try
            {
                var templates = await _notificationService.GetTemplatesAsync(SelectedChannel);
                Templates.Clear();
                foreach (var template in templates)
                {
                    Templates.Add(template);
                }

                if (Templates.Any())
                {
                    SelectedTemplateId = Templates.First().TemplateId;
                }
                OnPropertyChanged(nameof(IsGeneralTemplate));
            }
            catch (Exception ex)
            {
                // If table doesn't exist, show a helpful message instead of error
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("NotificationTemplate"))
                {
                    _dialogService.ShowMessage(
                        "The NotificationTemplate table doesn't exist yet. Please run the migration script:\n\n" +
                        "src/Daryva.Data/Migrations/009_CreateNotificationTables.sql\n\n" +
                        "This will create the required tables and default templates.",
                        "Migration Required");
                }
                else
                {
                    _dialogService.ShowMessage($"Error loading templates: {ex.Message}", "Error");
                }
            }
        }

        private async Task LoadTenantsAsync()
        {
            try
            {
                var tenants = await _tenantService.GetAllTenantsAsync();
                Tenants.Clear();
                foreach (var tenant in tenants.Where(t => !t.IsArchived))
                {
                    Tenants.Add(tenant);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading tenants: {ex.Message}", "Error");
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                Houses.Clear();
                Houses.Add(new House { HouseId = 0, AddressLine1 = "All Houses" });
                foreach (var house in houses)
                {
                    Houses.Add(house);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private async Task LoadQueueAsync()
        {
            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                var filter = new NotificationFilter
                {
                    Status = string.Equals(QueueStatusFilter, "All", StringComparison.OrdinalIgnoreCase) ? null : QueueStatusFilter
                };
                var notifications = await _notificationService.GetNotificationsAsync(filter);
                QueueNotifications.Clear();
                OnPropertyChanged(nameof(HasQueueNotifications));
                foreach (var notification in notifications)
                {
                    var row = new NotificationRowViewModel
                    {
                        NotificationId = notification.NotificationId,
                        ApiId = notification.ApiId,
                        ScheduledFor = notification.ScheduledFor,
                        TenantName = notification.Tenant?.FullName ?? "Unknown",
                        HouseAddress = notification.Tenancy?.House != null 
                            ? $"{notification.Tenancy.House.AddressLine1}, {notification.Tenancy.House.City}" 
                            : "Unknown",
                        Channel = notification.Channel,
                        Type = notification.Type,
                        Subject = notification.Subject ?? "",
                        BodyPreview = notification.Body.Length > 60 ? notification.Body.Substring(0, 60) + "..." : notification.Body,
                        Status = notification.Status
                    };
                    row.ScheduledDisplay = Daryva.Services.DateTimeFormatProvider.FormatDateTime(notification.ScheduledFor);
                    QueueNotifications.Add(row);
                }
                OnPropertyChanged(nameof(HasQueueNotifications));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading queue: {ex.Message}", "Error");
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                var filter = new NotificationFilter
                {
                    Status = string.Equals(HistoryStatusFilter, "All", StringComparison.OrdinalIgnoreCase) ? null : HistoryStatusFilter
                };
                var notifications = await _notificationService.GetNotificationsAsync(filter);
                HistoryNotifications.Clear();
                OnPropertyChanged(nameof(HasHistoryNotifications));
                foreach (var notification in notifications.OrderByDescending(n => n.SentAt ?? n.ScheduledFor).Take(100))
                {
                    var row = new NotificationRowViewModel
                    {
                        NotificationId = notification.NotificationId,
                        ApiId = notification.ApiId,
                        ScheduledFor = notification.ScheduledFor,
                        SentAt = notification.SentAt,
                        TenantName = notification.Tenant?.FullName ?? "Unknown",
                        HouseAddress = notification.Tenancy?.House != null 
                            ? $"{notification.Tenancy.House.AddressLine1}, {notification.Tenancy.House.City}" 
                            : "Unknown",
                        Channel = notification.Channel,
                        Type = notification.Type,
                        Subject = notification.Subject ?? "",
                        BodyPreview = notification.Body.Length > 60 ? notification.Body.Substring(0, 60) + "..." : notification.Body,
                        Status = notification.Status,
                        Error = notification.Error
                    };
                    row.ScheduledDisplay = Daryva.Services.DateTimeFormatProvider.FormatDateTime(notification.ScheduledFor);
                    row.SentDisplay = notification.SentAt.HasValue 
                        ? Daryva.Services.DateTimeFormatProvider.FormatDateTime(notification.SentAt.Value) 
                        : "-";
                    HistoryNotifications.Add(row);
                }
                OnPropertyChanged(nameof(HasHistoryNotifications));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading history: {ex.Message}", "Error");
            }
        }

        private void LoadTemplateContent()
        {
            if (SelectedTemplateId.HasValue)
            {
                var template = Templates.FirstOrDefault(t => t.TemplateId == SelectedTemplateId.Value);
                if (template != null)
                {
                    Subject = template.SubjectTemplate ?? "";
                    Body = template.BodyTemplate ?? "";
                    PreviewMessageCommand.Execute(null);
                }
            }
        }

        private async Task PreviewMessageAsync()
        {
            if (SelectedRecipient == null) return;

            var payInstructions = await _settingsService.GetSettingAsync("PaymentInstructions", "Please contact your landlord for payment instructions.") ?? "Please contact your landlord for payment instructions.";
            var context = new NotificationContext
            {
                TenantName = SelectedRecipient.TenantName,
                HouseAddress = SelectedRecipient.HouseAddress,
                AmountDue = SelectedRecipient.AmountDue,
                DueDate = SelectedRecipient.DueDate,
                Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                PayInstructions = payInstructions,
                Message = CustomMessage ?? ""
            };

            var renderedSubject = await _notificationService.RenderTemplateAsync(Subject, null, context);
            var renderedBody = await _notificationService.RenderTemplateAsync(Body, null, context);

            PreviewText = $"Subject: {renderedSubject}\n\n{renderedBody}";
            OnPropertyChanged(nameof(PreviewText));
        }

        private void RaiseSendCommandsCanExecuteChanged()
        {
            ((RelayCommand)SendNowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)QueueCommand).RaiseCanExecuteChanged();
        }

        private bool CanSend()
        {
            return Recipients.Any(r => r.IsSelected)
                && !string.IsNullOrWhiteSpace(Subject)
                && !string.IsNullOrWhiteSpace(Body);
        }

        private async Task SendNowAsync()
        {
            if (!CanSend())
            {
                _dialogService.ShowMessage("Please select a target (e.g. Single + Tenant, or All), ensure recipients are loaded, enter Subject and Body, then try again.", "Validation Error");
                return;
            }

            var selectedRecipients = Recipients.Where(r => r.IsSelected).ToList();
            var count = selectedRecipients.Count;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Send {count} notification(s) now?",
                "Confirm Send");

            if (!confirmed) return;

            try
            {
                int successCount = 0;
                foreach (var recipient in selectedRecipients)
                {
                    var context = new NotificationContext
                    {
                        TenantName = recipient.TenantName,
                        HouseAddress = recipient.HouseAddress,
                        AmountDue = recipient.AmountDue,
                        DueDate = recipient.DueDate,
                        Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                        PayInstructions = "Please contact your landlord for payment instructions."
                    };

                    var renderedSubject = await _notificationService.RenderTemplateAsync(Subject, null, context);
                    var renderedBody = await _notificationService.RenderTemplateAsync(Body, null, context);

                    var dto = new NotificationDto
                    {
                        TenantId = recipient.TenantId,
                        TenantApiId = recipient.ApiTenantId,
                        TenancyId = recipient.TenancyId,
                        TenancyApiId = recipient.ApiTenancyId,
                        Channel = SelectedChannel,
                        Type = GetNotificationType(),
                        Subject = renderedSubject,
                        Body = renderedBody,
                        ScheduledFor = DateTime.Now,
                        TemplateId = SelectedTemplateId,
                        TemplateApiId = Templates.FirstOrDefault(t => t.TemplateId == SelectedTemplateId)?.ApiId
                    };

                    var notification = await _notificationService.QueueNotificationAsync(dto);

                    if (SelectedChannel == "Email")
                    {
                        var toAddress = (recipient.Email ?? "").Trim();
                        if (await _notificationService.SendNotificationWithContentAsync(notification.NotificationId, toAddress, renderedSubject, renderedBody))
                            successCount++;
                    }
                    else
                    {
                        if (await _notificationService.SendNotificationAsync(notification.NotificationId))
                            successCount++;
                    }
                }

                _dialogService.ShowMessage($"Sent {successCount} of {count} notification(s) successfully.", "Send Complete");
                LoadQueueCommand.Execute(null);
                LoadHistoryCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error sending notifications: {ex.Message}", "Error");
            }
        }

        private async Task QueueAsync()
        {
            if (!CanSend())
            {
                _dialogService.ShowMessage("Please select a target (e.g. Single + Tenant, or All), ensure recipients are loaded, enter Subject and Body, then try again.", "Validation Error");
                return;
            }

            var selectedRecipients = Recipients.Where(r => r.IsSelected).ToList();
            var count = selectedRecipients.Count;

            try
            {
                var payInstructions = await _settingsService.GetSettingAsync("PaymentInstructions", "Please contact your landlord for payment instructions.") ?? "Please contact your landlord for payment instructions.";
                var d = ScheduledFor?.DateTime.Date ?? DateTime.Today;
                var t = ScheduledTime ?? new TimeSpan(15, 0, 0);
                var dt = d.Add(t);

                foreach (var recipient in selectedRecipients)
                {
                    var context = new NotificationContext
                    {
                        TenantName = recipient.TenantName,
                        HouseAddress = recipient.HouseAddress,
                        AmountDue = recipient.AmountDue,
                        DueDate = recipient.DueDate,
                        Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                        PayInstructions = payInstructions,
                        Message = CustomMessage ?? ""
                    };

                    var renderedSubject = await _notificationService.RenderTemplateAsync(Subject, null, context);
                    var renderedBody = await _notificationService.RenderTemplateAsync(Body, null, context);

                    var dto = new NotificationDto
                    {
                        TenantId = recipient.TenantId,
                        TenantApiId = recipient.ApiTenantId,
                        TenancyId = recipient.TenancyId,
                        TenancyApiId = recipient.ApiTenancyId,
                        Channel = SelectedChannel,
                        Type = GetNotificationType(),
                        Subject = renderedSubject,
                        Body = renderedBody,
                        ScheduledFor = dt,
                        TemplateId = SelectedTemplateId,
                        TemplateApiId = Templates.FirstOrDefault(t => t.TemplateId == SelectedTemplateId)?.ApiId
                    };

                    await _notificationService.QueueNotificationAsync(dto);
                }

                _dialogService.ShowMessage($"Queued {count} notification(s) for {dt:g}.", "Queued");
                LoadQueueCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error queueing notifications: {ex.Message}", "Error");
            }
        }

        private async Task SendTestAsync()
        {
            if (string.IsNullOrWhiteSpace(Body))
            {
                _dialogService.ShowMessage("Please enter a message body first.", "Validation Error");
                return;
            }

            // Prompt for email address
            var emailAddress = await _dialogService.ShowInputDialogAsync(
                "Enter your email address to receive the test message:",
                "Send Test Email",
                "");

            if (string.IsNullOrWhiteSpace(emailAddress))
            {
                return; // User cancelled
            }

            // Validate email format
            if (!emailAddress.Contains("@") || !emailAddress.Contains("."))
            {
                _dialogService.ShowMessage("Please enter a valid email address.", "Invalid Email");
                return;
            }

            try
            {
                var payInstructions = await _settingsService.GetSettingAsync("PaymentInstructions", "Please contact your landlord for payment instructions.") ?? "Please contact your landlord for payment instructions.";
                var context = new NotificationContext
                {
                    TenantName = "Test Tenant",
                    HouseAddress = "123 Test Street, Test City",
                    AmountDue = 500.00m,
                    DueDate = DateTime.Now.AddDays(7),
                    Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                    PayInstructions = payInstructions,
                    Message = CustomMessage ?? ""
                };

                var renderedSubject = await _notificationService.RenderTemplateAsync(Subject, null, context);
                var renderedBody = await _notificationService.RenderTemplateAsync(Body, null, context);

                // Send the test email
                var success = await _emailSender.SendEmailAsync(emailAddress, renderedSubject, renderedBody);

                if (success)
                {
                    _dialogService.ShowMessage($"Test email sent successfully to:\n{emailAddress}\n\nPlease check your inbox (and spam folder).", "Test Email Sent");
                }
                else
                {
                    _dialogService.ShowMessage("Failed to send test email. Please check your email configuration.", "Error");
                }
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nDetails: {ex.InnerException.Message}";
                }
                _dialogService.ShowMessage($"Error sending test email:\n\n{errorMessage}", "Email Error");
            }
        }

        private async Task SendQueueItemAsync(object? parameter)
        {
            var row = (parameter as NotificationRowViewModel) ?? SelectedNotification;
            if (row == null) return;

            try
            {
                if (await _notificationService.SendNotificationAsync(row.NotificationId))
                {
                    _dialogService.ShowMessage("Notification sent successfully.", "Success");
                    LoadQueueCommand.Execute(null);
                    LoadHistoryCommand.Execute(null);
                }
                else
                {
                    _dialogService.ShowMessage("Failed to send notification. Check error details.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error sending notification: {ex.Message}", "Error");
            }
        }

        private async Task CancelQueueItemAsync(object? parameter)
        {
            var row = (parameter as NotificationRowViewModel) ?? SelectedNotification;
            if (row == null) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Cancel this queued notification?",
                "Confirm Cancel");

            if (!confirmed) return;

            try
            {
                await _notificationService.CancelNotificationAsync(row.NotificationId);
                _dialogService.ShowMessage("Notification cancelled.", "Success");
                LoadQueueCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error cancelling notification: {ex.Message}", "Error");
            }
        }

        private async Task ViewDetailsAsync(object? parameter)
        {
            var row = (parameter as NotificationRowViewModel) ?? SelectedNotification;
            if (row == null) return;

            var notification = await _notificationService.GetNotificationByIdAsync(row.NotificationId);
            if (notification != null)
            {
                var details = $"Subject: {notification.Subject}\n\nBody:\n{notification.Body}";
                if (!string.IsNullOrWhiteSpace(notification.Error))
                {
                    details += $"\n\nError: {notification.Error}";
                }
                await _dialogService.ShowMessageAsync(details, "Notification Details");
            }
        }

        private string GetNotificationType()
        {
            if (SelectedTemplateId.HasValue)
            {
                var template = Templates.FirstOrDefault(t => t.TemplateId == SelectedTemplateId.Value);
                if (template != null)
                {
                    return template.Type;
                }
            }
            return "General";
        }
    }
}
