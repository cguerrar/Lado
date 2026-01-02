using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Lado.Data;
using Lado.Hubs;
using Lado.Models;
using Lado.Services;
using ImageMagick;

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
        private readonly IMediaConversionService _mediaConversionService;
        private readonly IHubContext<ChatHub> _hubContext;

        public StoriesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StoriesController> logger,
            IRateLimitService rateLimitService,
            IFileValidationService fileValidationService,
            IMediaConversionService mediaConversionService,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _rateLimitService = rateLimitService;
            _fileValidationService = fileValidationService;
            _mediaConversionService = mediaConversionService;
            _hubContext = hubContext;
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

                // Obtener IDs de usuarios cuyas historias están silenciadas
                var usuariosSilenciados = await _context.HistoriasSilenciadas
                    .Where(h => h.UsuarioId == usuarioId)
                    .Select(h => h.SilenciadoId)
                    .ToListAsync();

                // Filtrar stories de usuarios silenciados
                if (usuariosSilenciados.Any())
                {
                    storiesQuery = storiesQuery.Where(s => !usuariosSilenciados.Contains(s.CreadorId));
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
                        // Ordenar stories de cada usuario cronológicamente (más antigua primero)
                        // para que al navegar se vean en orden: primero la más antigua, al final la más nueva
                        stories = g.OrderBy(s => s.FechaPublicacion).Select(s => new
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
                                inicioSegundos = s.MusicaInicioSegundos ?? 0,
                                volumen = s.MusicaVolumen ?? 70
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
            int? musicaTrimStart,
            int? musicaVolumen,
            bool publicarEnFeed = false)
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
                    MusicaInicioSegundos = musicaTrimStart,
                    MusicaVolumen = musicaVolumen
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

                // ========================================
                // PUBLICAR TAMBIÉN EN FEED (si se solicitó)
                // ========================================
                int? contenidoId = null;
                if (publicarEnFeed)
                {
                    try
                    {
                        // Copiar archivo a carpeta de uploads
                        var rutaStoryCompleta = Path.Combine("wwwroot", rutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        var extension = Path.GetExtension(rutaStoryCompleta);
                        var nombreArchivoFeed = $"{Guid.NewGuid()}{extension}";
                        var carpetaFeed = Path.Combine("wwwroot", "uploads", usuarioId);

                        if (!Directory.Exists(carpetaFeed))
                        {
                            Directory.CreateDirectory(carpetaFeed);
                        }

                        var rutaArchivoFeed = Path.Combine(carpetaFeed, nombreArchivoFeed);

                        // Copiar archivo
                        if (System.IO.File.Exists(rutaStoryCompleta))
                        {
                            System.IO.File.Copy(rutaStoryCompleta, rutaArchivoFeed, overwrite: true);
                        }

                        // Crear contenido en Feed
                        var contenido = new Contenido
                        {
                            UsuarioId = usuarioId,
                            TipoContenido = tipoContenido,
                            Descripcion = texto ?? "Historia compartida en Feed",
                            RutaArchivo = $"/uploads/{usuarioId}/{nombreArchivoFeed}",
                            TipoLado = tipoLadoFinal,
                            EsGratis = true,
                            EsPremium = false,
                            EstaActivo = true,
                            FechaPublicacion = DateTime.Now,
                            NumeroLikes = 0,
                            NumeroComentarios = 0,
                            NumeroVistas = 0,
                            // Vincular música si aplica
                            PistaMusicalId = pistaMusicalId,
                            MusicaVolumen = musicaVolumen.HasValue ? (decimal)musicaVolumen.Value / 100 : null
                        };

                        _context.Contenidos.Add(contenido);
                        await _context.SaveChangesAsync();
                        contenidoId = contenido.Id;

                        _logger.LogInformation("Contenido en Feed creado desde Story: {ContenidoId} por usuario {UserId}",
                            contenido.Id, usuarioId);
                    }
                    catch (Exception exFeed)
                    {
                        // No fallar la story si falla el post en Feed
                        _logger.LogWarning(exFeed, "Error al crear post en Feed desde Story {StoryId}", story.Id);
                    }
                }

                _logger.LogInformation("Story creada: {StoryId} por usuario {UserId} (Editor: {TieneEditor}, Feed: {EnFeed})",
                    story.Id, usuarioId, !string.IsNullOrEmpty(elementosJson), publicarEnFeed);

                return Json(new
                {
                    success = true,
                    storyId = story.Id,
                    contenidoId = contenidoId,
                    message = publicarEnFeed && contenidoId.HasValue
                        ? "Historia publicada y compartida en tu Feed"
                        : "Story publicada exitosamente"
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

                // Obtener el post original con sus archivos (para carruseles)
                var post = await _context.Contenidos
                    .Include(c => c.Archivos)
                    .FirstOrDefaultAsync(c => c.Id == postId && c.EstaActivo);

                if (post == null)
                {
                    return Json(new { success = false, message = "Post no encontrado" });
                }

                // REGLA: Solo puedes compartir TUS PROPIOS posts como historia
                // No se permite compartir posts de otros usuarios a tu historia
                if (post.UsuarioId != usuarioId)
                {
                    return Json(new {
                        success = false,
                        message = "Solo puedes compartir tus propios posts a tu historia",
                        esPostAjeno = true
                    });
                }

                // Verificar que el post tiene media (incluyendo carruseles)
                var mediaUrl = post.PrimerArchivo;
                if (string.IsNullOrEmpty(mediaUrl))
                {
                    return Json(new { success = false, message = "El post no tiene imagen o video" });
                }

                // Crear story usando el primer archivo del post
                var story = new Story
                {
                    CreadorId = usuarioId,
                    RutaArchivo = mediaUrl,
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
                var carpeta = Path.Combine("wwwroot", "uploads", nombreCarpeta);

                if (!Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                var nombreBase = Guid.NewGuid().ToString();
                string nombreArchivo;
                string rutaArchivo;

                // Verificar si necesita conversión a formato estándar
                var requiereConversionImg = tipoContenido == TipoContenido.Imagen && _mediaConversionService.ImagenRequiereConversion(extension);
                var requiereConversionVideo = tipoContenido == TipoContenido.Video && _mediaConversionService.VideoRequiereConversion(extension);

                if (requiereConversionImg)
                {
                    _logger.LogInformation("[Reel] Convirtiendo imagen {Extension} a JPEG", extension);
                    using var stream = archivo.OpenReadStream();
                    var rutaConvertida = await _mediaConversionService.ConvertirImagenAsync(stream, carpeta, extension, nombreBase, 1920, 90);

                    if (!string.IsNullOrEmpty(rutaConvertida))
                    {
                        nombreArchivo = Path.GetFileName(rutaConvertida);
                        rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";
                        _logger.LogInformation("[Reel] Imagen convertida: {Original} → {Convertido}", archivo.FileName, nombreArchivo);
                    }
                    else
                    {
                        nombreArchivo = $"{nombreBase}{extension}";
                        var rutaCompleta = Path.Combine(carpeta, nombreArchivo);
                        using (var stream2 = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await archivo.CopyToAsync(stream2);
                        }
                        rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";
                    }
                }
                else if (requiereConversionVideo)
                {
                    _logger.LogInformation("[Reel] Convirtiendo video {Extension} a MP4", extension);
                    using var stream = archivo.OpenReadStream();
                    var rutaConvertida = await _mediaConversionService.ConvertirVideoAsync(stream, carpeta, extension, nombreBase, 20, 1920);

                    if (!string.IsNullOrEmpty(rutaConvertida))
                    {
                        nombreArchivo = Path.GetFileName(rutaConvertida);
                        rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";
                        _logger.LogInformation("[Reel] Video convertido: {Original} → {Convertido}", archivo.FileName, nombreArchivo);
                    }
                    else
                    {
                        nombreArchivo = $"{nombreBase}{extension}";
                        var rutaCompleta = Path.Combine(carpeta, nombreArchivo);
                        using (var stream2 = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await archivo.CopyToAsync(stream2);
                        }
                        rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";
                    }
                }
                else
                {
                    // Archivo ya está en formato estándar
                    nombreArchivo = $"{nombreBase}{extension}";
                    var rutaCompleta = Path.Combine(carpeta, nombreArchivo);
                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await archivo.CopyToAsync(stream);
                    }
                    rutaArchivo = $"/uploads/{nombreCarpeta}/{nombreArchivo}";
                }

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
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            var nombreBase = Guid.NewGuid().ToString();
            var carpeta = Path.Combine("wwwroot", "stories", usuarioId);

            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            // Verificar si necesita conversión a formato estándar
            var requiereConversionImg = _mediaConversionService.ImagenRequiereConversion(extension);
            var requiereConversionVideo = _mediaConversionService.VideoRequiereConversion(extension);

            if (requiereConversionImg)
            {
                // Convertir imagen a JPEG estándar
                _logger.LogInformation("[Story] Convirtiendo imagen {Extension} a JPEG", extension);
                using var stream = archivo.OpenReadStream();
                var rutaConvertida = await _mediaConversionService.ConvertirImagenAsync(stream, carpeta, extension, nombreBase, 1920, 90);

                if (!string.IsNullOrEmpty(rutaConvertida))
                {
                    var nombreArchivo = Path.GetFileName(rutaConvertida);
                    _logger.LogInformation("[Story] Imagen convertida: {Original} → {Convertido}", archivo.FileName, nombreArchivo);
                    return $"/stories/{usuarioId}/{nombreArchivo}";
                }

                _logger.LogWarning("[Story] Error convirtiendo imagen, usando original");
            }
            else if (requiereConversionVideo)
            {
                // Convertir video a MP4 H.264 estándar
                _logger.LogInformation("[Story] Convirtiendo video {Extension} a MP4", extension);
                using var stream = archivo.OpenReadStream();
                var rutaConvertida = await _mediaConversionService.ConvertirVideoAsync(stream, carpeta, extension, nombreBase, 23, 1080);

                if (!string.IsNullOrEmpty(rutaConvertida))
                {
                    var nombreArchivo = Path.GetFileName(rutaConvertida);
                    _logger.LogInformation("[Story] Video convertido: {Original} → {Convertido}", archivo.FileName, nombreArchivo);
                    return $"/stories/{usuarioId}/{nombreArchivo}";
                }

                _logger.LogWarning("[Story] Error convirtiendo video, usando original");
            }

            // Archivo ya está en formato estándar o conversión falló - guardar sin cambios
            var nombreArchivoFinal = $"{nombreBase}{extension}";
            var rutaCompleta = Path.Combine(carpeta, nombreArchivoFinal);

            using (var stream = new FileStream(rutaCompleta, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            return $"/stories/{usuarioId}/{nombreArchivoFinal}";
        }

        /// <summary>
        /// Convierte un archivo WebM a MP4 usando FFmpeg para compatibilidad con iOS/Safari
        /// </summary>
        private async Task<string?> ConvertirWebmAMp4Async(string rutaWebm, string carpeta, string nombreBase)
        {
            try
            {
                var rutaMp4 = Path.Combine(carpeta, $"{nombreBase}.mp4");

                // Verificar si FFmpeg está disponible
                var ffmpegPath = "ffmpeg"; // Asume que está en PATH

                // Argumentos para conversión optimizada para iOS
                // -c:v libx264 -preset fast -crf 23 : Video H.264 con buena calidad
                // -c:a aac -b:a 128k : Audio AAC a 128kbps
                // -movflags +faststart : Optimiza para streaming web
                // -pix_fmt yuv420p : Formato de pixel compatible con todos los dispositivos
                var argumentos = $"-i \"{rutaWebm}\" -c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k -movflags +faststart -pix_fmt yuv420p -y \"{rutaMp4}\"";

                var proceso = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = argumentos,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                proceso.Start();

                // Esperar máximo 2 minutos para conversión
                var completado = await Task.Run(() => proceso.WaitForExit(120000));

                if (!completado)
                {
                    proceso.Kill();
                    _logger.LogWarning("FFmpeg timeout al convertir: {Archivo}", rutaWebm);
                    return null;
                }

                if (proceso.ExitCode != 0)
                {
                    var error = await proceso.StandardError.ReadToEndAsync();
                    _logger.LogWarning("FFmpeg error al convertir {Archivo}: {Error}", rutaWebm, error);
                    return null;
                }

                // Eliminar archivo WebM original si la conversión fue exitosa
                if (System.IO.File.Exists(rutaMp4))
                {
                    try
                    {
                        System.IO.File.Delete(rutaWebm);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo eliminar WebM original: {Error}", ex.Message);
                    }
                    return rutaMp4;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al convertir WebM a MP4: {Archivo}", rutaWebm);
                return null; // Devolver null para usar el WebM original como fallback
            }
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

        // ========================================
        // RESPONDER A STORY (Mensaje o Reacción)
        // ========================================

        /// <summary>
        /// Enviar mensaje o reacción a una story sin salir del visor
        /// </summary>
        [HttpPost("ResponderStory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderStory([FromForm] ResponderStoryRequest request)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Rate limiting
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"story_response_{usuarioId}";
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 30, TimeSpan.FromMinutes(1),
                    TipoAtaque.SpamMensajes, "/Stories/ResponderStory", usuarioId))
                {
                    return Json(new { success = false, message = "Demasiadas respuestas. Espera un momento." });
                }

                // Validar story
                var story = await _context.Stories
                    .Include(s => s.Creador)
                    .FirstOrDefaultAsync(s => s.Id == request.StoryId && s.EstaActivo);

                if (story == null)
                {
                    return Json(new { success = false, message = "Historia no encontrada" });
                }

                // No puede responderse a sí mismo
                if (story.CreadorId == usuarioId)
                {
                    return Json(new { success = false, message = "No puedes responder a tu propia historia" });
                }

                // Verificar bloqueo
                var existeBloqueo = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == usuarioId && b.BloqueadoId == story.CreadorId) ||
                                  (b.BloqueadorId == story.CreadorId && b.BloqueadoId == usuarioId));

                if (existeBloqueo)
                {
                    return Json(new { success = false, message = "No puedes enviar mensajes a este usuario" });
                }

                // Verificar relación de suscripción o conversación previa
                var existeRelacion = await _context.Suscripciones
                    .AnyAsync(s => (s.FanId == usuarioId && s.CreadorId == story.CreadorId && s.EstaActiva) ||
                                  (s.FanId == story.CreadorId && s.CreadorId == usuarioId && s.EstaActiva));

                var tieneConversacion = await _context.MensajesPrivados
                    .AnyAsync(m => (m.RemitenteId == usuarioId && m.DestinatarioId == story.CreadorId) ||
                                  (m.RemitenteId == story.CreadorId && m.DestinatarioId == usuarioId));

                if (!existeRelacion && !tieneConversacion)
                {
                    return Json(new { success = false, message = "Debes estar suscrito para responder historias" });
                }

                // Determinar contenido del mensaje
                string contenidoMensaje;
                if (request.TipoRespuesta.HasValue && request.TipoRespuesta != TipoRespuestaStory.Texto)
                {
                    // Reacción rápida
                    contenidoMensaje = request.TipoRespuesta switch
                    {
                        TipoRespuestaStory.ReaccionFuego => "🔥",
                        TipoRespuestaStory.ReaccionCorazon => "❤️",
                        TipoRespuestaStory.ReaccionRisa => "😂",
                        TipoRespuestaStory.ReaccionSorpresa => "😮",
                        TipoRespuestaStory.ReaccionAplauso => "👏",
                        _ => request.Mensaje?.Trim() ?? ""
                    };
                }
                else
                {
                    contenidoMensaje = request.Mensaje?.Trim() ?? "";
                }

                if (string.IsNullOrWhiteSpace(contenidoMensaje))
                {
                    return Json(new { success = false, message = "El mensaje no puede estar vacío" });
                }

                // Crear mensaje
                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuarioId,
                    DestinatarioId = story.CreadorId,
                    Contenido = contenidoMensaje,
                    FechaEnvio = DateTime.Now,
                    Leido = false,
                    TipoMensaje = TipoMensaje.Texto,
                    StoryReferenciaId = story.Id,
                    TipoRespuestaStory = request.TipoRespuesta ?? TipoRespuestaStory.Texto
                };

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Respuesta a story enviada. StoryId: {StoryId}, MensajeId: {MensajeId}, Tipo: {Tipo}",
                    story.Id, mensaje.Id, request.TipoRespuesta);

                // Preparar DTO para SignalR
                var fechaUtc = DateTime.SpecifyKind(mensaje.FechaEnvio, DateTimeKind.Local).ToUniversalTime();
                var timestamp = new DateTimeOffset(fechaUtc).ToUnixTimeMilliseconds();

                var mensajeDto = new
                {
                    id = mensaje.Id,
                    contenido = mensaje.Contenido,
                    fechaEnvioTimestamp = timestamp,
                    remitenteId = usuarioId,
                    remitenteNombre = usuario.NombreCompleto ?? usuario.UserName,
                    remitenteFoto = usuario.FotoPerfil,
                    tipoMensaje = (int)TipoMensaje.Texto,
                    leido = false,
                    storyReferencia = new
                    {
                        id = story.Id,
                        rutaArchivo = story.RutaArchivo,
                        tipoContenido = (int)story.TipoContenido,
                        creadorNombre = story.Creador?.NombreCompleto ?? story.Creador?.UserName ?? "Usuario"
                    },
                    tipoRespuestaStory = (int)(request.TipoRespuesta ?? TipoRespuestaStory.Texto)
                };

                // Notificar via SignalR al creador de la story
                await _hubContext.Clients.Group($"user_{story.CreadorId}").SendAsync("RecibirMensaje", mensajeDto);

                return Json(new { success = true, mensaje = mensajeDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al responder story {StoryId}", request?.StoryId);
                return Json(new { success = false, message = "Error al enviar respuesta" });
            }
        }

        // ========================================
        // SILENCIAR/DESILENCIAR HISTORIAS
        // ========================================

        /// <summary>
        /// Silenciar las historias de un usuario
        /// </summary>
        [HttpPost("SilenciarHistorias")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SilenciarHistorias([FromForm] string usuarioId)
        {
            try
            {
                var usuarioActualId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioActualId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no especificado" });
                }

                if (usuarioActualId == usuarioId)
                {
                    return Json(new { success = false, message = "No puedes silenciar tus propias historias" });
                }

                // Verificar si ya está silenciado
                var yaExiste = await _context.HistoriasSilenciadas
                    .AnyAsync(h => h.UsuarioId == usuarioActualId && h.SilenciadoId == usuarioId);

                if (yaExiste)
                {
                    return Json(new { success = true, message = "Ya estaba silenciado" });
                }

                // Crear registro de silencio
                var silencio = new HistoriaSilenciada
                {
                    UsuarioId = usuarioActualId,
                    SilenciadoId = usuarioId,
                    FechaSilenciado = DateTime.Now
                };

                _context.HistoriasSilenciadas.Add(silencio);
                await _context.SaveChangesAsync();

                // Obtener nombre del usuario silenciado
                var usuarioSilenciado = await _userManager.FindByIdAsync(usuarioId);
                var nombre = usuarioSilenciado?.NombreCompleto ?? usuarioSilenciado?.UserName ?? "Usuario";

                return Json(new { success = true, message = $"Historias de {nombre} silenciadas" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al silenciar historias del usuario {UsuarioId}", usuarioId);
                return Json(new { success = false, message = "Error al silenciar historias" });
            }
        }

        /// <summary>
        /// Dejar de silenciar las historias de un usuario
        /// </summary>
        [HttpPost("DesilenciarHistorias")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesilenciarHistorias([FromForm] string usuarioId)
        {
            try
            {
                var usuarioActualId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioActualId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var silencio = await _context.HistoriasSilenciadas
                    .FirstOrDefaultAsync(h => h.UsuarioId == usuarioActualId && h.SilenciadoId == usuarioId);

                if (silencio == null)
                {
                    return Json(new { success = true, message = "No estaba silenciado" });
                }

                _context.HistoriasSilenciadas.Remove(silencio);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Historias desilenciadas" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desilenciar historias del usuario {UsuarioId}", usuarioId);
                return Json(new { success = false, message = "Error al desilenciar historias" });
            }
        }

        /// <summary>
        /// Obtener lista de usuarios cuyas historias han sido silenciadas
        /// </summary>
        [HttpGet("ObtenerSilenciados")]
        public async Task<IActionResult> ObtenerSilenciados()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var silenciados = await _context.HistoriasSilenciadas
                    .Where(h => h.UsuarioId == usuarioId)
                    .Include(h => h.Silenciado)
                    .Select(h => new
                    {
                        id = h.SilenciadoId,
                        nombre = h.Silenciado!.NombreCompleto ?? h.Silenciado.UserName,
                        username = h.Silenciado.UserName,
                        avatar = h.Silenciado.FotoPerfil,
                        fechaSilenciado = h.FechaSilenciado
                    })
                    .ToListAsync();

                return Json(new { success = true, silenciados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios silenciados");
                return Json(new { success = false, message = "Error al cargar usuarios silenciados" });
            }
        }

        /// <summary>
        /// Verificar si las historias de un usuario están silenciadas
        /// </summary>
        [HttpGet("EstaSilenciado/{usuarioId}")]
        public async Task<IActionResult> EstaSilenciado(string usuarioId)
        {
            try
            {
                var usuarioActualId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioActualId))
                {
                    return Json(new { success = false, silenciado = false });
                }

                var silenciado = await _context.HistoriasSilenciadas
                    .AnyAsync(h => h.UsuarioId == usuarioActualId && h.SilenciadoId == usuarioId);

                return Json(new { success = true, silenciado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar si usuario está silenciado {UsuarioId}", usuarioId);
                return Json(new { success = false, silenciado = false });
            }
        }

        /// <summary>
        /// Obtener historias de usuarios silenciados (para ver en sección aparte)
        /// </summary>
        [HttpGet("ObtenerStoriesSilenciados")]
        public async Task<IActionResult> ObtenerStoriesSilenciados()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // Obtener IDs de usuarios silenciados
                var usuariosSilenciados = await _context.HistoriasSilenciadas
                    .Where(h => h.UsuarioId == usuarioId)
                    .Select(h => h.SilenciadoId)
                    .ToListAsync();

                if (!usuariosSilenciados.Any())
                {
                    return Json(new { success = true, grupos = new List<object>() });
                }

                var ahora = DateTime.Now;

                // Obtener stories de usuarios silenciados que sigo
                var creadoresIds = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                var stories = await _context.Stories
                    .Include(s => s.Creador)
                    .Where(s => creadoresIds.Contains(s.CreadorId)
                            && usuariosSilenciados.Contains(s.CreadorId)
                            && s.FechaExpiracion > ahora
                            && s.EstaActivo)
                    .OrderByDescending(s => s.FechaPublicacion)
                    .ToListAsync();

                var storiesVistos = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == usuarioId)
                    .Select(sv => sv.StoryId)
                    .ToListAsync();

                var grupos = stories
                    .GroupBy(s => s.CreadorId)
                    .Select(g => new
                    {
                        creadorId = g.Key,
                        nombre = g.First().Creador?.NombreCompleto ?? g.First().Creador?.UserName,
                        avatar = g.First().Creador?.FotoPerfil,
                        totalStories = g.Count(),
                        sinVer = g.Count(s => !storiesVistos.Contains(s.Id))
                    })
                    .ToList();

                return Json(new { success = true, grupos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stories silenciados");
                return Json(new { success = false, message = "Error al cargar stories silenciados" });
            }
        }

        // ========================================
        // ANUNCIOS EN STORIES
        // ========================================

        /// <summary>
        /// Obtener un anuncio para mostrar entre grupos de historias
        /// </summary>
        [HttpGet("ObtenerAnuncioStory")]
        public async Task<IActionResult> ObtenerAnuncioStory()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Obtener anuncios activos con tipo imagen o video
                var ahora = DateTime.Now;
                var anunciosActivos = await _context.Anuncios
                    .Include(a => a.Agencia)
                    .Where(a => a.Estado == EstadoAnuncio.Activo
                            && (a.FechaInicio == null || a.FechaInicio <= ahora)
                            && (a.FechaFin == null || a.FechaFin >= ahora)
                            && (a.TipoCreativo == TipoCreativo.Imagen || a.TipoCreativo == TipoCreativo.Video)
                            && !string.IsNullOrEmpty(a.UrlCreativo)
                            && (a.PresupuestoTotal == 0 || a.GastoTotal < a.PresupuestoTotal)
                            && (a.PresupuestoDiario == 0 || a.GastoHoy < a.PresupuestoDiario))
                    .OrderByDescending(a => a.Prioridad)
                    .ThenBy(a => Guid.NewGuid()) // Random entre anuncios de misma prioridad
                    .Take(1)
                    .FirstOrDefaultAsync();

                if (anunciosActivos == null)
                {
                    return Json(new { success = false, message = "No hay anuncios disponibles" });
                }

                // Registrar impresión
                var impresion = new ImpresionAnuncio
                {
                    AnuncioId = anunciosActivos.Id,
                    UsuarioId = usuarioId,
                    FechaImpresion = DateTime.Now,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _context.ImpresionesAnuncios.Add(impresion);
                anunciosActivos.Impresiones++;
                await _context.SaveChangesAsync();

                // Devolver anuncio formateado como story
                return Json(new
                {
                    success = true,
                    anuncio = new
                    {
                        id = anunciosActivos.Id,
                        rutaArchivo = anunciosActivos.UrlCreativo,
                        tipo = anunciosActivos.TipoCreativo == TipoCreativo.Video ? "Video" : "Imagen",
                        titulo = anunciosActivos.Titulo,
                        descripcion = anunciosActivos.Descripcion,
                        urlDestino = anunciosActivos.UrlDestino,
                        textoBoton = anunciosActivos.TextoBotonDisplay,
                        esAnuncio = true,
                        nombreAnunciante = anunciosActivos.EsAnuncioLado ? "Lado" :
                            (anunciosActivos.Agencia?.NombreEmpresa ?? "Publicidad"),
                        avatarAnunciante = anunciosActivos.EsAnuncioLado ? "/images/logo-icon.png" :
                            (anunciosActivos.Agencia?.LogoUrl ?? "/images/ad-placeholder.png")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener anuncio para story");
                return Json(new { success = false, message = "Error al cargar anuncio" });
            }
        }

        /// <summary>
        /// Registrar clic en anuncio de story
        /// </summary>
        [HttpPost("RegistrarClicAnuncio")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarClicAnuncio([FromForm] int anuncioId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var anuncio = await _context.Anuncios.FindAsync(anuncioId);
                if (anuncio == null)
                {
                    return Json(new { success = false });
                }

                var clic = new ClicAnuncio
                {
                    AnuncioId = anuncioId,
                    UsuarioId = usuarioId,
                    FechaClic = DateTime.Now,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _context.ClicsAnuncios.Add(clic);
                anuncio.Clics++;

                // Calcular costo del clic
                if (anuncio.CostoPorClic > 0)
                {
                    anuncio.GastoTotal += anuncio.CostoPorClic;
                    anuncio.GastoHoy += anuncio.CostoPorClic;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, urlDestino = anuncio.UrlDestino });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar clic en anuncio {AnuncioId}", anuncioId);
                return Json(new { success = false });
            }
        }

        /// <summary>
        /// Convertir imagen HEIC a JPEG (para navegadores que no soportan HEIC)
        /// </summary>
        [HttpPost("ConvertirHeic")]
        [RequestSizeLimit(50_000_000)] // 50MB máximo
        public async Task<IActionResult> ConvertirHeic(IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(new { success = false, error = "No se recibió archivo" });
                }

                // Verificar extensión
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension != ".heic" && extension != ".heif")
                {
                    return BadRequest(new { success = false, error = "El archivo no es HEIC/HEIF" });
                }

                using var inputStream = archivo.OpenReadStream();
                using var image = new MagickImage(inputStream);

                // Convertir a JPEG
                image.Format = MagickFormat.Jpeg;
                image.Quality = 92;

                // Auto-orientar según EXIF
                image.AutoOrient();

                using var outputStream = new MemoryStream();
                await image.WriteAsync(outputStream);
                outputStream.Position = 0;

                var nuevoNombre = Path.GetFileNameWithoutExtension(archivo.FileName) + ".jpg";
                return File(outputStream.ToArray(), "image/jpeg", nuevoNombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error convirtiendo HEIC: {FileName}", archivo?.FileName);
                return BadRequest(new { success = false, error = "Error al convertir: " + ex.Message });
            }
        }
    }

    // ========================================
    // REQUEST MODEL
    // ========================================

    public class ResponderStoryRequest
    {
        public int StoryId { get; set; }
        public string? Mensaje { get; set; }
        public TipoRespuestaStory? TipoRespuesta { get; set; }
    }
}