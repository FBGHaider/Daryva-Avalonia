using Microsoft.Extensions.DependencyInjection;
using Daryva.Services.Database;
using Daryva.Services.Data;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Daryva.Services.Dialog;
using Daryva.Services.Platform;
using Daryva.Services.Update;
using Daryva.Services.Api;
using Daryva.Services.Auth;
using Daryva.Services.Migration;
using Daryva.Services.OrgContext;
using Daryva.Services.Session;
using Daryva.Services.AppReset;

namespace Daryva.Services
{
    /// <summary>
    /// Extension methods for configuring services in the dependency injection container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds all services required by the application to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Platform Services
            services.AddSingleton<IAppPaths, AppPaths>();
            services.AddSingleton<ISecureStore, SecureStore>();

            // Configuration Services
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // API Client Services
            services.AddSingleton<IAuthSessionService, AuthSessionService>();
            services.AddSingleton<ISessionContext, SessionContext>();
            services.AddSingleton<IScopedStorage, ScopedStorage>();
            services.AddSingleton<IAppResetService, AppResetService>();
            services.AddSingleton<IAccountDataClearer, AccountDataClearer>();
            services.AddSingleton<IApiClient, ApiClient>();
            // Singleton, not scoped: this app has no per-request DI scope, and AuthService (singleton)
            // depends on this directly -- a scoped registration here would be a captive-dependency bug.
            services.AddSingleton<IAuthApiService, AuthApiService>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddScoped<IOrganizationApiService, OrganizationApiService>();
            services.AddScoped<IMeApiService, MeApiService>();
            services.AddSingleton<IOrgContext, OrgContext.OrgContext>();
            services.AddScoped<IHouseApiService, HouseApiService>();
            services.AddScoped<ITenantApiService, TenantApiService>();
            services.AddScoped<ITenancyApiService, TenancyApiService>();
            services.AddScoped<IExpenseApiService, ExpenseApiService>();
            services.AddScoped<IDocumentApiService, DocumentApiService>();
            services.AddScoped<INotificationApiService, NotificationApiService>();
            services.AddScoped<IPaymentApiService, PaymentApiService>();
            services.AddScoped<IAuditLogApiService, AuditLogApiService>();
            services.AddScoped<ISupportSessionApiService, SupportSessionApiService>();

            // Database Services
            services.AddScoped<IDbContextFactory, DbContextFactory>();
            services.AddScoped<DatabaseMigrationRunner>();
            services.AddScoped<IDbContext>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IDbContextFactory>();
                return factory.CreateDbContext();
            });

            // Repositories
            services.AddScoped<IHouseRepository, HouseRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<ITenancyRepository, TenancyApiRepositoryAdapter>();
            services.AddScoped<IDepositPaymentRepository, DepositPaymentRepository>();
            services.AddScoped<IRentChargeRepository, RentChargeRepository>();
            services.AddScoped<IRentPaymentRepository, RentPaymentRepository>();
            services.AddScoped<IDepositReturnRepository, DepositReturnRepository>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IExpenseRepository, ExpenseRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<ISettingsRepository, SettingsRepository>();

            // Business Services — API-only: tenants, houses, expenses, documents, notifications use API adapters.
            // Tenancy create (add tenant) uses ITenancyApiService only; no SQLite.
            services.AddSingleton<IApiEntityIdMapper, ApiEntityIdMapper>();
            services.AddScoped<HouseService>();
            services.AddScoped<HouseApiServiceAdapter>();
            services.AddScoped<IHouseService, HouseApiServiceAdapter>();

            services.AddScoped<TenantService>();
            services.AddScoped<TenantApiServiceAdapter>();
            services.AddScoped<ITenantService, TenantApiServiceAdapter>();

            services.AddScoped<ExpenseService>();
            services.AddScoped<ExpenseApiServiceAdapter>();
            services.AddScoped<IExpenseService, ExpenseApiServiceAdapter>();

            services.AddScoped<DocumentService>();
            services.AddScoped<DocumentApiServiceAdapter>();
            services.AddScoped<IDocumentService, DocumentApiServiceAdapter>();

            services.AddScoped<PaymentService>();
            services.AddScoped<PaymentApiMigrationService>();
            services.AddScoped<IPaymentService, PaymentApiMigrationService>();

            services.AddScoped<NotificationService>();
            services.AddScoped<NotificationApiServiceAdapter>();
            services.AddScoped<INotificationService, NotificationApiServiceAdapter>();

            services.AddSingleton<NotificationFeedService>();
            services.AddSingleton<INotificationFeedService>(sp => sp.GetRequiredService<NotificationFeedService>());

            services.AddScoped<UserProfileService>();
            services.AddScoped<IUserProfileService>(sp => sp.GetRequiredService<UserProfileService>());

            services.AddScoped<UserPreferencesService>();
            services.AddScoped<IUserPreferencesService>(sp => sp.GetRequiredService<UserPreferencesService>());

            services.AddScoped<OrganisationMemberService>();
            services.AddScoped<IOrganisationMemberService>(sp => new OrganisationMemberApiDecorator(
                sp.GetRequiredService<IOrganizationApiService>(),
                sp.GetRequiredService<OrganisationMemberService>()));
            services.AddScoped<OrganisationService>();
            services.AddScoped<IOrganisationService>(sp => sp.GetRequiredService<OrganisationService>());

            services.AddScoped<IEmailSender>(serviceProvider =>
            {
                var configService = serviceProvider.GetService<IConfigurationService>();
                var apiClient = serviceProvider.GetService<IApiClient>();
                return new EmailSender(configService, apiClient);
            });
            services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IHouseReportExportService, HouseReportExportService>();
            services.AddScoped<ISettingsService, ApiSettingsService>();
            services.AddScoped<IBackupService, BackupService>();
            
            // Migration Service (SQLite to API)
            services.AddScoped<IMigrationService, SqliteToApiMigrationService>();

            // Update Service (UI layer - Velopack)
            services.AddSingleton<IUpdateService, VelopackUpdateService>();

            // Navigation and Dialog Services
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();

            services.AddSingleton<IQueueProcessedNotifier, QueueProcessedNotifier>();
            services.AddSingleton<ScheduledNotificationProcessor>();

            return services;
        }

        /// <summary>
        /// Adds all ViewModels to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add ViewModels to.</param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            services.AddSingleton<Daryva.MVVM.ViewModels.MainViewModel>();
            services.AddSingleton<Daryva.MVVM.ViewModels.NotificationCenterViewModel>();
            services.AddSingleton<Daryva.MVVM.ViewModels.ProfileMenuViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DashboardViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.HousesViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.TenantsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RentPaymentsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DocumentsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.ExpensesViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AddEditExpenseViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.NotificationsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.SettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AccountViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.OrganisationViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AuditLogViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.SupportModeViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.OnboardingViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.SetupRequiredViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.SignInViewModel>();

            // Account section ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.AccountProfileSectionViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AccountSecuritySectionViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AccountNotificationsSectionViewModel>();

            // Settings section ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.GeneralSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.ThemeSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AdvancedSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DatabaseSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RentSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DocumentSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.NotificationSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.BackupSettingsViewModel>();
            
            // Dialog ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.AddHouseViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AddTenantViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.EditTenantViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RecordPaymentViewModel>();
            
            // Rent & Payments tab ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.RentLedgerViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.TransactionsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.UploadDocumentViewModel>();

            return services;
        }
    }
}
