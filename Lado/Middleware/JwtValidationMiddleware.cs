using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Lado.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

        public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
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

            // Validar el token
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

            if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Token sin Jti o UserId - rechazando");
                await RespondUnauthorized(context, "Token inválido");
                return;
            }

            // Parsear SecurityVersion (default 1 para tokens antiguos sin este claim)
            var securityVersion = 1;
            if (!string.IsNullOrEmpty(securityVersionStr))
            {
                int.TryParse(securityVersionStr, out securityVersion);
            }

            // Verificar si el token está activo en la base de datos
            var isActive = await jwtService.IsTokenActiveAsync(jti, userId, securityVersion);
            if (!isActive)
            {
                _logger.LogWarning("Token revocado o SecurityVersion inválido - UserId: {UserId}, Jti: {Jti}",
                    userId, jti[..Math.Min(8, jti.Length)] + "...");
                await RespondUnauthorized(context, "Sesión expirada o revocada");
                return;
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
