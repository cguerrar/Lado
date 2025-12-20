using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Lado.Data;
using Lado.Services;
using Microsoft.EntityFrameworkCore;

namespace Lado.Middleware
{
    /// <summary>
    /// Middleware de seguridad JWT - Valida tokens contra la base de datos
    /// IMPORTANTE: Implementa whitelist - solo tokens registrados en ActiveTokens son válidos
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

            // ========================================
            // VALIDACIÓN 1: Firma y expiración del token
            // ========================================
            var principal = jwtService.ValidateToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Token JWT inválido (firma o expiración) - Path: {Path}", path);
                await RespondUnauthorized(context, "Token inválido o expirado");
                return; // CRÍTICO: Rechazar inmediatamente, no pasar al siguiente middleware
            }

            // ========================================
            // VALIDACIÓN 2: Claims requeridos
            // ========================================
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var securityVersionStr = principal.FindFirst("security_version")?.Value;

            // CRÍTICO: Jti es OBLIGATORIO para poder revocar tokens
            if (string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("Token sin Jti (posible token antiguo o manipulado) - Path: {Path}", path);
                await RespondUnauthorized(context, "Token inválido - falta identificador");
                return;
            }

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Token sin UserId - Path: {Path}", path);
                await RespondUnauthorized(context, "Token inválido");
                return;
            }

            // CRÍTICO: security_version es OBLIGATORIO
            if (string.IsNullOrEmpty(securityVersionStr) || !int.TryParse(securityVersionStr, out int tokenSecurityVersion))
            {
                _logger.LogWarning("Token sin security_version válido (posible token antiguo) - UserId: {UserId}", userId);
                await RespondUnauthorized(context, "Sesión expirada. Por favor inicia sesión nuevamente.");
                return;
            }

            // ========================================
            // VALIDACIÓN 3: Usuario existe y está activo
            // ========================================
            var user = await dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Usuario no encontrado: {UserId}", userId);
                await RespondUnauthorized(context, "Usuario no encontrado");
                return;
            }

            if (!user.EstaActivo)
            {
                _logger.LogWarning("Usuario desactivado intentando acceder: {UserId}", userId);
                await RespondUnauthorized(context, "Cuenta desactivada");
                return;
            }

            // ========================================
            // VALIDACIÓN 4: SecurityVersion (invalidación masiva)
            // ========================================
            if (tokenSecurityVersion < user.SecurityVersion)
            {
                _logger.LogWarning("SecurityVersion obsoleto - Token: {TokenVersion}, Usuario: {UserVersion}, UserId: {UserId}",
                    tokenSecurityVersion, user.SecurityVersion, userId);
                await RespondUnauthorized(context, "Sesión invalidada. Por favor inicia sesión nuevamente.");
                return;
            }

            // ========================================
            // VALIDACIÓN 5: Token en whitelist (ActiveTokens)
            // CRÍTICO: Solo tokens registrados son válidos
            // ========================================
            var activeToken = await dbContext.ActiveTokens
                .FirstOrDefaultAsync(t => t.Jti == jti && t.UserId == userId);

            // Si el token NO está en ActiveTokens, rechazar (whitelist approach)
            if (activeToken == null)
            {
                _logger.LogWarning("Token no registrado en ActiveTokens (posible token forjado) - Jti: {Jti}, UserId: {UserId}",
                    jti[..Math.Min(8, jti.Length)] + "...", userId);
                await RespondUnauthorized(context, "Sesión no válida. Por favor inicia sesión nuevamente.");
                return;
            }

            // Si el token está revocado, rechazar
            if (activeToken.IsRevoked)
            {
                _logger.LogWarning("Token revocado - Jti: {Jti}, UserId: {UserId}",
                    jti[..Math.Min(8, jti.Length)] + "...", userId);
                await RespondUnauthorized(context, "Sesión cerrada. Por favor inicia sesión nuevamente.");
                return;
            }

            // Verificar expiración en BD (doble check)
            if (activeToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Token expirado en BD - Jti: {Jti}, UserId: {UserId}",
                    jti[..Math.Min(8, jti.Length)] + "...", userId);
                await RespondUnauthorized(context, "Sesión expirada. Por favor inicia sesión nuevamente.");
                return;
            }

            // ========================================
            // TOKEN VÁLIDO - Continuar
            // ========================================
            await _next(context);
        }

        private static async Task RespondUnauthorized(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            // Agregar headers de seguridad
            context.Response.Headers.Append("WWW-Authenticate", "Bearer error=\"invalid_token\"");

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = message,
                code = "TOKEN_INVALID",
                errors = new[] { "Autenticación requerida. Por favor inicia sesión nuevamente." }
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
