using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectManagementSystem.Shared.Common.Exceptions;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net;
using System.Text.Json;

namespace ProjectManagementSystem.Shared.Common.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ValidationException validationEx => new
            {
                StatusCode = HttpStatusCode.BadRequest,
                Response = ApiResponse.ErrorResult(validationEx.Message, validationEx.Errors)
            },
            NotFoundException notFoundEx => new
            {
                StatusCode = HttpStatusCode.NotFound,
                Response = ApiResponse.ErrorResult(notFoundEx.Message)
            },
            UnauthorizedException unauthorizedEx => new
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Response = ApiResponse.ErrorResult(unauthorizedEx.Message)
            },
            ForbiddenException forbiddenEx => new
            {
                StatusCode = HttpStatusCode.Forbidden,
                Response = ApiResponse.ErrorResult(forbiddenEx.Message)
            },
            ConflictException conflictEx => new
            {
                StatusCode = HttpStatusCode.Conflict,
                Response = ApiResponse.ErrorResult(conflictEx.Message)
            },
            AppException appEx => new
            {
                StatusCode = HttpStatusCode.BadRequest,
                Response = ApiResponse.ErrorResult(appEx.Message)
            },
            _ => new
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Response = ApiResponse.ErrorResult("An error occurred while processing your request.")
            }
        };

        context.Response.StatusCode = (int)response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response.Response, _jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }
}