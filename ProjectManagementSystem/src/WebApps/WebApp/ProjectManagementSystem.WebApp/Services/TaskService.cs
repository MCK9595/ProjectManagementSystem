using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.WebApp.Services;

public class TaskService : ITaskService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;

    public TaskService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _authService = authService;
    }

    public async Task<PagedResult<TaskDto>?> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/tasks/project/{projectId}?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<TaskDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<PagedResult<TaskDto>?> GetTasksByUserAsync(int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/tasks/my-tasks?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<TaskDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<TaskDto?> GetTaskAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/tasks/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TaskDto>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("/api/tasks", taskDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TaskDto>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<TaskDto?> UpdateTaskAsync(Guid id, UpdateTaskDto taskDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{id}", taskDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TaskDto>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<bool> UpdateTaskStatusAsync(Guid id, string status)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsJsonAsync($"/api/tasks/{id}/status", new { Status = status });
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<bool> AssignTaskAsync(Guid id, int assignedToUserId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PatchAsJsonAsync($"/api/tasks/{id}/assign", new { AssignedToUserId = assignedToUserId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<bool> DeleteTaskAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/tasks/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    private async Task SetAuthHeaderAsync()
    {
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}