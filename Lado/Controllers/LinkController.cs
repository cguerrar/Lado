using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para la página "Link in Bio" de creadores
    /// URL: /link/@username
    /// </summary>
    public class LinkController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LinkController> _logger;

        public LinkController(
            ApplicationDbContext context,
            ILogger<LinkController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Página Link in Bio para creadores LadoB
        /// GET: /link/@username o /link/username
        /// </summary>
        [Route("link/{username}")]
        [Route("link/@{username}")]
        public async Task<IActionResult> Index(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            // Limpiar el @ si viene
            username = username.TrimStart('@');

            // Buscar usuario por username o seudónimo
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.EstaActivo &&
                    u.EsCreador &&
                    u.CreadorVerificado &&
                    (u.UserName == username ||
                     u.Seudonimo == username ||
                     (u.UserName != null && u.UserName.ToLower() == username.ToLower()) ||
                     (u.Seudonimo != null && u.Seudonimo.ToLower() == username.ToLower())));

            if (usuario == null)
            {
                return NotFound();
            }

            // Incrementar contador de visitas (opcional para analytics)
            usuario.VisitasPerfil++;
            await _context.SaveChangesAsync();

            // Determinar si mostrar LadoB
            var usarLadoB = usuario.TieneLadoB();

            // Preparar datos para la vista
            ViewBag.Usuario = usuario;
            ViewBag.UsarLadoB = usarLadoB;
            ViewBag.NombreDisplay = usarLadoB ? usuario.Seudonimo : usuario.NombreCompleto;
            ViewBag.FotoPerfil = usarLadoB ? (usuario.FotoPerfilLadoB ?? usuario.FotoPerfil) : usuario.FotoPerfil;
            ViewBag.Biografia = usarLadoB ? (usuario.BiografiaLadoB ?? usuario.Biografia) : usuario.Biografia;
            ViewBag.Username = usuario.UserName;
            ViewBag.Seudonimo = usuario.Seudonimo;

            // Redes sociales
            ViewBag.Instagram = usuario.Instagram;
            ViewBag.Twitter = usuario.Twitter;
            ViewBag.TikTok = usuario.TikTok;
            ViewBag.YouTube = usuario.YouTube;
            ViewBag.Facebook = usuario.Facebook;
            ViewBag.OnlyFans = usuario.OnlyFans;

            // Estadísticas públicas
            ViewBag.NumeroSeguidores = usuario.NumeroSeguidores;
            ViewBag.ContenidosPublicados = usuario.ContenidosPublicados;

            // Precio de suscripción
            ViewBag.PrecioSuscripcion = usarLadoB && usuario.PrecioSuscripcionLadoB.HasValue
                ? usuario.PrecioSuscripcionLadoB.Value
                : usuario.PrecioSuscripcion;

            // Meta tags SEO
            ViewData["Title"] = $"{ViewBag.NombreDisplay} - Lado";
            ViewData["MetaDescription"] = !string.IsNullOrEmpty(ViewBag.Biografia)
                ? ViewBag.Biografia
                : $"Encuentra a {ViewBag.NombreDisplay} en Lado. Contenido exclusivo y más.";
            ViewData["OgImage"] = ViewBag.FotoPerfil ?? "/images/og-default.jpg";
            ViewData["OgType"] = "profile";

            return View();
        }
    }
}
