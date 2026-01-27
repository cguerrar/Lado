using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para perfiles públicos accesibles via /@{username}
    /// Redirige a vista pública o privada según tipo de creador y autenticación
    /// </summary>
    public class PerfilPublicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PerfilPublicoController> _logger;

        public PerfilPublicoController(
            ApplicationDbContext context,
            ILogger<PerfilPublicoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /@{username}
        [Route("@{username}")]
        public async Task<IActionResult> Index(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("Index", "Home");
            }

            // Buscar usuario por UserName o Seudonimo (case-insensitive)
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.EstaActivo &&
                    (u.UserName == username ||
                     u.Seudonimo == username ||
                     (u.UserName != null && u.UserName.ToLower() == username.ToLower()) ||
                     (u.Seudonimo != null && u.Seudonimo.ToLower() == username.ToLower())));

            if (usuario == null)
            {
                _logger.LogWarning("Perfil público no encontrado: @{Username}", username);
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index", "Home");
            }

            // Determinar si se accedió via Seudonimo (LadoB) o UserName (LadoA)
            var accesoViaSeudonimo = !string.IsNullOrEmpty(usuario.Seudonimo) &&
                usuario.Seudonimo.Equals(username, StringComparison.OrdinalIgnoreCase);

            var estaAutenticado = User.Identity?.IsAuthenticated ?? false;
            var esCreadorLadoBVerificado = usuario.EsCreador && usuario.CreadorVerificado && usuario.TieneLadoB();

            // Si el perfil es privado y el usuario no está autenticado → redirigir al login
            if (usuario.PerfilPrivado && !estaAutenticado)
            {
                _logger.LogInformation("Perfil privado /@{Username} → redirigir a login (ID: {Id})",
                    username, usuario.Id);
                TempData["Info"] = "Este perfil es privado. Inicia sesión para verlo.";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/@{username}" });
            }

            // Si accedió via Seudonimo y es creador LadoB verificado → mostrar perfil LadoB
            if (accesoViaSeudonimo && esCreadorLadoBVerificado)
            {
                _logger.LogInformation("Acceso via Seudonimo /@{Seudonimo} → perfil LadoB", username);
                // ⭐ SEGURIDAD: Pasar seudónimo como identificador, NO el user ID real
                return RedirectToAction("VerPerfil", "FeedPublico", new { id = usuario.Seudonimo, ladoB = true });
            }

            // Si accedió via UserName → mostrar perfil LadoA (público/gratuito)
            if (!accesoViaSeudonimo)
            {
                _logger.LogInformation("Acceso via UserName /@{Username} → perfil LadoA", username);

                // Si está autenticado → perfil privado en Feed
                if (estaAutenticado)
                {
                    return RedirectToAction("Perfil", "Feed", new { id = usuario.Id, verSeudonimo = false });
                }

                // Si no está autenticado → mostrar perfil público LadoA
                return RedirectToAction("VerPerfil", "FeedPublico", new { id = usuario.Id, ladoB = false });
            }

            // Fallback: Si accedió via Seudonimo pero no es LadoB verificado → redirigir a LadoA
            _logger.LogWarning("Acceso via Seudonimo pero usuario no tiene LadoB activo: {Username}", username);
            return RedirectToAction("VerPerfil", "FeedPublico", new { id = usuario.Id });
        }
    }
}
