using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    /// <summary>
    /// Interfaz para el servicio de contenido del Feed
    /// Centraliza toda la lógica de negocio para obtener y ordenar contenido
    /// </summary>
    public interface IFeedContentService
    {
        /// <summary>
        /// Obtiene el contenido del feed para un usuario
        /// </summary>
        Task<FeedResultado> ObtenerContenidoFeedAsync(
            string usuarioId,
            int cantidad,
            HashSet<int>? idsYaVistos = null,
            int? semillaOverride = null);

        /// <summary>
        /// Obtiene datos auxiliares para la vista del feed (stories, colecciones, sugerencias)
        /// </summary>
        Task<FeedDatosAuxiliares> ObtenerDatosAuxiliaresAsync(
            string usuarioId,
            List<string> creadoresIds,
            List<string> usuariosBloqueadosIds,
            bool ocultarLadoB);

        /// <summary>
        /// Obtiene o crea la semilla de sesión para aleatorización consistente
        /// </summary>
        int ObtenerOCrearSemilla(ISession session);

        /// <summary>
        /// Invalida el cache del feed para un usuario
        /// </summary>
        void InvalidarCacheFeed(string usuarioId);
    }

    /// <summary>
    /// Resultado del feed con contenido y metadata
    /// </summary>
    public class FeedResultado
    {
        public List<Contenido> Contenidos { get; set; } = new();
        public bool HayMas { get; set; }
        public HashSet<int> ContenidoBloqueadoIds { get; set; } = new();
        public AlgoritmoFeed? AlgoritmoUsuario { get; set; }
        public int Semilla { get; set; }
        public List<string> CreadoresIds { get; set; } = new();
        public List<string> CreadoresLadoAIds { get; set; } = new();
        public List<string> CreadoresLadoBIds { get; set; } = new();
        public List<int> ContenidosCompradosIds { get; set; } = new();
        public List<string> UsuariosBloqueadosIds { get; set; } = new();
        public bool OcultarLadoB { get; set; }
        public int TotalContenidoDisponible { get; set; }
    }

    /// <summary>
    /// Datos auxiliares para la vista del feed
    /// </summary>
    public class FeedDatosAuxiliares
    {
        public List<object> CreadoresFavoritos { get; set; } = new();
        public List<object> Stories { get; set; } = new();
        public List<object> Colecciones { get; set; } = new();
        public List<ApplicationUser> CreadoresSugeridos { get; set; } = new();
        public List<ApplicationUser> UsuariosLadoBPorIntereses { get; set; } = new();
    }

    /// <summary>
    /// Servicio para obtener y procesar contenido del Feed
    /// </summary>
    public class FeedContentService : IFeedContentService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FeedContentService> _logger;
        private readonly IFeedAlgorithmService _feedAlgorithmService;
        private readonly IMemoryCache _cache;

        private const string SESSION_SEED_KEY = "FeedRandomSeed";
        private const string CACHE_PREFIX_BLOQUEOS = "bloqueos_";
        private const string CACHE_PREFIX_CONFIG = "feed_config_all";

        public FeedContentService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedContentService> logger,
            IFeedAlgorithmService feedAlgorithmService,
            IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _feedAlgorithmService = feedAlgorithmService;
            _cache = cache;
        }

        #region Métodos Públicos

        public int ObtenerOCrearSemilla(ISession session)
        {
            var semilla = session.GetInt32(SESSION_SEED_KEY);
            if (!semilla.HasValue)
            {
                semilla = DateTime.Now.GetHashCode() ^ Guid.NewGuid().GetHashCode();
                session.SetInt32(SESSION_SEED_KEY, semilla.Value);
                _logger.LogDebug("Nueva semilla de sesión creada: {Semilla}", semilla.Value);
            }
            return semilla.Value;
        }

        public void InvalidarCacheFeed(string usuarioId)
        {
            _cache.Remove($"{CACHE_PREFIX_BLOQUEOS}{usuarioId}");
        }

        public async Task<FeedResultado> ObtenerContenidoFeedAsync(
            string usuarioId,
            int cantidad,
            HashSet<int>? idsYaVistos = null,
            int? semillaOverride = null)
        {
            var resultado = new FeedResultado();
            var semilla = semillaOverride ?? DateTime.Today.GetHashCode();
            resultado.Semilla = semilla;

            // Obtener configuración
            var configFeed = await ObtenerConfigFeedAsync();
            var limiteLadoA = configFeed[ConfiguracionPlataforma.FEED_LIMITE_LADOA];
            var limiteLadoBSuscriptos = configFeed[ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS];
            var limiteLadoBPropio = configFeed[ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO];
            var limiteComprado = configFeed[ConfiguracionPlataforma.FEED_LIMITE_COMPRADO];
            var descubrimientoLadoACant = configFeed[ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD];
            var descubrimientoLadoBCant = configFeed[ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD];
            var maxPostsConsecutivos = configFeed[ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR];
            var cantidadPreview = configFeed[ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD];
            var intervaloPreview = configFeed[ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO];

            // Verificar configuración de bloqueo LadoB
            var usuarioActualConfig = await _userManager.FindByIdAsync(usuarioId);
            var bloquearLadoB = usuarioActualConfig?.BloquearLadoB ?? false;
            var enModoLadoA = usuarioActualConfig?.LadoPreferido == TipoLado.LadoA;
            var ocultarLadoB = bloquearLadoB || enModoLadoA;
            resultado.OcultarLadoB = ocultarLadoB;

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

            if (ocultarLadoB)
            {
                creadoresLadoBIds.Clear();
            }

            var creadoresIds = suscripcionesActivas
                .Select(s => s.CreadorId)
                .Distinct()
                .ToList();

            // Usuarios bloqueados
            var usuariosBloqueadosIds = await ObtenerUsuariosBloqueadosCacheadosAsync(usuarioId);
            creadoresLadoAIds = creadoresLadoAIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();
            creadoresLadoBIds = creadoresLadoBIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();
            creadoresIds = creadoresIds.Where(id => !usuariosBloqueadosIds.Contains(id)).ToList();

            resultado.CreadoresIds = creadoresIds;
            resultado.CreadoresLadoAIds = creadoresLadoAIds;
            resultado.CreadoresLadoBIds = creadoresLadoBIds;
            resultado.UsuariosBloqueadosIds = usuariosBloqueadosIds;

            // Contenidos comprados
            var contenidosCompradosIds = await _context.ComprasContenido
                .Where(cc => cc.UsuarioId == usuarioId)
                .Select(cc => cc.ContenidoId)
                .ToListAsync();
            resultado.ContenidosCompradosIds = contenidosCompradosIds;

            // Intereses del usuario
            var interesesUsuario = await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.3m)
                .ToDictionaryAsync(i => i.CategoriaInteresId, i => i.PesoInteres);

            // ========================================
            // CARGAR CONTENIDO POR FUENTE
            // ========================================

            // 1. Contenido público (LadoA)
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
                        && (creadoresIds.Contains(c.UsuarioId) || c.UsuarioId == usuarioId))
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(limiteLadoA)
                .ToListAsync();

            // 2. Contenido premium (LadoB) de suscripciones
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
                    .Take(limiteLadoBSuscriptos)
                    .ToListAsync()
                : new List<Contenido>();

            // 3. Contenido premium propio
            var contenidoPremiumPropio = !ocultarLadoB
                ? await _context.Contenidos
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
                    .Take(limiteLadoBPropio)
                    .ToListAsync()
                : new List<Contenido>();

            // 4. Contenido comprado
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
                            && c.Usuario != null
                            && (!ocultarLadoB || c.TipoLado != TipoLado.LadoB))
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(limiteComprado)
                    .ToListAsync()
                : new List<Contenido>();

            // 5. Descubrimiento LadoA (creadores NO seguidos)
            var contenidoDescubrimientoLadoA = new List<Contenido>();
            if (descubrimientoLadoACant > 0)
            {
                var candidatosLadoA = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA
                            && c.Usuario != null
                            && c.UsuarioId != usuarioId
                            && !creadoresIds.Contains(c.UsuarioId)
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId)
                            && c.FechaPublicacion > DateTime.Now.AddDays(-7))
                    .Take(descubrimientoLadoACant * 5)
                    .ToListAsync();

                contenidoDescubrimientoLadoA = AplicarScoringDescubrimiento(
                    candidatosLadoA, interesesUsuario, descubrimientoLadoACant, semilla);
            }

            // 6. Preview LadoB (creadores NO suscritos) - con selección determinística
            var contenidoLadoBBloqueado = new List<Contenido>();
            if (!ocultarLadoB && descubrimientoLadoBCant > 0)
            {
                var candidatosLadoB = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Include(c => c.PistaMusical)
                    .Include(c => c.Archivos.OrderBy(a => a.Orden))
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.UsuarioId != usuarioId
                            && !creadoresLadoBIds.Contains(c.UsuarioId)
                            && !contenidosCompradosIds.Contains(c.Id)
                            && !usuariosBloqueadosIds.Contains(c.UsuarioId))
                    .Take(descubrimientoLadoBCant * 5)
                    .ToListAsync();

                contenidoLadoBBloqueado = AplicarScoringDescubrimiento(
                    candidatosLadoB, interesesUsuario, descubrimientoLadoBCant, semilla);
            }

            // ========================================
            // COMBINAR Y FILTRAR
            // ========================================
            var idsVistos = idsYaVistos ?? new HashSet<int>();
            var todoContenido = new List<Contenido>();

            // Agregar en orden de prioridad, evitando duplicados
            foreach (var contenido in contenidoPublico
                .Concat(contenidoPremiumSuscripciones)
                .Concat(contenidoPremiumPropio)
                .Concat(contenidoPremiumComprado)
                .Concat(contenidoDescubrimientoLadoA))
            {
                if (!idsVistos.Contains(contenido.Id) && todoContenido.All(c => c.Id != contenido.Id))
                {
                    todoContenido.Add(contenido);
                }
            }

            // Filtrar contenido LadoB bloqueado también
            contenidoLadoBBloqueado = contenidoLadoBBloqueado
                .Where(c => !idsVistos.Contains(c.Id))
                .ToList();

            resultado.ContenidoBloqueadoIds = contenidoLadoBBloqueado.Select(c => c.Id).ToHashSet();

            // ========================================
            // APLICAR ALGORITMO
            // ========================================
            var algoritmoUsuario = await _feedAlgorithmService.ObtenerAlgoritmoUsuarioAsync(usuarioId, _context);
            var codigoAlgoritmo = algoritmoUsuario?.Codigo ?? "cronologico";
            resultado.AlgoritmoUsuario = algoritmoUsuario;

            var contenidoOrdenado = await _feedAlgorithmService.AplicarAlgoritmoAsync(
                todoContenido,
                codigoAlgoritmo,
                usuarioId,
                _context,
                semilla);

            // Aplicar variedad de creadores
            contenidoOrdenado = AplicarVariedadCreadores(contenidoOrdenado.ToList(), maxPostsConsecutivos);

            // Intercalar previews de LadoB
            if (contenidoLadoBBloqueado.Any())
            {
                contenidoOrdenado = IntercalarPreviewLadoB(
                    contenidoOrdenado.ToList(),
                    contenidoLadoBBloqueado,
                    cantidadPreview,
                    intervaloPreview);
            }

            resultado.TotalContenidoDisponible = contenidoOrdenado.Count;
            resultado.Contenidos = contenidoOrdenado.Take(cantidad).ToList();
            resultado.HayMas = contenidoOrdenado.Count > cantidad;

            _logger.LogDebug("Feed para usuario {UserId}: {Total} posts, hayMas={HayMas}, semilla={Semilla}",
                usuarioId, resultado.Contenidos.Count, resultado.HayMas, semilla);

            return resultado;
        }

        public async Task<FeedDatosAuxiliares> ObtenerDatosAuxiliaresAsync(
            string usuarioId,
            List<string> creadoresIds,
            List<string> usuariosBloqueadosIds,
            bool ocultarLadoB)
        {
            var datos = new FeedDatosAuxiliares();
            var configFeed = await ObtenerConfigFeedAsync();
            var descubrimientoUsuariosCant = configFeed[ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD];

            // 1. CREADORES FAVORITOS
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

            datos.CreadoresFavoritos = creadoresFavoritos
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
                .Cast<object>()
                .ToList();

            // 2. STORIES
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

            datos.Stories = storiesCreadores
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
                .Cast<object>()
                .ToList();

            // 3. COLECCIONES
            datos.Colecciones = await _context.Colecciones
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
                .Cast<object>()
                .ToListAsync();

            // 4. SUGERENCIAS DE USUARIOS
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var adminIds = adminUsers.Select(u => u.Id).ToList();

            var creadoresSugeridosQuery = _userManager.Users
                .Where(u => u.Id != usuarioId
                        && u.EstaActivo
                        && !creadoresIds.Contains(u.Id)
                        && !adminIds.Contains(u.Id));

            if (ocultarLadoB)
            {
                creadoresSugeridosQuery = creadoresSugeridosQuery
                    .Where(u => !u.CreadorVerificado || string.IsNullOrEmpty(u.Seudonimo));
            }

            datos.CreadoresSugeridos = await creadoresSugeridosQuery
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(descubrimientoUsuariosCant)
                .ToListAsync();

            // 5. USUARIOS LADOB POR INTERESES
            if (!ocultarLadoB)
            {
                datos.UsuariosLadoBPorIntereses = await ObtenerUsuariosLadoBPorInteresesAsync(
                    usuarioId,
                    usuariosBloqueadosIds,
                    creadoresIds,
                    descubrimientoUsuariosCant);
            }

            return datos;
        }

        #endregion

        #region Métodos Privados - Helpers

        private async Task<Dictionary<string, int>> ObtenerConfigFeedAsync()
        {
            if (!_cache.TryGetValue(CACHE_PREFIX_CONFIG, out Dictionary<string, int>? config))
            {
                var configuraciones = await _context.ConfiguracionesPlataforma
                    .Where(c => c.Categoria == "Feed" || c.Categoria == "Explorar")
                    .ToDictionaryAsync(c => c.Clave, c => c.Valor);

                config = new Dictionary<string, int>
                {
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOA, "30"), out var la) ? la : 30,
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOB_SUSCRIPTOS, "15"), out var lbs) ? lbs : 15,
                    [ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_LADOB_PROPIO, "10"), out var lbp) ? lbp : 10,
                    [ConfiguracionPlataforma.FEED_LIMITE_COMPRADO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_COMPRADO, "10"), out var lc) ? lc : 10,
                    [ConfiguracionPlataforma.FEED_LIMITE_TOTAL] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_LIMITE_TOTAL, "50"), out var lt) ? lt : 50,
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOA_CANTIDAD, "5"), out var dla) ? dla : 5,
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_LADOB_CANTIDAD, "5"), out var dlb) ? dlb : 5,
                    [ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_DESCUBRIMIENTO_USUARIOS_CANTIDAD, "5"), out var du) ? du : 5,
                    [ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_MAX_POSTS_CONSECUTIVOS_CREADOR, "2"), out var mc) ? mc : 2,
                    [ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_ANUNCIOS_INTERVALO, "8"), out var ai) ? ai : 8,
                    [ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.FEED_ANUNCIOS_CANTIDAD, "3"), out var ac) ? ac : 3,
                    [ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_CANTIDAD, "1"), out var pc) ? pc : 1,
                    [ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.LADOB_PREVIEW_INTERVALO, "5"), out var pi) ? pi : 5,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CREADORES] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CREADORES, "50"), out var elc) ? elc : 50,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO, "100"), out var elco) ? elco : 100,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_ZONAS_MAPA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_ZONAS_MAPA, "20"), out var elzm) ? elzm : 20,
                    [ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO_MAPA] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_LIMITE_CONTENIDO_MAPA, "30"), out var elcm) ? elcm : 30,
                    [ConfiguracionPlataforma.EXPLORAR_PAGE_SIZE] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_PAGE_SIZE, "30"), out var eps) ? eps : 30,
                    [ConfiguracionPlataforma.EXPLORAR_CONFIANZA_OBJETOS] = int.TryParse(configuraciones.GetValueOrDefault(ConfiguracionPlataforma.EXPLORAR_CONFIANZA_OBJETOS, "70"), out var eco) ? eco : 70,
                };

                _cache.Set(CACHE_PREFIX_CONFIG, config, TimeSpan.FromMinutes(5));
            }
            return config ?? new Dictionary<string, int>();
        }

        private async Task<List<string>> ObtenerUsuariosBloqueadosCacheadosAsync(string usuarioId)
        {
            var cacheKey = $"{CACHE_PREFIX_BLOQUEOS}{usuarioId}";
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

        private static int CalcularHashDeterministico(int id, int semilla)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + id;
                hash = hash * 31 + semilla;
                return hash;
            }
        }

        private double CalcularScoreDescubrimiento(Contenido contenido, Dictionary<int, decimal> interesesUsuario)
        {
            double score = 0;

            // 50% por coincidencia de intereses
            if (contenido.CategoriaInteresId.HasValue &&
                interesesUsuario.TryGetValue(contenido.CategoriaInteresId.Value, out decimal pesoInteres))
            {
                score += (double)pesoInteres * 50;
            }

            // 30% por engagement (logarítmico)
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
            else if (horasDesdePublicacion < 168)
                score += 5;

            return score;
        }

        private List<Contenido> AplicarScoringDescubrimiento(
            List<Contenido> candidatos,
            Dictionary<int, decimal> interesesUsuario,
            int cantidad,
            int semilla)
        {
            if (!candidatos.Any()) return new List<Contenido>();

            if (interesesUsuario.Any())
            {
                return candidatos
                    .Select(c => new { Contenido = c, Score = CalcularScoreDescubrimiento(c, interesesUsuario) })
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => CalcularHashDeterministico(x.Contenido.Id, semilla))
                    .Take(cantidad)
                    .Select(x => x.Contenido)
                    .ToList();
            }

            return candidatos
                .OrderByDescending(c => c.NumeroLikes + c.NumeroComentarios * 2)
                .ThenBy(c => CalcularHashDeterministico(c.Id, semilla))
                .Take(cantidad)
                .ToList();
        }

        private static List<Contenido> IntercalarPreviewLadoB(
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

                if (contadorIntervalo >= intervalo && previewQueue.Any())
                {
                    for (int i = 0; i < cantidad && previewQueue.Any(); i++)
                    {
                        resultado.Add(previewQueue.Dequeue());
                    }
                    contadorIntervalo = 0;
                }
            }

            while (previewQueue.Any())
            {
                resultado.Add(previewQueue.Dequeue());
            }

            return resultado;
        }

        private static List<Contenido> AplicarVariedadCreadores(List<Contenido> contenidos, int maxConsecutivos = 2)
        {
            if (!contenidos.Any() || maxConsecutivos <= 0) return contenidos;

            var postsPorCreador = new Dictionary<string, Queue<Contenido>>();
            var ordenCreadores = new List<string>();

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

            while (creadoresAgotados < ordenCreadores.Count && resultado.Count < contenidos.Count)
            {
                bool agregadoEnRonda = false;

                foreach (var creadorId in ordenCreadores)
                {
                    if (!postsPorCreador[creadorId].Any()) continue;

                    if (creadorId == ultimoCreadorId && consecutivos >= maxConsecutivos)
                    {
                        continue;
                    }

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

                    if (!postsPorCreador[creadorId].Any())
                    {
                        creadoresAgotados++;
                    }

                    if (consecutivos >= maxConsecutivos)
                    {
                        break;
                    }
                }

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

        private async Task<List<ApplicationUser>> ObtenerUsuariosLadoBPorInteresesAsync(
            string usuarioId,
            List<string> usuariosBloqueadosIds,
            List<string> creadoresYaSuscritos,
            int cantidad = 5)
        {
            var interesesUsuario = await _context.InteresesUsuarios
                .Where(i => i.UsuarioId == usuarioId && i.PesoInteres > 0.3m)
                .Select(i => i.CategoriaInteresId)
                .ToListAsync();

            if (!interesesUsuario.Any())
            {
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

            var usuariosConInteresesComunes = await _context.Contenidos
                .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
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

            return await _context.Users
                .Where(u => usuariosConInteresesComunes.Contains(u.Id)
                        && u.EstaActivo
                        && u.CreadorVerificado
                        && !string.IsNullOrEmpty(u.Seudonimo))
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(cantidad)
                .ToListAsync();
        }

        #endregion
    }
}
