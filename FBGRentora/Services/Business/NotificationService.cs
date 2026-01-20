using FBGRentora.MVVM.Models;
using FBGRentora.Services.Data;

namespace FBGRentora.Services.Business
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IHouseRepository _houseRepository;
        private readonly IPaymentService _paymentService;
        private readonly IDocumentService _documentService;
        private readonly IEmailSender _emailSender;

        public NotificationService(
            INotificationRepository notificationRepository,
            ITenantRepository tenantRepository,
            ITenancyRepository tenancyRepository,
            IHouseRepository houseRepository,
            IPaymentService paymentService,
            IDocumentService documentService,
            IEmailSender emailSender)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
            _houseRepository = houseRepository ?? throw new ArgumentNullException(nameof(houseRepository));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        }

        public async Task<IEnumerable<NotificationRecipient>> BuildRecipientsAsync(RecipientFilter filter)
        {
            var recipients = new List<NotificationRecipient>();

            if (filter.TargetType == "Single" && filter.TenantId.HasValue)
            {
                var tenant = await _tenantRepository.GetTenantByIdAsync(filter.TenantId.Value);
                if (tenant != null)
                {
                    var tenancies = await _tenancyRepository.GetTenanciesByTenantIdAsync(filter.TenantId.Value);
                    var tenancy = tenancies.FirstOrDefault(t => t.Status == "Active");
                    var house = tenancy?.House;
                    
                    recipients.Add(new NotificationRecipient
                    {
                        TenantId = tenant.TenantId,
                        TenantName = tenant.FullName,
                        Email = tenant.Email,
                        PhoneNumber = tenant.PhoneNumber,
                        TenancyId = tenancy?.TenancyId,
                        HouseAddress = house != null ? $"{house.AddressLine1}, {house.City}" : "Unknown",
                        HasEmail = !string.IsNullOrWhiteSpace(tenant.Email),
                        HasWhatsApp = !string.IsNullOrWhiteSpace(tenant.PhoneNumber)
                    });
                }
            }
            else if (filter.TargetType == "House" && filter.HouseId.HasValue)
            {
                var tenancies = await _tenancyRepository.GetTenanciesByHouseIdAsync(filter.HouseId.Value);
                var activeTenancies = tenancies.Where(t => t.Status == "Active");
                foreach (var tenancy in activeTenancies)
                {
                    if (tenancy.Tenant != null && !tenancy.Tenant.IsArchived)
                    {
                        recipients.Add(new NotificationRecipient
                        {
                            TenantId = tenancy.Tenant.TenantId,
                            TenantName = tenancy.Tenant.FullName,
                            Email = tenancy.Tenant.Email,
                            PhoneNumber = tenancy.Tenant.PhoneNumber,
                            TenancyId = tenancy.TenancyId,
                            HouseAddress = tenancy.House != null ? $"{tenancy.House.AddressLine1}, {tenancy.House.City}" : "Unknown",
                            HasEmail = !string.IsNullOrWhiteSpace(tenancy.Tenant.Email),
                            HasWhatsApp = !string.IsNullOrWhiteSpace(tenancy.Tenant.PhoneNumber)
                        });
                    }
                }
            }
            else if (filter.TargetType == "AllWithStatus")
            {
                if (filter.StatusFilter == "Due" || filter.StatusFilter == "Overdue")
                {
                    // Get rent due/overdue tenants
                    if (filter.Month.HasValue && filter.Year.HasValue)
                    {
                        var ledgerRows = await _paymentService.GetRentLedgerForMonthAsync(filter.Year.Value, filter.Month.Value);
                        var relevantRows = filter.StatusFilter == "Due" 
                            ? ledgerRows.Where(r => r.Status == "Unpaid" || r.Status == "PartPaid")
                            : ledgerRows.Where(r => r.Status == "Overdue");

                        foreach (var row in relevantRows)
                        {
                            // Get tenant from tenancy
                            var tenancy = await _tenancyRepository.GetTenancyByIdAsync(row.TenancyId);
                            if (tenancy?.Tenant != null && !tenancy.Tenant.IsArchived)
                            {
                                recipients.Add(new NotificationRecipient
                                {
                                    TenantId = tenancy.Tenant.TenantId,
                                    TenantName = tenancy.Tenant.FullName,
                                    Email = tenancy.Tenant.Email,
                                    PhoneNumber = tenancy.Tenant.PhoneNumber,
                                    TenancyId = row.TenancyId,
                                    HouseAddress = row.HouseAddress,
                                    HasEmail = !string.IsNullOrWhiteSpace(tenancy.Tenant.Email),
                                    HasWhatsApp = !string.IsNullOrWhiteSpace(tenancy.Tenant.PhoneNumber),
                                    AmountDue = row.Balance,
                                    DueDate = new DateTime(filter.Year.Value, filter.Month.Value, tenancy.PaymentDueDay)
                                });
                            }
                        }
                    }
                }
                else if (filter.StatusFilter == "MissingDocs")
                {
                    // Get tenants with missing documents
                    var tenants = await _tenantRepository.GetAllTenantsAsync();
                    foreach (var tenant in tenants.Where(t => !t.IsArchived))
                    {
                        var checklist = await _documentService.GetDocumentStatusChecklistAsync(tenantId: tenant.TenantId);
                        var missingDocs = checklist.Where(d => d.Status == "Missing");
                        if (missingDocs.Any())
                        {
                        var tenancies = await _tenancyRepository.GetTenanciesByTenantIdAsync(tenant.TenantId);
                        var tenancy = tenancies.FirstOrDefault(t => t.Status == "Active");
                        var house = tenancy?.House;
                            
                            recipients.Add(new NotificationRecipient
                            {
                                TenantId = tenant.TenantId,
                                TenantName = tenant.FullName,
                                Email = tenant.Email,
                                PhoneNumber = tenant.PhoneNumber,
                                TenancyId = tenancy?.TenancyId,
                                HouseAddress = house != null ? $"{house.AddressLine1}, {house.City}" : "Unknown",
                                HasEmail = !string.IsNullOrWhiteSpace(tenant.Email),
                                HasWhatsApp = !string.IsNullOrWhiteSpace(tenant.PhoneNumber)
                            });
                        }
                    }
                }
            }

            return recipients;
        }

        public async Task<string> RenderTemplateAsync(string templateBody, string? subjectTemplate, NotificationContext context)
        {
            var rendered = templateBody
                .Replace("{TenantName}", context.TenantName)
                .Replace("{HouseAddress}", context.HouseAddress)
                .Replace("{AmountDue}", context.AmountDue?.ToString("C") ?? "£0.00")
                .Replace("{DueDate}", context.DueDate?.ToString("dd MMMM yyyy") ?? "")
                .Replace("{Month}", context.Month)
                .Replace("{DepositRemaining}", context.DepositRemaining?.ToString("C") ?? "£0.00")
                .Replace("{PayInstructions}", context.PayInstructions)
                .Replace("{Message}", context.Message ?? "");

            return await Task.FromResult(rendered);
        }

        public async Task<Notification> QueueNotificationAsync(NotificationDto dto)
        {
            var notification = new Notification
            {
                TenantId = dto.TenantId,
                TenancyId = dto.TenancyId,
                Channel = dto.Channel,
                Type = dto.Type,
                ToAddress = "", // Will be set from tenant
                Subject = dto.Subject,
                Body = dto.Body,
                ScheduledFor = dto.ScheduledFor,
                Status = "Pending",
                TemplateId = dto.TemplateId
            };

            // Get tenant email/phone
            var tenant = await _tenantRepository.GetTenantByIdAsync(dto.TenantId);
            if (tenant != null)
            {
                notification.ToAddress = dto.Channel == "Email" ? tenant.Email : tenant.PhoneNumber ?? "";
            }

            var notificationId = await _notificationRepository.CreateNotificationAsync(notification);
            notification.NotificationId = notificationId;
            return notification;
        }

        public async Task<bool> SendNotificationAsync(int notificationId)
        {
            var notification = await _notificationRepository.GetNotificationByIdAsync(notificationId);
            if (notification == null || notification.Status != "Pending")
                return false;

            try
            {
                bool success = false;
                string? error = null;
                string? providerMessageId = null;

                if (notification.Channel == "Email")
                {
                    success = await _emailSender.SendEmailAsync(
                        notification.ToAddress,
                        notification.Subject ?? "",
                        notification.Body);
                }
                else if (notification.Channel == "WhatsApp")
                {
                    error = "WhatsApp integration not yet implemented";
                }

                // Record attempt
                var attempt = new NotificationAttempt
                {
                    NotificationId = notificationId,
                    AttemptedAt = DateTime.UtcNow,
                    Status = success ? "Success" : "Failed",
                    Error = error,
                    ProviderMessageId = providerMessageId
                };
                await _notificationRepository.CreateAttemptAsync(attempt);

                // Update notification
                notification.Status = success ? "Sent" : "Failed";
                notification.SentAt = DateTime.UtcNow;
                notification.Error = error;
                notification.ProviderMessageId = providerMessageId;
                await _notificationRepository.UpdateNotificationAsync(notification);

                return success;
            }
            catch (Exception ex)
            {
                var attempt = new NotificationAttempt
                {
                    NotificationId = notificationId,
                    AttemptedAt = DateTime.UtcNow,
                    Status = "Failed",
                    Error = ex.Message
                };
                await _notificationRepository.CreateAttemptAsync(attempt);

                notification.Status = "Failed";
                notification.Error = ex.Message;
                await _notificationRepository.UpdateNotificationAsync(notification);

                return false;
            }
        }

        public async Task<bool> SendBatchAsync(IEnumerable<int> notificationIds)
        {
            var results = new List<bool>();
            foreach (var id in notificationIds)
            {
                results.Add(await SendNotificationAsync(id));
            }
            return results.All(r => r);
        }

        public async Task<IEnumerable<Notification>> GetNotificationsAsync(NotificationFilter filter)
        {
            return await _notificationRepository.GetAllNotificationsAsync(
                filter.Status,
                filter.StartDate,
                filter.EndDate,
                filter.Channel,
                filter.Type,
                filter.TenantId,
                filter.HouseId);
        }

        public async Task<Notification?> GetNotificationByIdAsync(int notificationId)
        {
            return await _notificationRepository.GetNotificationByIdAsync(notificationId);
        }

        public async Task CancelNotificationAsync(int notificationId)
        {
            var notification = await _notificationRepository.GetNotificationByIdAsync(notificationId);
            if (notification != null && notification.Status == "Pending")
            {
                notification.Status = "Cancelled";
                await _notificationRepository.UpdateNotificationAsync(notification);
            }
        }

        public async Task<IEnumerable<NotificationTemplate>> GetTemplatesAsync(string? channel = null, string? type = null)
        {
            return await _notificationRepository.GetAllTemplatesAsync(channel, type);
        }

        public async Task<NotificationTemplate?> GetTemplateByIdAsync(int templateId)
        {
            return await _notificationRepository.GetTemplateByIdAsync(templateId);
        }
    }
}
