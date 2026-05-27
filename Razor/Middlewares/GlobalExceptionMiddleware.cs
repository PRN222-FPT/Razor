using System.Diagnostics;
using System.Text.Json;

namespace Razor.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            _logger.LogError(
                exception,
                "Unhandled exception | TraceId={TraceId} | {Method} {Path}",
                traceId,
                context.Request.Method,
                context.Request.Path);

            if (_environment.IsDevelopment())
            {
                throw;
            }

            await HandleExceptionAsync(context, exception, traceId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started; cannot write error response. TraceId={TraceId}",
                traceId);
            throw exception;
        }

        var statusCode = MapToStatusCode(exception);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        if (IsJsonRequest(context))
        {
            context.Response.ContentType = "application/json";

            var payload = new ErrorResponse
            {
                StatusCode = statusCode,
                Message = GetUserFriendlyMessage(statusCode),
                TraceId = traceId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        context.Response.Redirect($"/Error?statusCode={statusCode}");
    }

    private static int MapToStatusCode(Exception exception) => exception switch
    {
        KeyNotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status409Conflict,
        NotImplementedException => StatusCodes.Status501NotImplemented,
        OperationCanceledException => 499,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetUserFriendlyMessage(int statusCode) => statusCode switch
    {
        400 => "The request was invalid.",
        403 => "You do not have permission to access this resource.",
        404 => "The requested resource was not found.",
        409 => "The request could not be completed due to a conflict.",
        499 => "The request was cancelled.",
        501 => "This feature is not implemented yet.",
        _ => "An unexpected error occurred. Please try again later."
    };

    private static bool IsJsonRequest(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ErrorResponse
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
