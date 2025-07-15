using System;

namespace ProjectManagementSystem.WebApp.Wasm.Client.Components.Authentication;

/// <summary>
/// Specifies that the page or component requires authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the policy name that determines access to the resource.
    /// </summary>
    public string? Policy { get; set; }

    /// <summary>
    /// Gets or sets a comma delimited list of roles that are allowed to access the resource.
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets a comma delimited list of schemes from which user information is constructed.
    /// </summary>
    public string? AuthenticationSchemes { get; set; }

    public AuthorizeAttribute()
    {
    }

    public AuthorizeAttribute(string policy)
    {
        Policy = policy;
    }
}