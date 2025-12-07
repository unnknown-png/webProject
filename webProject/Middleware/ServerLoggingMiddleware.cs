namespace webProject.Middleware;

public class ServerLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServerLoggingMiddleware> _logger;
    private readonly string _serverName;

    public ServerLoggingMiddleware(RequestDelegate next, ILogger<ServerLoggingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _serverName = configuration["ServerInfo:ServerName"] ?? "UNKNOWN-SERVER";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // Log only important requests (API calls, not static files)
        if (path.StartsWith("/api/") || path.StartsWith("/progressHub"))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{_serverName}] {method} {path}");
            Console.ResetColor();
        }

        await _next(context);
    }
}

public static class ServerLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseServerLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ServerLoggingMiddleware>();
    }
}

