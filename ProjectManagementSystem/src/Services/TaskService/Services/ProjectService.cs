using System.Net.Http;
using System.Text.Json;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.TaskService.Services;

public class ProjectService : IProjectService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProjectService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProjectService(HttpClient httpClient, ILogger<ProjectService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ProjectDto?> GetProjectAsync(Guid projectId)
    {
        try
        {
            _logger.LogInformation("Calling ProjectService to get project {ProjectId}", projectId);
            
            // Get authorization header from current request and forward it
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authHeader);
                _logger.LogDebug("Authorization header set for ProjectService call");
            }
            else
            {
                _logger.LogWarning("No authorization header found in current request");
            }
            
            var response = await _httpClient.GetAsync($"/api/projects/{projectId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<ProjectDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("Successfully retrieved project {ProjectId}", projectId);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Project API returned unsuccessful response for ID {ProjectId}: {Message}", 
                        projectId, apiResponse?.Message ?? "Unknown error");
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("Failed to retrieve project {ProjectId}. Status: {StatusCode}", 
                    projectId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving project {ProjectId}", projectId);
            return null;
        }
    }
}