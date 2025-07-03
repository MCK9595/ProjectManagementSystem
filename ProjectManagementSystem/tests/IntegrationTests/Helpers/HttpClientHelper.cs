using System.Net;
using System.Text;
using Newtonsoft.Json;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IntegrationTests.Helpers;

/// <summary>
/// Helper class for HTTP client operations in integration tests
/// </summary>
public class HttpClientHelper
{
    private readonly HttpClient _httpClient;

    public HttpClientHelper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Send POST request and get API response
    /// </summary>
    public async Task<ApiResponse<T>?> PostAsync<T>(string endpoint, object? payload = null)
    {
        HttpResponseMessage response;
        
        if (payload != null)
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await _httpClient.PostAsync(endpoint, content);
        }
        else
        {
            response = await _httpClient.PostAsync(endpoint, null);
        }

        return await DeserializeResponseAsync<T>(response);
    }

    /// <summary>
    /// Send GET request and get API response
    /// </summary>
    public async Task<ApiResponse<T>?> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <summary>
    /// Send PUT request and get API response
    /// </summary>
    public async Task<ApiResponse<T>?> PutAsync<T>(string endpoint, object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(endpoint, content);
        
        return await DeserializeResponseAsync<T>(response);
    }

    /// <summary>
    /// Send DELETE request and get API response
    /// </summary>
    public async Task<ApiResponse<T>?> DeleteAsync<T>(string endpoint)
    {
        var response = await _httpClient.DeleteAsync(endpoint);
        return await DeserializeResponseAsync<T>(response);
    }

    /// <summary>
    /// Send raw HTTP request and get response without deserialization
    /// </summary>
    public async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string endpoint, object? payload = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        
        if (payload != null)
        {
            var json = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// Deserialize HTTP response to API response
    /// </summary>
    private async Task<ApiResponse<T>?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        
        if (string.IsNullOrEmpty(content))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<ApiResponse<T>>(content);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // If deserialization fails, create a manual API response
            return new ApiResponse<T>
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode ? "Success" : $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                Data = default,
                Errors = response.IsSuccessStatusCode ? null : new List<string> { content }
            };
        }
    }

    /// <summary>
    /// Get response content as string
    /// </summary>
    public async Task<string> GetStringAsync(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Check if endpoint returns expected status code
    /// </summary>
    public async Task<bool> CheckStatusCodeAsync(string endpoint, HttpStatusCode expectedStatusCode, HttpMethod? method = null)
    {
        HttpResponseMessage response;
        
        if (method == null || method == HttpMethod.Get)
        {
            response = await _httpClient.GetAsync(endpoint);
        }
        else
        {
            var request = new HttpRequestMessage(method, endpoint);
            response = await _httpClient.SendAsync(request);
        }

        return response.StatusCode == expectedStatusCode;
    }

    /// <summary>
    /// Test health check endpoint
    /// </summary>
    public async Task<bool> IsServiceHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health-check");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Test debug ping endpoint
    /// </summary>
    public async Task<bool> CanPingServiceAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/debug/ping");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Measure response time for an endpoint
    /// </summary>
    public async Task<(bool Success, TimeSpan ResponseTime)> MeasureResponseTimeAsync(string endpoint, HttpMethod? method = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            HttpResponseMessage response;
            
            if (method == null || method == HttpMethod.Get)
            {
                response = await _httpClient.GetAsync(endpoint);
            }
            else
            {
                var request = new HttpRequestMessage(method, endpoint);
                response = await _httpClient.SendAsync(request);
            }

            stopwatch.Stop();
            return (response.IsSuccessStatusCode, stopwatch.Elapsed);
        }
        catch
        {
            stopwatch.Stop();
            return (false, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Test concurrent requests to an endpoint
    /// </summary>
    public async Task<List<(bool Success, TimeSpan ResponseTime)>> TestConcurrentRequestsAsync(
        string endpoint, 
        int concurrentCount, 
        HttpMethod? method = null,
        object? payload = null)
    {
        var tasks = new List<Task<(bool Success, TimeSpan ResponseTime)>>();

        for (int i = 0; i < concurrentCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                try
                {
                    HttpResponseMessage response;
                    
                    if (method == null || method == HttpMethod.Get)
                    {
                        response = await _httpClient.GetAsync(endpoint);
                    }
                    else if (method == HttpMethod.Post && payload != null)
                    {
                        var json = JsonConvert.SerializeObject(payload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        response = await _httpClient.PostAsync(endpoint, content);
                    }
                    else
                    {
                        var request = new HttpRequestMessage(method, endpoint);
                        if (payload != null)
                        {
                            var json = JsonConvert.SerializeObject(payload);
                            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        }
                        response = await _httpClient.SendAsync(request);
                    }

                    stopwatch.Stop();
                    return (response.IsSuccessStatusCode, stopwatch.Elapsed);
                }
                catch
                {
                    stopwatch.Stop();
                    return (false, stopwatch.Elapsed);
                }
            }));
        }

        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>
    /// Validate API response structure
    /// </summary>
    public bool ValidateApiResponseStructure<T>(ApiResponse<T>? response, bool expectSuccess = true)
    {
        if (response == null) return false;
        
        // Check required properties
        if (response.Success != expectSuccess) return false;
        if (string.IsNullOrEmpty(response.Message)) return false;
        
        // For successful responses, data should not be null (unless T is nullable)
        if (expectSuccess && response.Data == null && !IsNullableType(typeof(T))) return false;
        
        // For failed responses, errors list might be present
        if (!expectSuccess && response.Errors == null) return false;

        return true;
    }

    private bool IsNullableType(Type type)
    {
        return !type.IsValueType || (Nullable.GetUnderlyingType(type) != null);
    }
}