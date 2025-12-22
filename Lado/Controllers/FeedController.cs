using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    public class FeedController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FeedController> _logger;
        private readonly IAdService _adService;
        private readonly INotificationService _notificationService;
        private readonly IFeedAlgorithmService _feedAlgorithmService;
        private readonly IInteresesService _interesesService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IMemoryCache _cache;

        public FeedController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedController> logger,
            IAdService adService,
            INotificationService notificationService,
            IFeedAlgorithmService feedAlgorithmService,
            IInteresesService interesesService,
            IRateLimitService rateLimitService,
            IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _adService = adService;
            _notificationService = notificationService;
            _feedAlgorithmService = feedAlgorithmService;
            _interesesService = interesesService;
            _rateLimitService = rateLimitService;
            _cache = cache;
        }

        // ⚡ Método helper para obtener usuarios bloqueados con cache
        private async Task<List<string>> ObtenerUsuariosBloqueadosCacheadosAsync(string usuarioId)
        {
            var cacheKey = $"bloqueos_{usuarioId}";
            if (!_cache.TryGetValue(cacheKey, out List<string>? bloqueados))
            {
                bloqueados = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == usuarioId || b.BloqueadoId == usuarioId)
                    .Select(b => b.BloqueadorId == usuarioId ? b.BloqueadoId : b.BloqueadorId)
                    .Distinct()
                    .ToListAsync();

                _cache.Set(cacheKey, bloqueados, TimeSpan.FromMinutes(5));
            }
            return bloqueados ?? new List<string>();
        }

        // ⚡ Método helper para obtener IDs de admins con cache
        private async Task<List<string>> ObtenerAdminIdsCacheadosAsync()
        {
            var cacheKey = "admin_ids";
            if (!_cache.TryGetValue(cacheKey, out List<string>? adminIds))
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                adminIds = adminUsers.Select(u => u.Id).ToList();
                _cache.Set(cacheKey, adminIds, TimeSpan.FromHours(1));
            }
            return adminIds ?? new List<string>();
        }


        /// <summary>
        /// Ruta para perfiles LadoB usando seudónimo (protege la identidad real)
        /// URL: /Feed/Creador/{seudonimo}
        /// </summary>
        [Route("Feed/Creador/{seudonimo}")]
        public async Task<IActionResult> PerfilLadoB(string seudonimo)
        {
            if (string.IsNullOrWhiteSpace(seudonimo))
            {
                return RedirectToAction("Index");
            }

            // Buscar usuario por seudónimo
            var usuario = await _context.Users
                .FirstOrDefaultAsync(u => u.Seudonimo != null &&
                                         u.Seudonimo.ToLower() == seudonimo.ToLower() &&
                                         u.EstaActivo);

            if (usuario == null)
            {
                TempData["Error"] = "Creador no encontrado";
                return RedirectToAction("Index");
            }

            // Redirigir a Perfil con verSeudonimo=true (internamente usa el ID)
            return await Perfil(usuario.Id, verSeudonimo: true);
        }

        public async Task<IActionResult> Perfil(string id, bool verSeudonimo = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    TempData["Error"] = "Usuario no especificado";
                    return RedirectToAction("Index");
                }

                var usuario = await _userManager.FindByIdAsync(id);

                if (usuario == null || !usuario.EstaActivo)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Index");
                }

                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // ⭐ PROTECCIÓN DE PRIVACIDAD LADOA/LADOB
                var tieneSeudonimoActivo = !string.IsNullOrEmpty(usuario.Seudonimo);
                var esPerfilPropio = usuarioActual.Id == id;

                // ⭐ REGLAS DE ACCESO:
                // - LadoA es PÚBLICO: siempre visible para todos
                // - LadoB es PREMIUM: visible para suscriptores (o preview para suscribirse)
                // - Si creador tiene OcultarIdentidadLadoA=true: desde LadoB no se puede acceder a LadoA
                //   (a menos que sigas en LadoA)

                if (tieneSeudonimoActivo && !esPerfilPropio && !verSeudonimo && usuario.OcultarIdentidadLadoA)
                {
                    // Intentando ver LadoA de un creador que oculta su identidad
                    // Solo permitir si el visitante sigue en LadoA
                    var sigueEnLadoA = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioActual.Id &&
                                      s.CreadorId == id &&
                                      s.TipoLado == TipoLado.LadoA &&
                                      s.EstaActiva);

                    if (!sigueEnLadoA)
                    {
                        // No sigue en LadoA y el creador oculta su identidad → Redirigir a LadoB
                        _logger.LogInformation("🔒 Redirigiendo a LadoB: creador oculta identidad LadoA y visitante no lo sigue");
                        return RedirectToAction("Perfil", new { id = id, verSeudonimo = true });
                    }
                }

                // ⭐ Determinar TipoLado según modo de visualización
                var tipoLadoActual = verSeudonimo ? TipoLado.LadoB : TipoLado.LadoA;

                // Verificar suscripción específica al TipoLado que se está viendo
                var estaSuscrito = await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuarioActual.Id &&
                             s.CreadorId == id &&
                             s.TipoLado == tipoLadoActual &&
                             s.EstaActiva);

                ViewBag.EstaSuscrito = estaSuscrito;
                ViewBag.TipoLadoActual = (int)tipoLadoActual;

                // ⭐ NUEVA LÓGICA: Determinar qué perfil mostrar
                ViewBag.MostrandoSeudonimo = verSeudonimo;

                // ⭐ Indicar si la identidad LadoA está protegida (no mostrar enlaces cruzados)
                ViewBag.IdentidadProtegida = tieneSeudonimoActivo && !esPerfilPropio;

                if (verSeudonimo)
                {
                    // 📌 MODO SEUDÓNIMO: Mostrar TODO el contenido LadoB (bloqueado con blur si no suscrito)
                    _logger.LogInformation("Mostrando perfil de seudónimo para {Username}", usuario.UserName);

                    var contenidosComprados = await _context.ComprasContenido
                        .Where(cc => cc.UsuarioId == usuarioActual.Id)
                        .Select(cc => cc.ContenidoId)
                        .ToListAsync();

                    // Cargar TODO el contenido LadoB (se mostrará con blur si no tiene acceso)
                    var contenidoLadoB = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && c.TipoLado == TipoLado.LadoB)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .ToListAsync();

                    // Determinar qué contenidos están desbloqueados (suscrito, comprado, o es el propio usuario)
                    var esPropio = id == usuarioActual.Id;
                    var contenidosDesbloqueadosIds = esPropio
                        ? contenidoLadoB.Select(c => c.Id).ToList() // Si es propio, todo desbloqueado
                        : estaSuscrito
                            ? contenidoLadoB.Select(c => c.Id).ToList() // Si suscrito, todo desbloqueado
                            : contenidosComprados; // Si no suscrito, solo los comprados

                    ViewBag.ContenidosDesbloqueadosIds = contenidosDesbloqueadosIds;
                    ViewBag.Contenidos = contenidoLadoB;
                    ViewBag.ContenidoLadoA = new List<Contenido>(); // Vacío en modo seudónimo
                    ViewBag.ContenidoLadoB = contenidoLadoB;

                    // Mostrar nombre del seudónimo
                    ViewBag.NombreMostrado = usuario.Seudonimo ?? usuario.NombreCompleto;
                    ViewBag.UsernameMostrado = $"@{usuario.Seudonimo?.ToLower() ?? usuario.UserName}";
                }
                else
                {
                    // 📌 MODO NORMAL (LadoA): Solo contenido público LadoA
                    _logger.LogInformation("Mostrando perfil LadoA para {Username}", usuario.UserName);

                    var contenidoLadoA = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && c.TipoLado == TipoLado.LadoA)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .ToListAsync();

                    // En modo LadoA NO mostramos contenido LadoB
                    var contenidoLadoB = new List<Contenido>();

                    ViewBag.Contenidos = contenidoLadoA;
                    ViewBag.ContenidoLadoA = contenidoLadoA;
                    ViewBag.ContenidoLadoB = contenidoLadoB;

                    // Mostrar nombre real
                    ViewBag.NombreMostrado = usuario.NombreCompleto;
                    ViewBag.UsernameMostrado = $"@{usuario.UserName}";
                }

                ViewBag.Colecciones = await _context.Colecciones
                    .Include(c => c.Contenidos)
                    .Where(c => c.CreadorId == id && c.EstaActiva)
                    .OrderByDescending(c => c.FechaCreacion)
                    .ToListAsync();

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                var contenidosTotales = ViewBag.Contenidos as List<Contenido>;
                ViewBag.TotalLikes = contenidosTotales?.Sum(c => c.NumeroLikes) ?? 0;
                ViewBag.TotalPublicaciones = contenidosTotales?.Count ?? 0;
                ViewBag.TotalLadoA = (ViewBag.ContenidoLadoA as List<Contenido>)?.Count ?? 0;
                ViewBag.TotalLadoB = (ViewBag.ContenidoLadoB as List<Contenido>)?.Count ?? 0;

                // Verificar si el creador tiene contenido LadoB (puede recibir propinas)
                var tieneContenidoLadoB = await _context.Contenidos
                    .AnyAsync(c => c.UsuarioId == id
                                && c.TipoLado == TipoLado.LadoB
                                && c.EstaActivo
                                && !c.EsBorrador);
                ViewBag.PuedeRecibirPropinas = tieneContenidoLadoB || usuario.CreadorVerificado;

                // ========================================
                // SEGUIDORES EN COMÚN
                // ========================================
                // Usuarios que YO sigo y que también siguen a este creador
                if (usuarioActual.Id != id)
                {
                    var seguidoresEnComun = await _context.Suscripciones
                        .Where(s1 => s1.FanId == usuarioActual.Id && s1.EstaActiva) // Usuarios que yo sigo
                        .Join(
                            _context.Suscripciones.Where(s2 => s2.CreadorId == id && s2.EstaActiva), // Que también siguen al creador
                            s1 => s1.CreadorId,  // El creador que yo sigo
                            s2 => s2.FanId,      // Es fan de este creador
                            (s1, s2) => s1.CreadorId
                        )
                        .Distinct()
                        .Take(5)
                        .Join(
                            _context.Users,
                            creadorId => creadorId,
                            u => u.Id,
                            (creadorId, u) => new { u.Id, u.UserName, u.NombreCompleto, u.FotoPerfil }
                        )
                        .ToListAsync();

                    ViewBag.SeguidoresEnComun = seguidoresEnComun;
                    ViewBag.TotalSeguidoresEnComun = seguidoresEnComun.Count;
                }
                else
                {
                    ViewBag.SeguidoresEnComun = new List<object>();
                    ViewBag.TotalSeguidoresEnComun = 0;
                }

                // Incrementar contador de visitas al perfil (solo si no es el propietario)
                if (usuarioActual.Id != usuario.Id)
                {
                    usuario.VisitasPerfil++;
                    await _context.SaveChangesAsync();
                }

                return View("Perfil", usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil {Id}", id);
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Index");
            }
        }


        // ========================================
        // OBTENER LIKES DEL USUARIO ACTUAL
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObtenerMisLikes([FromBody] ObtenerLikesRequest request)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                if (request?.ContenidoIds == null || !request.ContenidoIds.Any())
                {
                    return Json(new { success = true, likesUsuario = new List<int>() });
                }

                var likesUsuario = await _context.Likes
                    .Where(l => l.UsuarioId == usuarioId && request.ContenidoIds.Contains(l.ContenidoId))
                    .Select(l => l.ContenidoId)
                    .ToListAsync();

                _logger.LogInformation("Usuario {UserId} tiene {Count} likes en el feed actual",
                    usuarioId, likesUsuario.Count);

                return Json(new
                {
                    success = true,
                    likesUsuario = likesUsuario
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener likes del usuario");
                return Json(new { success = false, message = "Error al obtener estado de likes" });
            }
        }

        public class ObtenerLikesRequest
        {
            public List<int> ContenidoIds { get; set; } = new List<int>();
        }

        // ========================================
        // INDEX - FEED PRINCIPAL CON CREADORES FAVORITOS
        // ========================================

        public async Task<IActionResult> Index(int? post = null)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(usuarioId))
                {
                    _logger.LogWarning("Usuario no autenticado en Index");
                    return RedirectToAction("Login", "Account");
                }

                // 1. Obtener usuarios a los que está suscrito (separados por TipoLado)
                var suscripcionesActivas = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .ToListAsync();

                // IDs de creadores suscritos en LadoA (contenido público)
                var creadoresLadoAIds = suscripcionesActivas
                    .Where(s => s.TipoLado == TipoLado.LadoA)
                    .Select(s => s.CreadorId)
                    .ToList();

                // IDs de creadores suscritos en LadoB (contenido premium)
                var creadoresLadoBIds = suscripcionesActivas
                    .Where(s => s.TipoLado == TipoLado.LadoB)
                    .Select(s => s.CreadorId)
                    .ToList();

                // Lista completa para otros usos (stories, colecciones, etc.)
                var creadoresIds = suscripcionesActivas
                    .Select(s => s.CreadorId)
                    .Distinct()
                    .ToList();

                // ========================================
                // FILTRAR USUARIOS BLOQUEADOS
                // ========================================
                var usuariosBloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == usuarioId || b.BloqueadoId == usuarioId)
                    .Select(b => b.BloqueadorId == usuarioId ? b.BloqueadoId : b.BloqueadorId)
                    .Distinct()
                    .ToListAsync();

                // Remover usuarios bloqueados de las listas de creadores
                creadoresLadoAIds = creadoresLadoAIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();
                creadoresLadoBIds = creadoresLadoBIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();
                creadoresIds = creadoresIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();

                ViewBag.UsuariosBloqueadosIds = usuariosBloqueadosIds;

                _logger.LogInformation("Usuario {UserId} tiene {Count} suscripciones activas, {Blocked} usuarios bloqueados",
                    usuarioId, creadoresIds.Count, usuariosBloqueadosIds.Count);

                // 2. CREADORES FAVORITOS - Solo suscritos
                // ⚡ Optimización: Pre-calcular conteo de suscriptores en una sola query agrupada
                var suscriptoresPorCreador = await _context.Suscripciones
                    .Where(s => creadoresIds.Contains(s.CreadorId) && s.EstaActiva)
                    .GroupBy(s => s.CreadorId)
                    .Select(g => new { CreadorId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CreadorId, x => x.Count);

                var creadoresFavoritos = await _context.Users
                    .Where(u => creadoresIds.Contains(u.Id) && u.EstaActivo)
                    .Select(u => new
                    {
                        u.Id,
                        u.NombreCompleto,
                        u.UserName,
                        u.FotoPerfil,
                        u.CreadorVerificado
                    })
                    .ToListAsync();

                // Combinar con conteos pre-calculados y ordenar
                var creadoresFavoritosConConteo = creadoresFavoritos
                    .Select(u => new
                    {
                        u.Id,
                        u.NombreCompleto,
                        u.UserName,
                        u.FotoPerfil,
                        u.CreadorVerificado,
                        NumeroSuscriptores = suscriptoresPorCreador.GetValueOrDefault(u.Id, 0)
                    })
                    .OrderByDescending(u => u.NumeroSuscriptores)
                    .ToList();

                ViewBag.CreadoresFavoritos = creadoresFavoritosConConteo;

                // 3. STORIES - Solo de creadores suscritos + propias
                var ahoraStories = DateTime.Now;
                var storiesCreadores = await _context.Stories
                    .Include(s => s.Creador)
                    .Where(s => (creadoresIds.Contains(s.CreadorId) || s.CreadorId == usuarioId)
                            && s.FechaExpiracion > ahoraStories
                            && s.EstaActivo)
                    .ToListAsync();

                var storiesVistosIds = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == usuarioId)
                    .Select(sv => sv.StoryId)
                    .ToListAsync();

                ViewBag.Stories = storiesCreadores
                    .GroupBy(s => s.CreadorId)
                    .Select(g => new
                    {
                        CreadorId = g.Key,
                        Creador = g.First().Creador,
                        TieneStorysSinVer = g.Any(s => !storiesVistosIds.Contains(s.Id)),
                        UltimaFecha = g.Max(s => s.FechaPublicacion),
                        TotalStories = g.Count()
                    })
                    .OrderByDescending(x => x.TieneStorysSinVer)
                    .ThenByDescending(x => x.UltimaFecha)
                    .ToList();

                // 4. COLECCIONES - Solo de creadores suscritos + propias
                ViewBag.Colecciones = await _context.Colecciones
                    .Include(c => c.Creador)
                    .Include(c => c.Contenidos)
                    .Where(c => c.EstaActiva
                            && (creadoresIds.Contains(c.CreadorId) || c.CreadorId == usuarioId))
                    .OrderByDescending(c => c.FechaCreacion)
                    .Take(5)
                    .Select(c => new
                    {
                        c.Id,
                        c.Nombre,
                        c.Descripcion,
                        c.Precio,
                        c.PrecioOriginal,
                        c.DescuentoPorcentaje,
                        c.ImagenPortada,
                        ItemCount = c.Contenidos.Count(),
                        Creador = new
                        {
                            c.Creador.Id,
                            c.Creador.NombreCompleto,
                            c.Creador.UserName,
                            c.Creador.FotoPerfil
                        }
                    })
                    .ToListAsync();

                // 5. ✅ CORREGIDO: Contenido público (LadoA) SOLO de creadores suscritos en LadoA + propio
                var contenidoPublico = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical) // Incluir música asociada
                    .Include(c => c.Archivos.OrderBy(a => a.Orden)) // Incluir archivos del carrusel
                    .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                        .ThenInclude(com => com.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && (c.UsuarioId == usuarioId || !c.EsPrivado) // Mostrar privado solo si es propio
                            && c.TipoLado == TipoLado.LadoA
                            && c.Usuario != null
                            && (creadoresLadoAIds.Contains(c.UsuarioId) || c.UsuarioId == usuarioId)) // ✅ Solo LadoA
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(30) // ⚡ Limitar carga inicial
                    .ToListAsync();

                // 6. ✅ CORREGIDO: Contenido premium (LadoB) SOLO de creadores suscritos en LadoB
                var contenidoPremiumSuscripciones = creadoresLadoBIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.PistaMusical) // Incluir música asociada
                        .Include(c => c.Archivos.OrderBy(a => a.Orden)) // Incluir archivos del carrusel
                        .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                            .ThenInclude(com => com.Usuario)
                        .Where(c => creadoresLadoBIds.Contains(c.UsuarioId) // ✅ Solo LadoB
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoB
                                && c.Usuario != null)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(15) // ⚡ Limitar carga inicial
                        .ToListAsync()
                    : new List<Contenido>();

                // 7. Contenido premium (LadoB) PROPIO
                var contenidoPremiumPropio = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical) // Incluir música asociada
                    .Include(c => c.Archivos.OrderBy(a => a.Orden)) // Incluir archivos del carrusel
                    .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                        .ThenInclude(com => com.Usuario)
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(10) // ⚡ Limitar carga inicial
                    .ToListAsync();

                // 8. Contenido premium comprado individualmente
                var contenidosCompradosIds = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == usuarioId)
                    .Select(cc => cc.ContenidoId)
                    .ToListAsync();

                var contenidoPremiumComprado = contenidosCompradosIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.PistaMusical) // Incluir música asociada
                        .Include(c => c.Archivos.OrderBy(a => a.Orden)) // Incluir archivos del carrusel
                        .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                            .ThenInclude(com => com.Usuario)
                        .Where(c => contenidosCompradosIds.Contains(c.Id)
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.Usuario != null)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(10) // ⚡ Limitar carga inicial
                        .ToListAsync()
                    : new List<Contenido>();

                // 9. NUEVO: Contenido LadoB de creadores NO suscritos (para mostrar con blur)
                var todosLosCreadores = creadoresLadoAIds.Union(creadoresLadoBIds).ToList();
                var contenidoLadoBBloqueado = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical) // Incluir música asociada
                    .Include(c => c.Archivos.OrderBy(a => a.Orden)) // Incluir archivos del carrusel
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.UsuarioId != usuarioId
                            && !creadoresLadoBIds.Contains(c.UsuarioId) // NO suscrito a LadoB
                            && !contenidosCompradosIds.Contains(c.Id) // NO comprado
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId)) // NO bloqueado
                    .OrderBy(c => Guid.NewGuid()) // Aleatorio
                    .Take(5) // Limitar a 5 posts bloqueados
                    .ToListAsync();

                // Guardar IDs de contenido bloqueado para la vista
                var contenidoBloqueadoIds = contenidoLadoBBloqueado.Select(c => c.Id).ToList();
                ViewBag.ContenidoBloqueadoIds = contenidoBloqueadoIds;

                // 10. Combinar todo el contenido
                var todoContenido = contenidoPublico
                    .Union(contenidoPremiumSuscripciones)
                    .Union(contenidoPremiumPropio)
                    .Union(contenidoPremiumComprado)
                    .Union(contenidoLadoBBloqueado) // Incluir contenido bloqueado
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Contenido total: {Total} (Público: {Publico}, Premium Subs: {PremiumSubs}, Premium Propio: {PremiumPropio}, Comprado: {Comprado})",
                    todoContenido.Count, contenidoPublico.Count, contenidoPremiumSuscripciones.Count,
                    contenidoPremiumPropio.Count, contenidoPremiumComprado.Count);

                // 10. Obtener reacciones del usuario
                var reaccionesUsuario = await _context.Reacciones
                    .Where(r => r.UsuarioId == usuarioId)
                    .ToDictionaryAsync(r => r.ContenidoId, r => r.TipoReaccion);

                ViewBag.ReaccionesUsuario = reaccionesUsuario;

                // 11. Obtener conteo de reacciones
                var reaccionesPorContenido = await _context.Reacciones
                    .Where(r => todoContenido.Select(c => c.Id).Contains(r.ContenidoId))
                    .GroupBy(r => r.ContenidoId)
                    .Select(g => new
                    {
                        ContenidoId = g.Key,
                        TotalReacciones = g.Count(),
                        Reacciones = g.GroupBy(r => r.TipoReaccion)
                            .Select(rg => new { Tipo = rg.Key, Count = rg.Count() })
                            .ToList()
                    })
                    .ToListAsync();

                ViewBag.ReaccionesPorContenido = reaccionesPorContenido.ToDictionary(r => r.ContenidoId);

                // 12. Obtener algoritmo del usuario y aplicarlo
                var algoritmoUsuario = await _feedAlgorithmService.ObtenerAlgoritmoUsuarioAsync(usuarioId, _context);
                var codigoAlgoritmo = algoritmoUsuario?.Codigo ?? "cronologico";

                var contenidoOrdenado = await _feedAlgorithmService.AplicarAlgoritmoAsync(
                    todoContenido,
                    codigoAlgoritmo,
                    usuarioId,
                    _context);

                contenidoOrdenado = contenidoOrdenado.Take(50).ToList();

                // Incrementar contador de uso del algoritmo
                if (algoritmoUsuario != null)
                {
                    await _feedAlgorithmService.IncrementarUsoAsync(algoritmoUsuario.Id, _context);
                }

                // Pasar info del algoritmo a la vista
                ViewBag.AlgoritmoActual = algoritmoUsuario;
                ViewBag.AlgoritmosDisponibles = await _feedAlgorithmService.ObtenerAlgoritmosActivosAsync(_context);

                // 13. ViewBag data
                ViewBag.EstaSuscrito = true;
                ViewBag.TotalLadoA = contenidoPublico.Count;
                ViewBag.TotalLadoB = contenidoPremiumSuscripciones.Count + contenidoPremiumPropio.Count;

                // 14. Sugerencias de usuarios NO suscritos (excluyendo admins)
                var adminUsersIndex = await _userManager.GetUsersInRoleAsync("Admin");
                var adminIdsIndex = adminUsersIndex.Select(u => u.Id).ToList();

                ViewBag.CreadoresSugeridos = await _userManager.Users
                    .Where(u => u.Id != usuarioId
                            && u.EstaActivo
                            && !creadoresIds.Contains(u.Id) // Excluye a los que ya está suscrito
                            && !adminIdsIndex.Contains(u.Id)) // Excluir administradores
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .ThenBy(u => Guid.NewGuid())
                    .Take(5)
                    .ToListAsync();
                // Obtener usuario actual para el sidebar
                var usuarioActual = await _userManager.FindByIdAsync(usuarioId);
                ViewBag.UsuarioActual = usuarioActual;

                // Obtener los likes del usuario para marcarlos en el feed
                var contenidoIds = contenidoOrdenado.Select(c => c.Id).ToList();
                var likesUsuario = await _context.Likes
                    .Where(l => l.UsuarioId == usuarioId && contenidoIds.Contains(l.ContenidoId))
                    .Select(l => l.ContenidoId)
                    .ToListAsync();
                ViewBag.LikesUsuario = likesUsuario;

                // Obtener los favoritos del usuario para marcarlos en el feed
                var favoritosUsuario = await _context.Favoritos
                    .Where(f => f.UsuarioId == usuarioId && contenidoIds.Contains(f.ContenidoId))
                    .Select(f => f.ContenidoId)
                    .ToListAsync();
                ViewBag.FavoritosIds = favoritosUsuario;

                // Obtener los usuarios que el usuario actual sigue (para mostrar "Siguiendo" en fullscreen)
                var usuariosSeguidos = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();
                ViewBag.UsuariosSeguidosIds = usuariosSeguidos;

                // Cargar anuncios para el feed
                var anuncios = await _adService.ObtenerAnunciosActivos(3, usuarioId);
                ViewBag.Anuncios = anuncios;

                // Registrar impresiones de los anuncios
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                foreach (var anuncio in anuncios)
                {
                    await _adService.RegistrarImpresion(anuncio.Id, usuarioId, ipAddress);
                }

                // Si viene de un link compartido
                if (post.HasValue)
                {
                    // Cargar el post compartido
                    var postCompartido = await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.PistaMusical)
                        .Include(c => c.Archivos.OrderBy(a => a.Orden))
                        .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                            .ThenInclude(com => com.Usuario)
                        .FirstOrDefaultAsync(c => c.Id == post.Value && c.EstaActivo && !c.EsBorrador);

                    if (postCompartido != null)
                    {
                        // Verificar si el usuario tiene acceso al post
                        var creadorId = postCompartido.UsuarioId;
                        var tieneAcceso = false;

                        // Tiene acceso si: es su propio post, o sigue al creador en el lado correcto, o es contenido público
                        if (creadorId == usuarioId)
                        {
                            tieneAcceso = true;
                        }
                        else if (postCompartido.TipoLado == TipoLado.LadoA)
                        {
                            // LadoA: necesita suscripción a LadoA
                            tieneAcceso = creadoresLadoAIds.Contains(creadorId);
                        }
                        else if (postCompartido.TipoLado == TipoLado.LadoB)
                        {
                            // LadoB: necesita suscripción a LadoB o haber comprado el contenido
                            tieneAcceso = creadoresLadoBIds.Contains(creadorId) || contenidosCompradosIds.Contains(postCompartido.Id);
                        }

                        if (tieneAcceso)
                        {
                            // Usuario tiene acceso - mover al inicio del feed
                            ViewBag.SharedPostId = post.Value;

                            // Remover si ya existe en otra posición
                            contenidoOrdenado.RemoveAll(c => c.Id == post.Value);
                            // Insertar al inicio
                            contenidoOrdenado.Insert(0, postCompartido);
                        }
                        else
                        {
                            // Usuario NO tiene acceso - mostrar sugerencia de seguir
                            ViewBag.SharedPostNoAccess = new {
                                PostId = postCompartido.Id,
                                CreadorId = creadorId,
                                CreadorNombre = postCompartido.Usuario?.Seudonimo ?? postCompartido.Usuario?.NombreCompleto ?? "Creador",
                                CreadorUsername = postCompartido.Usuario?.UserName,
                                CreadorFoto = postCompartido.TipoLado == TipoLado.LadoB
                                    ? (postCompartido.Usuario?.FotoPerfilLadoB ?? postCompartido.Usuario?.FotoPerfil)
                                    : postCompartido.Usuario?.FotoPerfil,
                                TipoLado = postCompartido.TipoLado.ToString(),
                                Precio = postCompartido.TipoLado == TipoLado.LadoB
                                    ? postCompartido.Usuario?.PrecioSuscripcionLadoB
                                    : postCompartido.Usuario?.PrecioSuscripcion
                            };
                        }
                    }
                }

                return View(contenidoOrdenado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el feed");
                TempData["Error"] = "Error al cargar el feed. Por favor, intenta nuevamente.";
                return View(new List<Contenido>());
            }
        }

        // ========================================
        // API: CARGAR MÁS POSTS (Infinite Scroll)
        // ========================================
        [HttpGet]
        public async Task<IActionResult> CargarMasPosts(int pagina = 0, int cantidad = 10)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                // Obtener suscripciones
                var suscripcionesActivas = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .ToListAsync();

                var creadoresLadoAIds = suscripcionesActivas
                    .Where(s => s.TipoLado == TipoLado.LadoA)
                    .Select(s => s.CreadorId)
                    .ToList();

                var creadoresLadoBIds = suscripcionesActivas
                    .Where(s => s.TipoLado == TipoLado.LadoB)
                    .Select(s => s.CreadorId)
                    .ToList();

                // Usuarios bloqueados
                var usuariosBloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == usuarioId || b.BloqueadoId == usuarioId)
                    .Select(b => b.BloqueadorId == usuarioId ? b.BloqueadoId : b.BloqueadorId)
                    .Distinct()
                    .ToListAsync();

                creadoresLadoAIds = creadoresLadoAIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();
                creadoresLadoBIds = creadoresLadoBIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();

                // Contenidos comprados
                var contenidosCompradosIds = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == usuarioId)
                    .Select(cc => cc.ContenidoId)
                    .ToListAsync();

                // ⚡ Optimización: Limitar carga inicial y usar deduplicación correcta por Id
                var limitePorFuente = Math.Max(50, (pagina + 2) * cantidad); // Cargar suficiente para paginación

                // Contenido público (LadoA)
                var contenidoPublico = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && (c.UsuarioId == usuarioId || !c.EsPrivado)
                            && c.TipoLado == TipoLado.LadoA
                            && c.Usuario != null
                            && (creadoresLadoAIds.Contains(c.UsuarioId) || c.UsuarioId == usuarioId))
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(limitePorFuente)
                    .ToListAsync();

                // Contenido premium (LadoB) de suscripciones
                var contenidoPremiumSuscripciones = creadoresLadoBIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.PistaMusical)
                        .Include(c => c.Archivos.OrderBy(a => a.Orden))
                        .Where(c => creadoresLadoBIds.Contains(c.UsuarioId)
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoB
                                && c.Usuario != null)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(limitePorFuente)
                        .ToListAsync()
                    : new List<Contenido>();

                // Contenido premium propio
                var contenidoPremiumPropio = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(limitePorFuente)
                    .ToListAsync();

                // Contenido comprado
                var contenidoPremiumComprado = contenidosCompradosIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.PistaMusical)
                        .Include(c => c.Archivos.OrderBy(a => a.Orden))
                        .Where(c => contenidosCompradosIds.Contains(c.Id)
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.Usuario != null)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(limitePorFuente)
                        .ToListAsync()
                    : new List<Contenido>();

                // ⚡ Combinar contenido usando DistinctBy por Id (evita duplicados correctamente)
                var todoContenido = contenidoPublico
                    .Concat(contenidoPremiumSuscripciones)
                    .Concat(contenidoPremiumPropio)
                    .Concat(contenidoPremiumComprado)
                    .DistinctBy(c => c.Id)
                    .ToList();

                // Aplicar algoritmo
                var algoritmoUsuario = await _feedAlgorithmService.ObtenerAlgoritmoUsuarioAsync(usuarioId, _context);
                var codigoAlgoritmo = algoritmoUsuario?.Codigo ?? "cronologico";

                var contenidoOrdenado = await _feedAlgorithmService.AplicarAlgoritmoAsync(
                    todoContenido,
                    codigoAlgoritmo,
                    usuarioId,
                    _context);

                // Paginar
                var contenidoPaginado = contenidoOrdenado
                    .Skip(pagina * cantidad)
                    .Take(cantidad)
                    .ToList();

                var hayMas = contenidoOrdenado.Count > (pagina + 1) * cantidad;

                // Obtener likes del usuario
                var contenidoIds = contenidoPaginado.Select(c => c.Id).ToList();
                var likesUsuario = await _context.Likes
                    .Where(l => l.UsuarioId == usuarioId && contenidoIds.Contains(l.ContenidoId))
                    .Select(l => l.ContenidoId)
                    .ToListAsync();

                // Serializar posts
                var posts = contenidoPaginado.Select(post =>
                {
                    var esVideo = post.TipoContenido == TipoContenido.Video;
                    var esAudio = post.TipoContenido == TipoContenido.Audio;
                    var archivos = post.TodosLosArchivos;

                    return new
                    {
                        id = post.Id,
                        usuarioId = post.UsuarioId,
                        username = post.Usuario?.UserName,
                        nombreCompleto = post.Usuario?.NombreCompleto,
                        seudonimo = post.Usuario?.Seudonimo,
                        fotoPerfil = post.Usuario?.FotoPerfil,
                        fotoPerfilLadoB = post.Usuario?.FotoPerfilLadoB,
                        verificado = post.Usuario?.CreadorVerificado ?? false,
                        tipoLado = (int)post.TipoLado,
                        descripcion = post.Descripcion,
                        rutaArchivo = post.RutaArchivo,
                        thumbnail = post.Thumbnail,
                        tipoContenido = (int)post.TipoContenido,
                        esVideo = esVideo,
                        esAudio = esAudio,
                        esCarrusel = archivos.Count > 1,
                        archivos = archivos.Select(a => new
                        {
                            rutaArchivo = a.RutaArchivo,
                            tipoArchivo = (int)a.TipoArchivo,
                            thumbnail = a.Thumbnail
                        }).ToList(),
                        numeroLikes = post.NumeroLikes,
                        numeroComentarios = post.NumeroComentarios,
                        fechaPublicacion = post.FechaPublicacion,
                        yaLeDioLike = likesUsuario.Contains(post.Id),
                        esContenidoSensible = post.EsContenidoSensible,
                        nombreUbicacion = post.NombreUbicacion,
                        pistaMusical = post.PistaMusical != null ? new
                        {
                            titulo = post.PistaMusical.Titulo,
                            artista = post.PistaMusical.Artista,
                            rutaArchivo = post.PistaMusical.RutaArchivo
                        } : null,
                        audioTrimInicio = post.AudioTrimInicio,
                        musicaVolumen = post.MusicaVolumen
                    };
                }).ToList();

                return Json(new
                {
                    success = true,
                    posts = posts,
                    pagina = pagina,
                    hayMas = hayMas,
                    total = contenidoOrdenado.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar más posts");
                return Json(new { success = false, message = "Error al cargar posts" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDetalleContenido(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Comentarios)
                        .ThenInclude(com => com.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id && c.EstaActivo);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar acceso
                var tieneAcceso = await VerificarAccesoContenido(usuarioId, contenido);
                if (contenido.TipoLado == TipoLado.LadoB && !tieneAcceso)
                {
                    return Json(new { success = false, message = "Sin acceso" });
                }

                // Verificar si el usuario dio like
                var usuarioLike = await _context.Likes
                    .AnyAsync(l => l.ContenidoId == id && l.UsuarioId == usuarioId);

                return Json(new
                {
                    success = true,
                    id = contenido.Id,
                    descripcion = contenido.Descripcion,
                    rutaArchivo = contenido.RutaArchivo,
                    tipoContenido = (int)contenido.TipoContenido,
                    numeroLikes = contenido.NumeroLikes,
                    numeroComentarios = contenido.NumeroComentarios,
                    fechaPublicacion = contenido.FechaPublicacion,
                    usuarioLike = usuarioLike,
                    usuario = new
                    {
                        id = contenido.Usuario.Id,
                        username = contenido.Usuario.UserName,
                        nombreCompleto = contenido.Usuario.NombreCompleto,
                        fotoPerfil = contenido.Usuario.FotoPerfil
                    },
                    comentarios = contenido.Comentarios
                        .OrderBy(c => c.FechaCreacion)
                        .Select(c => new
                        {
                            id = c.Id,
                            texto = c.Texto,
                            fechaCreacion = c.FechaCreacion,
                            usuario = new
                            {
                                id = c.Usuario.Id,
                                username = c.Usuario.UserName,
                                fotoPerfil = c.Usuario.FotoPerfil
                            }
                        })
                        .ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle de contenido {Id}", id);
                return Json(new { success = false, message = "Error al cargar contenido" });
            }
        }


        // ========================================
        // CALCULAR SCORE CON REACCIONES
        // ========================================

        private double CalcularScoreMejorado(Contenido contenido, string usuarioActualId)
        {
            try
            {
                var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;

                double baseScore = 100.0 / (1 + horasDesdePublicacion / 24.0);

                if (horasDesdePublicacion < 6)
                {
                    baseScore += 50.0;
                }
                else if (horasDesdePublicacion < 24)
                {
                    baseScore += 25.0;
                }

                var totalReacciones = _context.Reacciones
                    .Count(r => r.ContenidoId == contenido.Id);

                double totalEngagement = contenido.NumeroLikes
                                       + (contenido.NumeroComentarios * 2.0)
                                       + (totalReacciones * 1.5)
                                       + (contenido.NumeroVistas * 0.1)
                                       + (contenido.NumeroCompartidos * 3.0);

                double engagementBoost = Math.Log(1 + totalEngagement) * 10.0;
                double premiumBoost = contenido.TipoLado == TipoLado.LadoB ? 15.0 : 0.0;
                double propioBoost = contenido.UsuarioId == usuarioActualId ? 20.0 : 0.0;
                double previewBoost = contenido.TienePreview ? 10.0 : 0.0;

                double edadPenalizacion = 1.0;
                if (horasDesdePublicacion > 168)
                {
                    edadPenalizacion = 0.5;
                }

                return (baseScore + engagementBoost + premiumBoost + propioBoost + previewBoost) * edadPenalizacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular score para contenido {Id}", contenido?.Id);
                return 0;
            }
        }

        // ========================================
        // CAMBIAR ALGORITMO DE FEED
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarAlgoritmo(int algoritmoId)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var algoritmo = await _context.AlgoritmosFeed.FindAsync(algoritmoId);
                if (algoritmo == null || !algoritmo.Activo)
                {
                    return Json(new { success = false, message = "Algoritmo no disponible" });
                }

                await _feedAlgorithmService.GuardarPreferenciaUsuarioAsync(usuarioId, algoritmoId, _context);

                _logger.LogInformation("Usuario {UsuarioId} cambio a algoritmo {Algoritmo}", usuarioId, algoritmo.Nombre);

                return Json(new
                {
                    success = true,
                    message = $"Algoritmo cambiado a '{algoritmo.Nombre}'",
                    algoritmo = new
                    {
                        algoritmo.Id,
                        algoritmo.Codigo,
                        algoritmo.Nombre,
                        algoritmo.Icono
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar algoritmo");
                return Json(new { success = false, message = "Error al cambiar algoritmo" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlgoritmosDisponibles()
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var algoritmos = await _feedAlgorithmService.ObtenerAlgoritmosActivosAsync(_context);
                var algoritmoActual = await _feedAlgorithmService.ObtenerAlgoritmoUsuarioAsync(usuarioId ?? "", _context);

                return Json(new
                {
                    success = true,
                    algoritmos = algoritmos.Select(a => new
                    {
                        a.Id,
                        a.Codigo,
                        a.Nombre,
                        a.Descripcion,
                        a.Icono,
                        EsActual = algoritmoActual?.Id == a.Id
                    }),
                    algoritmoActualId = algoritmoActual?.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener algoritmos disponibles");
                return Json(new { success = false, message = "Error" });
            }
        }

        // ========================================
        // COMENTAR
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comentar(int id, string texto, int? comentarioPadreId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return Json(new { success = false, message = "El comentario no puede estar vacío" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                // ========================================
                // RATE LIMITING MÚLTIPLE (Anti-Inyección)
                // ========================================

                // 1. Rate limit por USUARIO: máximo 10 comentarios por minuto
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"comentar_user_{usuarioId}",
                    10,
                    TimeSpan.FromMinutes(1),
                    TipoAtaque.SpamContenido,
                    "/Feed/Comentar",
                    usuarioId))
                {
                    return Json(new { success = false, message = "Has comentado demasiado rápido. Espera un momento." });
                }

                // 2. Rate limit por IP: máximo 20 comentarios por minuto (detecta multi-cuenta)
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"comentar_ip_{clientIp}",
                    20,
                    TimeSpan.FromMinutes(1),
                    TipoAtaque.SpamContenido,
                    "/Feed/Comentar",
                    usuarioId))
                {
                    _logger.LogWarning("🚨 POSIBLE ATAQUE MULTI-CUENTA desde IP: {Ip}", clientIp);
                    return Json(new { success = false, message = "Demasiada actividad desde tu conexión. Intenta más tarde." });
                }

                // 3. Rate limit por CONTENIDO: máximo 50 comentarios por hora en el mismo post
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"comentar_contenido_{id}",
                    50,
                    TimeSpan.FromHours(1),
                    TipoAtaque.SpamContenido,
                    "/Feed/Comentar",
                    usuarioId))
                {
                    _logger.LogWarning("🚨 FLOOD DE COMENTARIOS en contenido {Id} desde IP: {Ip}", id, clientIp);
                    return Json(new { success = false, message = "Esta publicación ha recibido muchos comentarios. Intenta más tarde." });
                }

                // 4. Rate limit por IP+Contenido: máximo 5 comentarios por post por IP
                if (!await _rateLimitService.IsAllowedAsync(
                    clientIp,
                    $"comentar_ip_contenido_{clientIp}_{id}",
                    5,
                    TimeSpan.FromHours(1),
                    TipoAtaque.SpamContenido,
                    "/Feed/Comentar",
                    usuarioId))
                {
                    return Json(new { success = false, message = "Ya has comentado suficiente en esta publicación." });
                }

                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar si los comentarios están desactivados
                if (contenido.ComentariosDesactivados)
                {
                    return Json(new { success = false, message = "Los comentarios están desactivados para esta publicación" });
                }

                if (contenido.TipoLado == TipoLado.LadoB)
                {
                    var tieneAcceso = await VerificarAccesoContenido(usuarioId, contenido);

                    if (!tieneAcceso)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Necesitas estar suscrito o comprar este contenido para comentar"
                        });
                    }
                }

                // Validar comentario padre si es una respuesta
                Comentario? comentarioPadre = null;
                string? usuarioPadreId = null;
                if (comentarioPadreId.HasValue)
                {
                    comentarioPadre = await _context.Comentarios
                        .FirstOrDefaultAsync(c => c.Id == comentarioPadreId.Value && c.ContenidoId == id && c.EstaActivo);

                    if (comentarioPadre == null)
                    {
                        return Json(new { success = false, message = "El comentario al que intentas responder no existe" });
                    }

                    // Solo permitir 1 nivel de anidación (respuestas directas, no respuestas a respuestas)
                    if (comentarioPadre.ComentarioPadreId.HasValue)
                    {
                        // Si el padre ya es una respuesta, usar el abuelo como padre (mantener en 1 nivel)
                        comentarioPadreId = comentarioPadre.ComentarioPadreId;
                    }

                    usuarioPadreId = comentarioPadre.UsuarioId;
                }

                var comentario = new Comentario
                {
                    ContenidoId = id,
                    UsuarioId = usuarioId,
                    Texto = texto.Trim(),
                    FechaCreacion = DateTime.Now,
                    ComentarioPadreId = comentarioPadreId
                };

                _context.Comentarios.Add(comentario);
                contenido.NumeroComentarios++;

                await _context.SaveChangesAsync();

                // Registrar interaccion para clasificacion de intereses
                if (!string.IsNullOrEmpty(usuarioId))
                {
                    _ = _interesesService.RegistrarInteraccionAsync(usuarioId, id, TipoInteraccion.Comentario);
                }

                var usuario = await _userManager.FindByIdAsync(usuarioId);

                // Notificar al dueño del contenido sobre el nuevo comentario
                if (contenido.UsuarioId != usuarioId)
                {
                    _ = _notificationService.NotificarNuevoComentarioAsync(
                        contenido.UsuarioId,
                        usuarioId!,
                        contenido.Id,
                        comentario.Id);
                }

                // Si es una respuesta, notificar también al autor del comentario original
                if (usuarioPadreId != null && usuarioPadreId != usuarioId && usuarioPadreId != contenido.UsuarioId)
                {
                    _ = _notificationService.NotificarNuevoComentarioAsync(
                        usuarioPadreId,
                        usuarioId!,
                        contenido.Id,
                        comentario.Id);
                }

                _logger.LogInformation("Comentario agregado: Usuario {UserId} en Contenido {ContenidoId} (Respuesta a: {PadreId})",
                    usuarioId, id, comentarioPadreId);

                return Json(new
                {
                    success = true,
                    comentario = new
                    {
                        id = comentario.Id,
                        texto = comentario.Texto,
                        nombreUsuario = usuario.NombreCompleto,
                        username = usuario.UserName,
                        avatar = usuario.FotoPerfil,
                        comentarioPadreId = comentario.ComentarioPadreId,
                        esRespuesta = comentario.ComentarioPadreId.HasValue
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

        // ========================================
        // OBTENER COMENTARIOS CON RESPUESTAS
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerComentarios(int contenidoId, int page = 1, int pageSize = 10)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var contenido = await _context.Contenidos.FindAsync(contenidoId);
                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Obtener comentarios principales (sin padre) con sus respuestas
                var comentariosPrincipales = await _context.Comentarios
                    .Include(c => c.Usuario)
                    .Include(c => c.Respuestas.Where(r => r.EstaActivo))
                        .ThenInclude(r => r.Usuario)
                    .Where(c => c.ContenidoId == contenidoId && c.EstaActivo && c.ComentarioPadreId == null)
                    .OrderByDescending(c => c.FechaCreacion)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalComentarios = await _context.Comentarios
                    .CountAsync(c => c.ContenidoId == contenidoId && c.EstaActivo && c.ComentarioPadreId == null);

                return Json(new
                {
                    success = true,
                    comentarios = comentariosPrincipales.Select(c => new
                    {
                        id = c.Id,
                        texto = c.Texto,
                        fechaCreacion = c.FechaCreacion,
                        usuario = new
                        {
                            id = c.Usuario?.Id,
                            username = c.Usuario?.UserName,
                            nombreCompleto = c.Usuario?.NombreCompleto,
                            fotoPerfil = c.Usuario?.FotoPerfil
                        },
                        respuestas = c.Respuestas
                            .Where(r => r.EstaActivo)
                            .OrderBy(r => r.FechaCreacion)
                            .Select(r => new
                            {
                                id = r.Id,
                                texto = r.Texto,
                                fechaCreacion = r.FechaCreacion,
                                usuario = new
                                {
                                    id = r.Usuario?.Id,
                                    username = r.Usuario?.UserName,
                                    nombreCompleto = r.Usuario?.NombreCompleto,
                                    fotoPerfil = r.Usuario?.FotoPerfil
                                }
                            }).ToList(),
                        totalRespuestas = c.Respuestas.Count(r => r.EstaActivo)
                    }),
                    totalComentarios,
                    hasMore = page * pageSize < totalComentarios,
                    page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener comentarios del contenido {Id}", contenidoId);
                return Json(new { success = false, message = "Error al cargar comentarios" });
            }
        }

        // ========================================
        // COMPARTIR CONTENIDO
        // ========================================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Compartir(int id)
        {
            try
            {
                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null || !contenido.EstaActivo)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                contenido.NumeroCompartidos++;
                await _context.SaveChangesAsync();

                // Generar URL que abre el post dentro del feed normal
                var feedUrl = Url.Action("Index", "Feed", null, Request.Scheme) + "?post=" + id;

                return Json(new
                {
                    success = true,
                    url = feedUrl,
                    totalCompartidos = contenido.NumeroCompartidos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al compartir contenido {ContenidoId}", id);
                return Json(new { success = false, message = "Error al compartir" });
            }
        }

        // ========================================
        // OBTENER USUARIOS QUE SIGO (PARA COMPARTIR)
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerSiguiendo()
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new List<object>());
                }

                // Obtener usuarios que sigo (con suscripción activa)
                var siguiendo = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .Distinct()
                    .ToListAsync();

                var usuarios = await _context.Users
                    .Where(u => siguiendo.Contains(u.Id))
                    .Select(u => new
                    {
                        id = u.Id,
                        nombre = u.NombreCompleto,
                        username = u.UserName,
                        fotoPerfil = u.FotoPerfil
                    })
                    .OrderBy(u => u.nombre)
                    .ToListAsync();

                return Json(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios siguiendo");
                return Json(new List<object>());
            }
        }

        // ========================================
        // ENVIAR CONTENIDO A AMIGOS
        // ========================================

        [HttpPost]
        public async Task<IActionResult> EnviarAAmigos([FromBody] EnviarAAmigosRequest request)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Debes iniciar sesión" });
                }

                if (request.AmigosIds == null || !request.AmigosIds.Any())
                {
                    return Json(new { success = false, message = "Selecciona al menos un amigo" });
                }

                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == request.ContenidoId);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                int enviados = 0;
                foreach (var amigoId in request.AmigosIds.Distinct())
                {
                    // Verificar que el amigo exista y que lo sigo
                    var suscripcion = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioActual.Id && s.CreadorId == amigoId && s.EstaActiva);

                    if (!suscripcion) continue;

                    // Crear mensaje privado con el contenido compartido
                    var tipoContenido = contenido.TipoContenido == TipoContenido.Foto || contenido.TipoContenido == TipoContenido.Imagen
                        ? "📷" : contenido.TipoContenido == TipoContenido.Video ? "🎬" : "📝";

                    var mensaje = new MensajePrivado
                    {
                        RemitenteId = usuarioActual.Id,
                        DestinatarioId = amigoId,
                        Contenido = $"{tipoContenido} Te compartió una publicación de @{contenido.Usuario?.UserName ?? "usuario"}\n{request.Url}",
                        FechaEnvio = DateTime.Now,
                        Leido = false
                    };

                    _context.MensajesPrivados.Add(mensaje);
                    enviados++;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, enviados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar contenido a amigos");
                return Json(new { success = false, message = "Error al enviar" });
            }
        }

        public class EnviarAAmigosRequest
        {
            public int ContenidoId { get; set; }
            public List<string> AmigosIds { get; set; } = new();
            public string Url { get; set; } = "";
        }

        // ========================================
        // SEGUIR / DEJAR DE SEGUIR
        // ========================================

        [HttpPost]
        public async Task<IActionResult> Seguir(string id, int? tipoLado = null)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Debes iniciar sesión" });
                }

                if (usuarioActual.Id == id)
                {
                    return Json(new { success = false, message = "No puedes seguirte a ti mismo" });
                }

                var creador = await _userManager.FindByIdAsync(id);
                if (creador == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Determinar el TipoLado (por defecto LadoA si no se especifica)
                var tipo = tipoLado.HasValue ? (TipoLado)tipoLado.Value : TipoLado.LadoA;

                // Verificar si ya existe una suscripción activa para este TipoLado específico
                var suscripcionExistente = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.FanId == usuarioActual.Id && s.CreadorId == id && s.TipoLado == tipo && s.EstaActiva);

                // Usar transacción para garantizar consistencia del contador
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    bool siguiendo;
                    if (suscripcionExistente != null)
                    {
                        // Dejar de seguir
                        suscripcionExistente.EstaActiva = false;
                        suscripcionExistente.FechaCancelacion = DateTime.Now;
                        creador.NumeroSeguidores = Math.Max(0, creador.NumeroSeguidores - 1);
                        siguiendo = false;
                    }
                    else
                    {
                        // Seguir (crear suscripción gratuita para el TipoLado específico)
                        var nuevaSuscripcion = new Suscripcion
                        {
                            FanId = usuarioActual.Id,
                            CreadorId = id,
                            PrecioMensual = 0,
                            Precio = 0,
                            FechaInicio = DateTime.Now,
                            ProximaRenovacion = DateTime.Now.AddMonths(1),
                            EstaActiva = true,
                            RenovacionAutomatica = false,
                            TipoLado = tipo
                        };
                        _context.Suscripciones.Add(nuevaSuscripcion);
                        creador.NumeroSeguidores++;
                        siguiendo = true;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Notificar al creador sobre el nuevo seguidor
                    if (siguiendo)
                    {
                        _ = _notificationService.NotificarNuevoSeguidorAsync(id, usuarioActual.Id);
                    }

                    return Json(new
                    {
                        success = true,
                        siguiendo,
                        seguidores = creador.NumeroSeguidores,
                        tipoLado = (int)tipo
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error en transacción de seguir usuario {UserId}", id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al seguir usuario {UserId}", id);
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }
        }

        // Endpoint para dejar de seguir (para el menú)
        [HttpPost]
        public async Task<IActionResult> DejarDeSeguir(string id, int? tipoLado = null)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Debes iniciar sesión" });
                }

                var creador = await _userManager.FindByIdAsync(id);
                if (creador == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                // Si se especifica tipoLado, dejar de seguir solo ese lado, sino todos
                var query = _context.Suscripciones
                    .Where(s => s.FanId == usuarioActual.Id && s.CreadorId == id && s.EstaActiva);

                if (tipoLado.HasValue)
                {
                    query = query.Where(s => s.TipoLado == (TipoLado)tipoLado.Value);
                }

                var suscripciones = await query.ToListAsync();

                foreach (var suscripcion in suscripciones)
                {
                    suscripcion.EstaActiva = false;
                    suscripcion.FechaCancelacion = DateTime.Now;
                    creador.NumeroSeguidores = Math.Max(0, creador.NumeroSeguidores - 1);
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Has dejado de seguir a este usuario" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dejar de seguir usuario {UserId}", id);
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }
        }

        // Endpoint para reportar contenido
        [HttpPost]
        public async Task<IActionResult> Reportar(int id, string motivo)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Debes iniciar sesión" });
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar si ya existe un reporte del mismo usuario
                var reporteExistente = await _context.Reportes
                    .AnyAsync(r => r.ContenidoId == id && r.UsuarioReportadorId == usuarioActual.Id && r.Estado == "Pendiente");

                if (reporteExistente)
                {
                    return Json(new { success = false, message = "Ya has reportado este contenido" });
                }

                var reporte = new Reporte
                {
                    ContenidoId = id,
                    UsuarioReportadorId = usuarioActual.Id,
                    Motivo = motivo ?? "Sin especificar",
                    TipoReporte = "Contenido",
                    FechaReporte = DateTime.Now,
                    Estado = "Pendiente"
                };

                _context.Reportes.Add(reporte);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Reporte enviado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reportar contenido {ContenidoId}", id);
                return Json(new { success = false, message = "Error al procesar el reporte" });
            }
        }

        // Endpoint para agregar a favoritos
        [HttpPost]
        public async Task<IActionResult> AgregarFavorito(int id)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "Debes iniciar sesión" });
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar si ya existe en favoritos
                var favoritoExistente = await _context.Favoritos
                    .FirstOrDefaultAsync(f => f.ContenidoId == id && f.UsuarioId == usuarioActual.Id);

                bool esFavorito;
                if (favoritoExistente != null)
                {
                    // Quitar de favoritos
                    _context.Favoritos.Remove(favoritoExistente);
                    esFavorito = false;
                }
                else
                {
                    // Agregar a favoritos
                    var favorito = new Favorito
                    {
                        ContenidoId = id,
                        UsuarioId = usuarioActual.Id,
                        FechaAgregado = DateTime.Now
                    };
                    _context.Favoritos.Add(favorito);
                    esFavorito = true;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    esFavorito,
                    message = esFavorito ? "Agregado a favoritos" : "Eliminado de favoritos"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al manejar favorito {ContenidoId}", id);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        // ========================================
        // EXPLORAR
        // ========================================

        public async Task<IActionResult> Explorar()
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // ⚡ Obtener IDs de administradores CON CACHE
                var adminIds = await ObtenerAdminIdsCacheadosAsync();

                // ⚡ Obtener usuarios bloqueados CON CACHE
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // Mostrar creadores: EsCreador = true O tienen Seudonimo (creadores de facto)
                var usuarios = await _userManager.Users
                    .AsNoTracking() // ⚡ No tracking para read-only
                    .Where(u => u.EstaActivo
                            && (u.EsCreador || u.Seudonimo != null)
                            && u.Id != usuarioActual.Id
                            && !adminIds.Contains(u.Id)
                            && !usuariosBloqueadosIds.Contains(u.Id))
                    .OrderByDescending(u => u.CreadorVerificado)
                    .ThenByDescending(u => u.NumeroSeguidores)
                    .Take(50)
                    .ToListAsync();

                // Separar creadores por tipo
                // LadoA: TODOS los creadores (muestran su identidad pública)
                // LadoB: Solo los que tienen LadoB habilitado (muestran su identidad premium/seudónimo)
                var creadoresLadoA = usuarios.ToList(); // Todos aparecen en Creadores
                var creadoresLadoB = usuarios.Where(c => c.TieneLadoB()).ToList(); // Solo LadoB en Premium

                // Debug: mostrar usuarios que podrían ser LadoB pero no cumplen todas las condiciones
                var potencialesLadoB = usuarios.Where(u => u.EsCreador && !string.IsNullOrEmpty(u.Seudonimo) && !u.CreadorVerificado).ToList();
                if (potencialesLadoB.Any())
                {
                    _logger.LogWarning("Usuarios con Seudonimo pero sin CreadorVerificado: {Usuarios}",
                        string.Join(", ", potencialesLadoB.Select(u => $"{u.UserName} (CreadorVerificado={u.CreadorVerificado})")));
                }

                _logger.LogInformation("Explorar - LadoA: {LadoA}, LadoB: {LadoB}, Total: {Total}",
                    creadoresLadoA.Count, creadoresLadoB.Count, usuarios.Count);

                ViewBag.CreadoresLadoA = creadoresLadoA;
                ViewBag.CreadoresLadoB = creadoresLadoB;

                // Obtener IDs de creadores a los que el usuario está suscrito (cualquier tipo)
                var suscripcionesActivas = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();
                ViewBag.SuscripcionesActivas = suscripcionesActivas;

                // Suscripciones específicas a LadoB (para desbloquear contenido premium)
                var suscripcionesLadoB = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva && s.TipoLado == TipoLado.LadoB)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Obtener contenido para explorar (60 items iniciales)
                // Mostrar TODO el contenido (LadoA y LadoB) - el LadoB se mostrará bloqueado si no está suscrito
                var contenidoExplorar = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .AsNoTracking() // ⚡ No tracking para read-only
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.RutaArchivo != null && c.RutaArchivo != "" // ⚡ Mejor para índices
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(60)
                    .ToListAsync();

                ViewBag.ContenidoExplorar = contenidoExplorar;
                ViewBag.SuscripcionesLadoBIds = suscripcionesLadoB; // Para verificar acceso a LadoB (solo suscripciones premium)

                // ========================================
                // DATOS PARA TAB MAPA (SOLO LADO A)
                // ========================================

                // Obtener zonas de ubicación únicas (SOLO LadoA - contenido público)
                var zonasUbicacion = await _context.Contenidos
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // SOLO LadoA
                            && c.NombreUbicacion != null && c.NombreUbicacion != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .GroupBy(c => c.NombreUbicacion)
                    .Select(g => new { Nombre = g.Key, Count = g.Count() })
                    .OrderByDescending(z => z.Count)
                    .Take(20)
                    .ToListAsync();

                ViewBag.ZonasUbicacion = zonasUbicacion;

                // Contenido inicial del mapa (30 items con ubicación, SOLO LadoA)
                var contenidoMapa = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // SOLO LadoA
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && c.NombreUbicacion != null && c.NombreUbicacion != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(30)
                    .ToListAsync();

                ViewBag.ContenidoMapa = contenidoMapa;

                _logger.LogInformation("Explorar: {Creadores} creadores, {Contenido} contenidos, {Zonas} zonas, {ContenidoMapa} items en mapa",
                    usuarios.Count, contenidoExplorar.Count, zonasUbicacion.Count, contenidoMapa.Count);

                return View(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar explorar");
                TempData["Error"] = "Error al cargar usuarios";
                return View(new List<ApplicationUser>());
            }
        }

        // ========================================
        // EXPLORAR CONTENIDO (AJAX - INFINITE SCROLL)
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ExplorarContenido(int page = 1, string tipo = "todos", string orden = "popular")
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                const int pageSize = 30;
                var skip = (page - 1) * pageSize;

                // ⚡ Usar métodos cacheados
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // Obtener suscripciones específicas a LadoB para verificar acceso a contenido premium
                var suscripcionesLadoB = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva && s.TipoLado == TipoLado.LadoB)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                var query = _context.Contenidos
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId));

                // Filtrar por tipo
                if (tipo == "fotos")
                {
                    query = query.Where(c => c.TipoContenido == TipoContenido.Foto || c.TipoContenido == TipoContenido.Imagen);
                }
                else if (tipo == "videos")
                {
                    query = query.Where(c => c.TipoContenido == TipoContenido.Video);
                }

                // Ordenar
                if (orden == "reciente")
                {
                    query = query.OrderByDescending(c => c.FechaPublicacion);
                }
                else
                {
                    query = query.OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                                 .ThenByDescending(c => c.FechaPublicacion);
                }

                // ⚡ Proyección directa - NO usar Include, solo cargar campos necesarios
                var contenido = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        id = c.Id,
                        rutaArchivo = c.RutaArchivo,
                        tipoContenido = (int)c.TipoContenido,
                        tipoLado = (int)c.TipoLado,
                        usuarioId = c.UsuarioId,
                        esContenidoSensible = c.EsContenidoSensible,
                        numeroLikes = c.NumeroLikes,
                        numeroComentarios = c.NumeroComentarios,
                        thumbnail = c.Thumbnail,
                        usuarioUsername = c.Usuario != null ? c.Usuario.UserName : null,
                        usuarioFotoPerfil = c.Usuario != null ? c.Usuario.FotoPerfil : null
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    items = contenido.Select(c => new
                    {
                        c.id,
                        c.rutaArchivo,
                        c.tipoContenido,
                        c.tipoLado,
                        esPremium = c.tipoLado == (int)TipoLado.LadoB,
                        bloqueado = c.tipoLado == (int)TipoLado.LadoB && !suscripcionesLadoB.Contains(c.usuarioId),
                        c.esContenidoSensible,
                        c.numeroLikes,
                        c.numeroComentarios,
                        c.thumbnail,
                        usuario = new
                        {
                            id = c.usuarioId,
                            username = c.usuarioUsername,
                            fotoPerfil = c.usuarioFotoPerfil
                        }
                    }),
                    hasMore = contenido.Count == pageSize,
                    page = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExplorarContenido");
                return Json(new { success = false, message = "Error al cargar contenido" });
            }
        }

        // ========================================
        // EXPLORAR CONTENIDO POR UBICACIÓN (AJAX - MAPA)
        // Solo contenido Lado A (público) con ubicación
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ExplorarContenidoPorUbicacion(int page = 1, string ubicacion = "", string orden = "popular")
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                const int pageSize = 30;
                var skip = (page - 1) * pageSize;

                // ⚡ Usar método cacheado
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // Query base: SOLO LadoA (contenido público), NUNCA LadoB
                var query = _context.Contenidos
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // CRÍTICO: Solo LadoA
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && c.NombreUbicacion != null && c.NombreUbicacion != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId));

                // Filtrar por ubicación si se especifica
                if (!string.IsNullOrEmpty(ubicacion) && ubicacion != "todas")
                {
                    query = query.Where(c => c.NombreUbicacion == ubicacion);
                }

                // Ordenar
                if (orden == "reciente")
                {
                    query = query.OrderByDescending(c => c.FechaPublicacion);
                }
                else
                {
                    query = query.OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                                 .ThenByDescending(c => c.FechaPublicacion);
                }

                // ⚡ Proyección directa - NO usar Include
                var contenido = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        id = c.Id,
                        rutaArchivo = c.RutaArchivo,
                        tipoContenido = (int)c.TipoContenido,
                        nombreUbicacion = c.NombreUbicacion,
                        esContenidoSensible = c.EsContenidoSensible,
                        numeroLikes = c.NumeroLikes,
                        numeroComentarios = c.NumeroComentarios,
                        thumbnail = c.Thumbnail,
                        usuarioId = c.UsuarioId,
                        usuarioUsername = c.Usuario != null ? c.Usuario.UserName : null,
                        usuarioFotoPerfil = c.Usuario != null ? c.Usuario.FotoPerfil : null
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    items = contenido.Select(c => new
                    {
                        c.id,
                        c.rutaArchivo,
                        c.tipoContenido,
                        c.nombreUbicacion,
                        c.esContenidoSensible,
                        c.numeroLikes,
                        c.numeroComentarios,
                        c.thumbnail,
                        usuario = new
                        {
                            id = c.usuarioId,
                            username = c.usuarioUsername,
                            fotoPerfil = c.usuarioFotoPerfil
                        }
                    }),
                    hasMore = contenido.Count == pageSize,
                    page = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ExplorarContenidoPorUbicacion");
                return Json(new { success = false, message = "Error al cargar contenido" });
            }
        }

        // ========================================
        // OBTENER CONTENIDO PARA MAPA (AJAX)
        // Solo Lado A con coordenadas + offset de privacidad
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerContenidoMapa()
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                // Obtener usuarios bloqueados
                var usuariosBloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == usuarioActual.Id || b.BloqueadoId == usuarioActual.Id)
                    .Select(b => b.BloqueadorId == usuarioActual.Id ? b.BloqueadoId : b.BloqueadorId)
                    .Distinct()
                    .ToListAsync();

                // Obtener contenido Lado A con coordenadas válidas
                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // SOLO Lado A
                            && c.Latitud.HasValue
                            && c.Longitud.HasValue
                            && !string.IsNullOrEmpty(c.RutaArchivo)
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(100) // Limitar a 100 para rendimiento del mapa
                    .ToListAsync();

                var random = new Random();

                return Json(new
                {
                    success = true,
                    items = contenido.Select(c =>
                    {
                        // Offset aleatorio para privacidad (~500m)
                        var latOffset = (random.NextDouble() - 0.5) * 0.01;
                        var lonOffset = (random.NextDouble() - 0.5) * 0.01;

                        return new
                        {
                            id = c.Id,
                            lat = c.Latitud!.Value + latOffset,
                            lon = c.Longitud!.Value + lonOffset,
                            thumbnail = !string.IsNullOrEmpty(c.Thumbnail) ? c.Thumbnail : c.RutaArchivo,
                            likes = c.NumeroLikes,
                            ubicacion = c.NombreUbicacion,
                            esVideo = c.TipoContenido == TipoContenido.Video,
                            esSensible = c.EsContenidoSensible
                        };
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerContenidoMapa");
                return Json(new { success = false, message = "Error al cargar mapa" });
            }
        }

        // ========================================
        // DETALLE DE CONTENIDO CON REACCIONES
        // ========================================

        [AllowAnonymous]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);

                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Archivos) // Incluir archivos para carrusel
                    .Include(c => c.PistaMusical) // Incluir música asociada
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

                // Si no está autenticado, solo puede ver contenido público general
                if (usuarioActual == null)
                {
                    if (!contenido.EsPublicoGeneral)
                    {
                        // Redirigir al login con returnUrl
                        return RedirectToAction("Login", "Account", new { returnUrl = $"/Feed/Detalle/{id}" });
                    }

                    // Usuario no autenticado viendo contenido público
                    contenido.NumeroVistas++;
                    await _context.SaveChangesAsync();

                    ViewBag.Reacciones = new List<object>();
                    ViewBag.TotalReacciones = 0;
                    ViewBag.MiReaccion = null;
                    ViewBag.EstaSuscrito = false;
                    ViewBag.EsPropio = false;
                    ViewBag.EsFavorito = false;
                    ViewBag.EstaAutenticado = false;

                    _logger.LogInformation("Detalle público visto: Contenido {Id} por usuario anónimo", id);

                    return View(contenido);
                }

                // Usuario autenticado
                ViewBag.EstaAutenticado = true;

                var esPropio = contenido.UsuarioId == usuarioActual.Id;
                var tieneAcceso = esPropio || await VerificarAccesoContenido(usuarioActual.Id, contenido);

                if (contenido.TipoLado == TipoLado.LadoB && !tieneAcceso)
                {
                    TempData["Error"] = $"Necesitas estar suscrito a {contenido.Usuario.NombreCompleto} o comprar este contenido para verlo";
                    return RedirectToAction("Perfil", new { id = contenido.UsuarioId });
                }

                if (!esPropio)
                {
                    contenido.NumeroVistas++;
                    await _context.SaveChangesAsync();
                }

                var reacciones = await _context.Reacciones
                    .Where(r => r.ContenidoId == id)
                    .GroupBy(r => r.TipoReaccion)
                    .Select(g => new
                    {
                        Tipo = g.Key.ToString().ToLower(),
                        Count = g.Count()
                    })
                    .ToListAsync();

                ViewBag.Reacciones = reacciones;
                ViewBag.TotalReacciones = reacciones.Sum(r => r.Count);

                var miReaccion = await _context.Reacciones
                    .Where(r => r.ContenidoId == id && r.UsuarioId == usuarioActual.Id)
                    .Select(r => r.TipoReaccion.ToString().ToLower())
                    .FirstOrDefaultAsync();

                ViewBag.MiReaccion = miReaccion;
                ViewBag.EstaSuscrito = tieneAcceso;
                ViewBag.EsPropio = esPropio;

                // Verificar si el contenido está en favoritos del usuario
                var esFavorito = await _context.Favoritos
                    .AnyAsync(f => f.ContenidoId == id && f.UsuarioId == usuarioActual.Id);
                ViewBag.EsFavorito = esFavorito;

                _logger.LogInformation("Detalle visto: Contenido {Id} ({TipoLado}) por Usuario {UserId}",
                    id, contenido.TipoLado, usuarioActual.Id);

                return View(contenido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar detalle del contenido {Id}. Mensaje: {Message}. StackTrace: {StackTrace}",
                    id, ex.Message, ex.StackTrace);
                TempData["Error"] = $"Error al cargar el contenido: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ========================================
        // CAMBIAR VISIBILIDAD (AJAX)
        // ========================================

        [HttpPost]
        public async Task<IActionResult> CambiarVisibilidad(int id, bool esPublico)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var contenido = await _context.Contenidos.FindAsync(id);
                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                // Verificar que el usuario es el propietario
                if (contenido.UsuarioId != usuarioActual.Id)
                {
                    return Json(new { success = false, message = "No tienes permiso para modificar este contenido" });
                }

                // No permitir hacer público el contenido de Lado B
                if (esPublico && contenido.TipoLado == TipoLado.LadoB)
                {
                    return Json(new { success = false, message = "El contenido de Lado B no puede ser público" });
                }

                contenido.EsPublicoGeneral = esPublico;
                contenido.FechaActualizacion = DateTime.Now;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Visibilidad cambiada: Contenido {Id} ahora es {Visibilidad} por Usuario {UserId}",
                    id, esPublico ? "Público" : "Privado", usuarioActual.Id);

                return Json(new
                {
                    success = true,
                    esPublico = esPublico,
                    message = esPublico ? "Contenido ahora es público" : "Contenido ahora es privado"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar visibilidad del contenido {Id}", id);
                return Json(new { success = false, message = "Error al cambiar visibilidad" });
            }
        }

        // ========================================
        // LIKE (AJAX)
        // ========================================

        [HttpPost]
        public async Task<IActionResult> Like(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                var tieneAcceso = await VerificarAccesoContenido(usuarioId, contenido);

                if (contenido.TipoLado == TipoLado.LadoB && !tieneAcceso)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Necesitas estar suscrito para dar like a este contenido"
                    });
                }

                var likeExistente = await _context.Likes
                    .FirstOrDefaultAsync(l => l.ContenidoId == id && l.UsuarioId == usuarioId);

                bool liked;

                if (likeExistente != null)
                {
                    _context.Likes.Remove(likeExistente);
                    contenido.NumeroLikes = Math.Max(0, contenido.NumeroLikes - 1);
                    liked = false;
                }
                else
                {
                    var like = new Like
                    {
                        ContenidoId = id,
                        UsuarioId = usuarioId,
                        FechaLike = DateTime.Now
                    };
                    _context.Likes.Add(like);
                    contenido.NumeroLikes++;
                    liked = true;
                }

                await _context.SaveChangesAsync();

                // Registrar interaccion para clasificacion de intereses (en segundo plano)
                if (liked && !string.IsNullOrEmpty(usuarioId))
                {
                    _ = _interesesService.RegistrarInteraccionAsync(usuarioId, id, TipoInteraccion.Like);
                }

                // Notificar en segundo plano (no bloquea la respuesta)
                if (liked && contenido.UsuarioId != usuarioId)
                {
                    var propietarioId = contenido.UsuarioId;
                    var likeUsuarioId = usuarioId!;
                    var contId = contenido.Id;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _notificationService.NotificarNuevoLikeAsync(propietarioId, likeUsuarioId, contId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error en notificacion de like");
                        }
                    });
                }

                return Json(new
                {
                    success = true,
                    likes = contenido.NumeroLikes,
                    liked = liked
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar like para contenido {Id}", id);
                return Json(new { success = false, message = "Error al procesar el like" });
            }
        }

        // ========================================
        // METODO AUXILIAR - VERIFICAR ACCESO
        // ========================================

        private async Task<bool> VerificarAccesoContenido(string usuarioId, Contenido contenido)
        {
            if (contenido.UsuarioId == usuarioId)
                return true;

            if (contenido.TipoLado == TipoLado.LadoA)
                return true;

            var estaSuscrito = await _context.Suscripciones
                .AnyAsync(s => s.FanId == usuarioId
                            && s.CreadorId == contenido.UsuarioId
                            && s.EstaActiva);

            if (estaSuscrito)
                return true;

            var comproContenido = await _context.ComprasContenido
                .AnyAsync(cc => cc.UsuarioId == usuarioId
                             && cc.ContenidoId == contenido.Id);

            if (comproContenido)
                return true;

            var contenidoEnColeccionComprada = await _context.ContenidoColecciones
                .Where(cc => cc.ContenidoId == contenido.Id)
                .Join(_context.ComprasColeccion,
                    cc => cc.ColeccionId,
                    coc => coc.ColeccionId,
                    (cc, coc) => coc)
                .AnyAsync(coc => coc.CompradorId == usuarioId);

            return contenidoEnColeccionComprada;
        }

        // ========================================
        // FAVORITOS
        // ========================================

        [HttpPost]
        public async Task<IActionResult> ToggleFavorito(int id)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var contenido = await _context.Contenidos.FindAsync(id);

                if (contenido == null)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                var favoritoExistente = await _context.Favoritos
                    .FirstOrDefaultAsync(f => f.ContenidoId == id && f.UsuarioId == usuarioId);

                if (favoritoExistente != null)
                {
                    _context.Favoritos.Remove(favoritoExistente);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, esFavorito = false, message = "Eliminado de favoritos" });
                }
                else
                {
                    var nuevoFavorito = new Favorito
                    {
                        ContenidoId = id,
                        UsuarioId = usuarioId,
                        FechaAgregado = DateTime.Now
                    };
                    _context.Favoritos.Add(nuevoFavorito);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, esFavorito = true, message = "Agregado a favoritos" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al toggle favorito para contenido {Id}", id);
                return Json(new { success = false, message = "Error al procesar favorito" });
            }
        }

        public async Task<IActionResult> MisFavoritos()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var favoritos = await _context.Favoritos
                .Where(f => f.UsuarioId == usuarioId)
                .Include(f => f.Contenido)
                    .ThenInclude(c => c.Usuario)
                .OrderByDescending(f => f.FechaAgregado)
                .Select(f => f.Contenido)
                .Where(c => c.EstaActivo && !c.EsBorrador && !c.Censurado)
                .ToListAsync();

            var favoritosIds = await _context.Favoritos
                .Where(f => f.UsuarioId == usuarioId)
                .Select(f => f.ContenidoId)
                .ToListAsync();

            ViewBag.FavoritosIds = favoritosIds;

            return View(favoritos);
        }

        // ========================================
        // SEGUIDORES EN COMÚN
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerSeguidoresEnComun(string creadorId)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                if (string.IsNullOrEmpty(creadorId) || creadorId == usuarioActual.Id)
                {
                    return Json(new { success = true, seguidoresEnComun = new List<object>(), total = 0 });
                }

                // Seguidores en común:
                // 1. Obtener usuarios que YO sigo (FanId = MiId)
                var usuariosQueSigo = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .Distinct()
                    .ToListAsync();

                // 2. De esos usuarios, encontrar cuáles también siguen al CREADOR
                var seguidoresEnComun = await _context.Suscripciones
                    .Where(s => usuariosQueSigo.Contains(s.FanId)
                            && s.CreadorId == creadorId
                            && s.EstaActiva)
                    .Select(s => s.FanId)
                    .Distinct()
                    .ToListAsync();

                // 3. Obtener información de esos usuarios
                var usuarios = await _context.Users
                    .Where(u => seguidoresEnComun.Contains(u.Id) && u.EstaActivo)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.UserName,
                        nombreCompleto = u.NombreCompleto,
                        fotoPerfil = u.FotoPerfil
                    })
                    .Take(10) // Limitar a 10 para no sobrecargar
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    seguidoresEnComun = usuarios,
                    total = seguidoresEnComun.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener seguidores en común para creador {CreadorId}", creadorId);
                return Json(new { success = false, message = "Error al cargar seguidores en común" });
            }
        }
    }
}
