using Lado.Models;
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

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // ========================================
        // REGISTRO
        // ========================================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                _logger.LogInformation("Usuario ya autenticado, redirigiendo a Feed");
                return RedirectToAction("Index", "Feed");
            }

            return View();
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

                // ✅ Verificar que el seudónimo sea único
                var existingSeudonimo = await _userManager.Users
                    .AnyAsync(u => u.Seudonimo == model.Seudonimo);
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
                    Seudonimo = model.Seudonimo, // ⭐ NUEVO campo obligatorio
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
                    _logger.LogInformation("✅ Usuario creado exitosamente: {Username}", usuario.UserName);

                    // Iniciar sesión automáticamente
                    await _signInManager.SignInAsync(usuario, isPersistent: false);

                    _logger.LogInformation("✅ Sesión iniciada para: {Username}", usuario.UserName);

                    TempData["Success"] = "¡Bienvenido a LADO! Tu cuenta ha sido creada exitosamente.";
                    return RedirectToAction("Index", "Feed");
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
                _logger.LogInformation("Usuario ya autenticado, redirigiendo a Feed");
                return RedirectToAction("Index", "Feed");
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

                    // ✅ Verificar si es Admin
                    var roles = await _userManager.GetRolesAsync(user);
                    _logger.LogInformation("Roles del usuario: {Roles}", string.Join(", ", roles));

                    if (roles.Contains("Admin"))
                    {
                        _logger.LogInformation("Redirigiendo a panel de Admin");
                        return RedirectToAction("Index", "Admin");
                    }

                    // ✅ CAMBIO: Feed es la página principal ahora
                    _logger.LogInformation("Redirigiendo a Feed");

                    TempData["Success"] = $"¡Bienvenido de nuevo, {user.NombreCompleto}!";

                    // Si hay returnUrl válido, redirigir ahí
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Feed");
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

            await _signInManager.SignOutAsync();

            _logger.LogInformation("✅ Usuario cerró sesión: {Username}", userName ?? "Unknown");

            TempData["Info"] = "Has cerrado sesión exitosamente.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            var userName = User?.Identity?.Name;

            await _signInManager.SignOutAsync();

            _logger.LogInformation("✅ Usuario cerró sesión (POST): {Username}", userName ?? "Unknown");

            TempData["Info"] = "Has cerrado sesión exitosamente.";
            return RedirectToAction("Index", "Home");
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
        // VERIFICAR DISPONIBILIDAD (AJAX)
        // ========================================

        [HttpPost]
        public async Task<JsonResult> CheckUsernameAvailability(string username)
        {
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
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains("Admin"))
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                }

                return LocalRedirect(returnUrl);
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
                // Vincular el login externo a la cuenta existente
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: true);
                    _logger.LogInformation("Login externo vinculado a usuario existente: {Email}", email);
                    return LocalRedirect(returnUrl);
                }
            }

            // Crear nuevo usuario
            var newUser = new ApplicationUser
            {
                UserName = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                Email = email,
                NombreCompleto = name ?? email.Split('@')[0],
                Seudonimo = email.Split('@')[0] + "_" + Guid.NewGuid().ToString("N").Substring(0, 4),
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
                    await _signInManager.SignInAsync(newUser, isPersistent: true);
                    _logger.LogInformation("Nuevo usuario creado con Google: {Email}", email);
                    TempData["Success"] = "¡Bienvenido a LADO! Tu cuenta ha sido creada con Google.";
                    return RedirectToAction("Index", "Feed");
                }
            }

            foreach (var error in createResult.Errors)
            {
                _logger.LogError("Error creando usuario: {Error}", error.Description);
            }

            TempData["Error"] = "Error al crear la cuenta. Por favor intenta nuevamente.";
            return RedirectToAction(nameof(Login));
        }
    }
}