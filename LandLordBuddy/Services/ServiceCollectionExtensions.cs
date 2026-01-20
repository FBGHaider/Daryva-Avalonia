using Microsoft.Extensions.DependencyInjection;
using LandLordBuddy.Services.Database;
using LandLordBuddy.Services.Data;
using LandLordBuddy.Services.Business;
using LandLordBuddy.Services.Navigation;
using LandLordBuddy.Services.Dialog;

namespace LandLordBuddy.Services
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
            services.AddTransient<MVVM.ViewModels.MainWindowViewModel>();
            services.AddTransient<MVVM.ViewModels.DashboardViewModel>();
            services.AddTransient<MVVM.ViewModels.HousesViewModel>();
            services.AddTransient<MVVM.ViewModels.TenantsViewModel>();
            services.AddTransient<MVVM.ViewModels.RentPaymentsViewModel>();
            services.AddTransient<MVVM.ViewModels.DocumentsViewModel>();
            services.AddTransient<MVVM.ViewModels.ExpensesViewModel>();
            services.AddTransient<MVVM.ViewModels.AddEditExpenseViewModel>();
            services.AddTransient<MVVM.ViewModels.NotificationsViewModel>();
            services.AddTransient<MVVM.ViewModels.SettingsViewModel>();
            
            // Dialog ViewModels
            services.AddTransient<MVVM.ViewModels.AddHouseViewModel>();
            services.AddTransient<MVVM.ViewModels.AddTenantViewModel>();
            services.AddTransient<MVVM.ViewModels.EditTenantViewModel>();
            services.AddTransient<MVVM.ViewModels.RecordPaymentViewModel>();
            
            // Rent & Payments tab ViewModels
            services.AddTransient<MVVM.ViewModels.RentLedgerViewModel>();
            services.AddTransient<MVVM.ViewModels.TransactionsViewModel>();

            return services;
        }
    }
}
