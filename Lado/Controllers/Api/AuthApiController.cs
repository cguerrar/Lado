using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Lado.Models;
using Lado.Services;
using Lado.DTOs.Auth;
using Lado.DTOs.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Lado.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthApiController> _logger;
        private readonly IRateLimitService _rateLimitService;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtService jwtService,
            IEmailService emailService,
            ILogger<AuthApiController> logger,
            IRateLimitService rateLimitService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _emailService = emailService;
            _logger = logger;
            _rateLimitService = rateLimitService;
        }

        /// <summary>
        /// Login con email y contraseña
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginRequest request)
        {
            try
            {
                // ========================================
                // 🚫 RATE LIMITING - Prevenir fuerza bruta
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKeyIp = $"auth_login_ip_{clientIp}";
                var rateLimitKeyEmail = $"auth_login_email_{request.Email?.ToLower()}";

                // Límite por IP: máximo 10 intentos por 15 minutos
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 10, TimeSpan.FromMinutes(15)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT LOGIN IP: IP {IP} excedió límite", clientIp);
                    return StatusCode(429, ApiResponse<TokenResponse>.Fail("Demasiados intentos. Espera 15 minutos."));
                }

                // Límite por email: máximo 5 intentos por 15 minutos
                if (!_rateLimitService.IsAllowed(rateLimitKeyEmail, RateLimits.Login_MaxRequests, RateLimits.Login_Window))
                {
                    _logger.LogWarning("🚨 RATE LIMIT LOGIN EMAIL: {Email} excedió límite - IP: {IP}", request.Email, clientIp);
                    return StatusCode(429, ApiResponse<TokenResponse>.Fail("Demasiados intentos para este email. Espera 15 minutos."));
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<TokenResponse>.Fail(
                        "Datos invalidos",
                        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    ));
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    _logger.LogWarning("Intento de login fallido - email no encontrado: {Email}", request.Email);
                    return Unauthorized(ApiResponse<TokenResponse>.Fail("Credenciales invalidas"));
                }

                if (!user.EstaActivo)
                {
                    _logger.LogWarning("Intento de login de cuenta desactivada: {Email}", request.Email);
                    return Unauthorized(ApiResponse<TokenResponse>.Fail("Tu cuenta ha sido desactivada"));
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Email}", request.Email);
                    return Unauthorized(ApiResponse<TokenResponse>.Fail("Cuenta bloqueada temporalmente. Intenta mas tarde."));
                }

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Contraseña incorrecta para: {Email}", request.Email);
                    return Unauthorized(ApiResponse<TokenResponse>.Fail("Credenciales invalidas"));
                }

                // Generar tokens
                var roles = await _userManager.GetRolesAsync(user);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var tokens = await _jwtService.GenerateTokensAsync(user, roles, request.DeviceInfo, ipAddress);

                _logger.LogInformation("Login exitoso para: {Email}", request.Email);
                return Ok(ApiResponse<TokenResponse>.Ok(tokens, "Login exitoso"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en login para {Email}", request.Email);
                return StatusCode(500, ApiResponse<TokenResponse>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Registro de nuevo usuario
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TokenResponse>>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // ========================================
                // 🚫 RATE LIMITING - Prevenir registro masivo de bots
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKeyIp = $"auth_register_ip_{clientIp}";

                // Límite por IP: máximo 3 registros por hora (muy estricto)
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 3, TimeSpan.FromHours(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT REGISTER: IP {IP} excedió límite de registros", clientIp);
                    return StatusCode(429, ApiResponse<TokenResponse>.Fail("Demasiados registros desde esta conexión. Intenta más tarde."));
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<TokenResponse>.Fail(
                        "Datos invalidos",
                        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    ));
                }

                // Verificar si el email ya existe
                var existingEmail = await _userManager.FindByEmailAsync(request.Email);
                if (existingEmail != null)
                {
                    return BadRequest(ApiResponse<TokenResponse>.Fail("Este email ya esta registrado"));
                }

                // Verificar si el username ya existe
                var existingUsername = await _userManager.FindByNameAsync(request.UserName);
                if (existingUsername != null)
                {
                    return BadRequest(ApiResponse<TokenResponse>.Fail("Este nombre de usuario ya esta en uso"));
                }

                // Crear usuario
                var user = new ApplicationUser
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    NombreCompleto = request.NombreCompleto,
                    EsCreador = request.EsCreador,
                    EstaActivo = true,
                    FechaRegistro = DateTime.Now,
                    EmailConfirmed = false, // Requiere confirmacion
                    AgeVerified = true, // Asumir que verifico edad en la app
                    AgeVerifiedDate = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return BadRequest(ApiResponse<TokenResponse>.Fail("Error al crear cuenta", errors));
                }

                // Asignar rol
                var role = request.EsCreador ? "Creador" : "Fan";
                await _userManager.AddToRoleAsync(user, role);

                // Enviar email de confirmacion
                try
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    // TODO: Implementar deep link para la app
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Confirma tu cuenta en Lado",
                        $"<h2>Bienvenido a Lado!</h2><p>Tu codigo de confirmacion es: <strong>{token}</strong></p>"
                    );
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "No se pudo enviar email de confirmacion a {Email}", request.Email);
                }

                // Generar tokens
                var roles = await _userManager.GetRolesAsync(user);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var tokens = await _jwtService.GenerateTokensAsync(user, roles, request.DeviceInfo, ipAddress);

                _logger.LogInformation("Registro exitoso para: {Email}", request.Email);
                return Ok(ApiResponse<TokenResponse>.Ok(tokens, "Registro exitoso"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en registro para {Email}", request.Email);
                return StatusCode(500, ApiResponse<TokenResponse>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Refrescar token de acceso
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<TokenResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso de refresh
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKeyIp = $"auth_refresh_ip_{clientIp}";

                // Límite por IP: máximo 30 refresh por minuto
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 30, TimeSpan.FromMinutes(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT REFRESH: IP {IP} excedió límite", clientIp);
                    return StatusCode(429, ApiResponse<TokenResponse>.Fail("Demasiadas solicitudes. Espera un momento."));
                }

                if (string.IsNullOrEmpty(request.RefreshToken))
                {
                    return BadRequest(ApiResponse<TokenResponse>.Fail("Refresh token requerido"));
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var tokens = await _jwtService.RefreshTokenAsync(request.RefreshToken, request.DeviceInfo, ipAddress);

                if (tokens == null)
                {
                    return Unauthorized(ApiResponse<TokenResponse>.Fail("Token invalido o expirado"));
                }

                return Ok(ApiResponse<TokenResponse>.Ok(tokens, "Token refrescado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en refresh token");
                return StatusCode(500, ApiResponse<TokenResponse>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Cerrar sesion (revocar tokens)
        /// </summary>
        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponse>> Logout([FromBody] RefreshTokenRequest? request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);

                // Revocar el access token actual
                if (!string.IsNullOrEmpty(jti))
                {
                    await _jwtService.RevokeAccessTokenAsync(jti);
                }

                if (request != null && !string.IsNullOrEmpty(request.RefreshToken))
                {
                    // Revocar solo el refresh token proporcionado
                    await _jwtService.RevokeTokenAsync(request.RefreshToken);
                }
                else if (!string.IsNullOrEmpty(userId))
                {
                    // Revocar todos los tokens del usuario
                    await _jwtService.RevokeAllUserTokensAsync(userId);
                }

                // Incrementar SecurityVersion para invalidar todos los tokens existentes
                if (!string.IsNullOrEmpty(userId))
                {
                    await _jwtService.IncrementSecurityVersionAsync(userId);
                }

                return Ok(ApiResponse.Ok("Sesion cerrada correctamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en logout");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtener perfil del usuario actual
        /// </summary>
        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponse<UserTokenInfo>>> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<UserTokenInfo>.Fail("No autenticado"));
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<UserTokenInfo>.Fail("Usuario no encontrado"));
                }

                var roles = await _userManager.GetRolesAsync(user);

                var userInfo = new UserTokenInfo
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "",
                    Email = user.Email ?? "",
                    NombreCompleto = user.NombreCompleto ?? "",
                    FotoPerfil = user.FotoPerfil,
                    EsCreador = user.EsCreador,
                    EstaVerificado = user.CreadorVerificado,
                    Roles = roles.ToList()
                };

                return Ok(ApiResponse<UserTokenInfo>.Ok(userInfo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo perfil");
                return StatusCode(500, ApiResponse<UserTokenInfo>.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Solicitar recuperacion de contraseña
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                // ========================================
                // 🚫 RATE LIMITING - Prevenir spam de emails
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKeyIp = $"auth_forgot_ip_{clientIp}";
                var rateLimitKeyEmail = $"auth_forgot_email_{request.Email?.ToLower()}";

                // Límite por IP: máximo 5 por hora
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 5, TimeSpan.FromHours(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT FORGOT: IP {IP} excedió límite", clientIp);
                    return StatusCode(429, ApiResponse.Fail("Demasiadas solicitudes. Intenta más tarde."));
                }

                // Límite por email: máximo 3 por hora
                if (!_rateLimitService.IsAllowed(rateLimitKeyEmail, 3, TimeSpan.FromHours(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT FORGOT EMAIL: {Email} excedió límite", request.Email);
                    // No revelamos que el email existe
                    return Ok(ApiResponse.Ok("Si el email existe, recibiras instrucciones para restablecer tu contraseña"));
                }

                var user = await _userManager.FindByEmailAsync(request.Email);

                // Siempre responder ok para no revelar si el email existe
                if (user == null)
                {
                    return Ok(ApiResponse.Ok("Si el email existe, recibiras instrucciones para restablecer tu contraseña"));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                try
                {
                    // TODO: Implementar deep link para la app
                    await _emailService.SendEmailAsync(
                        user.Email!,
                        "Restablecer contraseña - Lado",
                        $"<h2>Restablecer contraseña</h2><p>Tu codigo es: <strong>{token}</strong></p><p>Este codigo expira en 1 hora.</p>"
                    );
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "No se pudo enviar email de recuperacion a {Email}", request.Email);
                }

                return Ok(ApiResponse.Ok("Si el email existe, recibiras instrucciones para restablecer tu contraseña"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en forgot-password");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Restablecer contraseña con token
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.Fail(
                        "Datos invalidos",
                        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    ));
                }

                var user = await _userManager.FindByIdAsync(request.UserId);
                if (user == null)
                {
                    return BadRequest(ApiResponse.Fail("Token invalido o expirado"));
                }

                var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
                if (!result.Succeeded)
                {
                    return BadRequest(ApiResponse.Fail("Token invalido o expirado"));
                }

                // Revocar todos los tokens del usuario
                await _jwtService.RevokeAllUserTokensAsync(user.Id);

                return Ok(ApiResponse.Ok("Contraseña restablecida correctamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en reset-password");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Cambiar contraseña (usuario autenticado)
        /// </summary>
        [HttpPost("change-password")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.Fail(
                        "Datos invalidos",
                        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                    ));
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId!);

                if (user == null)
                {
                    return NotFound(ApiResponse.Fail("Usuario no encontrado"));
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return BadRequest(ApiResponse.Fail("Error al cambiar contraseña", errors));
                }

                // Incrementar SecurityVersion para invalidar todos los tokens existentes
                // Esto fuerza a todas las sesiones activas a cerrar
                await _jwtService.IncrementSecurityVersionAsync(user.Id);
                await _jwtService.RevokeAllUserTokensAsync(user.Id);

                return Ok(ApiResponse.Ok("Contraseña cambiada correctamente. Todas las sesiones han sido cerradas."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en change-password");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Verificar si un email esta disponible
        /// </summary>
        [HttpGet("check-email")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> CheckEmail([FromQuery] string email)
        {
            // Rate limiting para prevenir enumeración de usuarios
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"check_email_ip_{clientIp}";

            if (!_rateLimitService.IsAllowed(rateLimitKey, 30, TimeSpan.FromMinutes(5)))
            {
                _logger.LogWarning("🚨 RATE LIMIT CHECK EMAIL: IP {IP} excedió límite", clientIp);
                return StatusCode(429, ApiResponse<bool>.Fail("Demasiadas solicitudes. Espera unos minutos."));
            }

            var exists = await _userManager.FindByEmailAsync(email);
            return Ok(ApiResponse<bool>.Ok(exists == null, exists == null ? "Email disponible" : "Email ya registrado"));
        }

        /// <summary>
        /// Verificar si un username esta disponible
        /// </summary>
        [HttpGet("check-username")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> CheckUsername([FromQuery] string username)
        {
            // Rate limiting para prevenir enumeración de usuarios
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"check_username_ip_{clientIp}";

            if (!_rateLimitService.IsAllowed(rateLimitKey, 30, TimeSpan.FromMinutes(5)))
            {
                _logger.LogWarning("🚨 RATE LIMIT CHECK USERNAME: IP {IP} excedió límite", clientIp);
                return StatusCode(429, ApiResponse<bool>.Fail("Demasiadas solicitudes. Espera unos minutos."));
            }

            var exists = await _userManager.FindByNameAsync(username);
            return Ok(ApiResponse<bool>.Ok(exists == null, exists == null ? "Username disponible" : "Username ya registrado"));
        }
    }
}
