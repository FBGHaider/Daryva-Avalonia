using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;
using Daryva.Services.Migration;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels;

public class MigrationViewModel : BaseViewModel
{
    private readonly IMigrationService _migrationService;
    private readonly IApiClient _apiClient;
    private readonly IOrganizationApiService _organizationApiService;
    private readonly IDialogService _dialogService;

    private string _currentStep = "Ready to migrate";
    private int _completedSteps;
    private int _totalSteps = 12;
    private bool _isComplete;
    private bool _isMigrating;
    private string? _errorMessage;
    private bool _hasErrors;

    public MigrationViewModel(
        IMigrationService migrationService,
        IApiClient apiClient,
        IOrganizationApiService organizationApiService,
        IDialogService dialogService)
    {
        _migrationService = migrationService;
        _apiClient = apiClient;
        _organizationApiService = organizationApiService;
        _dialogService = dialogService;

        StartMigrationCommand = new RelayCommand(async _ => await StartMigrationAsync(), _ => !IsMigrating);
        
        Stats = new();
    }

    public ICommand StartMigrationCommand { get; }

    public string CurrentStep
    {
        get => _currentStep;
        set
        {
            _currentStep = value;
            OnPropertyChanged();
        }
    }

    public int CompletedSteps
    {
        get => _completedSteps;
        set
        {
            _completedSteps = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    public int TotalSteps
    {
        get => _totalSteps;
        set
        {
            _totalSteps = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    public double ProgressPercent => TotalSteps > 0 ? (double)CompletedSteps / TotalSteps * 100 : 0;

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            _isComplete = value;
            OnPropertyChanged();
        }
    }

    public bool IsMigrating
    {
        get => _isMigrating;
        set
        {
            _isMigrating = value;
            OnPropertyChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool HasErrors
    {
        get => _hasErrors;
        set
        {
            _hasErrors = value;
            OnPropertyChanged();
        }
    }

    public MigrationStats Stats { get; }

    private async Task StartMigrationAsync()
    {
        try
        {
            IsMigrating = true;
            IsComplete = false;
            HasErrors = false;
            ErrorMessage = null;
            CurrentStep = "Initializing...";

            // Ensure organization context is set before migration
            // This is critical: data will be imported under this org, and queries must use the same org
            var orgId = _apiClient.CurrentOrgId;
            if (orgId == null || orgId == Guid.Empty)
            {
                var orgs = await _organizationApiService.GetUserOrganizationsAsync();
                if (orgs.Count == 0)
                {
                    ErrorMessage = "No organizations found. Please create an organization first.";
                    HasErrors = true;
                    return;
                }

                // Use first organization
                orgId = orgs[0].Id;
                _apiClient.SetCurrentOrgId(orgId.Value);
                
                // Log the selected org for debugging
                System.Diagnostics.Debug.WriteLine(
                    $"[MigrationViewModel] Auto-selected organization: {orgId} ({orgs[0].Name})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MigrationViewModel] Using current organization: {orgId}");
            }

            var progress = new Progress<MigrationProgress>(p =>
            {
                CurrentStep = p.CurrentStep;
                CompletedSteps = p.CompletedSteps;
                TotalSteps = p.TotalSteps;
                
                Stats.HousesImported = p.Stats.HousesImported;
                Stats.TenantsImported = p.Stats.TenantsImported;
                Stats.TenanciesImported = p.Stats.TenanciesImported;
                Stats.ExpensesImported = p.Stats.ExpensesImported;
                Stats.DocumentsImported = p.Stats.DocumentsImported;
                Stats.RentPaymentsImported = p.Stats.RentPaymentsImported;
                Stats.DepositPaymentsImported = p.Stats.DepositPaymentsImported;
                Stats.NotificationTemplatesImported = p.Stats.NotificationTemplatesImported;
                Stats.NotificationsImported = p.Stats.NotificationsImported;
                Stats.NotificationAttemptsImported = p.Stats.NotificationAttemptsImported;
            });

            var result = await _migrationService.MigrateAllDataAsync(orgId.Value, progress);

            if (result.Success)
            {
                IsComplete = true;
                CurrentStep = $"✓ Migration complete! {result.Stats.TotalItemsImported} items imported.";
                
                await _dialogService.ShowMessageAsync(
                    $"Successfully migrated {result.Stats.TotalItemsImported} items to the API!\n\n" +
                    $"Houses: {result.Stats.HousesImported}\n" +
                    $"Tenants: {result.Stats.TenantsImported}\n" +
                    $"Tenancies: {result.Stats.TenanciesImported}\n" +
                    $"Expenses: {result.Stats.ExpensesImported}\n" +
                    $"Documents: {result.Stats.DocumentsImported}\n" +
                    $"Rent Payments: {result.Stats.RentPaymentsImported}\n" +
                    $"Deposit Payments: {result.Stats.DepositPaymentsImported}\n" +
                    $"Notification Templates: {result.Stats.NotificationTemplatesImported}\n" +
                    $"Notifications: {result.Stats.NotificationsImported}\n" +
                    $"Notification Attempts: {result.Stats.NotificationAttemptsImported}",
                    "Migration Complete"
                );
            }
            else
            {
                HasErrors = true;
                ErrorMessage = result.Message;
                CurrentStep = "✗ Migration failed";
                
                var errorDetails = result.Errors.Count > 0 
                    ? $"\n\nErrors:\n" + string.Join("\n", result.Errors) 
                    : "";
                
                await _dialogService.ShowMessageAsync(
                    result.Message + errorDetails,
                    "Migration Failed"
                );
            }
        }
        catch (Exception ex)
        {
            HasErrors = true;
            ErrorMessage = ex.Message;
            CurrentStep = "✗ Migration failed";
            
            await _dialogService.ShowMessageAsync(
                $"An error occurred during migration:\n\n{ex.Message}",
                "Migration Error"
            );
        }
        finally
        {
            IsMigrating = false;
        }
    }
}
