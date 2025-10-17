using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class FeedController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FeedController> _logger;

        public FeedController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // 1. Obtener creadores suscritos
                var creadoresIds = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                _logger.LogInformation($"Usuario {usuarioId} tiene {creadoresIds.Count} suscripciones activas");

                // 2. Obtener contenido con relaciones - ✅ AGREGADO FILTRO DE CONTENIDO CENSURADO
                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => creadoresIds.Contains(c.UsuarioId)
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado  // ✅ NUEVO: No mostrar contenido censurado
                            && c.Usuario != null)
                    .ToListAsync();

                _logger.LogInformation($"Se encontraron {contenido.Count} contenidos");

                // 3. ALGORITMO MEJORADO: Cronológico con boost de engagement
                var contenidoOrdenado = contenido
                    .Select(c => new {
                        Contenido = c,
                        Score = CalcularScoreMejorado(c)
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Contenido.FechaPublicacion)
                    .Select(x => x.Contenido)
                    .Take(50)
                    .ToList();

                // 4. Establecer ViewBag
                ViewBag.EstaSuscrito = true;

                // 5. Obtener creadores sugeridos
                ViewBag.CreadoresSugeridos = await _userManager.Users
                    .Where(u => u.TipoUsuario == 1
                            && u.Id != usuarioId
                            && !creadoresIds.Contains(u.Id))
                    .OrderBy(u => Guid.NewGuid())
                    .Take(5)
                    .ToListAsync();

                return View(contenidoOrdenado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el feed");
                TempData["Error"] = "Error al cargar el feed. Por favor, intenta nuevamente.";
                return View(new List<Contenido>());
            }
        }

        private double CalcularScoreMejorado(Contenido contenido)
        {
            try
            {
                var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;

                // 1. BASE TEMPORAL
                double baseScore = 100.0 / (1 + horasDesdePublicacion / 24.0);

                if (horasDesdePublicacion < 6)
                {
                    baseScore += 50.0;
                }
                else if (horasDesdePublicacion < 24)
                {
                    baseScore += 25.0;
                }

                // 2. BOOST POR ENGAGEMENT
                double totalEngagement = contenido.NumeroLikes
                                       + (contenido.NumeroComentarios * 2.0)
                                       + (contenido.NumeroVistas * 0.1);

                double engagementBoost = Math.Log(1 + totalEngagement) * 10.0;

                // 3. PENALIZACIÓN POR ANTIGÜEDAD
                double edadPenalizacion = 1.0;
                if (horasDesdePublicacion > 168)
                {
                    edadPenalizacion = 0.5;
                }

                return (baseScore + engagementBoost) * edadPenalizacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular score");
                return 0;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Comentar(int id, string texto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return Json(new { success = false, message = "El comentario no puede estar vacío" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                var comentario = new Comentario
                {
                    ContenidoId = id,
                    UsuarioId = usuarioId,
                    Texto = texto,
                    FechaCreacion = DateTime.Now
                };

                _context.Comentarios.Add(comentario);
                contenido.NumeroComentarios++;

                await _context.SaveChangesAsync();

                var usuario = await _userManager.FindByIdAsync(usuarioId);

                return Json(new
                {
                    success = true,
                    comentario = new
                    {
                        id = comentario.Id,
                        texto = comentario.Texto,
                        usuario = new
                        {
                            nombre = usuario.NombreCompleto,
                            username = usuario.UserName,
                            fotoPerfil = usuario.FotoPerfil
                        },
                        fechaCreacion = comentario.FechaCreacion
                    },
                    totalComentarios = contenido.NumeroComentarios
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al publicar comentario");
                return Json(new { success = false, message = "Error al publicar el comentario" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerComentarios(int id)
        {
            try
            {
                var comentarios = await _context.Comentarios
                    .Include(c => c.Usuario)
                    .Where(c => c.ContenidoId == id)
                    .OrderByDescending(c => c.FechaCreacion)
                    .Select(c => new
                    {
                        id = c.Id,
                        texto = c.Texto,
                        usuario = new
                        {
                            nombre = c.Usuario.NombreCompleto,
                            username = c.Usuario.UserName,
                            fotoPerfil = c.Usuario.FotoPerfil
                        },
                        fechaCreacion = c.FechaCreacion
                    })
                    .ToListAsync();

                return Json(new { success = true, comentarios });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentarios");
                return Json(new { success = false, message = "Error al cargar comentarios" });
            }
        }

        public async Task<IActionResult> Explorar()
        {
            try
            {
                var creadores = await _userManager.Users
                    .Where(u => u.TipoUsuario == 1)
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(50)
                    .ToListAsync();

                return View(creadores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar creadores");
                TempData["Error"] = "Error al cargar creadores";
                return View(new List<ApplicationUser>());
            }
        }

        public async Task<IActionResult> Perfil(string id)
        {
            try
            {
                var creador = await _userManager.FindByIdAsync(id);

                if (creador == null || creador.TipoUsuario != 1)
                {
                    TempData["Error"] = "Creador no encontrado";
                    return RedirectToAction("Index");
                }

                var usuarioActual = await _userManager.GetUserAsync(User);

                var estaSuscrito = await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuarioActual.Id &&
                             s.CreadorId == id &&
                             s.EstaActiva);

                ViewBag.EstaSuscrito = estaSuscrito;

                // ✅ AGREGADO FILTRO DE CONTENIDO CENSURADO
                var contenidos = await _context.Contenidos
                    .Where(c => c.UsuarioId == id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado)  // ✅ NUEVO: No mostrar contenido censurado
                    .OrderByDescending(c => c.FechaPublicacion)
                    .ToListAsync();

                if (!estaSuscrito)
                {
                    contenidos = contenidos.Where(c => !c.EsPremium).ToList();
                }

                ViewBag.Contenidos = contenidos;

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                ViewBag.TotalLikes = contenidos.Sum(c => c.NumeroLikes);
                ViewBag.TotalPublicaciones = contenidos.Count;

                return View(creador);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil del creador");
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Index");
            }
        }

        // ✅ NUEVO: Método Detalle para ver contenido individual con comentarios
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);

                // Obtener contenido con sus relaciones
                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Comentarios)
                        .ThenInclude(com => com.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id
                                           && c.EstaActivo
                                           && !c.EsBorrador
                                           && !c.Censurado);

                if (contenido == null)
                {
                    TempData["Error"] = "Contenido no encontrado";
                    return RedirectToAction("Index");
                }

                // Verificar si el usuario actual está suscrito al creador
                var estaSuscrito = await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuarioActual.Id &&
                                 s.CreadorId == contenido.UsuarioId &&
                                 s.EstaActiva);

                // Si es contenido premium y no está suscrito, denegar acceso
                if (contenido.EsPremium && !estaSuscrito && contenido.UsuarioId != usuarioActual.Id)
                {
                    TempData["Error"] = "Necesitas estar suscrito para ver este contenido";
                    return RedirectToAction("Perfil", new { id = contenido.UsuarioId });
                }

                // Incrementar vistas
                contenido.NumeroVistas++;
                await _context.SaveChangesAsync();

                ViewBag.EstaSuscrito = estaSuscrito;
                ViewBag.EsPropio = contenido.UsuarioId == usuarioActual.Id;

                return View(contenido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle del contenido");
                TempData["Error"] = "Error al cargar el contenido";
                return RedirectToAction("Index");
            }
        }
    }
}