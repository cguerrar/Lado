using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class StoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StoriesController> _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly IFileValidationService _fileValidationService;

        public StoriesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StoriesController> logger,
            IRateLimitService rateLimitService,
            IFileValidationService fileValidationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _rateLimitService = rateLimitService;
            _fileValidationService = fileValidationService;
        }

        /// <summary>
        /// Obtener stories activas de creadores suscritos
        /// </summary>
        [HttpGet("Obtener")]
        public async Task<IActionResult> ObtenerStories()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Obtener IDs de creadores suscritos
                var creadoresIds = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Agregar el propio usuario para ver sus propias stories
                if (!creadoresIds.Contains(usuarioId))
                {
                    creadoresIds.Add(usuarioId);
                }

                // Stories activas (últimas 24h)
                var ahora = DateTime.Now;
                var stories = await _context.Stories
                    .Include(s => s.Creador)
                    .Where(s => creadoresIds.Contains(s.CreadorId)
                            && s.FechaExpiracion > ahora
                            && s.EstaActivo)
                    .OrderByDescending(s => s.FechaPublicacion)
                    .ToListAsync();

                // Marcar cuáles ya vio el usuario
                var storiesVistos = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == usuarioId)
                    .Select(sv => sv.StoryId)
                    .ToListAsync();

                // Agrupar por creador
                var storiesPorCreador = stories
                    .GroupBy(s => s.CreadorId)
                    .Select(g => new
                    {
                        creadorId = g.Key,
                        creador = g.First().Creador,
                        stories = g.Select(s => new
                        {
                            id = s.Id,
                            rutaArchivo = s.RutaArchivo,
                            texto = s.Texto,
                            tipo = s.TipoContenido.ToString(),
                            visto = storiesVistos.Contains(s.Id),
                            fechaPublicacion = s.FechaPublicacion,
                            expiraEn = (s.FechaExpiracion - ahora).TotalHours
                        }).ToList(),
                        tieneStorysSinVer = g.Any(s => !storiesVistos.Contains(s.Id))
                    })
                    .OrderByDescending(g => g.tieneStorysSinVer)
                    .ThenByDescending(g => g.stories.Max(s => s.fechaPublicacion))
                    .ToList();

                _logger.LogInformation("Stories obtenidas para usuario {UserId}: {Count} creadores",
                    usuarioId, storiesPorCreador.Count);

                return Json(new
                {
                    success = true,
                    stories = storiesPorCreador.Select(g => new
                    {
                        creadorId = g.creadorId,
                        creador = new
                        {
                            id = g.creador.Id,
                            nombre = g.creador.NombreCompleto,
                            username = g.creador.UserName,
                            avatar = g.creador.FotoPerfil
                        },
                        stories = g.stories,
                        tieneStorysSinVer = g.tieneStorysSinVer
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stories");
                return Json(new { success = false, message = "Error al cargar stories" });
            }
        }

        /// <summary>
        /// Marcar una story como vista
        /// </summary>
        [HttpPost("MarcarVisto/{storyId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarStoryVisto(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var story = await _context.Stories.FindAsync(storyId);

                if (story == null || !story.EstaActivo)
                {
                    return Json(new { success = false, message = "Story no encontrada" });
                }

                // Verificar si ya la vio
                var yaVisto = await _context.StoryVistas
                    .AnyAsync(sv => sv.StoryId == storyId && sv.UsuarioId == usuarioId);

                if (!yaVisto)
                {
                    var vista = new StoryVista
                    {
                        StoryId = storyId,
                        UsuarioId = usuarioId,
                        FechaVista = DateTime.Now
                    };

                    _context.StoryVistas.Add(vista);
                    story.NumeroVistas++;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Story {StoryId} vista por usuario {UserId}", storyId, usuarioId);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar story como vista: {StoryId}", storyId);
                return Json(new { success = false, message = "Error al procesar la vista" });
            }
        }

        /// <summary>
        /// Crear una nueva story (solo creadores)
        /// </summary>
        [HttpPost("Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearStory(IFormFile archivo, string? texto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"story_create_{usuarioId}";
                var rateLimitKeyIp = $"story_create_ip_{clientIp}";

                // Límite por IP: máximo 20 stories por 5 minutos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window,
                    TipoAtaque.SpamContenido, "/Stories/Crear", usuarioId))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP STORY: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuarioId);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Espera unos minutos." });
                }

                // Límite por usuario: máximo 10 stories por 5 minutos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Stories/Crear", usuarioId))
                {
                    _logger.LogWarning("🚫 RATE LIMIT STORY: Usuario {UserId} excedió límite de 5 min - IP: {IP}", usuarioId, clientIp);
                    return Json(new { success = false, message = "Has creado demasiadas stories en poco tiempo. Espera unos minutos." });
                }

                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, message = "Debe subir un archivo" });
                }

                // ✅ SEGURIDAD: Validar archivo con magic bytes (no solo extensión)
                var validacionArchivo = await _fileValidationService.ValidarMediaAsync(archivo);
                if (!validacionArchivo.EsValido)
                {
                    _logger.LogWarning("⚠️ Archivo rechazado en Story: {FileName}, Error: {Error}",
                        archivo.FileName, validacionArchivo.MensajeError);
                    return Json(new { success = false, message = validacionArchivo.MensajeError ?? "Archivo no válido" });
                }

                // Determinar tipo de contenido basado en validación real
                var tipoContenido = validacionArchivo.Tipo == TipoArchivoValidacion.Video
                    ? TipoContenido.Video
                    : TipoContenido.Imagen;

                // Guardar archivo (implementar tu lógica de guardado)
                var rutaArchivo = await GuardarArchivoStory(archivo, usuarioId);

                // Crear story
                var story = new Story
                {
                    CreadorId = usuarioId,
                    RutaArchivo = rutaArchivo,
                    TipoContenido = tipoContenido,
                    Texto = texto,
                    FechaPublicacion = DateTime.Now,
                    FechaExpiracion = DateTime.Now.AddHours(24),
                    EstaActivo = true,
                    NumeroVistas = 0
                };

                _context.Stories.Add(story);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Story creada: {StoryId} por usuario {UserId}", story.Id, usuarioId);

                return Json(new
                {
                    success = true,
                    storyId = story.Id,
                    message = "Story publicada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear story");
                return Json(new { success = false, message = "Error al crear la story" });
            }
        }

        /// <summary>
        /// Eliminar una story propia
        /// </summary>
        [HttpPost("Eliminar/{storyId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarStory(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var story = await _context.Stories.FindAsync(storyId);

                if (story == null)
                {
                    return Json(new { success = false, message = "Story no encontrada" });
                }

                if (story.CreadorId != usuarioId)
                {
                    return Json(new { success = false, message = "No tienes permiso para eliminar esta story" });
                }

                story.EstaActivo = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Story {StoryId} eliminada por usuario {UserId}", storyId, usuarioId);

                return Json(new { success = true, message = "Story eliminada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar story: {StoryId}", storyId);
                return Json(new { success = false, message = "Error al eliminar la story" });
            }
        }

        /// <summary>
        /// Obtener vistas de una story propia
        /// </summary>
        [HttpGet("Vistas/{storyId}")]
        public async Task<IActionResult> ObtenerVistas(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var story = await _context.Stories.FindAsync(storyId);

                if (story == null || story.CreadorId != usuarioId)
                {
                    return Json(new { success = false, message = "No autorizado" });
                }

                var vistas = await _context.StoryVistas
                    .Include(sv => sv.Usuario)
                    .Where(sv => sv.StoryId == storyId)
                    .OrderByDescending(sv => sv.FechaVista)
                    .Select(sv => new
                    {
                        usuario = new
                        {
                            id = sv.Usuario.Id,
                            nombre = sv.Usuario.NombreCompleto,
                            username = sv.Usuario.UserName,
                            avatar = sv.Usuario.FotoPerfil
                        },
                        fechaVista = sv.FechaVista
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    totalVistas = vistas.Count,
                    vistas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener vistas de story: {StoryId}", storyId);
                return Json(new { success = false, message = "Error al cargar vistas" });
            }
        }

        // ========================================
        // MÉTODOS AUXILIARES
        // ========================================

        private async Task<string> GuardarArchivoStory(IFormFile archivo, string usuarioId)
        {
            // Implementar tu lógica de guardado de archivos
            // Puede ser en servidor local, Azure Blob Storage, AWS S3, etc.

            var nombreArchivo = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
            var carpeta = Path.Combine("wwwroot", "stories", usuarioId);

            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return $"/stories/{usuarioId}/{nombreArchivo}";
        }
    }
}