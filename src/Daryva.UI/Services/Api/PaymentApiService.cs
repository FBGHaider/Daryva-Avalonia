using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using Daryva.Services;
using Daryva.Services.Business;

namespace Daryva.Services.Api;

public class PaymentApiService : IPaymentApiService
{
    private const string HouseIdMapSettingKey = "ApiMigration.HouseIdMap";
    private const string TenantIdMapSettingKey = "ApiMigration.TenantIdMap";
    private const string TenancyIdMapSettingKey = "ApiMigration.TenancyIdMap";
    private const string RentPaymentIdMapSettingKey = "ApiMigration.RentPaymentIdMap";
    private const string DepositPaymentIdMapSettingKey = "ApiMigration.DepositPaymentIdMap";

    private const int SyntheticPaymentIdStart = 1_000_000;

    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configurationService;
    private readonly IApiEntityIdMapper? _idMapper;
    private readonly object _paymentMapLock = new();

    private readonly ConcurrentDictionary<int, Guid> _houseMap = new();
    private readonly ConcurrentDictionary<int, Guid> _tenantMap = new();
    private readonly ConcurrentDictionary<int, Guid> _tenancyMap = new();
    private readonly ConcurrentDictionary<int, Guid> _rentPaymentMap = new();
    private readonly ConcurrentDictionary<int, Guid> _depositPaymentMap = new();

    private readonly ConcurrentDictionary<Guid, int> _houseReverseMap = new();
    private readonly ConcurrentDictionary<Guid, int> _tenantReverseMap = new();
    private readonly ConcurrentDictionary<Guid, int> _tenancyReverseMap = new();
    private readonly ConcurrentDictionary<Guid, int> _rentPaymentReverseMap = new();
    private readonly ConcurrentDictionary<Guid, int> _depositPaymentReverseMap = new();

    private bool _loadedPersistedMaps;

    public PaymentApiService(IApiClient apiClient, IConfigurationService configurationService, IApiEntityIdMapper? idMapper = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _idMapper = idMapper;
    }

    public async Task<RecordPaymentApiResponse> RecordPaymentAsync(RecordPaymentApiRequest request, CancellationToken cancellationToken = default)
    {
        EnsureMapsLoaded();

        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/payments/record", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"{response.StatusCode}: {errorBody}");
        }

        var body = await response.Content.ReadFromJsonAsync<RecordPaymentApiResponse>(cancellationToken: cancellationToken);
        if (body == null)
            throw new InvalidOperationException("Failed to parse payment response from API.");

        if (body.RentPaymentId.HasValue)
        {
            _ = GetOrCreateLocalPaymentId(body.RentPaymentId.Value, "Rent");
        }

        if (body.DepositPaymentId.HasValue)
        {
            _ = GetOrCreateLocalPaymentId(body.DepositPaymentId.Value, "Deposit");
        }

