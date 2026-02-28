using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Daryva.Services.Api;

public class TenancyApiService : ITenancyApiService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TenancyApiService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<Guid> CreateTenancyAsync(CreateTenancyDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/tenancies", dto, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "create tenancy", cancellationToken);
        var created = await response.Content.ReadFromJsonAsync<CreateTenancyResponse>(_jsonOptions, cancellationToken);
        return created?.Id ?? Guid.Empty;
    }

    public async Task<IReadOnlyList<TenancyDetailDto>> GetTenanciesAsync(Guid? tenantId = null, Guid? houseId = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var q = new List<string>();
        if (tenantId.HasValue) q.Add($"tenantId={tenantId.Value}");
        if (houseId.HasValue) q.Add($"houseId={houseId.Value}");
        if (activeOnly == true) q.Add("activeOnly=true");
        var query = q.Count > 0 ? "?" + string.Join("&", q) : "";
        var response = await _apiClient.HttpClient.GetAsync("api/tenancies" + query, cancellationToken);
        await EnsureSuccessAsync(response, "get tenancies", cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<TenancyDetailDto>>(_jsonOptions, cancellationToken);
        return list ?? new List<TenancyDetailDto>();
    }

    public async Task<TenancyDetailDto?> GetTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/tenancies/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "get tenancy", cancellationToken);
        return await response.Content.ReadFromJsonAsync<TenancyDetailDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<TenancyDetailDto>> GetTenanciesActiveInPeriodAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/tenancies/active-in-period?year={year}&month={month}", cancellationToken);
        await EnsureSuccessAsync(response, "get tenancies active in period", cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<TenancyDetailDto>>(_jsonOptions, cancellationToken);
        return list ?? new List<TenancyDetailDto>();
    }

    public async Task<IReadOnlyList<TenancyDetailDto>> GetEndedTenanciesWithDepositAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/tenancies/ended-with-deposit", cancellationToken);
        await EnsureSuccessAsync(response, "get ended tenancies with deposit", cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<TenancyDetailDto>>(_jsonOptions, cancellationToken);
        return list ?? new List<TenancyDetailDto>();
    }

    public async Task EndTenancyAsync(Guid id, DateTime moveOutDate, CancellationToken cancellationToken = default)
    {
        var body = new { moveOutDate };
        var response = await _apiClient.HttpClient.PatchAsJsonAsync($"api/tenancies/{id}/end", body, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "end tenancy", cancellationToken);
    }

    public async Task ReactivateTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PatchAsync($"api/tenancies/{id}/reactivate", null, cancellationToken);
        await EnsureSuccessAsync(response, "reactivate tenancy", cancellationToken);
    }

    public async Task UpdateTenancyAsync(Guid id, UpdateTenancyDto dto, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PutAsJsonAsync($"api/tenancies/{id}", dto, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "update tenancy", cancellationToken);
    }

    public async Task DeleteTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.DeleteAsync($"api/tenancies/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Tenancy {id} not found.");
        await EnsureSuccessAsync(response, "delete tenancy", cancellationToken);
    }

    public async Task DeleteEndedTenanciesByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.DeleteAsync($"api/tenancies/by-house/{houseId}?endedOnly=true", cancellationToken);
        await EnsureSuccessAsync(response, "delete ended tenancies by house", cancellationToken);
    }

    public async Task<IReadOnlyList<RentRepairExportItemDto>> GetExportForRentRepairAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/tenancies/export-for-rent-repair", cancellationToken);
        await EnsureSuccessAsync(response, "export for rent repair", cancellationToken);
        var list = await response.Content.ReadFromJsonAsync<List<RentRepairExportItemDto>>(_jsonOptions, cancellationToken);
        return list ?? new List<RentRepairExportItemDto>();
    }

    public async Task<RentRepairResultDto> RepairRentAsync(IReadOnlyList<RentRepairUpdateItemDto> updates, CancellationToken cancellationToken = default)
    {
        var request = new { Updates = updates };
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/tenancies/repair-rent", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "repair rent", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<RentRepairResultDto>(_jsonOptions, cancellationToken);
        return result ?? new RentRepairResultDto();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ApiErrorFormatter.Format(action, response.StatusCode, body);
        throw new InvalidOperationException(message);
    }

    private class CreateTenancyResponse
    {
        public Guid Id { get; set; }
    }
}
