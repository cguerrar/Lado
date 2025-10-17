using Lado.Models;
using Lado.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Dashboard");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = new ApplicationUser
            {
                UserName = model.NombreUsuario,
                Email = model.Email,
                NombreCompleto = model.NombreCompleto,
                TipoUsuario = (int)model.TipoUsuario,
                FechaRegistro = DateTime.Now,
                EstaActivo = true
            };

            var result = await _userManager.CreateAsync(usuario, model.Contraseña);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(usuario, isPersistent: false);
                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Dashboard");

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

                // ✅ BUSCAR USUARIO POR EMAIL O USERNAME
                if (model.EmailOUsuario.Contains("@"))
                {
                    _logger.LogInformation("Buscando por email: {Email}", model.EmailOUsuario);
                    user = await _userManager.FindByEmailAsync(model.EmailOUsuario);
                }
                else
                {
                    _logger.LogInformation("Buscando por username: {Username}", model.EmailOUsuario);
                    user = await _userManager.FindByNameAsync(model.EmailOUsuario);
                }

                if (user == null)
                {
                    _logger.LogWarning("Usuario no encontrado: {EmailOUsuario}", model.EmailOUsuario);
                    ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
                    return View(model);
                }

                _logger.LogInformation("Usuario encontrado: {Username} ({Email})", user.UserName, user.Email);

                // ✅ VERIFICAR CONTRASEÑA
                var passwordCheck = await _userManager.CheckPasswordAsync(user, model.Contraseña);
                if (!passwordCheck)
                {
                    _logger.LogWarning("Contraseña incorrecta para usuario: {Username}", user.UserName);
                    ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
                    return View(model);
                }

                _logger.LogInformation("Contraseña correcta. Intentando login...");

                // ✅ HACER LOGIN
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    model.Contraseña,
                    isPersistent: model.Recordarme,
                    lockoutOnFailure: false
                );

                if (result.Succeeded)
                {
                    _logger.LogInformation("Login exitoso para: {Username}", user.UserName);

                    // ✅ VERIFICAR ROL DE ADMIN
                    var roles = await _userManager.GetRolesAsync(user);
                    _logger.LogInformation("Roles del usuario: {Roles}", string.Join(", ", roles));

                    if (roles.Contains("Admin"))
                    {
                        _logger.LogInformation("Redirigiendo a Admin panel");
                        return RedirectToAction("Index", "Admin");
                    }

                    // Redireccionar según tipo de usuario
                    if (user.TipoUsuario == 1) // Creador
                    {
                        _logger.LogInformation("Redirigiendo a Dashboard (Creador)");
                        return RedirectToAction("Index", "Dashboard");
                    }
                    else // Fan
                    {
                        _logger.LogInformation("Redirigiendo a Feed (Fan)");
                        return RedirectToAction("Index", "Feed");
                    }
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Cuenta bloqueada: {Username}", user.UserName);
                    return View("Lockout");
                }

                _logger.LogError("Login falló para: {Username}. Result: {Result}", user.UserName, result);
                ModelState.AddModelError(string.Empty, "Error al iniciar sesión.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción en Login para: {EmailOUsuario}", model.EmailOUsuario);
                ModelState.AddModelError(string.Empty, "Error del servidor. Por favor intenta nuevamente.");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}