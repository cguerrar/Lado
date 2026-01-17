using System.Security.Claims;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware temporal para debugear problemas de autenticación.
    /// Loguea el usuario autenticado en cada request para rastrear mezcla de sesiones.
    /// IMPORTANTE: Desactivar en producción cuando no se necesite debug.
    /// </summary>
    public class AuthDebugMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthDebugMiddleware> _logger;

        // Rutas a ignorar en logging (archivos estáticos)
        private static readonly HashSet<string> _rutasIgnorar = new(StringComparer.OrdinalIgnoreCase)
        {
            "/lib/", "/css/", "/js/", "/images/", "/uploads/", "/fonts/", "/_framework/", "/favicon"
        };

        public AuthDebugMiddleware(RequestDelegate next, ILogger<AuthDebugMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // Ignorar archivos estáticos
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension))
            {
                await _next(context);
                return;
            }

            foreach (var ruta in _rutasIgnorar)
            {
                if (path.StartsWith(ruta, StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }
            }

            // ========================================
            // DEBUG: Información de autenticación
            // ========================================
            var isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = context.User?.Identity?.Name;
            var authType = context.User?.Identity?.AuthenticationType;

            // Obtener cookies de autenticación para debug
            var cookies = context.Request.Cookies;
            // ⚠️ IMPORTANTE: La cookie de auth se llama ".Lado.Auth" (configurada en Program.cs)
            var authCookie = cookies.TryGetValue(".Lado.Auth", out var authValue)
                ? (authValue?.Length > 50 ? authValue.Substring(0, 50) + "..." : authValue)
                : "NO COOKIE";

            // También verificar si hay cookies duplicadas/conflictivas
            var tieneOtraCookie = cookies.ContainsKey(".AspNetCore.Identity.Application");
            if (tieneOtraCookie)
            {
                _logger.LogWarning("⚠️ COOKIE CONFLICTIVA: Existe .AspNetCore.Identity.Application además de .Lado.Auth");
            }

            _logger.LogWarning(
                "🔐 AUTH DEBUG [{Method}] {Path}\n" +
                "   IsAuth: {IsAuth} | UserId: {UserId} | UserName: {UserName}\n" +
                "   AuthType: {AuthType} | Cookie: {Cookie}",
                context.Request.Method,
                path,
                isAuthenticated,
                userId ?? "NULL",
                userName ?? "NULL",
                authType ?? "NULL",
                authCookie);

            // Si es un request de Login POST, loguear info adicional
            if (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase) &&
                context.Request.Method == "POST")
            {
                _logger.LogWarning("🔐 LOGIN POST - Usuario ANTES de login: {UserId} | {UserName}",
                    userId ?? "NULL", userName ?? "NULL");
            }

            await _next(context);

            // Después del request, verificar si cambió la autenticación
            var newUserId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != newUserId)
            {
                _logger.LogWarning("🔐 AUTH CAMBIÓ durante request: {OldUserId} → {NewUserId}",
                    userId ?? "NULL", newUserId ?? "NULL");
            }

            // Si se estableció una cookie de autenticación nueva
            if (context.Response.Headers.TryGetValue("Set-Cookie", out var setCookies))
            {
                foreach (var cookie in setCookies)
                {
                    if (cookie?.Contains("Identity.Application") == true)
                    {
                        _logger.LogWarning("🍪 NUEVA COOKIE DE AUTH establecida en response para {Path}", path);
                    }
                }
            }
        }
    }

    public static class AuthDebugMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthDebug(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthDebugMiddleware>();
        }
    }
}
