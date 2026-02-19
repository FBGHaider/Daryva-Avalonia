using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Daryva.Services.Migration;

public interface IMigrationService
{
    Task<MigrationResult> MigrateAllDataAsync(Guid targetOrgId, IProgress<MigrationProgress> progress);
}

public class MigrationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MigrationStats Stats { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class MigrationStats
{
    public int HousesImported { get; set; }
    public int TenantsImported { get; set; }
    public int TenanciesImported { get; set; }
    public int ExpensesImported { get; set; }
    public int DocumentsImported { get; set; }
    public int RentPaymentsImported { get; set; }
    public int DepositPaymentsImported { get; set; }
    
    public int TotalItemsImported => 
        HousesImported + TenantsImported + TenanciesImported + 
        ExpensesImported + DocumentsImported + 
        RentPaymentsImported + DepositPaymentsImported;
}

public class MigrationProgress
{
    public string CurrentStep { get; set; } = string.Empty;
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public MigrationStats Stats { get; set; } = new();
}
