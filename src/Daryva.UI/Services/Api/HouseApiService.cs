using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Daryva.Services.Api;

public class HouseApiService : IHouseApiService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public HouseApiService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<HouseDto>> GetHousesAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = includeArchived ? "?includeArchived=true" : "";
            var response = await _apiClient.HttpClient.GetAsync("api/houses" + query, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get houses: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var houses = JsonSerializer.Deserialize<List<HouseDto>>(content, _jsonOptions);
            return houses ?? new List<HouseDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<HouseDto?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/houses/{houseId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get house: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<HouseDto>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<HouseDto> CreateHouseAsync(CreateHouseDto house, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(house);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PostAsync("api/houses", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to create house: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdHouse = JsonSerializer.Deserialize<HouseDto>(responseContent, _jsonOptions);
            
            if (createdHouse == null)
                throw new InvalidOperationException("Failed to deserialize created house response.");

            return createdHouse;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<HouseDto> UpdateHouseAsync(Guid houseId, UpdateHouseDto house, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(house);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PutAsync($"api/houses/{houseId}", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to update house: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedHouse = JsonSerializer.Deserialize<HouseDto>(responseContent, _jsonOptions);
            
            if (updatedHouse == null)
                throw new InvalidOperationException("Failed to deserialize updated house response.");

            return updatedHouse;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<HouseDto?> ArchiveHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.PostAsync($"api/houses/{houseId}/archive", null, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to archive house: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<HouseDto>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> DeleteHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.DeleteAsync($"api/houses/{houseId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to delete house: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }
}