        return body;
    }

    public async Task<decimal> GetTotalDepositPaidAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/payments/totals/deposit/{tenancyId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"{response.StatusCode}: {body}");
        }
        var value = await response.Content.ReadFromJsonAsync<decimal>(cancellationToken: cancellationToken);
        return value;
    }

    public async Task<decimal> GetTotalRentPaidForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/payments/totals/rent/{tenancyId}?year={year}&month={month}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"{response.StatusCode}: {body}");
        }
        var value = await response.Content.ReadFromJsonAsync<decimal>(cancellationToken: cancellationToken);
        return value;
    }

    public async Task<string> GetDepositStatusAsync(Guid tenancyId, decimal requiredAmount, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/payments/status/deposit/{tenancyId}?requiredAmount={requiredAmount}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var value = await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken);
        return value ?? "Unpaid";
    }

    public async Task<string> GetRentStatusForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/payments/status/rent/{tenancyId}?year={year}&month={month}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var value = await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken);
        return value ?? "Unpaid";
    }

    public Task<Guid?> ResolveTenancyApiIdAsync(int localTenancyId, CancellationToken cancellationToken = default)
    {
        EnsureMapsLoaded();
        if (_tenancyMap.TryGetValue(localTenancyId, out var cached))
            return Task.FromResult<Guid?>(cached);
        if (_idMapper?.TryGetTenancyApiId(localTenancyId) is { } guid)
        {
            _tenancyMap[localTenancyId] = guid;
            _tenancyReverseMap[guid] = localTenancyId;
            return Task.FromResult<Guid?>(guid);
        }
        return Task.FromResult<Guid?>(null);
    }

    public Task<Guid?> ResolveHouseApiIdAsync(int localHouseId, CancellationToken cancellationToken = default)
    {
        EnsureMapsLoaded();
        if (_houseMap.TryGetValue(localHouseId, out var cached))
            return Task.FromResult<Guid?>(cached);
        if (_idMapper?.TryGetHouseApiId(localHouseId) is { } guid)
        {
            _houseMap[localHouseId] = guid;
            _houseReverseMap[guid] = localHouseId;
            return Task.FromResult<Guid?>(guid);
        }
        return Task.FromResult<Guid?>(null);
    }

    public Task<Guid?> ResolveTenantApiIdAsync(int localTenantId, CancellationToken cancellationToken = default)
    {
        EnsureMapsLoaded();
        if (_tenantMap.TryGetValue(localTenantId, out var cached))
            return Task.FromResult<Guid?>(cached);
        if (_idMapper?.TryGetTenantApiId(localTenantId) is { } guid)
        {
            _tenantMap[localTenantId] = guid;
            _tenantReverseMap[guid] = localTenantId;
            return Task.FromResult<Guid?>(guid);
        }
        return Task.FromResult<Guid?>(null);
    }

    public int? ResolveLocalTenancyId(Guid apiTenancyId)
    {
        EnsureMapsLoaded();
        if (_tenancyReverseMap.TryGetValue(apiTenancyId, out var local))
            return local;
        if (_idMapper != null)
        {
            local = _idMapper.MapTenancyId(apiTenancyId);
            _tenancyMap[local] = apiTenancyId;
            _tenancyReverseMap[apiTenancyId] = local;
            return local;
        }
        return null;
    }

    public int? ResolveLocalHouseId(Guid apiHouseId)
    {
        EnsureMapsLoaded();
        if (_houseReverseMap.TryGetValue(apiHouseId, out var local))
            return local;
        if (_idMapper != null)
        {
            local = _idMapper.MapHouseId(apiHouseId);
            _houseMap[local] = apiHouseId;
            _houseReverseMap[apiHouseId] = local;
            return local;
        }
        return null;
    }

    public int? ResolveLocalTenantId(Guid apiTenantId)
    {
        EnsureMapsLoaded();
        if (_tenantReverseMap.TryGetValue(apiTenantId, out var local))
            return local;
        if (_idMapper != null)
        {
            local = _idMapper.MapTenantId(apiTenantId);
            _tenantMap[local] = apiTenantId;
            _tenantReverseMap[apiTenantId] = local;
            return local;
        }
        return null;
    }

    public int GetOrCreateLocalPaymentId(Guid apiPaymentId, string paymentType)
    {
        EnsureMapsLoaded();

        var isRent = paymentType.Equals("Rent", StringComparison.OrdinalIgnoreCase);
        var reverse = isRent ? _rentPaymentReverseMap : _depositPaymentReverseMap;
        var forward = isRent ? _rentPaymentMap : _depositPaymentMap;
        var settingKey = isRent ? RentPaymentIdMapSettingKey : DepositPaymentIdMapSettingKey;

        if (reverse.TryGetValue(apiPaymentId, out var existingId))
        {
            return existingId;
        }

        lock (_paymentMapLock)
        {
            if (reverse.TryGetValue(apiPaymentId, out existingId))
            {
                return existingId;
            }

            var nextId = forward.Keys.DefaultIfEmpty(SyntheticPaymentIdStart - 1).Max() + 1;
            forward[nextId] = apiPaymentId;
            reverse[apiPaymentId] = nextId;
            PersistMap(settingKey, forward);
            return nextId;
        }
    }

    public Guid? ResolveApiPaymentId(int localPaymentId, string paymentType)
    {
        EnsureMapsLoaded();

        var isRent = paymentType.Equals("Rent", StringComparison.OrdinalIgnoreCase);
        var forward = isRent ? _rentPaymentMap : _depositPaymentMap;
        return forward.TryGetValue(localPaymentId, out var apiId) ? apiId : null;
    }

    public async Task<IReadOnlyList<RentLedgerItemApiDto>> GetRentLedgerForMonthAsync(int year, int month, Guid? houseId = null, string? statusFilter = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var uri = BuildQueryUri(
            "api/payments/ledger/rent",
            ("year", year.ToString()),
            ("month", month.ToString()),
            ("houseId", houseId?.ToString()),
            ("statusFilter", statusFilter),
            ("searchTerm", searchTerm));

        var response = await _apiClient.HttpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<RentLedgerItemApiDto>>(cancellationToken: cancellationToken);
        return rows ?? new List<RentLedgerItemApiDto>();
    }

    public async Task<IReadOnlyList<DepositLedgerItemApiDto>> GetDepositLedgerForMonthAsync(int year, int month, Guid? houseId = null, string? statusFilter = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var uri = BuildQueryUri(
            "api/payments/ledger/deposit",
            ("year", year.ToString()),
            ("month", month.ToString()),
            ("houseId", houseId?.ToString()),
            ("statusFilter", statusFilter),
            ("searchTerm", searchTerm));

        var response = await _apiClient.HttpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<DepositLedgerItemApiDto>>(cancellationToken: cancellationToken);
        return rows ?? new List<DepositLedgerItemApiDto>();
    }

    public async Task<IReadOnlyList<TransactionItemApiDto>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, Guid? houseId = null, Guid? tenantId = null, string? method = null, CancellationToken cancellationToken = default)
    {
        var uri = BuildQueryUri(
            "api/payments/transactions",
            ("startDate", startDate?.ToString("O")),
            ("endDate", endDate?.ToString("O")),
            ("paymentType", paymentType),
            ("houseId", houseId?.ToString()),
            ("tenantId", tenantId?.ToString()),
            ("method", method));

        var response = await _apiClient.HttpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<TransactionItemApiDto>>(cancellationToken: cancellationToken)
                   ?? new List<TransactionItemApiDto>();

        foreach (var row in rows)
        {
            _ = GetOrCreateLocalPaymentId(row.PaymentId, row.PaymentType);
        }

        return rows;
    }

    public async Task<bool> UnrecordPaymentAsync(Guid apiPaymentId, string paymentType, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.DeleteAsync($"api/payments/transactions/{paymentType}/{apiPaymentId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<IReadOnlyList<DepositReturnReminderApiDto>> GetDepositReturnRemindersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/payments/deposit-return-reminders", cancellationToken);
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<DepositReturnReminderApiDto>>(cancellationToken: cancellationToken);
        return list ?? new List<DepositReturnReminderApiDto>();
    }

    public async Task RecordDepositReturnedAsync(Guid tenancyId, DateTime returnedDate, decimal amountReturned, string? notes, CancellationToken cancellationToken = default)
    {
        var body = new { TenancyId = tenancyId, ReturnedDate = returnedDate, AmountReturned = amountReturned, Notes = notes };
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/payments/deposit-returned", body, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAllTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.DeleteAsync("api/payments/transactions", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void EnsureMapsLoaded()
    {
        if (_loadedPersistedMaps)
        {
            return;
        }

        LoadPersistedMap(HouseIdMapSettingKey, _houseMap, _houseReverseMap);
        LoadPersistedMap(TenantIdMapSettingKey, _tenantMap, _tenantReverseMap);
        LoadPersistedMap(TenancyIdMapSettingKey, _tenancyMap, _tenancyReverseMap);
        LoadPersistedMap(RentPaymentIdMapSettingKey, _rentPaymentMap, _rentPaymentReverseMap);
        LoadPersistedMap(DepositPaymentIdMapSettingKey, _depositPaymentMap, _depositPaymentReverseMap);
        _loadedPersistedMaps = true;
    }

    private void LoadPersistedMap(
        string settingKey,
        ConcurrentDictionary<int, Guid> forward,
        ConcurrentDictionary<Guid, int> reverse)
    {
        var raw = _configurationService.GetValue(settingKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<int, Guid>>(raw);
            if (map == null)
            {
                return;
            }

            foreach (var kvp in map)
            {
                forward[kvp.Key] = kvp.Value;
                reverse[kvp.Value] = kvp.Key;
            }
        }
        catch
        {
        }
    }

    private void PersistMap(string settingKey, ConcurrentDictionary<int, Guid> map)
    {
        var serialized = JsonSerializer.Serialize(map.ToDictionary(k => k.Key, v => v.Value));
        _configurationService.SetLocalValue(settingKey, serialized);
    }

    private static string BuildQueryUri(string basePath, params (string Key, string? Value)[] parts)
    {
        var filtered = parts.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToList();
        if (filtered.Count == 0)
        {
            return basePath;
        }

        var query = string.Join("&", filtered.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
        return $"{basePath}?{query}";
    }
}
