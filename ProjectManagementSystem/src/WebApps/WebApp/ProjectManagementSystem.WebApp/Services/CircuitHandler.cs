using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectManagementSystem.Shared.Common.Services;

namespace ProjectManagementSystem.WebApp.Services;

public class AuthenticationCircuitHandler : CircuitHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthenticationCircuitHandler> _logger;

    public AuthenticationCircuitHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<AuthenticationCircuitHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} opened", circuit.Id);
        await base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} closed", circuit.Id);
        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} connection up - attempting authentication restore", circuit.Id);
        
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionTokenService = scope.ServiceProvider.GetRequiredService<ISessionTokenService>();
            var authStateProvider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
            
            // Check if we have a token available
            var token = await sessionTokenService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Token found during circuit reconnection, refreshing authentication state");
                
                // If we have a custom authentication state provider, notify it of the reconnection
                if (authStateProvider is ServerAuthenticationStateProvider serverAuthProvider)
                {
                    await serverAuthProvider.NotifyUserAuthenticationAsync();
                    _logger.LogInformation("Authentication state refreshed for circuit {CircuitId}", circuit.Id);
                }
            }
            else
            {
                _logger.LogInformation("No token found during circuit reconnection for circuit {CircuitId}", circuit.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring authentication state for circuit {CircuitId}", circuit.Id);
        }
        
        await base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit {CircuitId} connection down", circuit.Id);
        await base.OnConnectionDownAsync(circuit, cancellationToken);
    }
}