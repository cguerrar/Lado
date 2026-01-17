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
            ViewBag.UsuarioId = usuario.Id;

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

            // === NUEVO: Cargar contenido premium para preview (solo LadoB) ===
            if (usarLadoB)
            {
                // Obtener contenido LadoB más reciente para mostrar como preview
                var contenidoPreview = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuario.Id &&
                                c.TipoLado == TipoLado.LadoB &&
                                !c.Censurado &&
                                !c.OcultoSilenciosamente &&
                                !c.EsPrivado)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(6)
                    .Select(c => new {
                        c.Id,
                        c.RutaArchivo,
                        c.Thumbnail,
                        c.TipoContenido,
                        c.NumeroLikes,
                        c.NumeroComentarios,
                        Precio = c.PrecioDesbloqueo ?? usuario.PrecioSuscripcionLadoB ?? 0
                    })
                    .ToListAsync();

                ViewBag.ContenidoPreview = contenidoPreview;

                // Total de likes del creador en LadoB
                var totalLikes = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuario.Id && c.TipoLado == TipoLado.LadoB)
                    .SumAsync(c => c.NumeroLikes);
                ViewBag.TotalLikes = totalLikes;

                // Contar fotos y videos
                var conteoTipos = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuario.Id &&
                                c.TipoLado == TipoLado.LadoB &&
                                !c.Censurado &&
                                !c.OcultoSilenciosamente)
                    .GroupBy(c => c.TipoContenido)
                    .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                    .ToListAsync();

                ViewBag.TotalFotos = conteoTipos.FirstOrDefault(x => x.Tipo == TipoContenido.Foto)?.Cantidad ?? 0;
                ViewBag.TotalVideos = conteoTipos.FirstOrDefault(x => x.Tipo == TipoContenido.Video)?.Cantidad ?? 0;
            }
            else
            {
                ViewBag.ContenidoPreview = new List<object>();
                ViewBag.TotalLikes = 0;
                ViewBag.TotalFotos = 0;
                ViewBag.TotalVideos = 0;
            }

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
