using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectManagementSystem.WebApp.Services;

public class OrganizationService : IOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;

    public OrganizationService(IHttpClientFactory httpClientFactory, IAuthService authService)
    {
        _httpClient = httpClientFactory.CreateClient("ApiGateway");
        _authService = authService;
    }

    public async Task<PagedResult<OrganizationDto>?> GetOrganizationsAsync(int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/organizations?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<OrganizationDto>>(jsonContent, new JsonSerializerOptions
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

    public async Task<OrganizationDto?> GetOrganizationAsync(int id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/organizations/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OrganizationDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<OrganizationDto?> CreateOrganizationAsync(CreateOrganizationDto organizationDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("/api/organizations", organizationDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OrganizationDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<OrganizationDto?> UpdateOrganizationAsync(int id, UpdateOrganizationDto organizationDto)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/organizations/{id}", organizationDto);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OrganizationDto>(jsonContent, new JsonSerializerOptions
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

    public async Task<bool> DeleteOrganizationAsync(int id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/organizations/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Log exception
        }

        return false;
    }

    public async Task<PagedResult<OrganizationMemberDto>?> GetOrganizationMembersAsync(int organizationId, int pageNumber = 1, int pageSize = 10)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/organizations/{organizationId}/members?pageNumber={pageNumber}&pageSize={pageSize}");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResult<OrganizationMemberDto>>(jsonContent, new JsonSerializerOptions
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