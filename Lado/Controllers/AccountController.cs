using Lado.Models;
using Lado.Services;
using Lado.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lado.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILadoCoinsService _ladoCoinsService;
        private readonly IReferidosService _referidosService;
        private readonly IRachasService _rachasService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger,
            IEmailService emailService,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IRateLimitService rateLimitService,
            ILadoCoinsService ladoCoinsService,
            IReferidosService referidosService,
            IRachasService rachasService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
            _environment = environment;
            _rateLimitService = rateLimitService;
            _ladoCoinsService = ladoCoinsService;
            _referidosService = referidosService;
            _rachasService = rachasService;
        }

        // ========================================
        // REGISTRO
        // ========================================

        [HttpGet]
        public IActionResult Register([FromQuery(Name = "ref")] string? refCode = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                _logger.LogInformation("Usuario ya autenticado, redirigiendo a FeedPublico");
                return RedirectToAction("Index", "FeedPublico");
            }

            var model = new RegisterViewModel
            {
                CodigoReferido = refCode
            };

            if (!string.IsNullOrEmpty(refCode))
            {
                _logger.LogInformation("Registro con código de referido: {RefCode}", refCode);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Modelo de registro inválido");
                return View(model);
            }

            // Rate limiting: máximo 3 registros por hora por IP
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!await _rateLimitService.IsAllowedAsync(
                clientIp,
                $"register_ip_{clientIp}",
                3,
                TimeSpan.FromHours(1),
                TipoAtaque.SpamRegistro,
                "/Account/Register"))
            {
                ModelState.AddModelError(string.Empty, "Demasiados intentos de registro. Intenta más tarde.");
                return View(model);
            }

            try
            {
                // ✅ Verificar que el nombre de usuario no exista
                var existingUser = await _userManager.FindByNameAsync(model.NombreUsuario);
                if (existingUser != null)
                {
                    ModelState.AddModelError("NombreUsuario", "Este nombre de usuario ya está en uso");
                    return View(model);
                }

                // ✅ Verificar que el email no exista
                var existingEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingEmail != null)
                {
                    ModelState.AddModelError("Email", "Este email ya está registrado");
                    return View(model);
                }

                // ✅ Determinar seudónimo (usar NombreUsuario si no se proporciona)
                var seudonimo = !string.IsNullOrWhiteSpace(model.Seudonimo)
                    ? model.Seudonimo.Trim()
                    : model.NombreUsuario;

                // ✅ Verificar que el seudónimo sea único
                var existingSeudonimo = await _userManager.Users
                    .AnyAsync(u => u.Seudonimo == seudonimo);
                if (existingSeudonimo)
                {
                    ModelState.AddModelError("Seudonimo", "Este seudónimo ya está en uso");
                    return View(model);
                }

                // ✅ CAMBIO PRINCIPAL: Todos los usuarios son creadores ahora
                var usuario = new ApplicationUser
                {
                    UserName = model.NombreUsuario,
                    Email = model.Email,
                    NombreCompleto = model.NombreCompleto,
                    Seudonimo = seudonimo, // Usa NombreUsuario si no se proporciona
                    LadoPreferido = model.LadoPreferido, // ⭐ Lado preferido seleccionado por el usuario
                    ZonaHoraria = model.ZonaHoraria, // ⭐ Zona horaria detectada automáticamente
                    FechaRegistro = DateTime.Now,
                    EstaActivo = true,
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    TwoFactorEnabled = false,
                    LockoutEnabled = false,
                    AccessFailedCount = 0,
                    // Valores predeterminados para creadores
                    PrecioSuscripcion = 9.99m,
                    NumeroSeguidores = 0,
                    Saldo = 0,
                    TotalGanancias = 0,
                    EsVerificado = false,
                    SeudonimoVerificado = false
                };

                _logger.LogInformation("Creando usuario: {Username}, Email: {Email}, Seudonimo: {Seudonimo}",
                    usuario.UserName, usuario.Email, usuario.Seudonimo);

                var result = await _userManager.CreateAsync(usuario, model.Contraseña);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Usuario creado exitosamente: {Username}", usuario.UserName);

                    // ⭐ Procesar LadoCoins - Bono de bienvenida
                    try
                    {
                        await _ladoCoinsService.AcreditarBonoAsync(
                            usuario.Id,
                            TipoTransaccionLadoCoin.BonoBienvenida,
                            "Bono de bienvenida por registrarte en LADO"
                        );
                        usuario.BonoBienvenidaEntregado = true;
                        await _userManager.UpdateAsync(usuario);
                        _logger.LogInformation("Bono de bienvenida entregado a: {Username}", usuario.UserName);

                        // ⭐ Procesar código de referido si existe
                        if (!string.IsNullOrEmpty(model.CodigoReferido))
                        {
                            var referidoRegistrado = await _referidosService.RegistrarReferidoAsync(
                                usuario.Id,
                                model.CodigoReferido
                            );

                            if (referidoRegistrado)
                            {
                                // Entregar bonos de referido (para el nuevo usuario y el referidor)
                                await _referidosService.EntregarBonosRegistroAsync(usuario.Id);
                                _logger.LogInformation("Referido registrado con código: {RefCode} para usuario: {Username}",
                                    model.CodigoReferido, usuario.UserName);
                            }
                            else
                            {
                                _logger.LogWarning("Código de referido inválido: {RefCode}", model.CodigoReferido);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al procesar LadoCoins en registro para: {Username}", usuario.UserName);
                        // No bloqueamos el registro por errores de LadoCoins
                    }

                    // Iniciar sesion automaticamente
                    await _signInManager.SignInAsync(usuario, isPersistent: false);

                    _logger.LogInformation("Sesion iniciada para: {Username}", usuario.UserName);

                    TempData["Success"] = "Bienvenido a LADO! Tu cuenta ha sido creada exitosamente. ¡Recibiste LadoCoins de bienvenida!";

                    // Redirigir al onboarding de intereses
                    return RedirectToAction("SeleccionarIntereses", "Usuario");
                }

                // Si hay errores, mostrarlos
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("Error en creación de usuario: {Code} - {Description}",
                        error.Code, error.Description);
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al registrar usuario: {Username}", model.NombreUsuario);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear la cuenta. Por favor intenta nuevamente.");
            }

            return View(model);
        }

        // ========================================
        // LOGIN
        // ========================================

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                _logger.LogInformation("Usuario ya autenticado, redirigiendo a FeedPublico");
                return RedirectToAction("Index", "FeedPublico");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState inválido en Login");
                return View(model);
            }

            // Rate limiting: máximo 10 intentos de login por 15 minutos por IP
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!await _rateLimitService.IsAllowedAsync(
                clientIp,
                $"login_ip_{clientIp}",
                10,
                TimeSpan.FromMinutes(15),
                TipoAtaque.FuerzaBruta,
                "/Account/Login"))
            {
                ModelState.AddModelError(string.Empty, "Demasiados intentos de inicio de sesión. Intenta en 15 minutos.");
                return View(model);
            }

            try
            {
                ApplicationUser user = null;

                // ✅ Buscar usuario por EMAIL o USERNAME
                if (model.EmailOUsuario.Contains("@"))
                {
                    _logger.LogInformation("Buscando usuario por email: {Email}", model.EmailOUsuario);
                    user = await _userManager.FindByEmailAsync(model.EmailOUsuario);
                }
                else
                {
                    _logger.LogInformation("Buscando usuario por username: {Username}", model.EmailOUsuario);
                    user = await _userManager.FindByNameAsync(model.EmailOUsuario);
                }

                if (user == null)
                {
                    _logger.LogWarning("❌ Usuario no encontrado: {EmailOUsuario}", model.EmailOUsuario);
                    ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
                    return View(model);
                }

                _logger.LogInformation("✅ Usuario encontrado: {Username} ({Email})", user.UserName, user.Email);

                // ✅ Verificar si la cuenta está activa
                if (!user.EstaActivo)
                {
                    _logger.LogWarning("❌ Cuenta inactiva: {Username}", user.UserName);
                    ModelState.AddModelError(string.Empty, "Tu cuenta ha sido desactivada. Contacta al soporte.");
                    return View(model);
                }

                // ✅ Verificar contraseña
                var passwordCheck = await _userManager.CheckPasswordAsync(user, model.Contraseña);
                if (!passwordCheck)
                {
                    _logger.LogWarning("❌ Contraseña incorrecta para: {Username}", user.UserName);
                    ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
                    return View(model);
                }

                _logger.LogInformation("✅ Contraseña correcta. Iniciando sesión...");

                // ✅ Hacer login
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    model.Contraseña,
                    isPersistent: model.Recordarme,
                    lockoutOnFailure: false
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ Login exitoso para: {Username}", user.UserName);

                    // ✅ Actualizar contador de ingresos y ultima actividad
                    user.ContadorIngresos++;
                    user.UltimaActividad = DateTime.Now;
                    await _userManager.UpdateAsync(user);

                    // ⭐ Registrar login diario para LadoCoins
                    try
                    {
                        var bonoOtorgado = await _rachasService.RegistrarLoginAsync(user.Id);
                        if (bonoOtorgado)
                        {
                            _logger.LogInformation("Bono de login diario otorgado a: {Username}", user.UserName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar login diario para: {Username}", user.UserName);
                        // No bloqueamos el login por errores de LadoCoins
                    }

                    // ✅ Verificar si es Admin
                    var roles = await _userManager.GetRolesAsync(user);
                    _logger.LogInformation("Roles del usuario: {Roles}", string.Join(", ", roles));

                    if (roles.Contains("Admin"))
                    {
                        _logger.LogInformation("Redirigiendo a panel de Admin");
                        return RedirectToAction("Index", "Admin");
                    }

                    // ✅ Siempre ir a FeedPublico después del login
                    _logger.LogInformation("Redirigiendo a FeedPublico");
                    TempData["Success"] = $"¡Bienvenido de nuevo, {user.NombreCompleto}!";
                    return RedirectToAction("Index", "FeedPublico");
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("❌ Cuenta bloqueada: {Username}", user.UserName);
                    return View("Lockout");
                }

                if (result.RequiresTwoFactor)
                {
                    _logger.LogInformation("Se requiere autenticación de dos factores");
                    return RedirectToAction("LoginWith2fa", new { returnUrl, model.Recordarme });
                }

                _logger.LogError("❌ Login falló para: {Username}. Result: {Result}", user.UserName, result);
                ModelState.AddModelError(string.Empty, "Error al iniciar sesión. Por favor intenta nuevamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Excepción en Login para: {EmailOUsuario}", model.EmailOUsuario);
                ModelState.AddModelError(string.Empty, "Error del servidor. Por favor intenta nuevamente.");
            }

            return View(model);
        }

        // ========================================
        // LOGOUT
        // ========================================

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var userName = User?.Identity?.Name;
            var isProduction = !_environment.IsDevelopment();

            _logger.LogInformation("🔄 Iniciando logout para: {Username}, Entorno: {Env}",
                userName ?? "Unknown",
                isProduction ? "Produccion" : "Desarrollo");

            try
            {
                // 1. Cerrar sesión de Identity
                await _signInManager.SignOutAsync();
                _logger.LogInformation("✅ SignOutAsync completado");

                // 2. Limpiar sesión HTTP
                HttpContext.Session.Clear();
                _logger.LogInformation("✅ Sesión HTTP limpiada");

                // 3. Eliminar cookies - probar todas las combinaciones posibles
                var cookieNames = new[] { ".Lado.Auth", ".AspNetCore.Identity.Application", ".AspNetCore.Session", ".AspNetCore.Cookies" };

                foreach (var cookieName in cookieNames)
                {
                    // Intentar eliminar con dominio de producción
                    if (isProduction)
                    {
                        Response.Cookies.Delete(cookieName, new CookieOptions
                        {
                            Domain = ".ladoapp.com",
                            Path = "/",
                            Secure = true,
                            SameSite = SameSiteMode.Lax
                        });

                        // También sin el punto inicial
                        Response.Cookies.Delete(cookieName, new CookieOptions
                        {
                            Domain = "ladoapp.com",
                            Path = "/",
                            Secure = true,
                            SameSite = SameSiteMode.Lax
                        });

                        // Con www
                        Response.Cookies.Delete(cookieName, new CookieOptions
                        {
                            Domain = "www.ladoapp.com",
                            Path = "/",
                            Secure = true,
                            SameSite = SameSiteMode.Lax
                        });
                    }

                    // Siempre eliminar sin dominio también (por si acaso)
                    Response.Cookies.Delete(cookieName, new CookieOptions
                    {
                        Path = "/",
                        Secure = isProduction,
                        SameSite = SameSiteMode.Lax
                    });

                    // Eliminar de forma simple
                    Response.Cookies.Delete(cookieName);
                }

                // 4. Expirar cookies estableciendo fecha pasada
                foreach (var cookieName in cookieNames)
                {
                    Response.Cookies.Append(cookieName, "", new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(-1),
                        Path = "/",
                        Domain = isProduction ? ".ladoapp.com" : null,
                        Secure = isProduction,
                        SameSite = SameSiteMode.Lax
                    });
                }

                _logger.LogInformation("✅ Logout completo para: {Username}", userName ?? "Unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante logout para: {Username}", userName ?? "Unknown");
            }

            TempData["Info"] = "Has cerrado sesión exitosamente.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            return await Logout();
        }

        // ========================================
        // ACCESO DENEGADO
        // ========================================

        [HttpGet]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            _logger.LogWarning("⚠️ Acceso denegado. ReturnUrl: {ReturnUrl}", returnUrl);
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // ========================================
        // RECUPERAR CONTRASEÑA
        // ========================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "FeedPublico");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // No revelar que el usuario no existe - mostrar el mismo mensaje
                _logger.LogWarning("Intento de recuperar contraseña para email no existente: {Email}", model.Email);
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // Generar token de restablecimiento
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = _configuration["AppSettings:BaseUrl"] ?? "https://www.ladoapp.com";
            var resetLink = $"{baseUrl}/Account/ResetPassword?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            _logger.LogInformation("Generando enlace de restablecimiento para: {Email}", model.Email);

            // Enviar email
            var emailSent = await _emailService.SendPasswordResetEmailAsync(
                user.Email!,
                user.NombreCompleto ?? user.UserName ?? "Usuario",
                resetLink
            );

            if (!emailSent)
            {
                _logger.LogError("Error al enviar email de restablecimiento a: {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Hubo un error al enviar el correo. Por favor intenta nuevamente.");
                return View(model);
            }

            _logger.LogInformation("Email de restablecimiento enviado exitosamente a: {Email}", model.Email);
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Intento de acceso a ResetPassword sin email o token");
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // No revelar que el usuario no existe
                _logger.LogWarning("Intento de restablecer contraseña para email no existente: {Email}", model.Email);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Contraseña restablecida exitosamente para: {Email}", model.Email);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                _logger.LogWarning("Error al restablecer contraseña: {Error}", error.Description);

                // Traducir errores comunes
                var errorMessage = error.Code switch
                {
                    "InvalidToken" => "El enlace de restablecimiento ha expirado o es invalido. Por favor solicita uno nuevo.",
                    "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres.",
                    "PasswordRequiresDigit" => "La contraseña debe contener al menos un numero.",
                    "PasswordRequiresLower" => "La contraseña debe contener al menos una letra minuscula.",
                    _ => error.Description
                };

                ModelState.AddModelError(string.Empty, errorMessage);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // ========================================
        // VERIFICACIÓN DE EMAIL
        // ========================================

        /// <summary>
        /// Enviar email de verificación al usuario autenticado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarVerificacionEmail()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Json(new { success = false, message = "Debes iniciar sesión" });
            }

            if (usuario.EmailConfirmed)
            {
                return Json(new { success = false, message = "Tu email ya está verificado" });
            }

            // Rate limiting
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"verify_email_{usuario.Id}";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 3, TimeSpan.FromHours(1),
                TipoAtaque.SpamMensajes, "/Account/EnviarVerificacionEmail", usuario.UserName, null))
            {
                return Json(new { success = false, message = "Has solicitado demasiados correos. Espera 1 hora." });
            }

            try
            {
                // Generar token de confirmación
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);

                // Crear link de confirmación
                var baseUrl = _configuration["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var confirmLink = $"{baseUrl}/Account/ConfirmarEmail?userId={Uri.EscapeDataString(usuario.Id)}&token={Uri.EscapeDataString(token)}";

                // Enviar email
                var enviado = await _emailService.SendConfirmationEmailAsync(
                    usuario.Email!,
                    usuario.NombreCompleto ?? usuario.UserName ?? "Usuario",
                    confirmLink
                );

                if (enviado)
                {
                    _logger.LogInformation("Email de verificación enviado a: {Email}", usuario.Email);
                    return Json(new { success = true, message = "Correo de verificación enviado. Revisa tu bandeja de entrada." });
                }
                else
                {
                    _logger.LogError("Error al enviar email de verificación a: {Email}", usuario.Email);
                    return Json(new { success = false, message = "Error al enviar el correo. Inténtalo más tarde." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar verificación de email a: {Email}", usuario.Email);
                return Json(new { success = false, message = "Error al enviar el correo. Inténtalo más tarde." });
            }
        }

        /// <summary>
        /// Confirmar email con token (cuando el usuario hace clic en el enlace)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConfirmarEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Enlace de verificación inválido";
                return RedirectToAction("Login");
            }

            var usuario = await _userManager.FindByIdAsync(userId);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Login");
            }

            if (usuario.EmailConfirmed)
            {
                TempData["Info"] = "Tu email ya estaba verificado";
                return RedirectToAction("Index", "LadoCoins");
            }

            var result = await _userManager.ConfirmEmailAsync(usuario, token);

            if (result.Succeeded)
            {
                _logger.LogInformation("Email verificado exitosamente para: {UserId}", userId);

                // ⭐ LADOCOINS: Entregar bono de verificación de email
                var mensajeBono = "";
                if (!usuario.BonoEmailVerificadoEntregado)
                {
                    try
                    {
                        var bonoEntregado = await _ladoCoinsService.AcreditarBonoAsync(
                            usuario.Id,
                            TipoTransaccionLadoCoin.BonoVerificarEmail,
                            "Bono por verificar tu email en LADO"
                        );

                        if (bonoEntregado)
                        {
                            usuario.BonoEmailVerificadoEntregado = true;
                            await _userManager.UpdateAsync(usuario);
                            _logger.LogInformation("⭐ Bono de verificación de email entregado a: {UserId}", userId);

                            // Obtener monto del bono para el mensaje
                            var montoBono = await _ladoCoinsService.ObtenerConfiguracionAsync(ConfiguracionLadoCoin.BONO_VERIFICAR_EMAIL);
                            if (montoBono <= 0) montoBono = 2;
                            mensajeBono = $" ¡Además, ganaste ${montoBono:N2} en LadoCoins!";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al entregar bono de email verificado a: {UserId}", userId);
                    }
                }

                TempData["Success"] = $"¡Tu email ha sido verificado correctamente!{mensajeBono}";
                return RedirectToAction("Index", "LadoCoins");
            }
            else
            {
                var errores = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Error al verificar email para {UserId}: {Errores}", userId, errores);
                TempData["Error"] = "El enlace de verificación ha expirado o es inválido. Solicita uno nuevo.";
                return RedirectToAction("Index", "LadoCoins");
            }
        }

        /// <summary>
        /// Página de confirmación de email verificado
        /// </summary>
        [HttpGet]
        public IActionResult EmailVerificado()
        {
            return View();
        }

        // ========================================
        // VERIFICAR DISPONIBILIDAD (AJAX)
        // ========================================

        [HttpPost]
        public async Task<JsonResult> CheckUsernameAvailability(string username)
        {
            // Rate limiting para prevenir enumeración de usuarios
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();
            var rateLimitKey = $"check_user_ip_{clientIp}";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 30, TimeSpan.FromMinutes(5),
                TipoAtaque.Scraping, "/Account/CheckUsernameAvailability", null, userAgent))
            {
                _logger.LogWarning("🚨 RATE LIMIT CHECK USERNAME: IP {IP} excedió límite", clientIp);
                return Json(new { available = false, message = "Demasiadas solicitudes. Espera unos minutos." });
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return Json(new { available = false, message = "Nombre de usuario requerido" });
            }

            var user = await _userManager.FindByNameAsync(username);
            var available = user == null;

            return Json(new
            {
                available = available,
                message = available ? "Disponible" : "Ya está en uso"
            });
        }

        [HttpPost]
        public async Task<JsonResult> CheckEmailAvailability(string email)
        {
            // Rate limiting para prevenir enumeración de usuarios
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();
            var rateLimitKey = $"check_email_ip_{clientIp}";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 30, TimeSpan.FromMinutes(5),
                TipoAtaque.Scraping, "/Account/CheckEmailAvailability", null, userAgent))
            {
                _logger.LogWarning("🚨 RATE LIMIT CHECK EMAIL: IP {IP} excedió límite", clientIp);
                return Json(new { available = false, message = "Demasiadas solicitudes. Espera unos minutos." });
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { available = false, message = "Email requerido" });
            }

            var user = await _userManager.FindByEmailAsync(email);
            var available = user == null;

            return Json(new
            {
                available = available,
                message = available ? "Disponible" : "Ya está registrado"
            });
        }

        [HttpPost]
        public async Task<JsonResult> CheckSeudonimoAvailability(string seudonimo)
        {
            // Rate limiting para prevenir enumeración de usuarios
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers["User-Agent"].ToString();
            var rateLimitKey = $"check_seud_ip_{clientIp}";
            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 30, TimeSpan.FromMinutes(5),
                TipoAtaque.Scraping, "/Account/CheckSeudonimoAvailability", null, userAgent))
            {
                _logger.LogWarning("🚨 RATE LIMIT CHECK SEUDONIMO: IP {IP} excedió límite", clientIp);
                return Json(new { available = false, message = "Demasiadas solicitudes. Espera unos minutos." });
            }

            if (string.IsNullOrWhiteSpace(seudonimo))
            {
                return Json(new { available = false, message = "Seudónimo requerido" });
            }

            var exists = await _userManager.Users.AnyAsync(u => u.Seudonimo == seudonimo);
            var available = !exists;

            return Json(new
            {
                available = available,
                message = available ? "Disponible" : "Ya está en uso"
            });
        }

        // ========================================
        // LOGIN EXTERNO (GOOGLE)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            try
            {
                returnUrl ??= Url.Content("~/");

                if (remoteError != null)
                {
                    _logger.LogError("Error de proveedor externo: {Error}", remoteError);
                    TempData["Error"] = $"Error del proveedor externo: {remoteError}";
                    return RedirectToAction(nameof(Login));
                }

                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    _logger.LogError("No se pudo cargar información de login externo");
                    TempData["Error"] = "Error al obtener información de login externo.";
                    return RedirectToAction(nameof(Login));
                }

                _logger.LogInformation("Login externo con {Provider}", info.LoginProvider);

                // Intentar login con el proveedor externo
                var result = await _signInManager.ExternalLoginSignInAsync(
                    info.LoginProvider,
                    info.ProviderKey,
                    isPersistent: true,
                    bypassTwoFactor: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Usuario logueado con {Provider}", info.LoginProvider);

                    // Obtener el usuario para redirigir correctamente
                    var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                    if (user != null)
                    {
                        // Actualizar contador de ingresos y ultima actividad
                        user.ContadorIngresos++;
                        user.UltimaActividad = DateTime.Now;
                        await _userManager.UpdateAsync(user);

                        // ⭐ Registrar login diario para LadoCoins
                        try
                        {
                            await _rachasService.RegistrarLoginAsync(user.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al registrar login diario (Google) para: {UserId}", user.Id);
                        }

                        var roles = await _userManager.GetRolesAsync(user);
                        if (roles.Contains("Admin"))
                        {
                            return RedirectToAction("Index", "Admin");
                        }
                    }

                    // Siempre ir a FeedPublico después del login con Google
                    return RedirectToAction("Index", "FeedPublico");
                }

                if (result.IsLockedOut)
                {
                    return View("Lockout");
                }

                // Si el usuario no existe, crear cuenta
                var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
                var name = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Name);

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("No se pudo obtener email del proveedor externo");
                    TempData["Error"] = "No se pudo obtener tu email de Google. Asegúrate de dar permiso.";
                    return RedirectToAction(nameof(Login));
                }

                // Verificar si ya existe usuario con ese email
                var existingUser = await _userManager.FindByEmailAsync(email);
                if (existingUser != null)
                {
                    // Verificar si la cuenta está activa
                    if (!existingUser.EstaActivo)
                    {
                        _logger.LogWarning("Intento de login con cuenta desactivada: {Email}", email);
                        TempData["Error"] = "Tu cuenta está desactivada. Contacta a soporte.";
                        return RedirectToAction(nameof(Login));
                    }

                    // Intentar vincular el login externo a la cuenta existente
                    var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        await _signInManager.SignInAsync(existingUser, isPersistent: true);
                        _logger.LogInformation("Login externo vinculado a usuario existente: {Email}", email);
                        return RedirectToAction("Index", "FeedPublico");
                    }
                    else
                    {
                        // Si falla porque ya existe el login, simplemente hacer sign in
                        _logger.LogInformation("Login externo ya vinculado, haciendo sign in: {Email}", email);
                        await _signInManager.SignInAsync(existingUser, isPersistent: true);
                        return RedirectToAction("Index", "FeedPublico");
                    }
                }

                // Crear nuevo usuario
                var newUser = new ApplicationUser
                {
                    UserName = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                    Email = email,
                    NombreCompleto = name ?? email.Split('@')[0],
                    Seudonimo = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N").Substring(0, 4),
                    LadoPreferido = Models.TipoLado.LadoA, // Por defecto LadoA para login con Google
                    FechaRegistro = DateTime.Now,
                    EstaActivo = true,
                    EmailConfirmed = true, // Email verificado por Google
                    AgeVerified = false,
                    PrecioSuscripcion = 9.99m,
                    NumeroSeguidores = 0,
                    Saldo = 0,
                    TotalGanancias = 0,
                    EsVerificado = false,
                    SeudonimoVerificado = false
                };

                var createResult = await _userManager.CreateAsync(newUser);
                if (createResult.Succeeded)
                {
                    var addLoginResult = await _userManager.AddLoginAsync(newUser, info);
                    if (addLoginResult.Succeeded)
                    {
                        // ⭐ Procesar LadoCoins - Bono de bienvenida para usuarios de Google
                        try
                        {
                            await _ladoCoinsService.AcreditarBonoAsync(
                                newUser.Id,
                                TipoTransaccionLadoCoin.BonoBienvenida,
                                "Bono de bienvenida por registrarte en LADO"
                            );

                            // Marcar email verificado ya que Google lo verificó
                            await _ladoCoinsService.AcreditarBonoAsync(
                                newUser.Id,
                                TipoTransaccionLadoCoin.BonoVerificarEmail,
                                "Email verificado por Google"
                            );

                            newUser.BonoBienvenidaEntregado = true;
                            newUser.BonoEmailVerificadoEntregado = true;
                            await _userManager.UpdateAsync(newUser);
                            _logger.LogInformation("Bonos de bienvenida entregados a usuario Google: {Email}", email);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al procesar LadoCoins en registro Google para: {Email}", email);
                        }

                        await _signInManager.SignInAsync(newUser, isPersistent: true);
                        _logger.LogInformation("Nuevo usuario creado con Google: {Email}", email);
                        TempData["Success"] = "¡Bienvenido a LADO! Tu cuenta ha sido creada con Google. ¡Recibiste LadoCoins de bienvenida!";
                        return RedirectToAction("Index", "FeedPublico");
                    }
                }

                foreach (var error in createResult.Errors)
                {
                    _logger.LogError("Error creando usuario: {Error}", error.Description);
                }

                TempData["Error"] = "Error al crear la cuenta. Por favor intenta nuevamente.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExternalLoginCallback");
                TempData["Error"] = "Ocurrió un error durante el login. Por favor intenta nuevamente.";
                return RedirectToAction(nameof(Login));
            }
        }
    }
}