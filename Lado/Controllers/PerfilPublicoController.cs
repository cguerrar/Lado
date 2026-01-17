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

            var estaAutenticado = User.Identity?.IsAuthenticated ?? false;
            var esCreadorLadoBVerificado = usuario.EsCreador && usuario.CreadorVerificado && usuario.TieneLadoB();

            // Si es creador LadoB verificado → siempre mostrar perfil público
            // para que cualquier persona pueda verlo y suscribirse
            if (esCreadorLadoBVerificado)
            {
                _logger.LogInformation("Redirigiendo /@{Username} a perfil público LadoB (ID: {Id})",
                    username, usuario.Id);
                return RedirectToAction("VerPerfil", "FeedPublico", new { id = usuario.Id });
            }

            // Si está autenticado → perfil privado en Feed
            if (estaAutenticado)
            {
                return RedirectToAction("Perfil", "Feed", new { id = usuario.Id });
            }

            // Si no está autenticado y no es LadoB → mostrar perfil público básico
            return RedirectToAction("VerPerfil", "FeedPublico", new { id = usuario.Id });
        }
    }
}
