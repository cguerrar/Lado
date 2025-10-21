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

        // ========================================
        // OBTENER LIKES DEL USUARIO ACTUAL
        // Agregar este método al FeedController.cs
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

                // Obtener los contenidos que el usuario ha dado like
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

        // ========================================
        // CLASE REQUEST (agregar al final del archivo o en Models)
        // ========================================
        public class ObtenerLikesRequest
        {
            public List<int> ContenidoIds { get; set; } = new List<int>();
        }

        // ========================================
        // INDEX - FEED PRINCIPAL CON STORIES Y COLECCIONES
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

                // 1. Obtener usuarios a los que está suscrito
                var creadoresIds = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                _logger.LogInformation("Usuario {UserId} tiene {Count} suscripciones activas",
                    usuarioId, creadoresIds.Count);

                // 2. STORIES - Obtener stories activas de creadores suscritos
                var ahoraStories = DateTime.Now;
                var storiesCreadores = creadoresIds.Any()
                    ? await _context.Stories
                        .Include(s => s.Creador)
                        .Where(s => (creadoresIds.Contains(s.CreadorId) || s.CreadorId == usuarioId)
                                && s.FechaExpiracion > ahoraStories
                                && s.EstaActivo)
                        .ToListAsync()
                    : await _context.Stories
                        .Include(s => s.Creador)
                        .Where(s => s.CreadorId == usuarioId
                                && s.FechaExpiracion > ahoraStories
                                && s.EstaActivo)
                        .ToListAsync();

                // Marcar stories vistas
                var storiesVistosIds = await _context.StoryVistas
                    .Where(sv => sv.UsuarioId == usuarioId)
                    .Select(sv => sv.StoryId)
                    .ToListAsync();

                // Agrupar stories por creador
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

                // 3. COLECCIONES DESTACADAS
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

                // 4. Contenido público (LadoA) de TODOS los usuarios INCLUYENDO propio
                var contenidoPublico = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoA
                            && c.Usuario != null)
                    .ToListAsync();

                // 5. Contenido premium (LadoB) de suscripciones
                var contenidoPremiumSuscripciones = creadoresIds.Any()
                    ? await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Where(c => creadoresIds.Contains(c.UsuarioId)
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && c.TipoLado == TipoLado.LadoB
                                && c.Usuario != null)
                        .ToListAsync()
                    : new List<Contenido>();

                // 6. Contenido premium (LadoB) PROPIO
                var contenidoPremiumPropio = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.UsuarioId == usuarioId
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null)
                    .ToListAsync();

                // 7. Contenido premium comprado individualmente
                var contenidosCompradosIds = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == usuarioId)
                    .Select(cc => cc.ContenidoId)
                    .ToListAsync();

                var contenidoPremiumComprado = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => contenidosCompradosIds.Contains(c.Id)
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.Usuario != null)
                    .ToListAsync();

                // 8. Combinar todo el contenido
                var todoContenido = contenidoPublico
                    .Union(contenidoPremiumSuscripciones)
                    .Union(contenidoPremiumPropio)
                    .Union(contenidoPremiumComprado)
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Contenido total: {Total} (Público: {Publico}, Premium Subs: {PremiumSubs}, Premium Propio: {PremiumPropio}, Comprado: {Comprado})",
                    todoContenido.Count, contenidoPublico.Count, contenidoPremiumSuscripciones.Count,
                    contenidoPremiumPropio.Count, contenidoPremiumComprado.Count);

                // 9. Obtener reacciones del usuario para cada contenido
                var reaccionesUsuario = await _context.Reacciones
                    .Where(r => r.UsuarioId == usuarioId)
                    .ToDictionaryAsync(r => r.ContenidoId, r => r.TipoReaccion);

                ViewBag.ReaccionesUsuario = reaccionesUsuario;

                // 10. Obtener conteo de reacciones por contenido
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

                // 11. Ordenar con algoritmo mejorado
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

                // 12. ViewBag data
                ViewBag.EstaSuscrito = true;
                ViewBag.TotalLadoA = contenidoPublico.Count;
                ViewBag.TotalLadoB = contenidoPremiumSuscripciones.Count + contenidoPremiumPropio.Count;

                // 13. Sugerencias de usuarios
                ViewBag.CreadoresSugeridos = await _userManager.Users
                    .Where(u => u.Id != usuarioId
                            && u.EstaActivo
                            && !creadoresIds.Contains(u.Id))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .ThenBy(u => Guid.NewGuid())
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

        // ========================================
        // CALCULAR SCORE CON REACCIONES
        // ========================================

        private double CalcularScoreMejorado(Contenido contenido, string usuarioActualId)
        {
            try
            {
                var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;

                // 1. BASE TEMPORAL
                double baseScore = 100.0 / (1 + horasDesdePublicacion / 24.0);

                // Boost para contenido reciente
                if (horasDesdePublicacion < 6)
                {
                    baseScore += 50.0;
                }
                else if (horasDesdePublicacion < 24)
                {
                    baseScore += 25.0;
                }

                // 2. BOOST POR ENGAGEMENT (incluyendo reacciones)
                var totalReacciones = _context.Reacciones
                    .Count(r => r.ContenidoId == contenido.Id);

                double totalEngagement = contenido.NumeroLikes
                                       + (contenido.NumeroComentarios * 2.0)
                                       + (totalReacciones * 1.5) // Reacciones valen más que likes
                                       + (contenido.NumeroVistas * 0.1)
                                       + (contenido.NumeroCompartidos * 3.0);

                double engagementBoost = Math.Log(1 + totalEngagement) * 10.0;

                // 3. BOOST ADICIONAL PARA CONTENIDO PREMIUM (LadoB)
                double premiumBoost = contenido.TipoLado == TipoLado.LadoB ? 15.0 : 0.0;

                // 4. BOOST PARA CONTENIDO PROPIO
                double propioBoost = contenido.UsuarioId == usuarioActualId ? 20.0 : 0.0;

                // 5. BOOST PARA CONTENIDO CON PREVIEW
                double previewBoost = contenido.TienePreview ? 10.0 : 0.0;

                // 6. PENALIZACIÓN POR ANTIGÜEDAD
                double edadPenalizacion = 1.0;
                if (horasDesdePublicacion > 168) // Más de 1 semana
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
        [ValidateAntiForgeryToken]
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

                // Verificar acceso a contenido premium
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
        // PERFIL CON INFO DE COLECCIONES
        // ========================================

        public async Task<IActionResult> Perfil(string id)
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

                var estaSuscrito = await _context.Suscripciones
                    .AnyAsync(s => s.FanId == usuarioActual.Id &&
                             s.CreadorId == id &&
                             s.EstaActiva);

                ViewBag.EstaSuscrito = estaSuscrito;

                // Contenido LadoA (siempre visible)
                var contenidoLadoA = await _context.Contenidos
                    .Where(c => c.UsuarioId == id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoA)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .ToListAsync();

                // Contenido LadoB (solo si está suscrito o compró)
                var contenidosComprados = await _context.ComprasContenido
                    .Where(cc => cc.UsuarioId == usuarioActual.Id)
                    .Select(cc => cc.ContenidoId)
                    .ToListAsync();

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

                var contenidos = contenidoLadoA.Union(contenidoLadoB)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .ToList();

                ViewBag.Contenidos = contenidos;
                ViewBag.ContenidoLadoA = contenidoLadoA;
                ViewBag.ContenidoLadoB = contenidoLadoB;

                // Colecciones del creador
                ViewBag.Colecciones = await _context.Colecciones
                    .Include(c => c.Contenidos)
                    .Where(c => c.CreadorId == id && c.EstaActiva)
                    .OrderByDescending(c => c.FechaCreacion)
                    .ToListAsync();

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                ViewBag.TotalLikes = contenidos.Sum(c => c.NumeroLikes);
                ViewBag.TotalPublicaciones = contenidos.Count;
                ViewBag.TotalLadoA = contenidoLadoA.Count;
                ViewBag.TotalLadoB = contenidoLadoB.Count;

                _logger.LogInformation("Perfil cargado: {Username} - LadoA: {LadoA}, LadoB: {LadoB}",
                    usuario.UserName, contenidoLadoA.Count, contenidoLadoB.Count);

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

                // Incrementar vistas
                if (!esPropio)
                {
                    contenido.NumeroVistas++;
                    await _context.SaveChangesAsync();
                }

                // Obtener reacciones
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

                // Reacción del usuario actual
                var miReaccion = await _context.Reacciones
                    .Where(r => r.ContenidoId == id && r.UsuarioId == usuarioActual.Id)
                    .Select(r => r.TipoReaccion.ToString().ToLower())
                    .FirstOrDefaultAsync();

                ViewBag.MiReaccion = miReaccion;
                ViewBag.EstaSuscrito = tieneAcceso;
                ViewBag.EsPropio = esPropio;

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
        // LIKE (AJAX) - MANTENER COMPATIBILIDAD
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
            // Es el creador del contenido
            if (contenido.UsuarioId == usuarioId)
                return true;

            // Contenido público
            if (contenido.TipoLado == TipoLado.LadoA)
                return true;

            // Verificar suscripción
            var estaSuscrito = await _context.Suscripciones
                .AnyAsync(s => s.FanId == usuarioId
                            && s.CreadorId == contenido.UsuarioId
                            && s.EstaActiva);

            if (estaSuscrito)
                return true;

            // Verificar compra individual
            var comproContenido = await _context.ComprasContenido
                .AnyAsync(cc => cc.UsuarioId == usuarioId
                             && cc.ContenidoId == contenido.Id);

            if (comproContenido)
                return true;

            // Verificar si está en una colección comprada
            var contenidoEnColeccionComprada = await _context.ContenidoColecciones
                .Where(cc => cc.ContenidoId == contenido.Id)
                .Join(_context.ComprasColeccion,
                    cc => cc.ColeccionId,
                    coc => coc.ColeccionId,
                    (cc, coc) => coc)
                .AnyAsync(coc => coc.CompradorId == usuarioId);

            return contenidoEnColeccionComprada;
        }
    }
}