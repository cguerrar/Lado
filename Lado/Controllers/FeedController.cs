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

                if (verSeudonimo)
                {
                    // 📌 MODO SEUDÓNIMO: Solo contenido LadoB
                    _logger.LogInformation("Mostrando perfil de seudónimo para {Username}", usuario.UserName);

                    var contenidosComprados = await _context.ComprasContenido
                        .Where(cc => cc.UsuarioId == usuarioActual.Id)
                        .Select(cc => cc.ContenidoId)
                        .ToListAsync();

                    // Solo contenido LadoB
                    var contenidoLadoB = (estaSuscrito || id == usuarioActual.Id)
                        ? await _context.Contenidos
                            .Where(c => c.UsuarioId == id
                                    && c.EstaActivo
                                    && !c.EsBorrador
                                    && !c.Censurado
                                    && c.TipoLado == TipoLado.LadoB)
                            .OrderByDescending(c => c.FechaPublicacion)
                            .ToListAsync()
                        : await _context.Contenidos
                            .Where(c => c.UsuarioId == id
                                    && c.EstaActivo
                                    && !c.EsBorrador
                                    && !c.Censurado
                                    && c.TipoLado == TipoLado.LadoB
                                    && contenidosComprados.Contains(c.Id))
                            .OrderByDescending(c => c.FechaPublicacion)
                            .ToListAsync();

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

          
                return View(usuario);
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

        public async Task<IActionResult> Index()
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

                _logger.LogInformation("Usuario {UserId} tiene {Count} suscripciones activas",
                    usuarioId, creadoresIds.Count);

                // 2. CREADORES FAVORITOS - Solo suscritos
                var creadoresFavoritos = await _context.Users
                    .Where(u => creadoresIds.Contains(u.Id) && u.EstaActivo)
                    .Select(u => new
                    {
                        u.Id,
                        u.NombreCompleto,
                        u.UserName,
                        u.FotoPerfil,
                        u.CreadorVerificado,
                        NumeroSuscriptores = _context.Suscripciones.Count(s => s.CreadorId == u.Id && s.EstaActiva)
                    })
                    .OrderByDescending(u => u.NumeroSuscriptores)
                    .ToListAsync();

                ViewBag.CreadoresFavoritos = creadoresFavoritos;

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
                    .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                        .ThenInclude(com => com.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoA
                            && c.Usuario != null
                            && (creadoresLadoAIds.Contains(c.UsuarioId) || c.UsuarioId == usuarioId)) // ✅ Solo LadoA
                    .ToListAsync();

                // 6. ✅ CORREGIDO: Contenido premium (LadoB) SOLO de creadores suscritos en LadoB
                var contenidoPremiumSuscripciones = creadoresLadoBIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                            .ThenInclude(com => com.Usuario)
                        .Where(c => creadoresLadoBIds.Contains(c.UsuarioId) // ✅ Solo LadoB
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && c.TipoLado == TipoLado.LadoB
                                && c.Usuario != null)
                        .ToListAsync()
                    : new List<Contenido>();

                // 7. Contenido premium (LadoB) PROPIO
                var contenidoPremiumPropio = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                        .ThenInclude(com => com.Usuario)
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null)
                    .ToListAsync();

                // 8. Contenido premium comprado individualmente
                var contenidosCompradosIds = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == usuarioId)
                    .Select(cc => cc.ContenidoId)
                    .ToListAsync();

                var contenidoPremiumComprado = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.Comentarios.OrderByDescending(com => com.FechaCreacion).Take(3))
                        .ThenInclude(com => com.Usuario)
                    .Where(c => contenidosCompradosIds.Contains(c.Id)
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.Usuario != null)
                    .ToListAsync();

                // 9. NUEVO: Contenido LadoB de creadores NO suscritos (para mostrar con blur)
                var todosLosCreadores = creadoresLadoAIds.Union(creadoresLadoBIds).ToList();
                var contenidoLadoBBloqueado = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.UsuarioId != usuarioId
                            && !creadoresLadoBIds.Contains(c.UsuarioId) // NO suscrito a LadoB
                            && !contenidosCompradosIds.Contains(c.Id)) // NO comprado
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

                // 12. Ordenar contenido
                var contenidoOrdenado = todoContenido
                    .Select(c => new
                    {
                        Contenido = c,
                        Score = CalcularScoreMejorado(c, usuarioId)
                    })
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Contenido.FechaPublicacion)
                    .Select(x => x.Contenido)
                    .Take(50)
                    .ToList();

                // 13. ViewBag data
                ViewBag.EstaSuscrito = true;
                ViewBag.TotalLadoA = contenidoPublico.Count;
                ViewBag.TotalLadoB = contenidoPremiumSuscripciones.Count + contenidoPremiumPropio.Count;

                // 14. Sugerencias de usuarios NO suscritos
                ViewBag.CreadoresSugeridos = await _userManager.Users
                    .Where(u => u.Id != usuarioId
                            && u.EstaActivo
                            && !creadoresIds.Contains(u.Id)) // ✅ Excluye a los que ya está suscrito
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

                return View(contenidoOrdenado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el feed");
                TempData["Error"] = "Error al cargar el feed. Por favor, intenta nuevamente.";
                return View(new List<Contenido>());
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
        // COMENTAR
        // ========================================

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

                var comentario = new Comentario
                {
                    ContenidoId = id,
                    UsuarioId = usuarioId,
                    Texto = texto.Trim(),
                    FechaCreacion = DateTime.Now
                };

                _context.Comentarios.Add(comentario);
                contenido.NumeroComentarios++;

                await _context.SaveChangesAsync();

                var usuario = await _userManager.FindByIdAsync(usuarioId);

                _logger.LogInformation("Comentario agregado: Usuario {UserId} en Contenido {ContenidoId}",
                    usuarioId, id);

                return Json(new
                {
                    success = true,
                    comentario = new
                    {
                        id = comentario.Id,
                        texto = comentario.Texto,
                        nombreUsuario = usuario.NombreCompleto,
                        username = usuario.UserName,
                        avatar = usuario.FotoPerfil
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
        // COMPARTIR CONTENIDO
        // ========================================

        [HttpPost]
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

                return Json(new
                {
                    success = true,
                    url = Url.Action("Detalle", "Feed", new { id }, Request.Scheme),
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

                var usuarios = await _userManager.Users
                    .Where(u => u.EstaActivo && u.Id != usuarioActual.Id)
                    .OrderByDescending(u => u.CreadorVerificado)
                    .ThenByDescending(u => u.NumeroSeguidores)
                    .Take(50)
                    .ToListAsync();

                _logger.LogInformation("Explorar: {Count} usuarios encontrados", usuarios.Count);

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
        // DETALLE DE CONTENIDO CON REACCIONES
        // ========================================

        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return RedirectToAction("Login", "Account");
                }

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
                _logger.LogError(ex, "Error al cargar detalle del contenido {Id}", id);
                TempData["Error"] = "Error al cargar el contenido";
                return RedirectToAction("Index");
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
        // MÉTODO AUXILIAR - VERIFICAR ACCESO
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
    }
}