using Lado.Services;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware que bloquea IPs en la lista negra
    /// Se ejecuta al inicio del pipeline para rechazar requests de IPs bloqueadas
    /// </summary>
    public class IpBlockingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IpBlockingMiddleware> _logger;

        public IpBlockingMiddleware(RequestDelegate next, ILogger<IpBlockingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimitService)
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Permitir localhost siempre
            if (clientIp == "::1" || clientIp == "127.0.0.1" || clientIp == "localhost")
            {
                await _next(context);
                return;
            }

            // Verificar si la IP está bloqueada
            if (await rateLimitService.IsIpBlockedAsync(clientIp))
            {
                _logger.LogWarning("🚫 IP BLOQUEADA rechazada: {Ip} - Path: {Path}",
                    clientIp, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Acceso denegado",
                    code = "IP_BLOCKED"
                });

                return;
            }

            await _next(context);
        }
    }

    public static class IpBlockingMiddlewareExtensions
    {
        public static IApplicationBuilder UseIpBlocking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<IpBlockingMiddleware>();
        }
    }
}
