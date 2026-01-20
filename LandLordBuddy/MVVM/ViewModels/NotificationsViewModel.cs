using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LandLordBuddy.MVVM.Commands;
using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Business;
using LandLordBuddy.Services.Dialog;

namespace LandLordBuddy.MVVM.ViewModels
{
    public class NotificationsViewModel : BaseViewModel
    {
        private readonly INotificationService _notificationService;
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IDialogService _dialogService;
        private readonly IEmailSender _emailSender;

        private string _selectedTab = "Compose";
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
        private DateTime _scheduledFor = DateTime.Now;
        private RecipientViewModel? _selectedRecipient;
        private NotificationRowViewModel? _selectedNotification;

        public NotificationsViewModel(
            INotificationService notificationService,
            IHouseService houseService,
            ITenantService tenantService,
            IDialogService dialogService,
            IEmailSender emailSender)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));

            Recipients = new ObservableCollection<RecipientViewModel>();
            Templates = new ObservableCollection<NotificationTemplate>();
            Tenants = new ObservableCollection<Tenant>();
            Houses = new ObservableCollection<House>();
            QueueNotifications = new ObservableCollection<NotificationRowViewModel>();
            HistoryNotifications = new ObservableCollection<NotificationRowViewModel>();

            LoadRecipientsCommand = new RelayCommand(async _ => await LoadRecipientsAsync());
            LoadTemplatesCommand = new RelayCommand(async _ => await LoadTemplatesAsync());
            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            LoadQueueCommand = new RelayCommand(async _ => await LoadQueueAsync());
            LoadHistoryCommand = new RelayCommand(async _ => await LoadHistoryAsync());
            PreviewMessageCommand = new RelayCommand(_ => PreviewMessage());
            SendNowCommand = new RelayCommand(async _ => await SendNowAsync(), _ => CanSend());
            QueueCommand = new RelayCommand(async _ => await QueueAsync(), _ => CanSend());
            SendTestCommand = new RelayCommand(async _ => await SendTestAsync());
            SendQueueItemCommand = new RelayCommand(async _ => await SendQueueItemAsync(), _ => SelectedNotification != null);
            CancelQueueItemCommand = new RelayCommand(async _ => await CancelQueueItemAsync(), _ => SelectedNotification != null);
            ViewDetailsCommand = new RelayCommand(_ => ViewDetails(), _ => SelectedNotification != null);

            // Don't load here - let the view's Loaded event handle it
            // This prevents duplicate loading
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

        public ObservableCollection<RecipientViewModel> Recipients { get; }
        public ObservableCollection<NotificationTemplate> Templates { get; }
        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<NotificationRowViewModel> QueueNotifications { get; }
        public ObservableCollection<NotificationRowViewModel> HistoryNotifications { get; }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    if (value == "Queue")
                        LoadQueueCommand.Execute(null);
                    else if (value == "History")
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
                    LoadRecipientsCommand.Execute(null);
                }
            }
        }

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
                if (SetProperty(ref _statusFilter, value))
                {
                    LoadRecipientsCommand.Execute(null);
                }
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
            set => SetProperty(ref _subject, value);
        }

        public string Body
        {
            get => _body;
            set => SetProperty(ref _body, value);
        }

        public DateTime ScheduledFor
        {
            get => _scheduledFor;
            set => SetProperty(ref _scheduledFor, value);
        }

        public string PreviewText { get; set; } = string.Empty;

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
            try
            {
                var filter = new RecipientFilter
                {
                    TargetType = TargetType,
                    TenantId = SelectedTenantId,
                    HouseId = SelectedHouseId == 0 ? null : SelectedHouseId,
                    StatusFilter = StatusFilter,
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
                        TenantName = recipient.TenantName,
                        Email = recipient.Email,
                        PhoneNumber = recipient.PhoneNumber,
                        TenancyId = recipient.TenancyId,
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
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading recipients: {ex.Message}", "Error");
            }
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
            }
            catch (Exception ex)
            {
                // If table doesn't exist, show a helpful message instead of error
                if (ex.Message.Contains("Invalid object name") && ex.Message.Contains("NotificationTemplate"))
                {
                    _dialogService.ShowMessage(
                        "The NotificationTemplate table doesn't exist yet. Please run the migration script:\n\n" +
                        "Database/Migrations/009_CreateNotificationTables.sql\n\n" +
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
                var filter = new NotificationFilter
                {
                    Status = "Pending"
                };
                var notifications = await _notificationService.GetNotificationsAsync(filter);
                
                QueueNotifications.Clear();
                foreach (var notification in notifications)
                {
                    QueueNotifications.Add(new NotificationRowViewModel
                    {
                        NotificationId = notification.NotificationId,
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
                    });
                }
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
                var filter = new NotificationFilter
                {
                    Status = "Sent"
                };
                var notifications = await _notificationService.GetNotificationsAsync(filter);
                
                HistoryNotifications.Clear();
                foreach (var notification in notifications.OrderByDescending(n => n.SentAt ?? n.ScheduledFor).Take(100))
                {
                    HistoryNotifications.Add(new NotificationRowViewModel
                    {
                        NotificationId = notification.NotificationId,
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
                    });
                }
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
                    Body = template.BodyTemplate;
                    PreviewMessageCommand.Execute(null);
                }
            }
        }

        private void PreviewMessage()
        {
            if (SelectedRecipient == null) return;

            var context = new NotificationContext
            {
                TenantName = SelectedRecipient.TenantName,
                HouseAddress = SelectedRecipient.HouseAddress,
                AmountDue = SelectedRecipient.AmountDue,
                DueDate = SelectedRecipient.DueDate,
                Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                PayInstructions = "Please contact your landlord for payment instructions."
            };

            var renderedSubject = _notificationService.RenderTemplateAsync(Subject, null, context).Result;
            var renderedBody = _notificationService.RenderTemplateAsync(Body, null, context).Result;

            PreviewText = $"Subject: {renderedSubject}\n\n{renderedBody}";
            OnPropertyChanged(nameof(PreviewText));
        }

        private bool CanSend()
        {
            return Recipients.Any(r => r.IsSelected) && !string.IsNullOrWhiteSpace(Body);
        }

        private async Task SendNowAsync()
        {
            if (!CanSend())
            {
                _dialogService.ShowMessage("Please select recipients and enter a message.", "Validation Error");
                return;
            }

            var selectedRecipients = Recipients.Where(r => r.IsSelected).ToList();
            var count = selectedRecipients.Count;

            var confirmed = _dialogService.ShowConfirmation(
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
                        TenancyId = recipient.TenancyId,
                        Channel = SelectedChannel,
                        Type = GetNotificationType(),
                        Subject = renderedSubject,
                        Body = renderedBody,
                        ScheduledFor = DateTime.Now,
                        TemplateId = SelectedTemplateId
                    };

                    var notification = await _notificationService.QueueNotificationAsync(dto);
                    if (await _notificationService.SendNotificationAsync(notification.NotificationId))
                    {
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
                _dialogService.ShowMessage("Please select recipients and enter a message.", "Validation Error");
                return;
            }

            var selectedRecipients = Recipients.Where(r => r.IsSelected).ToList();
            var count = selectedRecipients.Count;

            try
            {
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
                        TenancyId = recipient.TenancyId,
                        Channel = SelectedChannel,
                        Type = GetNotificationType(),
                        Subject = renderedSubject,
                        Body = renderedBody,
                        ScheduledFor = ScheduledFor,
                        TemplateId = SelectedTemplateId
                    };

                    await _notificationService.QueueNotificationAsync(dto);
                }

                _dialogService.ShowMessage($"Queued {count} notification(s) for {ScheduledFor:g}.", "Queued");
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
            var emailAddress = _dialogService.ShowInputDialog(
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
                // Render the template with sample data for preview
                var context = new NotificationContext
                {
                    TenantName = "Test Tenant",
                    HouseAddress = "123 Test Street, Test City",
                    AmountDue = 500.00m,
                    DueDate = DateTime.Now.AddDays(7),
                    Month = new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy"),
                    PayInstructions = "Please contact your landlord for payment instructions."
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

        private async Task SendQueueItemAsync()
        {
            if (SelectedNotification == null) return;

            try
            {
                if (await _notificationService.SendNotificationAsync(SelectedNotification.NotificationId))
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

        private async Task CancelQueueItemAsync()
        {
            if (SelectedNotification == null) return;

            var confirmed = _dialogService.ShowConfirmation(
                "Cancel this queued notification?",
                "Confirm Cancel");

            if (!confirmed) return;

            try
            {
                await _notificationService.CancelNotificationAsync(SelectedNotification.NotificationId);
                _dialogService.ShowMessage("Notification cancelled.", "Success");
                LoadQueueCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error cancelling notification: {ex.Message}", "Error");
            }
        }

        private void ViewDetails()
        {
            if (SelectedNotification == null) return;

            var notification = _notificationService.GetNotificationByIdAsync(SelectedNotification.NotificationId).Result;
            if (notification != null)
            {
                var details = $"Subject: {notification.Subject}\n\nBody:\n{notification.Body}";
                if (!string.IsNullOrWhiteSpace(notification.Error))
                {
                    details += $"\n\nError: {notification.Error}";
                }
                _dialogService.ShowMessage(details, "Notification Details");
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
