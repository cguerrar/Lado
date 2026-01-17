using Lado.Services;
using Microsoft.AspNetCore.Identity;
using Lado.Models;
using System.Text.Json;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware que bloquea el acceso al sitio durante el modo mantenimiento
    /// Excepto para administradores y rutas permitidas
    /// </summary>
    public class MantenimientoMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MantenimientoMiddleware> _logger;

        public MantenimientoMiddleware(RequestDelegate next, ILogger<MantenimientoMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IMantenimientoService mantenimientoService, UserManager<ApplicationUser> userManager)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // Siempre permitir recursos estáticos
            if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
                path.StartsWith("/images") || path.StartsWith("/uploads") || path.StartsWith("/_framework") ||
                path.StartsWith("/favicon") || path.EndsWith(".css") || path.EndsWith(".js") ||
                path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".ico") ||
                path.EndsWith(".woff") || path.EndsWith(".woff2"))
            {
                await _next(context);
                return;
            }

            // Verificar si está en mantenimiento
            var enMantenimiento = await mantenimientoService.EstaEnMantenimientoAsync();
            if (!enMantenimiento)
            {
                await _next(context);
                return;
            }

            // Verificar rutas permitidas
            if (await mantenimientoService.EsRutaPermitidaAsync(path))
            {
                await _next(context);
                return;
            }

            // Si el usuario está autenticado, verificar si es admin
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user != null)
                {
                    var isAdmin = await userManager.IsInRoleAsync(user, "Administrador");
                    if (isAdmin)
                    {
                        await _next(context);
                        return;
                    }

                    // Verificar si se permite creadores verificados
                    var config = await mantenimientoService.ObtenerConfiguracionAsync();
                    if (config.PermitirCreadoresVerificados && user.CreadorVerificado)
                    {
                        await _next(context);
                        return;
                    }
                }
            }

            // Obtener configuración para mostrar página de mantenimiento
            var mantenimientoConfig = await mantenimientoService.ObtenerConfiguracionAsync();

            // Si es una petición AJAX/API, devolver JSON
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                path.StartsWith("/api/") ||
                context.Request.ContentType?.Contains("application/json") == true)
            {
                context.Response.StatusCode = 503;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "mantenimiento",
                    titulo = mantenimientoConfig.Titulo,
                    mensaje = mantenimientoConfig.Mensaje,
                    finEstimado = mantenimientoConfig.FechaFinEstimado?.ToString("o")
                });
                return;
            }

            // Mostrar página de mantenimiento HTML
            context.Response.StatusCode = 503;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.Append("Retry-After", "300"); // Reintentar en 5 minutos

            var html = GenerarPaginaMantenimiento(mantenimientoConfig);
            await context.Response.WriteAsync(html);
        }

        private string GenerarPaginaMantenimiento(ModoMantenimiento config)
        {
            var finEstimadoJs = config.FechaFinEstimado.HasValue
                ? $"new Date('{config.FechaFinEstimado.Value:yyyy-MM-ddTHH:mm:ss}')"
                : "null";

            var cuentaRegresivaHtml = config.MostrarCuentaRegresiva && config.FechaFinEstimado.HasValue
                ? @"
                <div class='countdown' id='countdown'>
                    <div class='countdown-item'>
                        <span class='countdown-value' id='hours'>00</span>
                        <span class='countdown-label'>Horas</span>
                    </div>
                    <div class='countdown-item'>
                        <span class='countdown-value' id='minutes'>00</span>
                        <span class='countdown-label'>Minutos</span>
                    </div>
                    <div class='countdown-item'>
                        <span class='countdown-value' id='seconds'>00</span>
                        <span class='countdown-label'>Segundos</span>
                    </div>
                </div>"
                : "";

            var countdownScript = config.MostrarCuentaRegresiva && config.FechaFinEstimado.HasValue
                ? $@"
                <script>
                    const finEstimado = {finEstimadoJs};
                    function updateCountdown() {{
                        if (!finEstimado) return;
                        const now = new Date();
                        const diff = finEstimado - now;
                        if (diff <= 0) {{
                            document.getElementById('countdown').innerHTML = '<p style=""color: #00d4aa;"">¡El mantenimiento debería terminar pronto!</p>';
                            setTimeout(() => location.reload(), 30000);
                            return;
                        }}
                        const hours = Math.floor(diff / (1000 * 60 * 60));
                        const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
                        const seconds = Math.floor((diff % (1000 * 60)) / 1000);
                        document.getElementById('hours').textContent = String(hours).padStart(2, '0');
                        document.getElementById('minutes').textContent = String(minutes).padStart(2, '0');
                        document.getElementById('seconds').textContent = String(seconds).padStart(2, '0');
                    }}
                    updateCountdown();
                    setInterval(updateCountdown, 1000);
                    // Auto-refresh cada 60 segundos para verificar si terminó
                    setTimeout(() => location.reload(), 60000);
                </script>"
                : "";

            return $@"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{System.Net.WebUtility.HtmlEncode(config.Titulo)} - Lado</title>
    <link rel=""icon"" type=""image/png"" href=""/images/favicon.png"">
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            color: #fff;
            padding: 20px;
        }}
        .container {{
            text-align: center;
            max-width: 600px;
        }}
        .logo {{
            width: 120px;
            height: 120px;
            margin-bottom: 30px;
            animation: pulse 2s ease-in-out infinite;
        }}
        @keyframes pulse {{
            0%, 100% {{ transform: scale(1); opacity: 1; }}
            50% {{ transform: scale(1.05); opacity: 0.8; }}
        }}
        .icon {{
            font-size: 80px;
            margin-bottom: 20px;
        }}
        h1 {{
            font-size: 2.5rem;
            margin-bottom: 20px;
            background: linear-gradient(90deg, #00d4aa, #00b4d8);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }}
        .message {{
            font-size: 1.2rem;
            color: #b0b0b0;
            line-height: 1.6;
            margin-bottom: 40px;
        }}
        .countdown {{
            display: flex;
            justify-content: center;
            gap: 20px;
            margin-bottom: 40px;
        }}
        .countdown-item {{
            background: rgba(255, 255, 255, 0.1);
            border-radius: 15px;
            padding: 20px 25px;
            min-width: 90px;
            backdrop-filter: blur(10px);
        }}
        .countdown-value {{
            display: block;
            font-size: 2.5rem;
            font-weight: 700;
            color: #00d4aa;
        }}
        .countdown-label {{
            font-size: 0.85rem;
            color: #888;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        .progress-bar {{
            width: 100%;
            height: 4px;
            background: rgba(255, 255, 255, 0.1);
            border-radius: 2px;
            overflow: hidden;
            margin-bottom: 20px;
        }}
        .progress-fill {{
            height: 100%;
            background: linear-gradient(90deg, #00d4aa, #00b4d8);
            animation: progress 2s ease-in-out infinite;
        }}
        @keyframes progress {{
            0% {{ width: 0%; margin-left: 0; }}
            50% {{ width: 70%; margin-left: 15%; }}
            100% {{ width: 0%; margin-left: 100%; }}
        }}
        .social-links {{
            margin-top: 30px;
        }}
        .social-links a {{
            color: #00d4aa;
            text-decoration: none;
            margin: 0 15px;
            font-size: 1.1rem;
            transition: color 0.3s;
        }}
        .social-links a:hover {{
            color: #00b4d8;
        }}
        @media (max-width: 480px) {{
            h1 {{ font-size: 1.8rem; }}
            .message {{ font-size: 1rem; }}
            .countdown-item {{ padding: 15px 18px; min-width: 70px; }}
            .countdown-value {{ font-size: 1.8rem; }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <img src=""/images/logo.png"" alt=""Lado"" class=""logo"" onerror=""this.style.display='none'"">
        <div class=""icon"">🔧</div>
        <h1>{System.Net.WebUtility.HtmlEncode(config.Titulo)}</h1>
        <div class=""progress-bar"">
            <div class=""progress-fill""></div>
        </div>
        <p class=""message"">{System.Net.WebUtility.HtmlEncode(config.Mensaje)}</p>
        {cuentaRegresivaHtml}
        <div class=""social-links"">
            <a href=""https://twitter.com/ladoapp"" target=""_blank"">Twitter</a>
            <a href=""https://instagram.com/ladoapp"" target=""_blank"">Instagram</a>
        </div>
    </div>
    {countdownScript}
</body>
</html>";
        }
    }

    public static class MantenimientoMiddlewareExtensions
    {
        public static IApplicationBuilder UseMantenimiento(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MantenimientoMiddleware>();
        }
    }
}
