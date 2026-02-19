using System.Text;
using System.Text.Json;

namespace Daryva.Services.Api;

public class DocumentApiService : IDocumentApiService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public DocumentApiService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync("api/documents", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get documents: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var documents = JsonSerializer.Deserialize<List<DocumentDto>>(content, _jsonOptions);
            return documents ?? new List<DocumentDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<List<DocumentDto>> GetDocumentsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/tenants/{tenantId}/documents", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get tenant documents: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var documents = JsonSerializer.Deserialize<List<DocumentDto>>(content, _jsonOptions);
            return documents ?? new List<DocumentDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<List<DocumentDto>> GetDocumentsByTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/tenancies/{tenancyId}/documents", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get tenancy documents: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var documents = JsonSerializer.Deserialize<List<DocumentDto>>(content, _jsonOptions);
            return documents ?? new List<DocumentDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<List<DocumentDto>> GetDocumentsByHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/houses/{houseId}/documents", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get house documents: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var documents = JsonSerializer.Deserialize<List<DocumentDto>>(content, _jsonOptions);
            return documents ?? new List<DocumentDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<DocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/documents/{documentId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get document: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DocumentDto>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<DocumentDto> CreateDocumentAsync(CreateDocumentDto document, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(document);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PostAsync("api/documents", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to create document: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdDocument = JsonSerializer.Deserialize<DocumentDto>(responseContent, _jsonOptions);
            
            if (createdDocument == null)
                throw new InvalidOperationException("Failed to deserialize created document response.");

            return createdDocument;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<DocumentDto> UpdateDocumentAsync(Guid documentId, UpdateDocumentDto document, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(document);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PutAsync($"api/documents/{documentId}", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to update document: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedDocument = JsonSerializer.Deserialize<DocumentDto>(responseContent, _jsonOptions);
            
            if (updatedDocument == null)
                throw new InvalidOperationException("Failed to deserialize updated document response.");

            return updatedDocument;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<byte[]?> DownloadDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/documents/{documentId}/download", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to download document: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.DeleteAsync($"api/documents/{documentId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to delete document: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }
}
