using System.Text;
using System.Text.Json;

namespace Daryva.Services.Api;

public class ExpenseApiService : IExpenseApiService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ExpenseApiService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync("api/expenses", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get expenses: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var expenses = JsonSerializer.Deserialize<List<ExpenseDto>>(content, _jsonOptions);
            return expenses ?? new List<ExpenseDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<List<ExpenseDto>> GetExpensesByHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/houses/{houseId}/expenses", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get expenses for house: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var expenses = JsonSerializer.Deserialize<List<ExpenseDto>>(content, _jsonOptions);
            return expenses ?? new List<ExpenseDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<ExpenseDto?> GetExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/expenses/{expenseId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get expense: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ExpenseDto>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto expense, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(expense);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PostAsync("api/expenses", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to create expense: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdExpense = JsonSerializer.Deserialize<ExpenseDto>(responseContent, _jsonOptions);
            
            if (createdExpense == null)
                throw new InvalidOperationException("Failed to deserialize created expense response.");

            return createdExpense;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<ExpenseDto> UpdateExpenseAsync(Guid expenseId, UpdateExpenseDto expense, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(expense);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PutAsync($"api/expenses/{expenseId}", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to update expense: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedExpense = JsonSerializer.Deserialize<ExpenseDto>(responseContent, _jsonOptions);
            
            if (updatedExpense == null)
                throw new InvalidOperationException("Failed to deserialize updated expense response.");

            return updatedExpense;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> DeleteExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.DeleteAsync($"api/expenses/{expenseId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to delete expense: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }
}
