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
        private readonly IFeedContentService _feedContentService;
        private readonly IInteresesService _interesesService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IMemoryCache _cache;
        private readonly IRachasService _rachasService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IPushNotificationService _pushService;

        public FeedController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedController> logger,
            IAdService adService,
            INotificationService notificationService,
            IFeedAlgorithmService feedAlgorithmService,
            IFeedContentService feedContentService,
            IInteresesService interesesService,
            IRateLimitService rateLimitService,
            IMemoryCache cache,
            IRachasService rachasService,
            IServiceProvider serviceProvider,
            IPushNotificationService pushService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _adService = adService;
            _notificationService = notificationService;
            _feedAlgorithmService = feedAlgorithmService;
            _feedContentService = feedContentService;
            _interesesService = interesesService;
            _rateLimitService = rateLimitService;
            _cache = cache;
            _rachasService = rachasService;
            _serviceProvider = serviceProvider;
            _pushService = pushService;
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

        // ⚡ Método helper para obtener configuración de distribución LadoB Preview
        private async Task<(int cantidad, int intervalo)> ObtenerConfigDistribucionLadoBAsync()
        {
            var cacheKey = "ladob_preview_config";
            if (!_cache.TryGetValue(cacheKey, out (int cantidad, int intervalo) config))
            {
                var configuraciones = await _context.ConfiguracionesPlataforma
                    .Where(c => c.Clave == ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD
                             || c.Clave == ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO)
                    .ToDictionaryAsync(c => c.Clave, c => c.Valor);

                int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD, "1"), out int cantidad);
                int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO, "5"), out int intervalo);

                // Validar valores mínimos
                config = (Math.Max(1, cantidad), Math.Max(2, intervalo));
                _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));
            }
            return config;
        }

        // ⚡ Método para intercalar contenido LadoB Preview de forma garantizada
        private List<Contenido> IntercalarPreviewLadoB(
            List<Contenido> contenidoPrincipal,
            List<Contenido> previewLadoB,
            int cantidad,
            int intervalo)
        {
            if (!previewLadoB.Any()) return contenidoPrincipal;

            var resultado = new List<Contenido>();
            var previewQueue = new Queue<Contenido>(previewLadoB);
            int contadorIntervalo = 0;

            foreach (var contenido in contenidoPrincipal)
            {
                resultado.Add(contenido);
                contadorIntervalo++;

                // Cada 'intervalo' posts, insertar 'cantidad' previews de LadoB
                if (contadorIntervalo >= intervalo && previewQueue.Any())
                {
                    for (int i = 0; i < cantidad && previewQueue.Any(); i++)
                    {
                        resultado.Add(previewQueue.Dequeue());
                    }
                    contadorIntervalo = 0;
                }
            }

            // Agregar los previews restantes al final
            while (previewQueue.Any())
            {
                resultado.Add(previewQueue.Dequeue());
            }

            return resultado;
        }

        // ⚡ Método helper para obtener TODAS las configuraciones del Feed y Explorar con cache
        private async Task<Dictionary<string, int>> ObtenerConfigFeedAsync()
        {
            var cacheKey = "feed_config_all";
            if (!_cache.TryGetValue(cacheKey, out Dictionary<string, int>? config))
            {
                var configuraciones = await _context.ConfiguracionesPlataforma
                    .Where(c => c.Categoria == "Feed" || c.Categoria == "Explorar")
                    .ToDictionaryAsync(c => c.Clave, c => c.Valor);

                config = new Dictionary<string, int>
                {
                    // Límites de carga (Feed)
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOA, "30"), out var la) ? la : 30,
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS, "15"), out var lbs) ? lbs : 15,
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO, "10"), out var lbp) ? lbp : 10,
                    [ConfiguracionPlataforma.FEED_LIMITE_COMPRADO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_COMPRADO, "10"), out var lc) ? lc : 10,
                    [ConfiguracionPlataforma.FEED_LIMITE_TOTAL] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_TOTAL, "50"), out var lt) ? lt : 50,
                    // Descubrimiento
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD, "5"), out var dla) ? dla : 5,
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD, "5"), out var dlb) ? dlb : 5,
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD, "5"), out var du) ? du : 5,
                    // Variedad
                    [ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR, "2"), out var mc) ? mc : 2,
                    // Anuncios
                    [ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO, "8"), out var ai) ? ai : 8,
                    [ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD, "3"), out var ac) ? ac : 3,
                    // Preview LadoB
                    [ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD, "1"), out var pc) ? pc : 1,
                    [ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO, "5"), out var pi) ? pi : 5,
                    // Explorar
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CREADORES] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CREADORES, "50"), out var elc) ? elc : 50,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO, "100"), out var elco) ? elco : 100,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_ZONAS_MAPA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_ZONAS_MAPA, "20"), out var elzm) ? elzm : 20,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO_MAPA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO_MAPA, "30"), out var elcm) ? elcm : 30,
                    [ConfiguracionPlataforma.EXPLORAR_PAGE_SIZE] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_PAGE_SIZE, "30"), out var eps) ? eps : 30,
                    [ConfiguracionPlataforma.EXPLORAR_CONFIANZA_OBJETOS] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_CONFIANZA_OBJETOS, "70"), out var eco) ? eco : 70,
                };

                _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));
            }
            return config ?? new Dictionary<string, int>();
        }

        // ⚡ Calcular score de descubrimiento basado en intereses del usuario
        private double CalcularScoreDescubrimiento(
            Contenido contenido,
            Dictionary<int, decimal> interesesUsuario)
        {
            double score = 0;

            // 50% por coincidencia de intereses
            if (contenido.CategoriaInteresId.HasValue &&
                interesesUsuario.TryGetValue(contenido.CategoriaInteresId.Value, out decimal pesoInteres))
            {
                score += (double)pesoInteres * 50;
            }

            // 30% por engagement (logarítmico para evitar dominancia de virales)
            // Incluye likes, comentarios, vistas y compartidos del contenido
            double engagement = (contenido.NumeroLikes * 1.0) +
                               (contenido.NumeroComentarios * 2.0) +
                               (contenido.NumeroVistas * 0.1) +
                               (contenido.NumeroCompartidos * 1.5);
            score += Math.Log(1 + engagement) * 5 * 0.3;

            // 20% por recencia
            var horasDesdePublicacion = (DateTime.Now - contenido.FechaPublicacion).TotalHours;
            if (horasDesdePublicacion < 24)
                score += 20;
            else if (horasDesdePublicacion < 72)
                score += 10;
            else if (horasDesdePublicacion < 168) // 1 semana
                score += 5;

            return score;
        }

        // ⚡ Aplicar variedad de creadores: evita más de N posts consecutivos del mismo creador
        // Usa algoritmo round-robin para distribuir posts uniformemente manteniendo relevancia
        private List<Contenido> AplicarVariedadCreadores(List<Contenido> contenidos, int maxConsecutivos = 2)
        {
            if (!contenidos.Any() || maxConsecutivos <= 0) return contenidos;

            // Agrupar posts por creador manteniendo orden original (por score/relevancia)
            var postsPorCreador = new Dictionary<string, Queue<Contenido>>();
            var ordenCreadores = new List<string>(); // Para mantener prioridad de aparición

            foreach (var contenido in contenidos)
            {
                var creadorId = contenido.UsuarioId ?? "unknown";
                if (!postsPorCreador.ContainsKey(creadorId))
                {
                    postsPorCreador[creadorId] = new Queue<Contenido>();
                    ordenCreadores.Add(creadorId);
                }
                postsPorCreador[creadorId].Enqueue(contenido);
            }

            var resultado = new List<Contenido>();
            string? ultimoCreadorId = null;
            int consecutivos = 0;
            int creadoresAgotados = 0;

            // Round-robin con límite de consecutivos
            while (creadoresAgotados < ordenCreadores.Count && resultado.Count < contenidos.Count)
            {
                bool agregadoEnRonda = false;

                foreach (var creadorId in ordenCreadores)
                {
                    if (!postsPorCreador[creadorId].Any()) continue;

                    // Si es el mismo creador y ya alcanzamos el límite, saltar en esta ronda
                    if (creadorId == ultimoCreadorId && consecutivos >= maxConsecutivos)
                    {
                        continue;
                    }

                    // Tomar el siguiente post de este creador
                    var post = postsPorCreador[creadorId].Dequeue();
                    resultado.Add(post);
                    agregadoEnRonda = true;

                    if (creadorId == ultimoCreadorId)
                    {
                        consecutivos++;
                    }
                    else
                    {
                        ultimoCreadorId = creadorId;
                        consecutivos = 1;
                    }

                    // Verificar si este creador se agotó
                    if (!postsPorCreador[creadorId].Any())
                    {
                        creadoresAgotados++;
                    }

                    // Limitar a maxConsecutivos por ronda para mejor distribución
                    if (consecutivos >= maxConsecutivos)
                    {
                        break; // Forzar cambio de creador en siguiente iteración
                    }
                }

                // Si no se agregó nada, forzar agregar de cualquier creador restante
                if (!agregadoEnRonda)
                {
                    foreach (var creadorId in ordenCreadores)
                    {
                        if (postsPorCreador[creadorId].Any())
                        {
                            var post = postsPorCreador[creadorId].Dequeue();
                            resultado.Add(post);
                            ultimoCreadorId = creadorId;
                            consecutivos = 1;
                            if (!postsPorCreador[creadorId].Any())
                                creadoresAgotados++;
                            break;
                        }
                    }
                }
            }

            return resultado;
        }

        // ⚡ Obtener usuarios LadoB con intereses comunes al usuario actual
        private async Task<List<ApplicationUser>> ObtenerUsuariosLadoBPorInteresesAsync(
            string usuarioId,
            List<string> usuariosBloqueadosIds,
            List<string> creadoresYaSuscritos,
            int cantidad = 5)
        {
            // Obtener intereses del usuario actual
            var interesesUsuario = await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.3m)
                .Select(i => i.CategoriaInteresId)
                .ToListAsync();

            if (!interesesUsuario.Any())
            {
                // Si no tiene intereses, devolver creadores populares con LadoB
                return await _context.Users
                    .Where(u => u.Id != usuarioId
                            && u.EstaActivo
                            && u.CreadorVerificado
                            && !string.IsNullOrEmpty(u.Seudonimo)
                            && !usuariosBloqueadosIds.Contains(u.Id)
                            && !creadoresYaSuscritos.Contains(u.Id))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(cantidad)
                    .ToListAsync();
            }

            // Buscar usuarios que tengan contenido en las mismas categorías de interés
            var usuariosConInteresesComunes = await _context.Contenidos
                .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
                        && !c.OcultoSilenciosamente // Shadow hide
                        && c.TipoLado == TipoLado.LadoB
                        && c.CategoriaInteresId.HasValue
                        && interesesUsuario.Contains(c.CategoriaInteresId.Value)
                        && c.UsuarioId != usuarioId
                        && !usuariosBloqueadosIds.Contains(c.UsuarioId)
                        && !creadoresYaSuscritos.Contains(c.UsuarioId))
                .GroupBy(c => c.UsuarioId)
                .Select(g => new
                {
                    UsuarioId = g.Key,
                    CantidadContenidoRelevante = g.Count(),
                    TotalEngagement = g.Sum(c => c.NumeroLikes + c.NumeroComentarios * 2)
                })
                .OrderByDescending(x => x.CantidadContenidoRelevante)
                .ThenByDescending(x => x.TotalEngagement)
                .Take(cantidad * 2)
                .Select(x => x.UsuarioId)
                .ToListAsync();

            // Obtener los usuarios completos
            var usuarios = await _context.Users
                .Where(u => usuariosConInteresesComunes.Contains(u.Id)
                        && u.EstaActivo
                        && u.CreadorVerificado
                        && !string.IsNullOrEmpty(u.Seudonimo))
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(cantidad)
                .ToListAsync();

            return usuarios;
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

                // NOTA: LadoA es SIEMPRE público y visible para todos
                // La opción OcultarIdentidadLadoA solo oculta los enlaces entre LadoA y LadoB,
                // pero NO impide el acceso al perfil LadoA (que es gratuito y público)
                // El usuario puede ver y seguir LadoA sin restricciones

                // ⭐ VALIDACIÓN: Solo permitir verSeudonimo=true si el usuario tiene seudónimo activo
                // Esto evita que se trate como LadoB a usuarios que son solo LadoA
                if (verSeudonimo && !tieneSeudonimoActivo)
                {
                    _logger.LogWarning("Intento de ver seudónimo de usuario sin seudónimo: {UserId}", id);
                    verSeudonimo = false; // Forzar a LadoA si no tiene seudónimo
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
                    var esPropio = id == usuarioActual.Id;
                    var contenidoLadoB = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && (esPropio || !c.OcultoSilenciosamente) // Shadow hide
                                && c.TipoLado == TipoLado.LadoB)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .ToListAsync();

                    // Determinar qué contenidos están desbloqueados (suscrito, comprado, o es el propio usuario)
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
                                && (esPerfilPropio || !c.OcultoSilenciosamente) // Shadow hide
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
                // ⚠️ CRÍTICO: Desactivar cache del navegador para evitar datos de usuario anterior
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userName = User?.Identity?.Name;

                // DEBUG: Log de autenticación al entrar al Feed
                _logger.LogWarning("🔐 FEED INDEX: UserId={UserId}, UserName={UserName}",
                    usuarioId ?? "NULL", userName ?? "NULL");

                if (string.IsNullOrEmpty(usuarioId))
                {
                    _logger.LogWarning("Usuario no autenticado en Index");
                    return RedirectToAction("Login", "Account");
                }

                // ========================================
                // ⚡ REFACTORIZADO: Usar FeedContentService
                // ========================================

                // Obtener semilla de sesión para consistencia
                var semilla = _feedContentService.ObtenerOCrearSemilla(HttpContext.Session);

                // Obtener configuración para límites
                var configFeed = await ObtenerConfigFeedAsync();
                var limiteTotal = configFeed[ConfiguracionPlataforma.FEED_LIMITE_TOTAL];
                var anunciosIntervalo = configFeed[ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO];
                var anunciosCantidad = configFeed[ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD];

                // Obtener contenido del feed usando el servicio
                var feedResultado = await _feedContentService.ObtenerContenidoFeedAsync(
                    usuarioId,
                    limiteTotal,
                    null, // Sin IDs ya vistos en carga inicial
                    semilla);

                // Extraer datos del resultado
                var contenidoOrdenado = feedResultado.Contenidos;
                var creadoresIds = feedResultado.CreadoresIds;
                var creadoresLadoAIds = feedResultado.CreadoresLadoAIds;
                var creadoresLadoBIds = feedResultado.CreadoresLadoBIds;
                var contenidosCompradosIds = feedResultado.ContenidosCompradosIds;
                var usuariosBloqueadosIds = feedResultado.UsuariosBloqueadosIds;
                var ocultarLadoB = feedResultado.OcultarLadoB;

                // ViewBag para la vista
                ViewBag.ModoActual = ocultarLadoB ? "LadoA" : "LadoB";
                ViewBag.ContenidoBloqueadoIds = feedResultado.ContenidoBloqueadoIds.ToList();
                ViewBag.UsuariosBloqueadosIds = usuariosBloqueadosIds;
                ViewBag.AlgoritmoActual = feedResultado.AlgoritmoUsuario;
                ViewBag.AlgoritmosDisponibles = await _feedAlgorithmService.ObtenerAlgoritmosActivosAsync(_context);

                // DEBUG
                ViewBag.DEBUG_SuscripcionesLadoBIds = contenidoOrdenado.Where(c => c.TipoLado == TipoLado.LadoB && creadoresLadoBIds.Contains(c.UsuarioId)).Select(c => c.Id).ToList();
                ViewBag.DEBUG_CompradosIds = contenidosCompradosIds;
                ViewBag.DEBUG_CreadoresLadoBIds = creadoresLadoBIds;

                // ========================================
                // DATOS AUXILIARES (usando servicio)
                // ========================================
                var datosAuxiliares = await _feedContentService.ObtenerDatosAuxiliaresAsync(
                    usuarioId,
                    creadoresIds,
                    usuariosBloqueadosIds,
                    ocultarLadoB);

                ViewBag.CreadoresFavoritos = datosAuxiliares.CreadoresFavoritos;
                ViewBag.Stories = datosAuxiliares.Stories;
                ViewBag.Colecciones = datosAuxiliares.Colecciones;
                ViewBag.CreadoresSugeridos = datosAuxiliares.CreadoresSugeridos;
                ViewBag.UsuariosLadoBPorIntereses = datosAuxiliares.UsuariosLadoBPorIntereses;

                // ========================================
                // DATOS ADICIONALES PARA LA VISTA
                // ========================================
                ViewBag.EstaSuscrito = true;
                ViewBag.TotalLadoA = contenidoOrdenado.Count(c => c.TipoLado == TipoLado.LadoA);
                ViewBag.TotalLadoB = contenidoOrdenado.Count(c => c.TipoLado == TipoLado.LadoB);

                // Usuario actual
                var usuarioActual = await _userManager.FindByIdAsync(usuarioId);
                ViewBag.UsuarioActual = usuarioActual;

                // Likes y favoritos del usuario
                var contenidoIds = contenidoOrdenado.Select(c => c.Id).ToList();
                var likesUsuario = await _context.Likes
                    .Where(l => l.UsuarioId == usuarioId && contenidoIds.Contains(l.ContenidoId))
                    .Select(l => l.ContenidoId)
                    .ToListAsync();
                ViewBag.LikesUsuario = likesUsuario;

                var favoritosUsuario = await _context.Favoritos
                    .Where(f => f.UsuarioId == usuarioId && contenidoIds.Contains(f.ContenidoId))
                    .Select(f => f.ContenidoId)
                    .ToListAsync();
                ViewBag.FavoritosIds = favoritosUsuario;

                // Usuarios seguidos (para mostrar "Siguiendo" en fullscreen)
                ViewBag.UsuariosSeguidosIds = creadoresIds;

                // Anuncios
                var anuncios = await _adService.ObtenerAnunciosActivos(anunciosCantidad, usuarioId);
                ViewBag.Anuncios = anuncios;
                ViewBag.AnunciosIntervalo = anunciosIntervalo;

                // Mensajes no leídos para badge del FAB
                ViewBag.MensajesNoLeidos = await _context.ChatMensajes
                    .CountAsync(m => m.DestinatarioId == usuarioId && !m.Leido);

                // Registrar impresiones
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                foreach (var anuncio in anuncios)
                {
                    await _adService.RegistrarImpresion(anuncio.Id, usuarioId, ipAddress);
                }

                // Incrementar contador de uso del algoritmo
                if (feedResultado.AlgoritmoUsuario != null)
                {
                    await _feedAlgorithmService.IncrementarUsoAsync(feedResultado.AlgoritmoUsuario.Id, _context);
                }

                // ========================================
                // MANEJO DE POST COMPARTIDO
                // ========================================
                if (post.HasValue)
                {
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
        // ⚡ MEJORADO: Usa método unificado y acepta IDs ya vistos
        // ========================================
        [HttpGet]
        public async Task<IActionResult> CargarMasPosts(int cantidad = 10, string? idsVistos = null)
        {
            // Limitar cantidad para prevenir DoS
            cantidad = Math.Clamp(cantidad, 1, 50);

            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                // ⚡ NUEVO: Parsear IDs ya vistos para excluirlos
                var idsYaVistos = new HashSet<int>();
                if (!string.IsNullOrEmpty(idsVistos))
                {
                    try
                    {
                        var ids = idsVistos.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var idStr in ids)
                        {
                            if (int.TryParse(idStr.Trim(), out int id))
                            {
                                idsYaVistos.Add(id);
                            }
                        }
                        _logger.LogDebug("CargarMasPosts: {Count} IDs ya vistos recibidos", idsYaVistos.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parseando idsVistos: {IdsVistos}", idsVistos);
                    }
                }

                // ⚡ Usar FeedContentService para consistencia
                var semilla = _feedContentService.ObtenerOCrearSemilla(HttpContext.Session);
                var feedResultado = await _feedContentService.ObtenerContenidoFeedAsync(
                    usuarioId,
                    cantidad,
                    idsYaVistos,
                    semilla);

                // Obtener likes del usuario para los posts retornados
                var contenidoIds = feedResultado.Contenidos.Select(c => c.Id).ToList();
                var likesUsuario = await _context.Likes
                    .Where(l => l.UsuarioId == usuarioId && contenidoIds.Contains(l.ContenidoId))
                    .Select(l => l.ContenidoId)
                    .ToListAsync();

                // Cargar comentarios para los posts (2 por post, ordenados por fecha)
                var comentariosPorPost = await _context.Comentarios
                    .Where(c => contenidoIds.Contains(c.ContenidoId) && c.ComentarioPadreId == null)
                    .Include(c => c.Usuario)
                    .Include(c => c.Respuestas)
                        .ThenInclude(r => r.Usuario)
                    .GroupBy(c => c.ContenidoId)
                    .Select(g => new {
                        ContenidoId = g.Key,
                        Comentarios = g.OrderByDescending(c => c.FechaCreacion).Take(3).ToList()
                    })
                    .ToListAsync();

                var comentariosDict = comentariosPorPost.ToDictionary(x => x.ContenidoId, x => x.Comentarios);

                // Serializar posts
                var posts = feedResultado.Contenidos.Select(post =>
                {
                    var esVideo = post.TipoContenido == TipoContenido.Video;
                    var esAudio = post.TipoContenido == TipoContenido.Audio;
                    // Shadow Hide: Si no es el creador, excluir archivos ocultos silenciosamente
                    var esCreadorDelPost = post.UsuarioId == usuarioId;
                    var archivos = esCreadorDelPost ? post.TodosLosArchivos : post.ArchivosVisibles;
                    var estaBloqueado = feedResultado.ContenidoBloqueadoIds.Contains(post.Id);

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
                        estaBloqueado = estaBloqueado,
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
                        esReel = post.EsReel,
                        pistaMusicalId = post.PistaMusicalId,
                        pistaMusical = post.PistaMusical != null ? new
                        {
                            titulo = post.PistaMusical.Titulo,
                            artista = post.PistaMusical.Artista,
                            rutaArchivo = post.PistaMusical.RutaArchivo
                        } : null,
                        audioTrimInicio = post.AudioTrimInicio,
                        musicaVolumen = post.MusicaVolumen,
                        comentarios = comentariosDict.TryGetValue(post.Id, out var coms)
                            ? coms.Take(2).Select(c => new {
                                id = c.Id,
                                texto = c.Texto,
                                fechaCreacion = c.FechaCreacion,
                                numeroLikes = c.NumeroLikes,
                                usuario = new {
                                    userName = c.Usuario?.UserName,
                                    fotoPerfil = c.Usuario?.FotoPerfil
                                },
                                numRespuestas = c.Respuestas?.Count ?? 0,
                                respuestas = c.Respuestas?.Take(2).Select(r => new {
                                    usuario = new {
                                        fotoPerfil = r.Usuario?.FotoPerfil
                                    }
                                }).ToList()
                            }).ToArray()
                            : Array.Empty<object>()
                    };
                }).ToList();

                return Json(new
                {
                    success = true,
                    posts = posts,
                    hayMas = feedResultado.HayMas,
                    total = feedResultado.Contenidos.Count,
                    semilla = feedResultado.Semilla // Para debug
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar más posts");
                return Json(new { success = false, message = "Error al cargar posts" });
            }
        }

        // ========================================
        // API: VERIFICAR NUEVOS POSTS (Auto-refresh)
        // ========================================
        [HttpGet]
        public async Task<IActionResult> VerificarNuevosPosts(string? idsVistos = null)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, hayNuevos = false });
                }

                // Parsear IDs ya vistos
                var idsYaVistos = new HashSet<int>();
                if (!string.IsNullOrEmpty(idsVistos))
                {
                    var ids = idsVistos.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var idStr in ids.Take(500)) // Limitar para evitar DoS
                    {
                        if (int.TryParse(idStr.Trim(), out int id))
                        {
                            idsYaVistos.Add(id);
                        }
                    }
                }

                if (idsYaVistos.Count == 0)
                {
                    return Json(new { success = true, hayNuevos = false, cantidad = 0 });
                }

                // Obtener el ID más alto que el usuario ya vio
                var maxIdVisto = idsYaVistos.Max();

                // Contar posts nuevos que el usuario podría ver
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null)
                {
                    return Json(new { success = false, hayNuevos = false });
                }

                // IDs de creadores a los que está suscrito
                var creadoresSuscritos = await _context.Suscripciones
                    .Where(s => s.FanId == usuarioId && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Contar posts nuevos (ID mayor al máximo visto)
                var cantidadNuevos = await _context.Contenidos
                    .Where(c => c.EstaActivo &&
                               !c.EsBorrador &&
                               c.Id > maxIdVisto &&
                               !idsYaVistos.Contains(c.Id) &&
                               (c.TipoLado == TipoLado.LadoA ||
                                c.UsuarioId == usuarioId ||
                                creadoresSuscritos.Contains(c.UsuarioId)))
                    .CountAsync();

                return Json(new
                {
                    success = true,
                    hayNuevos = cantidadNuevos > 0,
                    cantidad = cantidadNuevos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar nuevos posts");
                return Json(new { success = false, hayNuevos = false });
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

                // ⭐ Verificar configuración de privacidad QuienPuedeComentar del creador
                var creador = await _context.Users.FindAsync(contenido.UsuarioId);
                if (creador != null && usuarioId != creador.Id)
                {
                    var puedeComentarResult = await VerificarQuienPuedeComentar(usuarioId, creador);
                    if (!puedeComentarResult.puede)
                    {
                        return Json(new { success = false, message = puedeComentarResult.mensaje });
                    }
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

                // Registrar interacciones (secuencialmente para evitar conflictos de DbContext)
                if (!string.IsNullOrEmpty(usuarioId))
                {
                    // ⭐ Registrar comentario para LadoCoins PRIMERO (racha de 3 comentarios diarios)
                    try
                    {
                        var bonoComentario = await _rachasService.RegistrarComentarioAsync(usuarioId);
                        _logger.LogDebug("⭐ Comentario registrado para LadoCoins: {UserId}, Bono: {Bono}", usuarioId, bonoComentario);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar comentario para LadoCoins: {UserId}", usuarioId);
                    }

                    // Registrar interaccion para clasificacion de intereses DESPUÉS
                    await _interesesService.RegistrarInteraccionAsync(usuarioId, id, TipoInteraccion.Comentario);
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

                    // 🔔 Push Notification
                    var nombreComentador = usuario?.NombreCompleto ?? usuario?.UserName ?? "Alguien";
                    var preview = texto.Length > 50 ? texto[..50] + "..." : texto;
                    _ = _pushService.EnviarNotificacionAsync(
                        contenido.UsuarioId,
                        "💬 Nuevo comentario",
                        $"{nombreComentador}: {preview}",
                        $"/Feed/Detalle/{contenido.Id}",
                        TipoNotificacionPush.NuevoComentario,
                        usuario?.FotoPerfil
                    );
                }

                // Si es una respuesta, notificar también al autor del comentario original
                if (usuarioPadreId != null && usuarioPadreId != usuarioId && usuarioPadreId != contenido.UsuarioId)
                {
                    _ = _notificationService.NotificarNuevoComentarioAsync(
                        usuarioPadreId,
                        usuarioId!,
                        contenido.Id,
                        comentario.Id);

                    // 🔔 Push Notification para respuesta
                    var nombreComentador = usuario?.NombreCompleto ?? usuario?.UserName ?? "Alguien";
                    _ = _pushService.EnviarNotificacionAsync(
                        usuarioPadreId,
                        "💬 Respuesta a tu comentario",
                        $"{nombreComentador} te respondió",
                        $"/Feed/Detalle/{contenido.Id}",
                        TipoNotificacionPush.NuevoComentario,
                        usuario?.FotoPerfil
                    );
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

                // Obtener IDs de comentarios que el usuario actual ha dado like
                var comentarioIds = comentariosPrincipales.Select(c => c.Id)
                    .Concat(comentariosPrincipales.SelectMany(c => c.Respuestas.Select(r => r.Id)))
                    .ToList();

                var misLikes = !string.IsNullOrEmpty(usuarioId)
                    ? await _context.LikesComentarios
                        .Where(l => comentarioIds.Contains(l.ComentarioId) && l.UsuarioId == usuarioId)
                        .Select(l => l.ComentarioId)
                        .ToListAsync()
                    : new List<int>();

                return Json(new
                {
                    success = true,
                    comentarios = comentariosPrincipales.Select(c => new
                    {
                        id = c.Id,
                        texto = c.Texto,
                        fechaCreacion = c.FechaCreacion,
                        numeroLikes = c.NumeroLikes,
                        meDioLike = misLikes.Contains(c.Id),
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
                                numeroLikes = r.NumeroLikes,
                                meDioLike = misLikes.Contains(r.Id),
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
        // LIKES EN COMENTARIOS
        // ========================================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LikeComentario(int comentarioId)
        {
            try
            {
                var usuarioId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var comentario = await _context.Comentarios
                    .Include(c => c.Contenido)
                    .FirstOrDefaultAsync(c => c.Id == comentarioId && c.EstaActivo);

                if (comentario == null)
                {
                    return Json(new { success = false, message = "Comentario no encontrado" });
                }

                // Verificar si ya dio like
                var likeExistente = await _context.LikesComentarios
                    .FirstOrDefaultAsync(l => l.ComentarioId == comentarioId && l.UsuarioId == usuarioId);

                if (likeExistente != null)
                {
                    return Json(new { success = true, likes = comentario.NumeroLikes, meDioLike = true });
                }

                // Agregar like
                var nuevoLike = new LikeComentario
                {
                    ComentarioId = comentarioId,
                    UsuarioId = usuarioId,
                    FechaLike = DateTime.Now
                };

                _context.LikesComentarios.Add(nuevoLike);
                comentario.NumeroLikes++;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Like en comentario: Usuario {UserId} en Comentario {ComentarioId}",
                    usuarioId, comentarioId);

                return Json(new { success = true, likes = comentario.NumeroLikes, meDioLike = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al dar like al comentario {ComentarioId}", comentarioId);
                return Json(new { success = false, message = "Error al procesar like" });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlikeComentario(int comentarioId)
        {
            try
            {
                var usuarioId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var comentario = await _context.Comentarios
                    .FirstOrDefaultAsync(c => c.Id == comentarioId && c.EstaActivo);

                if (comentario == null)
                {
                    return Json(new { success = false, message = "Comentario no encontrado" });
                }

                var likeExistente = await _context.LikesComentarios
                    .FirstOrDefaultAsync(l => l.ComentarioId == comentarioId && l.UsuarioId == usuarioId);

                if (likeExistente == null)
                {
                    return Json(new { success = true, likes = comentario.NumeroLikes, meDioLike = false });
                }

                // Remover like
                _context.LikesComentarios.Remove(likeExistente);
                comentario.NumeroLikes = Math.Max(0, comentario.NumeroLikes - 1);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Unlike en comentario: Usuario {UserId} en Comentario {ComentarioId}",
                    usuarioId, comentarioId);

                return Json(new { success = true, likes = comentario.NumeroLikes, meDioLike = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al quitar like del comentario {ComentarioId}", comentarioId);
                return Json(new { success = false, message = "Error al procesar unlike" });
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLikeComentario(int comentarioId)
        {
            try
            {
                var usuarioId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var comentario = await _context.Comentarios
                    .FirstOrDefaultAsync(c => c.Id == comentarioId && c.EstaActivo);

                if (comentario == null)
                {
                    return Json(new { success = false, message = "Comentario no encontrado" });
                }

                var likeExistente = await _context.LikesComentarios
                    .FirstOrDefaultAsync(l => l.ComentarioId == comentarioId && l.UsuarioId == usuarioId);

                bool meDioLike;

                if (likeExistente != null)
                {
                    // Quitar like
                    _context.LikesComentarios.Remove(likeExistente);
                    comentario.NumeroLikes = Math.Max(0, comentario.NumeroLikes - 1);
                    meDioLike = false;
                }
                else
                {
                    // Agregar like
                    var nuevoLike = new LikeComentario
                    {
                        ComentarioId = comentarioId,
                        UsuarioId = usuarioId,
                        FechaLike = DateTime.Now
                    };
                    _context.LikesComentarios.Add(nuevoLike);
                    comentario.NumeroLikes++;
                    meDioLike = true;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, likes = comentario.NumeroLikes, meDioLike });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al toggle like del comentario {ComentarioId}", comentarioId);
                return Json(new { success = false, message = "Error al procesar like" });
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
                var usuarioActualId = _userManager.GetUserId(User);
                var contenido = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (contenido == null || !contenido.EstaActivo)
                {
                    return Json(new { success = false, message = "Contenido no encontrado" });
                }

                contenido.NumeroCompartidos++;
                await _context.SaveChangesAsync();

                // Generar URL que abre el post dentro del feed normal
                var feedUrl = Url.Action("Index", "Feed", null, Request.Scheme) + "?post=" + id;

                // Determinar si es post propio (puede compartir a historia)
                var esPostPropio = contenido.UsuarioId == usuarioActualId;

                return Json(new
                {
                    success = true,
                    url = feedUrl,
                    totalCompartidos = contenido.NumeroCompartidos,
                    esPostPropio,
                    preview = new {
                        imagen = contenido.RutaArchivo,
                        tieneMedia = !string.IsNullOrEmpty(contenido.RutaArchivo),
                        creador = contenido.Usuario?.NombreCompleto ?? contenido.Usuario?.UserName,
                        descripcion = contenido.Descripcion?.Length > 50
                            ? contenido.Descripcion.Substring(0, 50) + "..."
                            : contenido.Descripcion
                    }
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

                        // 🔔 Push Notification
                        var nombreSeguidor = usuarioActual.NombreCompleto ?? usuarioActual.UserName ?? "Alguien";
                        _ = _pushService.EnviarNotificacionAsync(
                            id,
                            "👤 Nuevo seguidor",
                            $"{nombreSeguidor} comenzó a seguirte",
                            $"/@{usuarioActual.UserName}",
                            TipoNotificacionPush.NuevoSeguidor,
                            usuarioActual.FotoPerfil
                        );
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

                // ⚡ Cargar TODAS las configuraciones de Explorar
                var configExplorar = await ObtenerConfigFeedAsync();
                var limiteCreadores = configExplorar[ConfiguracionPlataforma.EXPLORAR_LIMITE_CREADORES];
                var limiteContenido = configExplorar[ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO];
                var limiteZonasMapa = configExplorar[ConfiguracionPlataforma.EXPLORAR_LIMITE_ZONAS_MAPA];
                var limiteContenidoMapa = configExplorar[ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO_MAPA];

                // Verificar configuración de bloqueo LadoB
                var bloquearLadoB = usuarioActual.BloquearLadoB;

                // ⚡ Obtener IDs de administradores CON CACHE
                var adminIds = await ObtenerAdminIdsCacheadosAsync();

                // ⚡ Obtener usuarios bloqueados CON CACHE
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // ⚡ Obtener intereses del usuario para scoring
                var interesesUsuario = await _context.InteresesUsuarios
                    .Where(i => i.UsuarioId == usuarioActual.Id && i.PesoInteres > 0)
                    .ToDictionaryAsync(i => i.CategoriaInteresId, i => i.PesoInteres);

                // Mostrar creadores: EsCreador = true O tienen Seudonimo (creadores de facto)
                var usuariosQuery = _userManager.Users
                    .AsNoTracking() // ⚡ No tracking para read-only
                    .Where(u => u.EstaActivo
                            && (u.EsCreador || u.Seudonimo != null)
                            && u.Id != usuarioActual.Id
                            && !adminIds.Contains(u.Id)
                            && !usuariosBloqueadosIds.Contains(u.Id));

                // Si BloquearLadoB está activo, excluir creadores con contenido adulto
                if (bloquearLadoB)
                {
                    usuariosQuery = usuariosQuery
                        .Where(u => !u.CreadorVerificado || string.IsNullOrEmpty(u.Seudonimo));
                }

                var usuarios = await usuariosQuery
                    .OrderByDescending(u => u.CreadorVerificado)
                    .ThenByDescending(u => u.NumeroSeguidores)
                    .Take(limiteCreadores)
                    .ToListAsync();

                // Separar creadores por tipo
                // LadoA: TODOS los creadores (muestran su identidad pública)
                // LadoB: Solo los que tienen LadoB habilitado (muestran su identidad premium/seudónimo)
                var creadoresLadoA = usuarios.ToList(); // Todos aparecen en Creadores
                // Si BloquearLadoB está activo, la lista de LadoB estará vacía
                var creadoresLadoB = bloquearLadoB
                    ? new List<ApplicationUser>()
                    : usuarios.Where(c => c.TieneLadoB()).ToList();

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

                // Obtener contenido para explorar (límite configurable)
                // Mostrar TODO el contenido (LadoA y LadoB) - el LadoB se mostrará bloqueado si no está suscrito
                var contenidoExplorarQuery = _context.Contenidos
                    .Include(c => c.Usuario)
                    .AsNoTracking() // ⚡ No tracking para read-only
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.RutaArchivo != null && c.RutaArchivo != "" // ⚡ Mejor para índices
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId));

                // Si BloquearLadoB está activo, excluir contenido LadoB
                if (bloquearLadoB)
                {
                    contenidoExplorarQuery = contenidoExplorarQuery
                        .Where(c => c.TipoLado != TipoLado.LadoB);
                }

                // ⚡ Cargar más candidatos para aplicar scoring por intereses
                var candidatosExplorar = await contenidoExplorarQuery
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(limiteContenido * 2) // Cargar más para mejor selección
                    .ToListAsync();

                // ⚡ Aplicar scoring de intereses si el usuario tiene intereses configurados
                List<Contenido> contenidoExplorar;
                if (interesesUsuario.Any())
                {
                    contenidoExplorar = candidatosExplorar
                        .Select(c => new { Contenido = c, Score = CalcularScoreDescubrimiento(c, interesesUsuario) })
                        .OrderByDescending(x => x.Score)
                        .ThenByDescending(x => x.Contenido.NumeroLikes + x.Contenido.NumeroComentarios * 2)
                        .Take(limiteContenido)
                        .Select(x => x.Contenido)
                        .ToList();
                }
                else
                {
                    // Sin intereses, usar solo engagement
                    contenidoExplorar = candidatosExplorar.Take(limiteContenido).ToList();
                }

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
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // SOLO LadoA
                            && c.NombreUbicacion != null && c.NombreUbicacion != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .GroupBy(c => c.NombreUbicacion)
                    .Select(g => new { Nombre = g.Key, Count = g.Count() })
                    .OrderByDescending(z => z.Count)
                    .Take(limiteZonasMapa)
                    .ToListAsync();

                ViewBag.ZonasUbicacion = zonasUbicacion;

                // Contenido inicial del mapa (límite configurable, SOLO LadoA)
                var contenidoMapa = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // SOLO LadoA
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && c.NombreUbicacion != null && c.NombreUbicacion != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(limiteContenidoMapa)
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
        public async Task<IActionResult> ExplorarContenido(int page = 1, string tipo = "todos", string orden = "popular", int? categoriaId = null, string? objeto = null, string? buscar = null)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                // Verificar configuración de bloqueo LadoB
                var bloquearLadoB = usuarioActual.BloquearLadoB;

                // ⚡ Cargar configuración de page size
                var configExplorar = await ObtenerConfigFeedAsync();
                var pageSize = configExplorar[ConfiguracionPlataforma.EXPLORAR_PAGE_SIZE];
                var skip = (page - 1) * pageSize;

                // ⚡ Usar métodos cacheados
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // Obtener suscripciones específicas a LadoB para verificar acceso a contenido premium
                var suscripcionesLadoB = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva && s.TipoLado == TipoLado.LadoB)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Obtener IDs de creadores que el usuario sigue (todas las suscripciones activas)
                var suscripcionesTodas = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.FanId == usuarioActual.Id && s.EstaActiva)
                    .Select(s => s.CreadorId)
                    .ToListAsync();

                // Si se busca por objeto, obtener los IDs de contenido que tienen ese objeto
                List<int>? contenidosConObjeto = null;
                if (!string.IsNullOrWhiteSpace(objeto))
                {
                    var objetoNormalizado = objeto.Trim().ToLower();
                    contenidosConObjeto = await _context.ObjetosContenido
                        .AsNoTracking()
                        .Where(o => o.NombreObjeto.Contains(objetoNormalizado))
                        .Select(o => o.ContenidoId)
                        .Distinct()
                        .ToListAsync();
                }

                var query = _context.Contenidos
                    .AsNoTracking() // ⚡ No tracking
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId));

                // Si BloquearLadoB está activo, excluir contenido LadoB
                if (bloquearLadoB)
                {
                    query = query.Where(c => c.TipoLado != TipoLado.LadoB);
                }

                // Filtrar por objeto detectado
                if (contenidosConObjeto != null)
                {
                    query = query.Where(c => contenidosConObjeto.Contains(c.Id));
                }

                // Filtrar por categoría de interés
                if (categoriaId.HasValue)
                {
                    query = query.Where(c => c.CategoriaInteresId == categoriaId.Value);
                }

                // Filtrar por tipo
                if (tipo == "siguiendo")
                {
                    // Solo contenido de creadores que el usuario sigue
                    query = query.Where(c => suscripcionesTodas.Contains(c.UsuarioId));
                }
                else if (tipo == "fotos")
                {
                    query = query.Where(c => c.TipoContenido == TipoContenido.Foto || c.TipoContenido == TipoContenido.Imagen);
                }
                else if (tipo == "videos")
                {
                    query = query.Where(c => c.TipoContenido == TipoContenido.Video);
                }

                // ⚡ Filtrar por búsqueda de texto (descripción o nombre de usuario)
                if (!string.IsNullOrWhiteSpace(buscar) && buscar.Length >= 2)
                {
                    var buscarNormalizado = buscar.Trim().ToLower();
                    query = query.Where(c =>
                        (c.Descripcion != null && c.Descripcion.ToLower().Contains(buscarNormalizado)) ||
                        (c.NombreMostrado != null && c.NombreMostrado.ToLower().Contains(buscarNormalizado)) ||
                        (c.Usuario != null && c.Usuario.UserName != null && c.Usuario.UserName.ToLower().Contains(buscarNormalizado)) ||
                        (c.Usuario != null && c.Usuario.NombreCompleto != null && c.Usuario.NombreCompleto.ToLower().Contains(buscarNormalizado)) ||
                        (c.Usuario != null && c.Usuario.Seudonimo != null && c.Usuario.Seudonimo.ToLower().Contains(buscarNormalizado)));
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
                            && !c.OcultoSilenciosamente // Shadow hide
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
        // BUSCAR CONTENIDO POR OBJETO DETECTADO (AJAX)
        // Permite buscar por objetos visuales: "moto", "perro", "playa"
        // ========================================

        [HttpGet]
        public async Task<IActionResult> BuscarPorObjeto(string objeto, int page = 1, string orden = "popular")
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                if (string.IsNullOrWhiteSpace(objeto))
                {
                    return Json(new { success = false, message = "Objeto no especificado" });
                }

                // Normalizar el objeto buscado (minúsculas, sin tildes)
                objeto = objeto.ToLowerInvariant().Trim()
                    .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i')
                    .Replace('ó', 'o').Replace('ú', 'u');

                const int pageSize = 30;
                var skip = (page - 1) * pageSize;

                // Obtener usuarios bloqueados
                var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioActual.Id);

                // Buscar contenido que tenga el objeto detectado
                var query = _context.ObjetosContenido
                    .AsNoTracking()
                    .Where(o => o.NombreObjeto.Contains(objeto) && o.Confianza >= 0.7f)
                    .Select(o => o.ContenidoId)
                    .Distinct();

                // Obtener el contenido asociado
                var contenidoQuery = _context.Contenidos
                    .AsNoTracking()
                    .Where(c => query.Contains(c.Id)
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA  // Solo LadoA (público)
                            && c.RutaArchivo != null && c.RutaArchivo != ""
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId));

                // Ordenar
                if (orden == "reciente")
                {
                    contenidoQuery = contenidoQuery.OrderByDescending(c => c.FechaPublicacion);
                }
                else
                {
                    contenidoQuery = contenidoQuery.OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                                                   .ThenByDescending(c => c.FechaPublicacion);
                }

                // Proyección
                var contenido = await contenidoQuery
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        id = c.Id,
                        rutaArchivo = c.RutaArchivo,
                        tipoContenido = (int)c.TipoContenido,
                        esContenidoSensible = c.EsContenidoSensible,
                        numeroLikes = c.NumeroLikes,
                        numeroComentarios = c.NumeroComentarios,
                        thumbnail = c.Thumbnail,
                        usuarioId = c.UsuarioId,
                        usuarioUsername = c.Usuario != null ? c.Usuario.UserName : null,
                        usuarioFotoPerfil = c.Usuario != null ? c.Usuario.FotoPerfil : null,
                        objetosDetectados = c.ObjetosDetectados.Select(o => o.NombreObjeto).ToList()
                    })
                    .ToListAsync();

                // Obtener objetos relacionados (sugerencias)
                var objetosRelacionados = await _context.ObjetosContenido
                    .AsNoTracking()
                    .Where(o => query.Contains(o.ContenidoId) && o.NombreObjeto != objeto)
                    .GroupBy(o => o.NombreObjeto)
                    .Select(g => new { nombre = g.Key, count = g.Count() })
                    .OrderByDescending(g => g.count)
                    .Take(10)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    objeto = objeto,
                    items = contenido.Select(c => new
                    {
                        c.id,
                        c.rutaArchivo,
                        c.tipoContenido,
                        c.esContenidoSensible,
                        c.numeroLikes,
                        c.numeroComentarios,
                        c.thumbnail,
                        c.objetosDetectados,
                        usuario = new
                        {
                            id = c.usuarioId,
                            username = c.usuarioUsername,
                            fotoPerfil = c.usuarioFotoPerfil
                        }
                    }),
                    objetosRelacionados = objetosRelacionados.Select(o => o.nombre),
                    hasMore = contenido.Count == pageSize,
                    page = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BuscarPorObjeto: {Objeto}", objeto);
                return Json(new { success = false, message = "Error al buscar contenido" });
            }
        }

        /// <summary>
        /// Obtiene los objetos más populares detectados en el contenido
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerObjetosPopulares(int limit = 20)
        {
            try
            {
                var objetosPopulares = await _context.ObjetosContenido
                    .AsNoTracking()
                    .Where(o => o.Confianza >= 0.7f)
                    .GroupBy(o => o.NombreObjeto)
                    .Select(g => new
                    {
                        nombre = g.Key,
                        count = g.Count()
                    })
                    .OrderByDescending(g => g.count)
                    .Take(limit)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    objetos = objetosPopulares
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener objetos populares");
                return Json(new { success = false, message = "Error" });
            }
        }

        // ========================================
        // OBTENER USUARIOS QUE DIERON LIKE (AJAX)
        // ========================================

        /// <summary>
        /// Obtiene los usuarios que dieron like a un contenido
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerLikesUsuarios(int contenidoId, int limit = 50)
        {
            try
            {
                var usuarioActualId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                var likes = await _context.Likes
                    .AsNoTracking()
                    .Where(l => l.ContenidoId == contenidoId)
                    .OrderByDescending(l => l.FechaLike)
                    .Take(limit)
                    .Include(l => l.Usuario)
                    .Select(l => new
                    {
                        id = l.UsuarioId,
                        username = l.Usuario.UserName,
                        nombre = l.Usuario.NombreCompleto ?? l.Usuario.UserName,
                        foto = l.Usuario.FotoPerfil ?? "/images/default-avatar.png",
                        esVerificado = l.Usuario.EsVerificado,
                        esCreador = l.Usuario.EsCreador
                    })
                    .ToListAsync();

                // Obtener IDs de usuarios que el usuario actual sigue
                var siguiendoIds = new HashSet<string>();
                if (!string.IsNullOrEmpty(usuarioActualId))
                {
                    siguiendoIds = (await _context.Suscripciones
                        .AsNoTracking()
                        .Where(s => s.FanId == usuarioActualId && s.EstaActiva)
                        .Select(s => s.CreadorId)
                        .ToListAsync())
                        .ToHashSet();
                }

                // Combinar datos con estado de seguimiento
                var usuariosConSeguimiento = likes.Select(l => new
                {
                    l.id,
                    l.username,
                    l.nombre,
                    l.foto,
                    l.esVerificado,
                    l.esCreador,
                    loSigo = siguiendoIds.Contains(l.id),
                    esSoyYo = l.id == usuarioActualId
                }).ToList();

                // Obtener total de likes
                var totalLikes = await _context.Likes
                    .CountAsync(l => l.ContenidoId == contenidoId);

                return Json(new
                {
                    success = true,
                    usuarios = usuariosConSeguimiento,
                    total = totalLikes,
                    hayMas = totalLikes > limit
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener likes de contenido {ContenidoId}", contenidoId);
                return Json(new { success = false, message = "Error" });
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
                            && !c.OcultoSilenciosamente // Shadow hide
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
        // GRAFO DE INTERESES
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerGrafoIntereses()
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);

                // Obtener TODAS las categorías principales activas (con o sin contenido)
                var todasCategorias = await _context.CategoriasIntereses
                    .Where(c => c.EstaActiva && c.CategoriaPadreId == null)
                    .Select(c => new
                    {
                        c.Id,
                        c.Nombre,
                        c.Color,
                        c.Icono,
                        c.Orden,
                        ContadorContenido = _context.Contenidos.Count(con =>
                            con.CategoriaInteresId == c.Id &&
                            con.EstaActivo &&
                            !con.EsBorrador &&
                            !con.Censurado &&
                            !con.EsPrivado)
                    })
                    .OrderBy(c => c.Orden)
                    .ThenByDescending(c => c.ContadorContenido)
                    .Take(12)
                    .ToListAsync();

                // Si no hay categorías en la BD, mostrar mensaje vacío
                if (!todasCategorias.Any())
                {
                    return Json(new {
                        success = true,
                        nodos = new List<object> { new { id = "user", label = "YO", tipo = "centro", color = "#4682b4" } },
                        enlaces = new List<object>(),
                        mensaje = "Las categorías se crean automáticamente al subir contenido"
                    });
                }

                // Si el usuario está autenticado, obtener sus intereses
                var interesesUsuario = new List<InteresUsuario>();
                if (usuarioActual != null)
                {
                    interesesUsuario = await _context.InteresesUsuarios
                        .Where(i => i.UsuarioId == usuarioActual.Id)
                        .Include(i => i.CategoriaInteres)
                        .OrderByDescending(i => i.PesoInteres)
                        .Take(10)
                        .ToListAsync();
                }

                // Construir nodos del grafo
                var nodosResult = new List<object>();
                var enlacesResult = new List<object>();

                // Nodo central "YO"
                nodosResult.Add(new
                {
                    id = "user",
                    label = "YO",
                    tipo = "centro",
                    color = "#4682b4"
                });

                // Mostrar todas las categorías principales
                foreach (var categoria in todasCategorias)
                {
                    // Verificar si el usuario tiene interés en esta categoría
                    var interesUsuario = interesesUsuario.FirstOrDefault(i => i.CategoriaInteresId == categoria.Id);
                    var peso = interesUsuario?.PesoInteres ?? (categoria.ContadorContenido > 0 ? 5m : 3m);

                    nodosResult.Add(new
                    {
                        id = $"cat_{categoria.Id}",
                        label = categoria.Nombre,
                        tipo = "categoria",
                        categoriaId = categoria.Id,
                        peso = peso,
                        color = categoria.Color ?? "",
                        contadorContenido = categoria.ContadorContenido
                    });

                    enlacesResult.Add(new
                    {
                        source = "user",
                        target = $"cat_{categoria.Id}"
                    });
                }

                // Obtener subcategorías
                var categoriaIds = todasCategorias.Select(c => c.Id).ToList();

                var subcategorias = await _context.CategoriasIntereses
                    .Where(c => c.EstaActiva && c.CategoriaPadreId.HasValue && categoriaIds.Contains(c.CategoriaPadreId.Value))
                    .Select(c => new
                    {
                        c.Id,
                        c.Nombre,
                        c.Color,
                        c.CategoriaPadreId,
                        ContadorContenido = _context.Contenidos.Count(con =>
                            con.CategoriaInteresId == c.Id &&
                            con.EstaActivo &&
                            !con.EsBorrador &&
                            !con.Censurado &&
                            !con.EsPrivado)
                    })
                    .ToListAsync();

                foreach (var sub in subcategorias)
                {
                    nodosResult.Add(new
                    {
                        id = $"cat_{sub.Id}",
                        label = sub.Nombre,
                        tipo = "subcategoria",
                        categoriaId = sub.Id,
                        peso = sub.ContadorContenido > 0 ? 4m : 2m,
                        color = sub.Color ?? "",
                        contadorContenido = sub.ContadorContenido
                    });

                    enlacesResult.Add(new
                    {
                        source = $"cat_{sub.CategoriaPadreId}",
                        target = $"cat_{sub.Id}"
                    });
                }

                return Json(new
                {
                    success = true,
                    nodos = nodosResult,
                    enlaces = enlacesResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerGrafoIntereses");
                return Json(new { success = false, message = "Error al cargar intereses" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerMiContenidoGrafo(int page = 1)
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

                var contenido = await _context.Contenidos
                    .AsNoTracking()
                    .Where(c => c.UsuarioId == usuarioActual.Id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.RutaArchivo != null && c.RutaArchivo != "")
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        id = c.Id,
                        rutaArchivo = c.RutaArchivo,
                        tipoContenido = (int)c.TipoContenido,
                        thumbnail = c.Thumbnail,
                        numeroLikes = c.NumeroLikes,
                        numeroComentarios = c.NumeroComentarios
                    })
                    .ToListAsync();

                var totalCount = await _context.Contenidos
                    .AsNoTracking()
                    .CountAsync(c => c.UsuarioId == usuarioActual.Id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.RutaArchivo != null && c.RutaArchivo != "");

                return Json(new
                {
                    success = true,
                    items = contenido,
                    hasMore = skip + contenido.Count < totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerMiContenidoGrafo");
                return Json(new { success = false, message = "Error al cargar contenido" });
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
                    ViewBag.CurrentUserId = "";

                    _logger.LogInformation("Detalle público visto: Contenido {Id} por usuario anónimo", id);

                    return View(contenido);
                }

                // Usuario autenticado
                ViewBag.EstaAutenticado = true;
                ViewBag.CurrentUserId = usuarioActual.Id;

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
                _logger.LogInformation("⭐ Like iniciado: ContenidoId={Id}", id);
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

                // Registrar interacciones (secuencialmente para evitar conflictos de DbContext)
                if (liked && !string.IsNullOrEmpty(usuarioId))
                {
                    // ⭐ Registrar like para LadoCoins PRIMERO (racha de 5 likes diarios)
                    try
                    {
                        var bonoLike = await _rachasService.RegistrarLikeAsync(usuarioId);
                        _logger.LogDebug("⭐ Like registrado para LadoCoins: {UserId}, Bono: {Bono}", usuarioId, bonoLike);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al registrar like para LadoCoins: {UserId}", usuarioId);
                    }

                    // Registrar interaccion para clasificacion de intereses DESPUÉS
                    await _interesesService.RegistrarInteraccionAsync(usuarioId, id, TipoInteraccion.Like);
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
                            // Notificación interna
                            await _notificationService.NotificarNuevoLikeAsync(propietarioId, likeUsuarioId, contId);

                            // 🔔 Push Notification
                            var likeUser = await _userManager.FindByIdAsync(likeUsuarioId);
                            var nombreLiker = likeUser?.NombreCompleto ?? likeUser?.UserName ?? "Alguien";
                            await _pushService.EnviarNotificacionAsync(
                                propietarioId,
                                "❤️ Nuevo like",
                                $"{nombreLiker} le dio like a tu publicación",
                                $"/Feed/Detalle/{contId}",
                                TipoNotificacionPush.NuevoLike,
                                likeUser?.FotoPerfil
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error en notificacion de like");
                        }
                    });
                }

                _logger.LogInformation("⭐ Like completado: ContenidoId={Id}, Liked={Liked}, TotalLikes={Likes}",
                    id, liked, contenido.NumeroLikes);

                return Json(new
                {
                    success = true,
                    likes = contenido.NumeroLikes,
                    liked = liked
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al procesar like para contenido {Id}: {Error}", id, ex.Message);
                return Json(new { success = false, message = "Error al procesar el like: " + ex.Message });
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

            // IMPORTANTE: Verificar suscripción específica a LadoB
            // Una suscripción a LadoA NO da acceso a contenido LadoB
            var estaSuscrito = await _context.Suscripciones
                .AnyAsync(s => s.FanId == usuarioId
                            && s.CreadorId == contenido.UsuarioId
                            && s.TipoLado == TipoLado.LadoB
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
        // METODO AUXILIAR - VERIFICAR QUIEN PUEDE COMENTAR
        // ========================================

        private async Task<(bool puede, string mensaje)> VerificarQuienPuedeComentar(string? usuarioId, ApplicationUser creador)
        {
            if (string.IsNullOrEmpty(usuarioId))
            {
                return (false, "Debes iniciar sesión para comentar");
            }

            // Si el creador es el mismo usuario, siempre puede comentar
            if (creador.Id == usuarioId)
            {
                return (true, "");
            }

            var permiso = creador.QuienPuedeComentar;

            switch (permiso)
            {
                case PermisoPrivacidad.Todos:
                    return (true, "");

                case PermisoPrivacidad.Seguidores:
                    var esSeguidor = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioId &&
                                      s.CreadorId == creador.Id &&
                                      s.EstaActiva);
                    return esSeguidor
                        ? (true, "")
                        : (false, "Solo los seguidores de este creador pueden comentar");

                case PermisoPrivacidad.Suscriptores:
                    var esSuscriptor = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioId &&
                                      s.CreadorId == creador.Id &&
                                      s.EstaActiva &&
                                      s.TipoLado == TipoLado.LadoB);
                    return esSuscriptor
                        ? (true, "")
                        : (false, "Solo los suscriptores premium pueden comentar en el contenido de este creador");

                case PermisoPrivacidad.Nadie:
                    return (false, "El creador ha desactivado los comentarios");

                default:
                    return (true, "");
            }
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
                .Where(c => c.EstaActivo && !c.EsBorrador && !c.Censurado && !c.OcultoSilenciosamente) // Shadow hide
                .ToListAsync();

            var favoritosIds = await _context.Favoritos
                .Where(f => f.UsuarioId == usuarioId)
                .Select(f => f.ContenidoId)
                .ToListAsync();

            ViewBag.FavoritosIds = favoritosIds;

            return View(favoritos);
        }

        // ========================================
        // MIS COMPRAS (Contenido individual comprado)
        // ========================================

        public async Task<IActionResult> MisCompras()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Obtener todas las compras de contenido individual
            var compras = await _context.ComprasContenido
                .Where(cc => cc.UsuarioId == usuarioId)
                .Include(cc => cc.Contenido)
                    .ThenInclude(c => c.Usuario)
                .Include(cc => cc.Contenido)
                    .ThenInclude(c => c.Archivos.OrderBy(a => a.Orden))
                .OrderByDescending(cc => cc.FechaCompra)
                .ToListAsync();

            // Filtrar solo contenido activo
            var comprasActivas = compras
                .Where(cc => cc.Contenido != null
                          && cc.Contenido.EstaActivo
                          && !cc.Contenido.EsBorrador
                          && !cc.Contenido.Censurado)
                .ToList();

            // Estadísticas
            ViewBag.TotalCompras = comprasActivas.Count;
            ViewBag.TotalGastado = comprasActivas.Sum(cc => cc.Monto);
            ViewBag.CreadoresUnicos = comprasActivas
                .Select(cc => cc.Contenido.UsuarioId)
                .Distinct()
                .Count();

            // Agrupar por creador para la vista
            ViewBag.ComprasPorCreador = comprasActivas
                .GroupBy(cc => cc.Contenido.Usuario)
                .Select(g => new {
                    Creador = g.Key,
                    Compras = g.OrderByDescending(cc => cc.FechaCompra).ToList(),
                    Total = g.Sum(cc => cc.Monto)
                })
                .OrderByDescending(g => g.Compras.First().FechaCompra)
                .ToList();

            _logger.LogInformation("Usuario {UserId} consultó sus {Count} compras de contenido",
                usuarioId, comprasActivas.Count);

            return View(comprasActivas);
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

        // ========================================
        // OBTENER POST (para compartir como story)
        // ========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerPost(int id)
        {
            try
            {
                var usuarioActual = await _userManager.GetUserAsync(User);
                if (usuarioActual == null)
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                var post = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.Id == id && c.EstaActivo);

                if (post == null)
                {
                    return Json(new { success = false, message = "Post no encontrado" });
                }

                // Verificar que el usuario tenga acceso al post
                var tieneAcceso = post.UsuarioId == usuarioActual.Id;
                if (!tieneAcceso)
                {
                    // Verificar si sigue al creador
                    var suscripcion = await _context.Suscripciones
                        .AnyAsync(s => s.FanId == usuarioActual.Id && s.CreadorId == post.UsuarioId && s.EstaActiva);
                    tieneAcceso = suscripcion || post.TipoLado == TipoLado.LadoA;
                }

                if (!tieneAcceso)
                {
                    return Json(new { success = false, message = "No tienes acceso a este contenido" });
                }

                return Json(new
                {
                    success = true,
                    post = new
                    {
                        id = post.Id,
                        creadorId = post.UsuarioId,
                        creadorNombre = post.Usuario?.NombreCompleto,
                        descripcion = post.Descripcion,
                        rutaArchivo = post.RutaArchivo,
                        tipoContenido = post.TipoContenido.ToString(),
                        tipoLado = post.TipoLado.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener post {PostId}", id);
                return Json(new { success = false, message = "Error al obtener el post" });
            }
        }

        // ========================================
        // ⭐ PHOTOWALL (MURO) - ENDPOINTS
        // ========================================

        /// <summary>
        /// Vista principal del PhotoWall - Mosaico fullscreen de fotos
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> Muro()
        {
            var usuario = await _userManager.GetUserAsync(User);
            ViewBag.UsuarioId = usuario?.Id;

            // Cargar saldo de LadoCoins directamente
            if (usuario != null)
            {
                var ladoCoin = await _context.LadoCoins.FirstOrDefaultAsync(l => l.UsuarioId == usuario.Id);
                ViewBag.SaldoLadoCoins = ladoCoin?.SaldoDisponible ?? 0;
            }
            else
            {
                ViewBag.SaldoLadoCoins = 0;
            }

            return View();
        }

        /// <summary>
        /// API para obtener fotos del muro con sus niveles de destacado
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ObtenerFotosMuro(int cantidad = 500)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var bloqueados = string.IsNullOrEmpty(usuarioId)
                    ? new List<string>()
                    : await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioId);

                // Obtener fotos elegibles: LadoA, públicas, activas, solo fotos
                var fotosElegibles = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
                        && !c.OcultoSilenciosamente // Shadow hide
                        && !c.EsPrivado
                        && c.TipoLado == TipoLado.LadoA
                        && c.TipoContenido == TipoContenido.Foto
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && !bloqueados.Contains(c.UsuarioId))
                    .OrderBy(c => Guid.NewGuid()) // Orden aleatorio
                    .Take(cantidad)
                    .Select(c => new
                    {
                        c.Id,
                        c.RutaArchivo,
                        c.Thumbnail,
                        CreadorId = c.UsuarioId,
                        CreadorNombre = c.Usuario!.NombreCompleto ?? c.Usuario.UserName,
                        CreadorFoto = c.Usuario.FotoPerfil,
                        CreadorUsername = c.Usuario.UserName
                    })
                    .ToListAsync();

                // Obtener destacados activos
                var ahora = DateTime.UtcNow;
                var destacados = await _context.FotosDestacadas
                    .Where(f => f.FechaInicio <= ahora && f.FechaExpiracion >= ahora)
                    .ToDictionaryAsync(f => f.ContenidoId, f => (int)f.Nivel);

                // Combinar fotos con sus niveles
                var resultado = fotosElegibles.Select(f => new
                {
                    f.Id,
                    Thumbnail = !string.IsNullOrEmpty(f.Thumbnail) ? f.Thumbnail : f.RutaArchivo,
                    f.CreadorId,
                    f.CreadorNombre,
                    f.CreadorFoto,
                    f.CreadorUsername,
                    Nivel = destacados.GetValueOrDefault(f.Id, 0),
                    Tamano = ObtenerTamanoNivel(destacados.GetValueOrDefault(f.Id, 0))
                }).ToList();

                // Si no hay suficientes fotos, duplicar para llenar
                if (resultado.Count < cantidad && resultado.Count > 0)
                {
                    var original = resultado.ToList();
                    while (resultado.Count < cantidad)
                    {
                        var toAdd = original.OrderBy(x => Guid.NewGuid()).Take(cantidad - resultado.Count);
                        resultado.AddRange(toAdd);
                    }
                }

                return Json(new { success = true, fotos = resultado });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener fotos del muro");
                return Json(new { success = false, message = "Error al cargar las fotos" });
            }
        }

        /// <summary>
        /// API para destacar una foto pagando con LadoCoins
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DestacarFoto(int contenidoId, int nivel)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                    return Json(new { success = false, message = "Debes iniciar sesión" });

                // Validar nivel
                if (!Enum.IsDefined(typeof(NivelDestacado), nivel) || nivel == 0)
                    return Json(new { success = false, message = "Nivel de destacado inválido" });

                var nivelDestacado = (NivelDestacado)nivel;
                var costo = FotoDestacada.ObtenerCosto(nivelDestacado);

                // Verificar que el contenido existe y es del usuario
                var contenido = await _context.Contenidos
                    .FirstOrDefaultAsync(c => c.Id == contenidoId
                        && c.UsuarioId == usuario.Id
                        && c.TipoContenido == TipoContenido.Foto
                        && c.TipoLado == TipoLado.LadoA
                        && c.EstaActivo
                        && !c.EsBorrador);

                if (contenido == null)
                    return Json(new { success = false, message = "Contenido no encontrado o no elegible" });

                // Verificar saldo de LadoCoins
                var ladoCoin = await _context.LadoCoins.FirstOrDefaultAsync(l => l.UsuarioId == usuario.Id);
                if (ladoCoin == null || ladoCoin.SaldoDisponible < costo)
                    return Json(new { success = false, message = $"Saldo insuficiente. Necesitas {costo} LadoCoins" });

                // Verificar si ya tiene un destacado activo
                var ahora = DateTime.UtcNow;
                var destacadoExistente = await _context.FotosDestacadas
                    .FirstOrDefaultAsync(f => f.ContenidoId == contenidoId && f.FechaExpiracion > ahora);

                if (destacadoExistente != null)
                {
                    // Si el nuevo nivel es mayor, actualizar
                    if (nivel > (int)destacadoExistente.Nivel)
                    {
                        var costoDiferencia = costo - FotoDestacada.ObtenerCosto(destacadoExistente.Nivel);
                        if (ladoCoin.SaldoDisponible < costoDiferencia)
                            return Json(new { success = false, message = $"Necesitas {costoDiferencia} LadoCoins más para mejorar" });

                        // Actualizar destacado
                        destacadoExistente.Nivel = nivelDestacado;
                        destacadoExistente.FechaExpiracion = ahora.AddHours(FotoDestacada.ObtenerDuracionHoras(nivelDestacado));
                        destacadoExistente.CostoPagado += costoDiferencia;

                        // Descontar LadoCoins
                        ladoCoin.SaldoDisponible -= costoDiferencia;
                        ladoCoin.TotalGastado += costoDiferencia;

                        // Registrar transacción
                        _context.TransaccionesLadoCoins.Add(new TransaccionLadoCoin
                        {
                            UsuarioId = usuario.Id,
                            Tipo = TipoTransaccionLadoCoin.GastoMuro,
                            Monto = -costoDiferencia,
                            SaldoAnterior = ladoCoin.SaldoDisponible + costoDiferencia,
                            SaldoPosterior = ladoCoin.SaldoDisponible,
                            Descripcion = $"Mejora destacado Muro: {destacadoExistente.Nivel} → {nivelDestacado}",
                            ReferenciaId = contenidoId.ToString(),
                            TipoReferencia = "FotoDestacada"
                        });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Ya tienes un destacado igual o superior activo" });
                    }
                }
                else
                {
                    // Crear nuevo destacado
                    var duracionHoras = FotoDestacada.ObtenerDuracionHoras(nivelDestacado);
                    var nuevoDestacado = new FotoDestacada
                    {
                        ContenidoId = contenidoId,
                        UsuarioId = usuario.Id,
                        Nivel = nivelDestacado,
                        FechaInicio = ahora,
                        FechaExpiracion = ahora.AddHours(duracionHoras),
                        CostoPagado = costo
                    };
                    _context.FotosDestacadas.Add(nuevoDestacado);

                    // Descontar LadoCoins
                    ladoCoin.SaldoDisponible -= costo;
                    ladoCoin.TotalGastado += costo;

                    // Registrar transacción
                    _context.TransaccionesLadoCoins.Add(new TransaccionLadoCoin
                    {
                        UsuarioId = usuario.Id,
                        Tipo = TipoTransaccionLadoCoin.GastoMuro,
                        Monto = -costo,
                        SaldoAnterior = ladoCoin.SaldoDisponible + costo,
                        SaldoPosterior = ladoCoin.SaldoDisponible,
                        Descripcion = $"Destacar foto en Muro: Nivel {nivelDestacado}",
                        ReferenciaId = contenidoId.ToString(),
                        TipoReferencia = "FotoDestacada"
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"¡Foto destacada con nivel {nivelDestacado}!",
                    nuevoSaldo = ladoCoin.SaldoDisponible
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al destacar foto {ContenidoId}", contenidoId);
                return Json(new { success = false, message = "Error al procesar la solicitud" });
            }
        }

        /// <summary>
        /// API para obtener las fotos propias del usuario que puede destacar
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerMisFotosParaDestacar()
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                    return Json(new { success = false, message = "No autenticado" });

                var ahora = DateTime.UtcNow;

                // Primero obtenemos los contenidos
                var contenidos = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuario.Id
                        && c.TipoContenido == TipoContenido.Foto
                        && c.TipoLado == TipoLado.LadoA
                        && c.EstaActivo
                        && !c.EsBorrador
                        && !string.IsNullOrEmpty(c.RutaArchivo))
                    .OrderByDescending(c => c.Id)
                    .Take(100)
                    .Select(c => new { c.Id, c.Thumbnail, c.RutaArchivo, c.Descripcion })
                    .ToListAsync();

                // Luego obtenemos los destacados activos de estas fotos
                var contenidoIds = contenidos.Select(c => c.Id).ToList();
                var destacadosActivos = await _context.FotosDestacadas
                    .Where(f => contenidoIds.Contains(f.ContenidoId) && f.FechaExpiracion > ahora)
                    .Select(f => new { f.ContenidoId, f.Nivel, f.FechaExpiracion })
                    .ToListAsync();

                // Combinamos la información
                var fotos = contenidos.Select(c =>
                {
                    var destacado = destacadosActivos.FirstOrDefault(d => d.ContenidoId == c.Id);
                    string? tiempoRestante = null;

                    if (destacado != null)
                    {
                        var tiempo = destacado.FechaExpiracion - ahora;
                        if (tiempo.TotalDays >= 1)
                            tiempoRestante = $"{(int)tiempo.TotalDays}d {tiempo.Hours}h";
                        else if (tiempo.TotalHours >= 1)
                            tiempoRestante = $"{(int)tiempo.TotalHours}h {tiempo.Minutes}m";
                        else
                            tiempoRestante = $"{tiempo.Minutes}m";
                    }

                    return new
                    {
                        c.Id,
                        Thumbnail = !string.IsNullOrEmpty(c.Thumbnail) ? c.Thumbnail : c.RutaArchivo,
                        c.Descripcion,
                        DestacadoActual = destacado != null ? (int?)destacado.Nivel : null,
                        TiempoRestante = tiempoRestante
                    };
                }).ToList();

                return Json(new { success = true, fotos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener fotos para destacar");
                return Json(new { success = false, message = "Error al cargar las fotos" });
            }
        }

        /// <summary>
        /// Obtiene información de precios de destacado
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult ObtenerPreciosDestacado()
        {
            var precios = new[]
            {
                new { Nivel = 1, Nombre = "Bronce", Costo = FotoDestacada.ObtenerCosto(NivelDestacado.Bronce), Duracion = FotoDestacada.ObtenerDuracionHoras(NivelDestacado.Bronce), Tamano = 36 },
                new { Nivel = 2, Nombre = "Plata", Costo = FotoDestacada.ObtenerCosto(NivelDestacado.Plata), Duracion = FotoDestacada.ObtenerDuracionHoras(NivelDestacado.Plata), Tamano = 54 },
                new { Nivel = 3, Nombre = "Oro", Costo = FotoDestacada.ObtenerCosto(NivelDestacado.Oro), Duracion = FotoDestacada.ObtenerDuracionHoras(NivelDestacado.Oro), Tamano = 72 },
                new { Nivel = 4, Nombre = "Diamante", Costo = FotoDestacada.ObtenerCosto(NivelDestacado.Diamante), Duracion = FotoDestacada.ObtenerDuracionHoras(NivelDestacado.Diamante), Tamano = 90 }
            };

            return Json(new { success = true, precios });
        }

        // ========================================
        // BÚSQUEDA GLOBAL (Navbar)
        // ========================================

        /// <summary>
        /// Búsqueda global para el navbar - busca usuarios, contenido y hashtags
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BuscarGlobal(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Json(new { success = true, usuarios = new object[0], contenidos = new object[0], hashtags = new object[0] });
            }

            var usuarioActual = await _userManager.GetUserAsync(User);
            var usuarioId = usuarioActual?.Id;
            var query = q.Trim().ToLower();

            // Obtener usuarios bloqueados
            var usuariosBloqueados = usuarioId != null
                ? await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioId)
                : new List<string>();

            // Obtener IDs de admins para excluirlos
            var adminIds = await ObtenerAdminIdsCacheadosAsync();

            // 1. BUSCAR USUARIOS (máx 5)
            var usuariosQuery = _userManager.Users
                .AsNoTracking()
                .Where(u => u.EstaActivo
                    && !adminIds.Contains(u.Id)
                    && !usuariosBloqueados.Contains(u.Id)
                    && (u.UserName!.ToLower().Contains(query)
                        || (u.NombreCompleto != null && u.NombreCompleto.ToLower().Contains(query))
                        || (u.Seudonimo != null && u.Seudonimo.ToLower().Contains(query))));

            // Si el usuario tiene LadoB bloqueado, excluir creadores adultos
            if (usuarioActual?.BloquearLadoB == true)
            {
                usuariosQuery = usuariosQuery.Where(u => !u.CreadorVerificado || string.IsNullOrEmpty(u.Seudonimo));
            }

            var usuarios = await usuariosQuery
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(5)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.UserName,
                    nombre = u.Seudonimo ?? u.NombreCompleto ?? u.UserName,
                    foto = u.FotoPerfil ?? "/images/default-avatar.svg",
                    verificado = u.CreadorVerificado,
                    seguidores = u.NumeroSeguidores
                })
                .ToListAsync();

            // 2. BUSCAR CONTENIDO (máx 5) - solo LadoA público
            var contenidosQuery = _context.Contenidos
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Where(c => c.EstaActivo
                    && !c.EsBorrador
                    && !c.Censurado
                    && !c.OcultoSilenciosamente // Shadow hide
                    && !c.EsPrivado
                    && c.TipoLado == TipoLado.LadoA
                    && c.Usuario != null
                    && !usuariosBloqueados.Contains(c.UsuarioId)
                    && (c.Descripcion != null && c.Descripcion.ToLower().Contains(query)));

            var contenidos = await contenidosQuery
                .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios)
                .Take(5)
                .Select(c => new
                {
                    id = c.Id,
                    descripcion = c.Descripcion != null && c.Descripcion.Length > 60
                        ? c.Descripcion.Substring(0, 60) + "..."
                        : c.Descripcion,
                    thumbnail = c.Thumbnail ?? c.RutaArchivo,
                    tipo = c.TipoContenido.ToString(),
                    usuario = c.Usuario!.Seudonimo ?? c.Usuario.NombreCompleto ?? c.Usuario.UserName,
                    likes = c.NumeroLikes
                })
                .ToListAsync();

            // 3. BUSCAR HASHTAGS/TAGS (máx 5)
            // Buscar en los tags de contenidos populares
            var hashtags = await _context.Contenidos
                .AsNoTracking()
                .Where(c => c.EstaActivo
                    && !c.EsBorrador
                    && !c.Censurado
                    && !c.OcultoSilenciosamente // Shadow hide
                    && c.Tags != null
                    && c.Tags.ToLower().Contains(query))
                .GroupBy(c => c.Tags)
                .Select(g => new { tag = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(5)
                .ToListAsync();

            // Extraer tags individuales que coincidan
            var tagsEncontrados = new List<object>();
            foreach (var item in hashtags)
            {
                if (item.tag != null)
                {
                    // Los tags pueden estar separados por comas o ser JSON
                    var tagsList = item.tag.Split(new[] { ',', '[', ']', '"' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim().ToLower())
                        .Where(t => t.Contains(query) && t.Length > 1)
                        .Distinct();

                    foreach (var tag in tagsList)
                    {
                        if (tagsEncontrados.Count < 5 && !tagsEncontrados.Any(t => ((dynamic)t).nombre == tag))
                        {
                            // Contar cuántos contenidos tienen este tag
                            var conteo = await _context.Contenidos
                                .CountAsync(c => c.EstaActivo && c.Tags != null && c.Tags.ToLower().Contains(tag));

                            tagsEncontrados.Add(new { nombre = tag, cantidad = conteo });
                        }
                    }
                }
            }

            return Json(new
            {
                success = true,
                usuarios,
                contenidos,
                hashtags = tagsEncontrados.Take(5)
            });
        }

        private static int ObtenerTamanoNivel(int nivel)
        {
            return nivel switch
            {
                1 => 36,  // Bronce (2x)
                2 => 54,  // Plata (3x)
                3 => 72,  // Oro (4x)
                4 => 90,  // Diamante (5x)
                _ => 18   // Normal
            };
        }
    }
}
