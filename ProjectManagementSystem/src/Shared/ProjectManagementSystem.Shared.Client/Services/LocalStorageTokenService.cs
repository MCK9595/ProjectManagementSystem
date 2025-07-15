using Microsoft.JSInterop;

namespace ProjectManagementSystem.Shared.Client.Services;

public class LocalStorageTokenService : ISessionTokenService
{
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "projectmanagement_token";
    private string? _pendingToken;

    public LocalStorageTokenService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            // If we have a pending token (not yet persisted), return it
            if (!string.IsNullOrEmpty(_pendingToken))
            {
                return _pendingToken;
            }

            // Try to get token from localStorage
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        }
        catch (Exception)
        {
            // If localStorage is not available (e.g., during SSR), return null
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            // Store immediately in localStorage if available
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
            _pendingToken = null; // Clear pending token since it's now persisted
        }
        catch (Exception)
        {
            // If localStorage is not available (e.g., during SSR), store as pending
            _pendingToken = token;
        }
    }

    public async Task RemoveTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            _pendingToken = null; // Clear any pending token
        }
        catch (Exception)
        {
            // If localStorage is not available, just clear the pending token
            _pendingToken = null;
        }
    }

    public async Task FlushPendingTokenAsync()
    {
        if (!string.IsNullOrEmpty(_pendingToken))
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, _pendingToken);
                _pendingToken = null;
            }
            catch (Exception)
            {
                // If still can't access localStorage, keep the pending token
            }
        }
    }
}