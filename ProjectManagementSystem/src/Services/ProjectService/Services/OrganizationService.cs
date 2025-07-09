using System.Net.Http;
using System.Text.Json;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.ProjectService.Services;

public class OrganizationService : IOrganizationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrganizationService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrganizationService(HttpClient httpClient, ILogger<OrganizationService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid organizationId)
    {
        try
        {
            _logger.LogInformation("Calling OrganizationService to get organization {OrganizationId}", organizationId);
            
            // Get authorization header from current request and forward it
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(authHeader);
                _logger.LogDebug("Authorization header set for OrganizationService call");
            }
            else
            {
                _logger.LogWarning("No authorization header found in current request");
            }
            
            var response = await _httpClient.GetAsync($"/api/organizations/{organizationId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<OrganizationDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    _logger.LogInformation("Successfully retrieved organization {OrganizationId}", organizationId);
                    return apiResponse.Data;
                }
                else
                {
                    _logger.LogWarning("Organization API returned unsuccessful response for ID {OrganizationId}: {Message}", 
                        organizationId, apiResponse?.Message ?? "Unknown error");
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("Failed to retrieve organization {OrganizationId}. Status: {StatusCode}", 
                    organizationId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization {OrganizationId}", organizationId);
            return null;
        }
    }
}