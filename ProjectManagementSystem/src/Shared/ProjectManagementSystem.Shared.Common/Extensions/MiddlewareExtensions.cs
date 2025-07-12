using Microsoft.AspNetCore.Builder;
using ProjectManagementSystem.Shared.Common.Middleware;

namespace ProjectManagementSystem.Shared.Common.Extensions;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Adds global exception handling middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}