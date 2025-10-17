using Microsoft.AspNetCore.Identity;
using Lado.Models;

namespace Lado.Middleware
{
    public class AgeVerificationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AgeVerificationMiddleware> _logger;

        public AgeVerificationMiddleware(RequestDelegate next, ILogger<AgeVerificationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // ✅ LOG DE DEBUG
            _logger.LogInformation("========================================");
            _logger.LogInformation("🔍 MIDDLEWARE VERIFICACIÓN EDAD EJECUTÁNDOSE");
            _logger.LogInformation("📍 Path: {Path}", path);
            _logger.LogInformation("🔐 Usuario autenticado: {IsAuth}", context.User.Identity?.IsAuthenticated);

            // Lista de rutas públicas que NO requieren verificación de edad
            var rutasPublicas = new[]
            {
                "/home",
                "/account/login",
                "/account/register",
                "/account/logout",
                "/account/forgotpassword",
                "/account/resetpassword",
                "/ageverification",
                "/css", "/js", "/lib", "/images", "/uploads"
            };

            // ✅ CORRECCIÓN: Verificar si es exactamente "/" o si empieza con alguna ruta pública
            bool esRutaPublica = path == "/" || rutasPublicas.Any(r => path.StartsWith(r));

            _logger.LogInformation("📂 Es ruta pública: {EsPublica}", esRutaPublica);

            // Si el usuario está autenticado y NO está en una ruta pública
            if (context.User.Identity?.IsAuthenticated == true && !esRutaPublica)
            {
                _logger.LogInformation("✅ Usuario autenticado y NO en ruta pública - Verificando edad...");

                var userManager = context.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();

                var user = await userManager.GetUserAsync(context.User);

                if (user != null)
                {
                    _logger.LogInformation("👤 Usuario: {Email}", user.Email);
                    _logger.LogInformation("🎂 AgeVerified: {AgeVerified}", user.AgeVerified);

                    // Si el usuario existe y NO ha verificado su edad
                    if (!user.AgeVerified)
                    {
                        _logger.LogWarning("⚠️ USUARIO SIN VERIFICAR - REDIRIGIENDO A /AgeVerification/Verify");
                        context.Response.Redirect("/AgeVerification/Verify");
                        return;
                    }
                    else
                    {
                        _logger.LogInformation("✅ Usuario YA verificado - Continuando...");
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Usuario es NULL");
                }
            }
            else
            {
                _logger.LogInformation("ℹ️ No se requiere verificación (usuario no autenticado o ruta pública)");
            }

            _logger.LogInformation("========================================");

            // Continuar con el siguiente middleware
            await _next(context);
        }
    }

    public static class AgeVerificationMiddlewareExtensions
    {
        public static IApplicationBuilder UseAgeVerification(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AgeVerificationMiddleware>();
        }
    }
}