using Microsoft.Extensions.DependencyInjection;
using FBGRentora.Services.Database;
using FBGRentora.Services.Data;
using FBGRentora.Services.Business;
using FBGRentora.Services.Navigation;
using FBGRentora.Services.Dialog;

namespace FBGRentora.Services
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
            services.AddTransient<FBGRentora.MVVM.ViewModels.MainWindowViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.DashboardViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.HousesViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.TenantsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.RentPaymentsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.DocumentsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.ExpensesViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.AddEditExpenseViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.NotificationsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.SettingsViewModel>();
            
            // Settings section ViewModels
            services.AddTransient<FBGRentora.MVVM.ViewModels.GeneralSettingsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.RentSettingsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.DocumentSettingsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.NotificationSettingsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.EmailSettingsViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.BackupSettingsViewModel>();
            
            // Dialog ViewModels
            services.AddTransient<FBGRentora.MVVM.ViewModels.AddHouseViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.AddTenantViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.EditTenantViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.RecordPaymentViewModel>();
            
            // Rent & Payments tab ViewModels
            services.AddTransient<FBGRentora.MVVM.ViewModels.RentLedgerViewModel>();
            services.AddTransient<FBGRentora.MVVM.ViewModels.TransactionsViewModel>();

            return services;
        }
    }
}
