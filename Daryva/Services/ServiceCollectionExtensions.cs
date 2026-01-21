using Microsoft.Extensions.DependencyInjection;
using Daryva.Services.Database;
using Daryva.Services.Data;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Daryva.Services.Dialog;

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
            // Configuration Services
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Database Services
            services.AddScoped<IDbContextFactory, DbContextFactory>();
            services.AddScoped<IDbContext>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IDbContextFactory>();
                return factory.CreateDbContext();
            });

            // Repositories
            services.AddScoped<IHouseRepository, HouseRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<ITenancyRepository, TenancyRepository>();
            services.AddScoped<IDepositPaymentRepository, DepositPaymentRepository>();
            services.AddScoped<IRentChargeRepository, RentChargeRepository>();
            services.AddScoped<IRentPaymentRepository, RentPaymentRepository>();
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IExpenseRepository, ExpenseRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<ISettingsRepository, SettingsRepository>();

            // Business Services
            services.AddScoped<IHouseService, HouseService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<IExpenseService, ExpenseService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IEmailSender>(serviceProvider =>
            {
                var configService = serviceProvider.GetService<IConfigurationService>();
                return new EmailSender(configService);
            });
            services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IDocumentService, DocumentService>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<IBackupService, BackupService>();

            // Navigation and Dialog Services
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();

            return services;
        }

        /// <summary>
        /// Adds all ViewModels to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add ViewModels to.</param>
        /// <returns>The service collection for method chaining.</returns>
        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            services.AddTransient<Daryva.MVVM.ViewModels.MainWindowViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DashboardViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.HousesViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.TenantsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RentPaymentsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DocumentsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.ExpensesViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AddEditExpenseViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.NotificationsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.SettingsViewModel>();
            
            // Settings section ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.GeneralSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.ThemeSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RentSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.DocumentSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.NotificationSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.EmailSettingsViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.BackupSettingsViewModel>();
            
            // Dialog ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.AddHouseViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.AddTenantViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.EditTenantViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.RecordPaymentViewModel>();
            
            // Rent & Payments tab ViewModels
            services.AddTransient<Daryva.MVVM.ViewModels.RentLedgerViewModel>();
            services.AddTransient<Daryva.MVVM.ViewModels.TransactionsViewModel>();

            return services;
        }
    }
}
