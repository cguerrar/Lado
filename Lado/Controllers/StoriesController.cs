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

                // Verificar si el usuario tiene BloquearLadoB activado
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                var bloquearLadoB = usuario?.BloquearLadoB ?? false;

                // Stories activas (últimas 24h)
                var ahora = DateTime.Now;
                var storiesQuery = _context.Stories
                    .Include(s => s.Creador)
                    .Include(s => s.PistaMusical)
                    .Where(s => creadoresIds.Contains(s.CreadorId)
                            && s.FechaExpiracion > ahora
                            && s.EstaActivo);

                // Filtrar stories LadoB si el usuario lo tiene bloqueado
                if (bloquearLadoB)
                {
                    storiesQuery = storiesQuery.Where(s => s.TipoLado != TipoLado.LadoB);
                }

                var stories = await storiesQuery
                    .OrderByDescending(s => s.FechaPublicacion)
                    .ToListAsync();

                // Marcar cuáles ya vio el usuario
                var storiesVistos = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == usuarioId)
                    .Select(sv => sv.StoryId)
                    .ToListAsync();

                // Obtener stories que el usuario le dio like
                var storiesConLike = await _context.StoryLikes
                    .Where(sl => sl.UsuarioId == usuarioId)
                    .Select(sl => sl.StoryId)
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
                            expiraEn = (s.FechaExpiracion - ahora).TotalHours,
                            // Datos del editor visual
                            elementosJson = s.ElementosJson,
                            tieneOverlays = !string.IsNullOrEmpty(s.ElementosJson),
                            // Música
                            musica = s.PistaMusical != null ? new
                            {
                                id = s.PistaMusical.Id,
                                titulo = s.PistaMusical.Titulo,
                                artista = s.PistaMusical.Artista,
                                audioUrl = s.PistaMusical.RutaArchivo,
                                inicioSegundos = s.MusicaInicioSegundos ?? 0
                            } : null,
                            // Likes
                            likes = s.NumeroLikes,
                            liked = storiesConLike.Contains(s.Id)
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
        public async Task<IActionResult> CrearStory(
            IFormFile archivo,
            string? texto,
            string? tipoLado,
            string? elementosJson,
            string? mencionesIds,
            int? pistaMusicalId,
            int? musicaInicioSegundos)
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

                // Determinar TipoLado
                var tipoLadoFinal = TipoLado.LadoA;
                if (!string.IsNullOrEmpty(tipoLado) && Enum.TryParse<TipoLado>(tipoLado, out var parsedTipoLado))
                {
                    tipoLadoFinal = parsedTipoLado;
                }

                // Crear story
                var story = new Story
                {
                    CreadorId = usuarioId,
                    RutaArchivo = rutaArchivo,
                    TipoContenido = tipoContenido,
                    Texto = texto,
                    TipoLado = tipoLadoFinal,
                    FechaPublicacion = DateTime.Now,
                    FechaExpiracion = DateTime.Now.AddHours(24),
                    EstaActivo = true,
                    NumeroVistas = 0,
                    // Campos del editor visual
                    ElementosJson = elementosJson,
                    MencionesIds = mencionesIds,
                    PistaMusicalId = pistaMusicalId,
                    MusicaInicioSegundos = musicaInicioSegundos
                };

                _context.Stories.Add(story);
                await _context.SaveChangesAsync();

                // Enviar notificaciones a usuarios mencionados
                if (!string.IsNullOrEmpty(mencionesIds))
                {
                    await EnviarNotificacionesMenciones(story, mencionesIds, usuarioId);
                }

                // Incrementar uso de pista musical
                if (pistaMusicalId.HasValue)
                {
                    var pista = await _context.PistasMusica.FindAsync(pistaMusicalId.Value);
                    if (pista != null)
                    {
                        pista.ContadorUsos++;
                        await _context.SaveChangesAsync();
                    }
                }

                _logger.LogInformation("Story creada: {StoryId} por usuario {UserId} (Editor: {TieneEditor})",
                    story.Id, usuarioId, !string.IsNullOrEmpty(elementosJson));

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

        /// <summary>
        /// Crear story desde un post existente
        /// </summary>
        [HttpPost("CrearDesdePost")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearDesdePost(int postId, string? texto)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Rate limiting
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"story_create_{usuarioId}";

                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Stories/CrearDesdePost", usuarioId))
                {
                    return Json(new { success = false, message = "Has creado demasiadas stories en poco tiempo. Espera unos minutos." });
                }

                // Obtener el post original
                var post = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == postId && c.EstaActivo);

                if (post == null)
                {
                    return Json(new { success = false, message = "Post no encontrado" });
                }

                // Verificar que el usuario tiene acceso al post
                var tieneAcceso = post.UsuarioId == usuarioId;
                if (!tieneAcceso)
                {
                    var suscripcion = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioId && s.CreadorId == post.UsuarioId && s.EstaActiva);
                    tieneAcceso = suscripcion || post.TipoLado == TipoLado.LadoA;
                }

                if (!tieneAcceso)
                {
                    return Json(new { success = false, message = "No tienes acceso a este contenido" });
                }

                // Verificar que el post tiene media
                if (string.IsNullOrEmpty(post.RutaArchivo))
                {
                    return Json(new { success = false, message = "El post no tiene imagen o video" });
                }

                // Crear story usando el mismo archivo del post
                var story = new Story
                {
                    CreadorId = usuarioId,
                    RutaArchivo = post.RutaArchivo,
                    TipoContenido = post.TipoContenido,
                    Texto = texto ?? "",
                    TipoLado = post.TipoLado,
                    FechaPublicacion = DateTime.Now,
                    FechaExpiracion = DateTime.Now.AddHours(24),
                    EstaActivo = true,
                    NumeroVistas = 0
                };

                _context.Stories.Add(story);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Story creada desde post {PostId} por usuario {UserId}", postId, usuarioId);

                return Json(new
                {
                    success = true,
                    storyId = story.Id,
                    message = "Compartido en tu historia"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear story desde post {PostId}", postId);
                return Json(new { success = false, message = "Error al crear la historia" });
            }
        }

        // ========================================
        // EDITOR DE STORIES
        // ========================================

        /// <summary>
        /// Vista del editor unificado de stories/reels con herramientas visuales
        /// </summary>
        /// <param name="modo">story (default) o reel</param>
        [HttpGet("Editor")]
        public IActionResult Editor(string modo = "story")
        {
            ViewBag.Modo = modo;
            ViewBag.Titulo = modo == "reel" ? "Crear Reel" : "Crear Historia";
            ViewBag.TextoPublicar = modo == "reel" ? "Publicar Reel" : "Publicar";
            return View();
        }

        /// <summary>
        /// Crear un Reel (contenido permanente) desde el editor unificado
        /// </summary>
        [HttpPost("CrearReel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearReel(
            IFormFile archivo,
            string? descripcion,
            string? tipoLado,
            string? elementosJson)
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
                var rateLimitKey = $"reel_create_{usuarioId}";
                var rateLimitKeyIp = $"reel_create_ip_{clientIp}";

                // Límite por IP: máximo 20 reels por 5 minutos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window,
                    TipoAtaque.SpamContenido, "/Stories/CrearReel", usuarioId))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP REEL: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuarioId);
                    return Json(new { success = false, message = "Demasiadas solicitudes desde tu conexión. Espera unos minutos." });
                }

                // Límite por usuario: máximo 10 reels por 5 minutos
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window,
                    TipoAtaque.SpamContenido, "/Stories/CrearReel", usuarioId))
                {
                    _logger.LogWarning("🚫 RATE LIMIT REEL: Usuario {UserId} excedió límite de 5 min - IP: {IP}", usuarioId, clientIp);
                    return Json(new { success = false, message = "Has creado demasiados reels en poco tiempo. Espera unos minutos." });
                }

                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, message = "Debe subir un archivo" });
                }

                // ✅ SEGURIDAD: Validar archivo con magic bytes (no solo extensión)
                var validacionArchivo = await _fileValidationService.ValidarMediaAsync(archivo);
                if (!validacionArchivo.EsValido)
                {
                    _logger.LogWarning("⚠️ Archivo rechazado en Reel: {FileName}, Error: {Error}",
                        archivo.FileName, validacionArchivo.MensajeError);
                    return Json(new { success = false, message = validacionArchivo.MensajeError ?? "Archivo no válido" });
                }

                // Determinar tipo de contenido basado en validación real
                var tipoContenido = validacionArchivo.Tipo == TipoArchivoValidacion.Video
                    ? TipoContenido.Video
                    : TipoContenido.Imagen;

                // Obtener usuario para la carpeta
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                var nombreCarpeta = usuario?.UserName ?? usuarioId;

                // Guardar archivo en carpeta de uploads
                var nombreArchivo = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
                var carpeta = Path.Combine("wwwroot", "uploads", nombreCarpeta);

                if (!Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await archivo.CopyToAsync(stream);
                }

                var rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";

                // Determinar TipoLado
                var tipoLadoFinal = TipoLado.LadoA;
                if (!string.IsNullOrEmpty(tipoLado) && Enum.TryParse<TipoLado>(tipoLado, out var parsedTipoLado))
                {
                    tipoLadoFinal = parsedTipoLado;
                }

                // Crear contenido como Reel
                var contenido = new Contenido
                {
                    UsuarioId = usuarioId,
                    RutaArchivo = rutaArchivo,
                    TipoContenido = tipoContenido,
                    Descripcion = descripcion ?? "",
                    TipoLado = tipoLadoFinal,
                    EsReel = true,
                    FechaPublicacion = DateTime.Now,
                    EstaActivo = true,
                    NumeroLikes = 0,
                    NumeroComentarios = 0,
                    NumeroCompartidos = 0
                };

                _context.Contenidos.Add(contenido);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Reel creado: {ContenidoId} por usuario {UserId} (Editor unificado)",
                    contenido.Id, usuarioId);

                return Json(new
                {
                    success = true,
                    contenidoId = contenido.Id,
                    message = "Reel publicado exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear reel");
                return Json(new { success = false, message = "Error al crear el reel" });
            }
        }

        /// <summary>
        /// Buscar usuarios para menciones en stories
        /// </summary>
        [HttpGet("BuscarUsuarios")]
        public async Task<IActionResult> BuscarUsuarios(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Json(new { success = true, usuarios = new List<object>() });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var queryLower = query.ToLower();

                var usuarios = await _context.Users
                    .Where(u => u.Id != usuarioId &&
                               (u.UserName!.ToLower().Contains(queryLower) ||
                                u.NombreCompleto.ToLower().Contains(queryLower)))
                    .Take(10)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        nombre = u.NombreCompleto,
                        avatar = u.FotoPerfil,
                        esCreador = u.EsCreador
                    })
                    .ToListAsync();

                return Json(new { success = true, usuarios });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios para menciones");
                return Json(new { success = false, message = "Error al buscar usuarios" });
            }
        }

        /// <summary>
        /// Buscar pistas musicales para stories
        /// </summary>
        [HttpGet("BuscarMusica")]
        public async Task<IActionResult> BuscarMusica(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    // Devolver pistas populares si no hay query
                    var pistasPopulares = await _context.PistasMusica
                        .Where(p => p.Activo)
                        .OrderByDescending(p => p.ContadorUsos)
                        .Take(10)
                        .Select(p => new
                        {
                            id = p.Id,
                            titulo = p.Titulo,
                            artista = p.Artista,
                            duracion = p.Duracion,
                            portada = p.RutaPortada,
                            audioUrl = p.RutaArchivo
                        })
                        .ToListAsync();

                    return Json(new { success = true, pistas = pistasPopulares });
                }

                var queryLower = query.ToLower();

                var pistas = await _context.PistasMusica
                    .Where(p => p.Activo &&
                               (p.Titulo.ToLower().Contains(queryLower) ||
                                p.Artista.ToLower().Contains(queryLower)))
                    .Take(15)
                    .Select(p => new
                    {
                        id = p.Id,
                        titulo = p.Titulo,
                        artista = p.Artista,
                        duracion = p.Duracion,
                        portada = p.RutaPortada,
                        audioUrl = p.RutaArchivo
                    })
                    .ToListAsync();

                return Json(new { success = true, pistas });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar música");
                return Json(new { success = false, message = "Error al buscar música" });
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

        private async Task EnviarNotificacionesMenciones(Story story, string mencionesIds, string creadorId)
        {
            try
            {
                var ids = mencionesIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var creador = await _userManager.FindByIdAsync(creadorId);

                foreach (var id in ids)
                {
                    if (id != creadorId) // No notificar al creador si se menciona a sí mismo
                    {
                        var notificacion = new Notificacion
                        {
                            UsuarioId = id.Trim(),
                            Tipo = TipoNotificacion.MencionEnStory,
                            Titulo = "Te mencionaron en una historia",
                            Mensaje = $"@{creador?.UserName ?? "Alguien"} te mencionó en su historia",
                            UrlDestino = $"/Feed?story={story.CreadorId}",
                            UsuarioOrigenId = creadorId,
                            ImagenUrl = creador?.FotoPerfil,
                            FechaCreacion = DateTime.Now,
                            Leida = false
                        };

                        _context.Notificaciones.Add(notificacion);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificaciones de menciones en story {StoryId}", story.Id);
            }
        }

        // ========================================
        // SISTEMA DE LIKES EN STORIES
        // ========================================

        /// <summary>
        /// Da like a una story
        /// </summary>
        [HttpPost("DarLike/{storyId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DarLike(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "No autenticado" });

                var story = await _context.Stories
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == storyId && s.EstaActivo);

                if (story == null)
                    return Json(new { success = false, message = "Story no encontrada" });

                // Verificar si ya dio like
                var likeExistente = await _context.StoryLikes
                    .FirstOrDefaultAsync(l => l.StoryId == storyId && l.UsuarioId == usuarioId);

                if (likeExistente != null)
                    return Json(new { success = true, liked = true, likes = story.NumeroLikes, message = "Ya diste like" });

                // Crear like
                var nuevoLike = new StoryLike
                {
                    StoryId = storyId,
                    UsuarioId = usuarioId,
                    FechaLike = DateTime.Now
                };

                _context.StoryLikes.Add(nuevoLike);
                story.NumeroLikes++;
                await _context.SaveChangesAsync();

                // Crear notificacion (si no es el mismo usuario)
                if (story.CreadorId != usuarioId)
                {
                    var usuario = await _context.Users.FindAsync(usuarioId);
                    var notificacion = new Notificacion
                    {
                        UsuarioId = story.CreadorId,
                        Tipo = TipoNotificacion.LikeEnStory,
                        Titulo = "Nuevo like en tu historia",
                        Mensaje = $"@{usuario?.UserName ?? "Alguien"} le dio like a tu historia",
                        UrlDestino = $"/Feed?story={story.CreadorId}",
                        UsuarioOrigenId = usuarioId,
                        ImagenUrl = usuario?.FotoPerfil,
                        StoryId = storyId,
                        FechaCreacion = DateTime.Now,
                        Leida = false
                    };
                    _context.Notificaciones.Add(notificacion);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, liked = true, likes = story.NumeroLikes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dar like a story {StoryId}", storyId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        /// <summary>
        /// Quita like de una story
        /// </summary>
        [HttpPost("QuitarLike/{storyId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarLike(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "No autenticado" });

                var story = await _context.Stories
                    .FirstOrDefaultAsync(s => s.Id == storyId && s.EstaActivo);

                if (story == null)
                    return Json(new { success = false, message = "Story no encontrada" });

                var like = await _context.StoryLikes
                    .FirstOrDefaultAsync(l => l.StoryId == storyId && l.UsuarioId == usuarioId);

                if (like == null)
                    return Json(new { success = true, liked = false, likes = story.NumeroLikes, message = "No tenias like" });

                _context.StoryLikes.Remove(like);
                story.NumeroLikes = Math.Max(0, story.NumeroLikes - 1);
                await _context.SaveChangesAsync();

                return Json(new { success = true, liked = false, likes = story.NumeroLikes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar like de story {StoryId}", storyId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        /// <summary>
        /// Toggle like (dar o quitar segun estado actual)
        /// </summary>
        [HttpPost("ToggleLike/{storyId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                    return Json(new { success = false, message = "No autenticado" });

                var likeExistente = await _context.StoryLikes
                    .FirstOrDefaultAsync(l => l.StoryId == storyId && l.UsuarioId == usuarioId);

                if (likeExistente != null)
                    return await QuitarLike(storyId);
                else
                    return await DarLike(storyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al toggle like en story {StoryId}", storyId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        /// <summary>
        /// Obtiene info de likes de una story
        /// </summary>
        [HttpGet("ObtenerLikes/{storyId}")]
        public async Task<IActionResult> ObtenerLikes(int storyId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var story = await _context.Stories
                    .FirstOrDefaultAsync(s => s.Id == storyId && s.EstaActivo);

                if (story == null)
                    return Json(new { success = false, message = "Story no encontrada" });

                var usuarioLeDioLike = false;
                if (!string.IsNullOrEmpty(usuarioId))
                {
                    usuarioLeDioLike = await _context.StoryLikes
                        .AnyAsync(l => l.StoryId == storyId && l.UsuarioId == usuarioId);
                }

                return Json(new {
                    success = true,
                    likes = story.NumeroLikes,
                    liked = usuarioLeDioLike
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener likes de story {StoryId}", storyId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }
    }
}