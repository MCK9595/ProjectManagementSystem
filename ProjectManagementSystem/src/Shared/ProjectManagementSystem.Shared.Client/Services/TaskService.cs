using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Client.Models;
using ProjectManagementSystem.Shared.Models.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ProjectManagementSystem.Shared.Client.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>?> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<TaskDto>?> GetTasksByUserAsync(int pageNumber = 1, int pageSize = 10);
    Task<TaskDto?> GetTaskAsync(Guid id);
    Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto);
    Task<TaskDto?> UpdateTaskAsync(Guid id, UpdateTaskDto taskDto);
    Task<bool> UpdateTaskStatusAsync(Guid id, string status);
    Task<bool> AssignTaskAsync(Guid id, int assignedToUserId);
    Task<bool> DeleteTaskAsync(Guid id);
    
    // コメント関連メソッド
    Task<PagedResult<TaskCommentDto>?> GetTaskCommentsAsync(Guid taskId, int pageNumber = 1, int pageSize = 10);
    Task<TaskCommentDto?> CreateTaskCommentAsync(Guid taskId, CreateTaskCommentDto commentDto);
    Task<TaskCommentDto?> UpdateTaskCommentAsync(int commentId, UpdateTaskCommentDto commentDto, Guid taskId);
    Task<bool> DeleteTaskCommentAsync(int commentId, Guid taskId);
    
    // ダッシュボード統計メソッド
    Task<ProjectDashboardStatsDto?> GetProjectDashboardStatsAsync(Guid projectId);
}

public class TaskService : ITaskService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TaskService> _logger;

    public TaskService(IHttpClientFactory httpClientFactory, ILogger<TaskService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _logger = logger;
        
        _logger.LogDebug("TaskService initialized with HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
    }

    public async Task<PagedResult<TaskDto>?> GetTasksAsync(Guid projectId, int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/tasks/project/{projectId}?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetTasksAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogDebug("HTTP response received - StatusCode: {StatusCode}, IsSuccessStatusCode: {Success}", 
                response.StatusCode, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response content length: {ContentLength}", jsonContent.Length);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<TaskDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogDebug("Deserialization successful - TotalCount: {TotalCount}", apiResponse.Data.TotalCount);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response: {Message}", apiResponse?.Message);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetTasksAsync - Type: {ExceptionType}, Message: {ExceptionMessage}", 
                ex.GetType().Name, ex.Message);
        }

        return null;
    }

    public async Task<PagedResult<TaskDto>?> GetTasksByUserAsync(int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/tasks/my-tasks?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetTasksByUserAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<TaskDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response: {Message}", apiResponse?.Message);
                    return null;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetTasksByUserAsync");
        }

        return null;
    }

    public async Task<TaskDto?> GetTaskAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tasks/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TaskDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for task {TaskId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for task {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetTaskAsync for task {TaskId}", id);
        }

        return null;
    }

    public async Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/tasks", taskDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TaskDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for create task: {Message}", apiResponse?.Message);
                    throw new InvalidOperationException(apiResponse?.Message ?? "Unknown error occurred");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Validation failed for create task - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<TaskDto>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    var validationErrors = errorResponse?.Errors?.Any() == true 
                        ? string.Join(", ", errorResponse.Errors)
                        : errorResponse?.Message ?? "Validation failed";
                    throw new ArgumentException(validationErrors);
                }
                catch (JsonException)
                {
                    throw new ArgumentException("Validation failed");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for create task - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Request failed with status code {response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is HttpRequestException)
        {
            throw; // Re-throw specific exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateTaskAsync");
            throw new Exception("An unexpected error occurred while creating the task");
        }
    }

    public async Task<TaskDto?> UpdateTaskAsync(Guid id, UpdateTaskDto taskDto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{id}", taskDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TaskDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for update task {TaskId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for update task {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateTaskAsync for task {TaskId}", id);
        }

        return null;
    }

    public async Task<bool> UpdateTaskStatusAsync(Guid id, string status)
    {
        try
        {
            var request = new { Status = status };
            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{id}/status", request);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for update task status {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateTaskStatusAsync for task {TaskId}", id);
        }

        return false;
    }

    public async Task<bool> AssignTaskAsync(Guid id, int assignedToUserId)
    {
        try
        {
            var request = new { AssignedToUserId = assignedToUserId };
            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{id}/assign", request);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for assign task {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AssignTaskAsync for task {TaskId}", id);
        }

        return false;
    }

    public async Task<bool> DeleteTaskAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/tasks/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for delete task {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteTaskAsync for task {TaskId}", id);
        }

        return false;
    }

    public async Task<PagedResult<TaskCommentDto>?> GetTaskCommentsAsync(Guid taskId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tasks/{taskId}/comments?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<TaskCommentDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for get task comments {TaskId}: {Message}", taskId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for get task comments {TaskId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    taskId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetTaskCommentsAsync for task {TaskId}", taskId);
        }

        return null;
    }

    public async Task<TaskCommentDto?> CreateTaskCommentAsync(Guid taskId, CreateTaskCommentDto commentDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/tasks/{taskId}/comments", commentDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TaskCommentDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for create task comment: {Message}", apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for create task comment - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in CreateTaskCommentAsync for task {TaskId}", taskId);
        }

        return null;
    }

    public async Task<TaskCommentDto?> UpdateTaskCommentAsync(int commentId, UpdateTaskCommentDto commentDto, Guid taskId)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/tasks/{taskId}/comments/{commentId}", commentDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TaskCommentDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for update task comment: {Message}", apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for update task comment - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateTaskCommentAsync for comment {CommentId}, task {TaskId}", commentId, taskId);
        }

        return null;
    }

    public async Task<bool> DeleteTaskCommentAsync(int commentId, Guid taskId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/tasks/{taskId}/comments/{commentId}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return apiResponse?.Success == true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for delete task comment - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteTaskCommentAsync for comment {CommentId}, task {TaskId}", commentId, taskId);
        }

        return false;
    }

    public async Task<ProjectDashboardStatsDto?> GetProjectDashboardStatsAsync(Guid projectId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tasks/project/{projectId}/dashboard-stats");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDashboardStatsDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for get project dashboard stats {ProjectId}: {Message}", projectId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for get project dashboard stats {ProjectId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    projectId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetProjectDashboardStatsAsync for project {ProjectId}", projectId);
        }

        return null;
    }
}