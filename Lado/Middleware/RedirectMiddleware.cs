using Lado.Services;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware que maneja redirecciones 301/302 configuradas desde Admin.
    /// Se ejecuta al inicio del pipeline para redirigir URLs antiguas.
    /// </summary>
    public class RedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RedirectMiddleware> _logger;

        public RedirectMiddleware(RequestDelegate next, ILogger<RedirectMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISeoConfigService seoConfigService)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Ignorar archivos estáticos y rutas especiales
            if (string.IsNullOrEmpty(path) ||
                path.StartsWith("/api/") ||
                path.StartsWith("/_") ||
                path.StartsWith("/lib/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/images/") ||
                path.StartsWith("/Content/") ||
                path.Contains('.'))
            {
                await _next(context);
                return;
            }

            // Buscar redirección para esta URL
            var redireccion = await seoConfigService.BuscarRedireccionAsync(path);

            if (redireccion != null)
            {
                // Construir URL de destino
                var destinoUrl = redireccion.UrlDestino;

                // Preservar query string si está configurado
                if (redireccion.PreservarQueryString && context.Request.QueryString.HasValue)
                {
                    if (destinoUrl.Contains('?'))
                    {
                        destinoUrl += "&" + context.Request.QueryString.Value?.TrimStart('?');
                    }
                    else
                    {
                        destinoUrl += context.Request.QueryString.Value;
                    }
                }

                _logger.LogInformation("↪️ Redirección {Tipo}: {Origen} → {Destino}",
                    (int)redireccion.Tipo, path, destinoUrl);

                // Incrementar contador de uso (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await seoConfigService.IncrementarUsoRedireccionAsync(redireccion.Id);
                    }
                    catch { }
                });

                // Establecer código de respuesta según tipo
                var statusCode = redireccion.Tipo == Models.TipoRedireccion.Permanente301
                    ? StatusCodes.Status301MovedPermanently
                    : StatusCodes.Status302Found;

                context.Response.StatusCode = statusCode;
                context.Response.Headers.Location = destinoUrl;

                // Headers adicionales para SEO
                if (redireccion.Tipo == Models.TipoRedireccion.Permanente301)
                {
                    context.Response.Headers.CacheControl = "public, max-age=31536000"; // 1 año
                }

                return;
            }

            await _next(context);
        }
    }

    public static class RedirectMiddlewareExtensions
    {
        public static IApplicationBuilder UseRedirects(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RedirectMiddleware>();
        }
    }
}
