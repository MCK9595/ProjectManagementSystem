using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.WebApp.Services;

public class ProjectService : IProjectService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;

    public ProjectService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _authService = authService;
    }

    public async Task<PagedResult<ProjectDto>?> GetProjectsAsync(int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/projects?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<ProjectDto>>(jsonContent, new JsonSerializerOptions
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

    public async Task<PagedResult<ProjectDto>?> GetProjectsByOrganizationAsync(Guid organizationId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/projects/organization/{organizationId}?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<ProjectDto>>(jsonContent, new JsonSerializerOptions
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

    public async Task<ProjectDto?> GetProjectAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/projects/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProjectDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectDto projectDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("/api/projects", projectDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProjectDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<ProjectDto?> UpdateProjectAsync(Guid id, UpdateProjectDto projectDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/projects/{id}", projectDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProjectDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<bool> DeleteProjectAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/projects/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<PagedResult<ProjectMemberDto>?> GetProjectMembersAsync(Guid projectId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/projects/{projectId}/members?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<ProjectMemberDto>>(jsonContent, new JsonSerializerOptions
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

    public async Task<ProjectMemberDto?> AddMemberAsync(Guid projectId, AddProjectMemberDto addMemberDto)
    {
        try
        {
            await SetAuthHeaderAsync();
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
        catch (Exception)
        {
            // Log exception
        }

        return null;
    }

    public async Task<bool> RemoveMemberAsync(Guid projectId, int userId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/projects/{projectId}/members/{userId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<bool> UpdateMemberRoleAsync(Guid projectId, int userId, string role)
    {
        try
        {
            await SetAuthHeaderAsync();
            var request = new { Role = role };
            var response = await _httpClient.PutAsJsonAsync($"/api/projects/{projectId}/members/{userId}/role", request);
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