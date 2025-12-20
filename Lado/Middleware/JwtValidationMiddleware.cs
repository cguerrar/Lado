using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Lado.Data;
using Lado.Services;
using Microsoft.EntityFrameworkCore;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware que valida tokens JWT contra la base de datos
    /// Verifica que el token esté activo (no revocado) y que el SecurityVersion sea válido
    /// </summary>
    public class JwtValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtValidationMiddleware> _logger;

        public JwtValidationMiddleware(RequestDelegate next, ILogger<JwtValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, IJwtService jwtService)
        {
            // Solo validar si hay un token Bearer
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                await _next(context);
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // Verificar si es un endpoint de API (no web)
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (!path.StartsWith("/api/") && !path.StartsWith("/chathub"))
            {
                await _next(context);
                return;
            }

            // Validar el token (firma, expiración, etc.)
            var principal = jwtService.ValidateToken(token);
            if (principal == null)
            {
                // Token inválido - dejar que el middleware de autenticación lo maneje
                await _next(context);
                return;
            }

            // Extraer claims necesarios
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var securityVersionStr = principal.FindFirst("security_version")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Token sin UserId - rechazando");
                await RespondUnauthorized(context, "Token inválido");
                return;
            }

            // Obtener el usuario actual para verificar SecurityVersion
            var user = await dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Usuario no encontrado: {UserId}", userId);
                await RespondUnauthorized(context, "Usuario no encontrado");
                return;
            }

            if (!user.EstaActivo)
            {
                _logger.LogWarning("Usuario desactivado: {UserId}", userId);
                await RespondUnauthorized(context, "Cuenta desactivada");
                return;
            }

            // Verificar SecurityVersion del token vs usuario
            if (!string.IsNullOrEmpty(securityVersionStr))
            {
                if (int.TryParse(securityVersionStr, out int tokenSecurityVersion))
                {
                    if (tokenSecurityVersion < user.SecurityVersion)
                    {
                        _logger.LogWarning("SecurityVersion del token ({TokenVersion}) es menor que el del usuario ({UserVersion}) - UserId: {UserId}",
                            tokenSecurityVersion, user.SecurityVersion, userId);
                        await RespondUnauthorized(context, "Sesión invalidada. Por favor inicia sesión nuevamente.");
                        return;
                    }
                }
            }

            // Si hay Jti, verificar si está en la lista negra (revocado)
            if (!string.IsNullOrEmpty(jti))
            {
                var activeToken = await dbContext.ActiveTokens
                    .FirstOrDefaultAsync(t => t.Jti == jti);

                // Si el token existe en ActiveTokens y está revocado, rechazar
                if (activeToken != null && activeToken.IsRevoked)
                {
                    _logger.LogWarning("Token revocado - Jti: {Jti}, UserId: {UserId}",
                        jti[..Math.Min(8, jti.Length)] + "...", userId);
                    await RespondUnauthorized(context, "Sesión cerrada. Por favor inicia sesión nuevamente.");
                    return;
                }
            }

            await _next(context);
        }

        private static async Task RespondUnauthorized(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = message,
                errors = new[] { "Token revocado o inválido. Por favor inicia sesión nuevamente." }
            });
        }
    }

    public static class JwtValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtValidationMiddleware>();
        }
    }
}
