using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para perfiles públicos accesibles via /@{username}
    /// No requiere autenticación
    /// </summary>
    public class PerfilPublicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PerfilPublicoController> _logger;

        public PerfilPublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<PerfilPublicoController> logger)
        {
            _context = context;
            _userManager = userManager;
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

            // Buscar usuario por UserName o Seudonimo
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u =>
                    (u.UserName == username || u.Seudonimo == username) &&
                    u.EstaActivo);

            if (usuario == null)
            {
                _logger.LogWarning("Perfil público no encontrado: @{Username}", username);
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation("Visitando perfil público: @{Username}", username);

            // Verificar si el visitante está autenticado y suscrito
            var usuarioActual = await _userManager.GetUserAsync(User);
            var estaSuscrito = false;
            var esPropietario = false;

            if (usuarioActual != null)
            {
                esPropietario = usuarioActual.Id == usuario.Id;

                if (!esPropietario)
                {
                    estaSuscrito = await _context.Suscripciones
                        .AnyAsync(s =>
                            s.CreadorId == usuario.Id &&
                            s.FanId == usuarioActual.Id &&
                            s.EstaActiva);
                }
            }

            // Obtener contenido según nivel de acceso
            List<Contenido> contenidoVisible;

            if (usuarioActual == null)
            {
                // NO AUTENTICADO: Solo contenido marcado como público general
                contenidoVisible = await _context.Contenidos
                    .Include(c => c.Likes)
                    .Include(c => c.Comentarios)
                    .Where(c => c.UsuarioId == usuario.Id &&
                               c.EstaActivo &&
                               !c.EsBorrador &&
                               c.EsPublicoGeneral)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(50)
                    .ToListAsync();
            }
            else if (estaSuscrito || esPropietario)
            {
                // SUSCRITO o PROPIETARIO: Todo el contenido (Lado A + Lado B)
                contenidoVisible = await _context.Contenidos
                    .Include(c => c.Likes)
                    .Include(c => c.Comentarios)
                    .Where(c => c.UsuarioId == usuario.Id &&
                               c.EstaActivo &&
                               !c.EsBorrador)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(50)
                    .ToListAsync();
            }
            else
            {
                // AUTENTICADO NO SUSCRITO: Lado A + contenido público general
                contenidoVisible = await _context.Contenidos
                    .Include(c => c.Likes)
                    .Include(c => c.Comentarios)
                    .Where(c => c.UsuarioId == usuario.Id &&
                               c.EstaActivo &&
                               !c.EsBorrador &&
                               (c.TipoLado == TipoLado.LadoA || c.EsPublicoGeneral))
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(50)
                    .ToListAsync();
            }

            // Contar seguidores
            var totalSeguidores = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuario.Id && s.EstaActiva);

            // Contar contenido total
            var totalContenido = await _context.Contenidos
                .CountAsync(c => c.UsuarioId == usuario.Id && c.EstaActivo && !c.EsBorrador);

            // Incrementar contador de visitas al perfil (solo si no es el propietario)
            if (!esPropietario)
            {
                usuario.VisitasPerfil++;
                await _context.SaveChangesAsync();
            }

            // SEO Meta Tags dinamicos
            var nombreDisplay = usuario.NombreCompleto ?? usuario.UserName ?? username;
            var descripcionPerfil = !string.IsNullOrEmpty(usuario.Biografia)
                ? usuario.Biografia
                : $"Perfil de {nombreDisplay} en Lado. {totalContenido} publicaciones, {totalSeguidores} seguidores.";

            ViewData["Title"] = $"{nombreDisplay} (@{usuario.UserName})";
            ViewData["MetaDescription"] = descripcionPerfil.Length > 160
                ? descripcionPerfil.Substring(0, 157) + "..."
                : descripcionPerfil;
            ViewData["MetaKeywords"] = $"{nombreDisplay}, {usuario.UserName}, perfil, creador, lado";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/@{usuario.UserName}";
            ViewData["OgTitle"] = nombreDisplay;
            ViewData["OgDescription"] = descripcionPerfil;
            ViewData["OgType"] = "profile";
            ViewData["OgUrl"] = $"{Request.Scheme}://{Request.Host}/@{usuario.UserName}";

            // Imagen de perfil para OG
            if (!string.IsNullOrEmpty(usuario.FotoPerfil))
            {
                var fotoUrl = usuario.FotoPerfil.StartsWith("http")
                    ? usuario.FotoPerfil
                    : $"{Request.Scheme}://{Request.Host}{usuario.FotoPerfil}";
                ViewData["OgImage"] = fotoUrl;
            }

            // Schema.org Person
            ViewData["SchemaType"] = "Person";
            ViewData["SchemaName"] = nombreDisplay;
            ViewData["SchemaDescription"] = descripcionPerfil;

            // Preparar ViewModel
            var viewModel = new PerfilPublicoViewModel
            {
                Usuario = usuario,
                Contenidos = contenidoVisible,
                TotalSeguidores = totalSeguidores,
                TotalContenido = totalContenido,
                EstaSuscrito = estaSuscrito,
                EsPropietario = esPropietario,
                EstaAutenticado = usuarioActual != null
            };

            return View(viewModel);
        }
    }

    // ViewModel para el perfil público
    public class PerfilPublicoViewModel
    {
        public ApplicationUser Usuario { get; set; } = null!;
        public List<Contenido> Contenidos { get; set; } = new();
        public int TotalSeguidores { get; set; }
        public int TotalContenido { get; set; }
        public bool EstaSuscrito { get; set; }
        public bool EsPropietario { get; set; }
        public bool EstaAutenticado { get; set; }
    }
}
