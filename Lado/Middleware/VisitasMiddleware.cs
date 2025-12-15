using Lado.Services;

namespace Lado.Middleware
{
    public class VisitasMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<VisitasMiddleware> _logger;

        // Extensiones de archivos estaticos a ignorar
        private static readonly HashSet<string> _extensionesIgnorar = new(StringComparer.OrdinalIgnoreCase)
        {
            ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot",
            ".map", ".json", ".xml", ".txt", ".pdf", ".mp3", ".mp4", ".webm", ".ogg", ".wav"
        };

        // Rutas a ignorar
        private static readonly HashSet<string> _rutasIgnorar = new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/", "/lib/", "/css/", "/js/", "/images/", "/uploads/", "/fonts/", "/_framework/", "/swagger"
        };

        public VisitasMiddleware(RequestDelegate next, ILogger<VisitasMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IVisitasService visitasService)
        {
            // Primero continuar con la solicitud para no bloquear
            await _next(context);

            // Obtener la ruta de la solicitud
            var path = context.Request.Path.Value ?? "";

            // Verificar si debemos contar esta visita (solo respuestas exitosas)
            if (DebeContarVisita(path, context.Request.Method) && context.Response.StatusCode < 400)
            {
                try
                {
                    // Obtener IP del cliente
                    var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                    // Si hay un proxy, intentar obtener la IP real
                    if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
                    {
                        ipAddress = context.Request.Headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim();
                    }

                    var userAgent = context.Request.Headers["User-Agent"].ToString();
                    var usuarioId = context.User?.Identity?.IsAuthenticated == true
                        ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                        : null;

                    // Registrar la visita de forma sincrona (el servicio ya es scoped)
                    await visitasService.RegistrarVisitaAsync(ipAddress, userAgent, path, usuarioId);
                }
                catch (Exception ex)
                {
                    // Solo loguear, no interrumpir la respuesta
                    _logger.LogError(ex, "Error al registrar visita para {Path}", path);
                }
            }
        }

        private bool DebeContarVisita(string path, string method)
        {
            // Solo contar solicitudes GET
            if (!method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                return false;

            // Ignorar rutas vacias
            if (string.IsNullOrEmpty(path))
                return false;

            // Ignorar archivos estaticos por extension
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension) && _extensionesIgnorar.Contains(extension))
                return false;

            // Ignorar rutas especificas
            foreach (var rutaIgnorar in _rutasIgnorar)
            {
                if (path.StartsWith(rutaIgnorar, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    public static class VisitasMiddlewareExtensions
    {
        public static IApplicationBuilder UseVisitasMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<VisitasMiddleware>();
        }
    }
}
