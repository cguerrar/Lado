using Lado.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware que actualiza la última actividad del usuario autenticado.
    /// Esto permite mostrar el estado "en línea" en la plataforma.
    /// </summary>
    public class UserActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserActivityMiddleware> _logger;

        // Extensiones de archivos estáticos a ignorar
        private static readonly HashSet<string> _extensionesIgnorar = new(StringComparer.OrdinalIgnoreCase)
        {
            ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot",
            ".map", ".json", ".xml", ".txt", ".pdf", ".mp3", ".mp4", ".webm", ".ogg", ".wav", ".webp"
        };

        // Rutas a ignorar
        private static readonly HashSet<string> _rutasIgnorar = new(StringComparer.OrdinalIgnoreCase)
        {
            "/lib/", "/css/", "/js/", "/images/", "/uploads/", "/fonts/", "/_framework/", "/swagger", "/favicon"
        };

        // Intervalo mínimo entre actualizaciones (para no saturar la BD)
        private static readonly TimeSpan _intervaloMinimo = TimeSpan.FromSeconds(30);

        // Cache en memoria para evitar actualizaciones muy frecuentes
        private static readonly Dictionary<string, DateTime> _ultimaActualizacion = new();
        private static readonly object _lock = new();

        public UserActivityMiddleware(RequestDelegate next, ILogger<UserActivityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            // Continuar con la solicitud primero
            await _next(context);

            // Solo procesar si el usuario está autenticado y la respuesta fue exitosa
            if (!context.User?.Identity?.IsAuthenticated == true || context.Response.StatusCode >= 400)
            {
                return;
            }

            var path = context.Request.Path.Value ?? "";

            // Ignorar rutas de archivos estáticos y APIs de polling
            if (!DebeActualizarActividad(path))
            {
                return;
            }

            try
            {
                var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return;
                }

                // Verificar si ya actualizamos recientemente (cache en memoria)
                lock (_lock)
                {
                    if (_ultimaActualizacion.TryGetValue(userId, out var ultimaVez))
                    {
                        if (DateTime.UtcNow - ultimaVez < _intervaloMinimo)
                        {
                            return; // Muy pronto para actualizar
                        }
                    }
                    _ultimaActualizacion[userId] = DateTime.UtcNow;
                }

                // Actualizar en la base de datos de forma eficiente (sin cargar toda la entidad)
                await dbContext.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimaActividad, DateTime.UtcNow));

                // Limpiar cache antiguo periódicamente
                LimpiarCacheAntiguo();
            }
            catch (Exception ex)
            {
                // No interrumpir la solicitud por errores de tracking
                _logger.LogWarning(ex, "Error actualizando actividad del usuario");
            }
        }

        private bool DebeActualizarActividad(string path)
        {
            // Ignorar archivos estáticos
            var extension = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(extension) && _extensionesIgnorar.Contains(extension))
            {
                return false;
            }

            // Ignorar rutas específicas
            foreach (var ruta in _rutasIgnorar)
            {
                if (path.StartsWith(ruta, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private void LimpiarCacheAntiguo()
        {
            // Limpiar entradas antiguas cada cierto tiempo
            if (DateTime.UtcNow.Minute % 5 != 0) return; // Solo cada 5 minutos

            lock (_lock)
            {
                var antiguos = _ultimaActualizacion
                    .Where(kv => DateTime.UtcNow - kv.Value > TimeSpan.FromMinutes(10))
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in antiguos)
                {
                    _ultimaActualizacion.Remove(key);
                }
            }
        }
    }

    // Extensión para registrar el middleware
    public static class UserActivityMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserActivity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserActivityMiddleware>();
        }
    }
}
