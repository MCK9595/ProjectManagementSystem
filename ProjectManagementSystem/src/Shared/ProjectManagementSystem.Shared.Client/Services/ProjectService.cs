using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Client.Models;
using ProjectManagementSystem.Shared.Models.Common;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ProjectManagementSystem.Shared.Client.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>?> GetProjectsAsync(int pageNumber = 1, int pageSize = 10);
    Task<PagedResult<ProjectDto>?> GetProjectsByOrganizationAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectDto?> GetProjectAsync(Guid id);
    Task<ProjectDto?> CreateProjectAsync(CreateProjectDto projectDto);
    Task<ProjectDto?> UpdateProjectAsync(Guid id, UpdateProjectDto projectDto);
    Task<bool> DeleteProjectAsync(Guid id);
    Task<PagedResult<ProjectMemberDto>?> GetProjectMembersAsync(Guid projectId, int pageNumber = 1, int pageSize = 10);
    Task<ProjectMemberDto?> AddMemberAsync(Guid projectId, AddProjectMemberDto addMemberDto);
    Task<bool> RemoveMemberAsync(Guid projectId, int userId);
    Task<bool> UpdateMemberRoleAsync(Guid projectId, int userId, string role);
}

public class ProjectService : IProjectService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IHttpClientFactory httpClientFactory, ILogger<ProjectService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _logger = logger;
        
        _logger.LogDebug("ProjectService initialized with HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
    }

    public async Task<PagedResult<ProjectDto>?> GetProjectsAsync(int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/projects?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetProjectsAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogDebug("HTTP response received - StatusCode: {StatusCode}, IsSuccessStatusCode: {Success}", 
                response.StatusCode, response.IsSuccessStatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Response content length: {ContentLength}", jsonContent.Length);
                
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<ProjectDto>>>(jsonContent, new JsonSerializerOptions
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
            _logger.LogError(ex, "Exception in GetProjectsAsync - Type: {ExceptionType}, Message: {ExceptionMessage}", 
                ex.GetType().Name, ex.Message);
        }

        return null;
    }

    public async Task<PagedResult<ProjectDto>?> GetProjectsByOrganizationAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10)
    {
        var requestUrl = $"/api/projects/by-organization/{organizationId}?pageNumber={pageNumber}&pageSize={pageSize}";
        _logger.LogDebug("GetProjectsByOrganizationAsync called - requesting: {RequestUrl}", requestUrl);
        
        try
        {
            var response = await _httpClient.GetAsync(requestUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<ProjectDto>>>(jsonContent, new JsonSerializerOptions
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
            _logger.LogError(ex, "Exception in GetProjectsByOrganizationAsync for organization {OrganizationId}", organizationId);
        }

        return null;
    }

    public async Task<ProjectDto?> GetProjectAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/projects/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for project {ProjectId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for project {ProjectId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetProjectAsync for project {ProjectId}", id);
        }

        return null;
    }

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectDto projectDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/projects", projectDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for create project: {Message}", apiResponse?.Message);
                    throw new InvalidOperationException(apiResponse?.Message ?? "Unknown error occurred");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Project creation failed due to conflict - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(errorContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    throw new InvalidOperationException(errorResponse?.Message ?? "Project with this name already exists");
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("Project with this name already exists");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Validation failed for create project - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    response.StatusCode, errorContent);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(errorContent, new JsonSerializerOptions
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
                _logger.LogWarning("HTTP request failed for create project - StatusCode: {StatusCode}, Content: {ErrorContent}", 
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
            _logger.LogError(ex, "Exception in CreateProjectAsync");
            throw new Exception("An unexpected error occurred while creating the project");
        }
    }

    public async Task<ProjectDto?> UpdateProjectAsync(Guid id, UpdateProjectDto projectDto)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/projects/{id}", projectDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for update project {ProjectId}: {Message}", id, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for update project {ProjectId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateProjectAsync for project {ProjectId}", id);
        }

        return null;
    }

    public async Task<bool> DeleteProjectAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/projects/{id}");
            
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
                _logger.LogWarning("HTTP request failed for delete project {ProjectId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    id, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in DeleteProjectAsync for project {ProjectId}", id);
        }

        return false;
    }

    public async Task<PagedResult<ProjectMemberDto>?> GetProjectMembersAsync(Guid projectId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/projects/{projectId}/members?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<PagedResult<ProjectMemberDto>>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (apiResponse?.Success == true)
                {
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("API returned failure response for get project members {ProjectId}: {Message}", projectId, apiResponse?.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("HTTP request failed for get project members {ProjectId} - StatusCode: {StatusCode}, Content: {ErrorContent}", 
                    projectId, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GetProjectMembersAsync for project {ProjectId}", projectId);
        }

        return null;
    }

    public async Task<ProjectMemberDto?> AddMemberAsync(Guid projectId, AddProjectMemberDto addMemberDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/projects/{projectId}/members", addMemberDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectMemberDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return apiResponse?.Data;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in AddMemberAsync for project {ProjectId}", projectId);
        }

        return null;
    }

    public async Task<bool> RemoveMemberAsync(Guid projectId, int userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/projects/{projectId}/members/{userId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in RemoveMemberAsync for project {ProjectId}, user {UserId}", projectId, userId);
        }

        return false;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid projectId, int userId, string role)
    {
        try
        {
            var request = new { Role = role };
            var response = await _httpClient.PutAsJsonAsync($"/api/projects/{projectId}/members/{userId}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in UpdateMemberRoleAsync for project {ProjectId}, user {UserId}", projectId, userId);
        }

        return false;
    }
}