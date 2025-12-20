using Lado.Models;
using Lado.Services;
using System.Security.Claims;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware para capturar y registrar excepciones no manejadas
    /// </summary>
    public class ExceptionLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionLoggingMiddleware> _logger;

        public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ILogEventoService logEventoService)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepcion no manejada capturada por middleware");

                // Obtener info del usuario si está autenticado
                string? usuarioId = null;
                string? usuarioNombre = null;

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    usuarioNombre = context.User.Identity?.Name;
                }

                // Registrar en la base de datos
                try
                {
                    await logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Sistema, usuarioId, usuarioNombre);
                }
                catch (Exception logEx)
                {
                    // Si falla el logging a BD, al menos lo registramos en el log de archivos
                    _logger.LogError(logEx, "Error al registrar excepcion en base de datos");
                }

                // Re-lanzar la excepción para que el handler de errores normal la maneje
                throw;
            }
        }
    }

    /// <summary>
    /// Extension method para registrar el middleware
    /// </summary>
    public static class ExceptionLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionLoggingMiddleware>();
        }
    }
}
