using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para el Feed Público - accesible para usuarios anónimos
    /// Muestra contenido público, sugerencias de creadores y contenido premium difuminado
    /// OPTIMIZADO: Usa caché en memoria para evitar queries repetitivas
    /// </summary>
    public class FeedPublicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FeedPublicoController> _logger;
        private readonly IAdService _adService;
        private readonly IMemoryCache _cache;

        // Constantes de configuración - OPTIMIZADO v2
        private const int MaxContenidoPublico = 200; // 200 items para mosaico denso
        private const int MaxContenidoPremium = 10;
        private const int MaxCreadoresSugeridos = 8;
        private const int MaxCreadoresPremium = 6;
        private const int MaxAnuncios = 2;

        // Claves de caché
        private const string CacheKeyFeedPublico = "FeedPublico_Contenido";
        private const string CacheKeyFeedPremium = "FeedPublico_Premium";
        private const string CacheKeyCreadoresSugeridos = "FeedPublico_Creadores";
        private const string CacheKeyCreadoresPremium = "FeedPublico_CreadoresPremium";
        private const string CacheKeyEstadisticas = "FeedPublico_Stats";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public FeedPublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedPublicoController> logger,
            IAdService adService,
            IMemoryCache cache)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _adService = adService;
            _cache = cache;
        }

        #region Acciones Públicas

        /// <summary>
        /// GET: /FeedPublico - Página principal del feed público con mosaico
        /// OPTIMIZADO v2: Solo carga datos necesarios, selección aleatoria en SQL
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var estaAutenticado = User.Identity?.IsAuthenticated ?? false;

                // Cache del navegador para usuarios NO autenticados (el contenido es igual para todos)
                if (!estaAutenticado)
                {
                    Response.Headers["Cache-Control"] = "public, max-age=60"; // 1 minuto
                }
                else
                {
                    Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
                    Response.Headers["Pragma"] = "no-cache";
                    Response.Headers["Expires"] = "0";
                }

                ViewBag.EstaAutenticado = estaAutenticado;
                ConfigurarSeoIndex();

                // 1. CONTENIDO PÚBLICO con selección aleatoria - desde caché o BD
                var contenidoPublico = await ObtenerContenidoPublicoCacheadoAsync();

                // 2. ESTADÍSTICAS - desde caché o BD (para animación)
                var stats = await ObtenerEstadisticasCacheadasAsync();
                ViewBag.TotalCreadores = stats.TotalCreadores;
                ViewBag.TotalUsuarios = stats.TotalUsuarios;
                ViewBag.TotalContenido = stats.TotalContenido;

                // NOTA: Premium, creadores y anuncios NO se usan en la vista Index (mosaico)
                // Se mantienen los métodos para otros usos pero no se llaman aquí

                return View(contenidoPublico);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el feed público");
                TempData["Error"] = "Error al cargar el feed. Por favor, intenta nuevamente.";
                return View(new List<Contenido>());
            }
        }

        /// <summary>
        /// Obtiene contenido público desde caché o BD con SELECCIÓN ALEATORIA
        /// OPTIMIZADO v2: Usa Guid.NewGuid() que se traduce a NEWID() en SQL Server
        /// </summary>
        private async Task<List<Contenido>> ObtenerContenidoPublicoCacheadoAsync()
        {
            // Solo usar caché si tiene contenido (no cachear listas vacías)
            if (_cache.TryGetValue(CacheKeyFeedPublico, out List<Contenido>? cached) && cached != null && cached.Any())
            {
                return cached;
            }

            // Query con selección ALEATORIA en SQL Server (NEWID())
            // Prioriza contenido con thumbnail para carga más rápida
            // IMPORTANTE: EsPublicoGeneral filtra contenido marcado como "Solo para seguidores"
            var contenido = await _context.Contenidos
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
                        && !c.OcultoSilenciosamente
                        && !c.EsPrivado
                        && !c.EsContenidoSensible
                        && c.EsPublicoGeneral  // Solo contenido visible en feed público (no "Solo para seguidores")
                        && c.TipoLado == TipoLado.LadoA
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && c.Usuario != null
                        && c.Usuario.EstaActivo
                        && !c.Usuario.OcultarDeFeedPublico)
                .OrderByDescending(c => !string.IsNullOrEmpty(c.Thumbnail)) // Priorizar con thumbnail
                .ThenBy(c => Guid.NewGuid()) // Aleatorio en SQL Server (NEWID())
                .Take(MaxContenidoPublico)
                .ToListAsync();

            // Fallback 1: sin filtros de contenido sensible/thumbnail, pero con TODOS los filtros de seguridad
            if (!contenido.Any())
            {
                _logger.LogWarning("FeedPublico: Query principal vacía, usando fallback 1 (sin filtro sensible/EsPublicoGeneral)");
                contenido = await _context.Contenidos
                    .AsNoTracking()
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA
                            && !string.IsNullOrEmpty(c.RutaArchivo)
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(100)
                    .ToListAsync();
            }

            // Fallback 2: sin filtro OcultarDeFeedPublico del usuario, pero con TODOS los filtros de seguridad del contenido
            if (!contenido.Any())
            {
                _logger.LogWarning("FeedPublico: Fallback 1 vacío, usando fallback 2 (sin filtro OcultarDeFeedPublico)");
                contenido = await _context.Contenidos
                    .AsNoTracking()
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoA
                            && !string.IsNullOrEmpty(c.RutaArchivo)
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(50)
                    .ToListAsync();
            }

            // Solo cachear si hay contenido
            if (contenido.Any())
            {
                _cache.Set(CacheKeyFeedPublico, contenido, CacheDuration);
                _logger.LogInformation("FeedPublico: Cacheados {Count} contenidos públicos", contenido.Count);
            }
            else
            {
                _logger.LogWarning("FeedPublico: No hay contenido para mostrar en ningún fallback");
            }

            return contenido;
        }

        /// <summary>
        /// Obtiene contenido premium desde caché o BD
        /// </summary>
        private async Task<List<Contenido>> ObtenerContenidoPremiumCacheadoAsync()
        {
            if (_cache.TryGetValue(CacheKeyFeedPremium, out List<Contenido>? cached) && cached != null)
            {
                return cached;
            }

            var contenido = await _context.Contenidos
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Where(c => c.EstaActivo
                        && !c.EsBorrador
                        && !c.Censurado
                        && !c.OcultoSilenciosamente
                        && !c.EsPrivado
                        && c.TipoLado == TipoLado.LadoB
                        && !string.IsNullOrEmpty(c.RutaArchivo)
                        && c.Usuario != null
                        && c.Usuario.EstaActivo
                        && !c.Usuario.OcultarDeFeedPublico)
                .OrderByDescending(c => c.NumeroLikes)
                .ThenByDescending(c => c.FechaPublicacion)
                .Take(MaxContenidoPremium)
                .ToListAsync();

            _cache.Set(CacheKeyFeedPremium, contenido, CacheDuration);
            return contenido;
        }

        /// <summary>
        /// Obtiene creadores sugeridos desde caché o BD
        /// </summary>
        private async Task<List<ApplicationUser>> ObtenerCreadoresSugeridosCacheadosAsync()
        {
            if (_cache.TryGetValue(CacheKeyCreadoresSugeridos, out List<ApplicationUser>? cached) && cached != null)
            {
                return cached;
            }

            var creadores = await _userManager.Users
                .AsNoTracking()
                .Where(u => u.EstaActivo
                        && u.EsCreador
                        && u.UserName != "admin"
                        && !u.Email.ToLower().Contains("admin")
                        && !u.OcultarDeFeedPublico)
                .OrderByDescending(u => u.CreadorVerificado)
                .ThenByDescending(u => u.NumeroSeguidores)
                .Take(MaxCreadoresSugeridos)
                .ToListAsync();

            _cache.Set(CacheKeyCreadoresSugeridos, creadores, CacheDuration);
            return creadores;
        }

        /// <summary>
        /// Obtiene creadores premium desde caché o BD
        /// </summary>
        private async Task<List<ApplicationUser>> ObtenerCreadoresPremiumCacheadosAsync()
        {
            if (_cache.TryGetValue(CacheKeyCreadoresPremium, out List<ApplicationUser>? cached) && cached != null)
            {
                return cached;
            }

            var creadoresPremiumIds = await _context.Contenidos
                .AsNoTracking()
                .Where(c => c.TipoLado == TipoLado.LadoB
                        && c.EstaActivo
                        && !c.EsBorrador
                        && !c.EsPrivado
                        && !c.Censurado
                        && !c.OcultoSilenciosamente)
                .Select(c => c.UsuarioId)
                .Distinct()
                .ToListAsync();

            var creadores = await _userManager.Users
                .AsNoTracking()
                .Where(u => creadoresPremiumIds.Contains(u.Id)
                        && u.EstaActivo
                        && u.UserName != "admin"
                        && !u.Email.ToLower().Contains("admin")
                        && !u.OcultarDeFeedPublico)
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(MaxCreadoresPremium)
                .ToListAsync();

            _cache.Set(CacheKeyCreadoresPremium, creadores, CacheDuration);
            return creadores;
        }

        /// <summary>
        /// Obtiene estadísticas desde caché o BD
        /// </summary>
        private async Task<(int TotalCreadores, int TotalUsuarios, int TotalContenido)> ObtenerEstadisticasCacheadasAsync()
        {
            if (_cache.TryGetValue(CacheKeyEstadisticas, out (int, int, int) cached))
            {
                return cached;
            }

            // Una sola query con proyección en lugar de 3 queries COUNT
            var totalCreadores = await _userManager.Users
                .CountAsync(u => u.EstaActivo && (u.EsCreador || u.TipoUsuario == 1));
            var totalUsuarios = await _userManager.Users
                .CountAsync(u => u.EstaActivo);
            var totalContenido = await _context.Contenidos
                .CountAsync(c => c.EstaActivo && !c.EsBorrador);

            var stats = (totalCreadores, totalUsuarios, totalContenido);
            _cache.Set(CacheKeyEstadisticas, stats, CacheDuration);
            return stats;
        }

        /// <summary>
        /// GET: /FeedPublico/VerPerfil/{id} - Ver perfil público de un creador
        /// ladoB: true = mostrar perfil LadoB (seudónimo), false = mostrar perfil LadoA (username)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VerPerfil(string id, bool ladoB = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    TempData["Error"] = "Usuario no especificado";
                    return RedirectToAction("Index");
                }

                ApplicationUser? usuario;

                if (ladoB)
                {
                    // ⭐ SEGURIDAD: Para LadoB, buscar por seudónimo (NO por user ID real)
                    // Esto evita que alguien descubra el seudónimo conociendo el user ID
                    usuario = await _context.Users.FirstOrDefaultAsync(u =>
                        u.EstaActivo &&
                        u.Seudonimo != null &&
                        u.Seudonimo.ToLower() == id.ToLower());
                }
                else
                {
                    // Para LadoA, buscar por ID normalmente
                    usuario = await _userManager.FindByIdAsync(id);
                }

                if (usuario == null || !usuario.EstaActivo)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Index");
                }

                // Usar el ID real del usuario para las consultas internas
                var usuarioId = usuario.Id;

                var estaAutenticado = User.Identity?.IsAuthenticated ?? false;

                // Si el perfil es privado y el usuario no está autenticado → redirigir al login
                if (usuario.PerfilPrivado && !estaAutenticado)
                {
                    TempData["Info"] = "Este perfil es privado. Inicia sesión para verlo.";
                    return RedirectToAction("Login", "Account", new { returnUrl = $"/@{usuario.UserName}" });
                }

                var tieneLadoB = usuario.TieneLadoB();

                // Determinar qué perfil mostrar:
                // - Si ladoB=true Y el usuario tiene LadoB activo → mostrar perfil LadoB
                // - En cualquier otro caso → mostrar perfil LadoA
                var mostrarPerfilLadoB = ladoB && tieneLadoB;

                ViewBag.EsCreadorLadoB = mostrarPerfilLadoB;
                // ⭐ SEGURIDAD: No exponer si el usuario tiene LadoB cuando se ve desde LadoA
                ViewBag.TieneLadoB = mostrarPerfilLadoB;

                var contenidoPublico = new List<Contenido>();
                var contenidoPremium = new List<Contenido>();

                // LÓGICA DE CONTENIDO:
                // - Si mostrarPerfilLadoB: SOLO mostrar contenido LadoB (bloqueado/difuminado)
                // - Si mostrar LadoA: mostrar contenido LadoA público

                if (mostrarPerfilLadoB)
                {
                    // Perfil LadoB: SOLO mostrar contenido LadoB (bloqueado para no suscriptores)
                    // NO mezclar con contenido LadoA para proteger privacidad
                    contenidoPremium = await _context.Contenidos
                        .Where(c => c.UsuarioId == usuarioId
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.OcultoSilenciosamente
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoB)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(18)
                        .ToListAsync();

                    // NO cargar contenido LadoA en perfil LadoB
                    contenidoPublico = new List<Contenido>();
                }
                else
                {
                    // Perfil LadoA: mostrar TODO el contenido LadoA (autenticado o no)
                    // El filtro EsPublicoGeneral solo aplica al feed de descubrimiento, NO al perfil
                    // Así "Solo para seguidores" significa: visible en perfil pero no en exploración
                    contenidoPublico = await _context.Contenidos
                        .Where(c => c.UsuarioId == usuarioId
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.OcultoSilenciosamente
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoA)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(50)  // Aumentado de 18 a 50 para mostrar más contenido
                        .ToListAsync();
                }

                ViewBag.ContenidoPublico = contenidoPublico;
                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();
                ViewBag.TotalPublicaciones = contenidoPublico.Count + contenidoPremium.Count;
                ViewBag.TotalLikes = contenidoPublico.Sum(c => c.NumeroLikes) + contenidoPremium.Sum(c => c.NumeroLikes);

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == usuarioId && s.EstaActiva);

                ViewBag.EstaAutenticado = estaAutenticado;

                // Datos adicionales para perfil LadoB
                if (mostrarPerfilLadoB)
                {
                    ViewBag.PrecioSuscripcion = usuario.PrecioSuscripcionLadoB ?? 0;

                    // Contar total de contenido por tipo
                    var conteoTipos = await _context.Contenidos
                        .Where(c => c.UsuarioId == usuarioId &&
                                    c.TipoLado == TipoLado.LadoB &&
                                    c.EstaActivo &&
                                    !c.Censurado &&
                                    !c.OcultoSilenciosamente)
                        .GroupBy(c => c.TipoContenido)
                        .Select(g => new { Tipo = g.Key, Cantidad = g.Count() })
                        .ToListAsync();

                    ViewBag.TotalFotos = conteoTipos.FirstOrDefault(x => x.Tipo == TipoContenido.Foto)?.Cantidad ?? 0;
                    ViewBag.TotalVideos = conteoTipos.FirstOrDefault(x => x.Tipo == TipoContenido.Video)?.Cantidad ?? 0;
                    ViewBag.TotalContenidosLadoB = conteoTipos.Sum(x => x.Cantidad);
                }
                else
                {
                    ViewBag.PrecioSuscripcion = 0m;
                    ViewBag.TotalFotos = 0;
                    ViewBag.TotalVideos = 0;
                    ViewBag.TotalContenidosLadoB = 0;
                }

                // Determinar datos de perfil a mostrar (nombre, foto, bio según lado)
                ConfigurarDatosPerfilViewBag(usuario, mostrarPerfilLadoB);

                _logger.LogInformation("VerPerfil: Identificador={Id}, MostrandoLadoB={LadoB}", id, mostrarPerfilLadoB);

                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil público {Id}", id);
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: /FeedPublico/RequiereLogin - Indica que se requiere autenticación
        /// </summary>
        [HttpPost]
        public IActionResult RequiereLogin(string accion)
        {
            return Json(new
            {
                success = false,
                requireLogin = true,
                message = $"Para {accion} necesitas crear una cuenta o iniciar sesión",
                loginUrl = Url.Action("Login", "Account"),
                registerUrl = Url.Action("Register", "Account")
            });
        }

        /// <summary>
        /// POST: /FeedPublico/RegistrarClicAnuncio - Registra clic en anuncio
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarClicAnuncio(int anuncioId)
        {
            try
            {
                var anuncio = await _context.Anuncios.FindAsync(anuncioId);
                if (anuncio == null)
                    return Json(new { success = false, message = "Anuncio no encontrado" });

                var usuarioId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var registrado = await _adService.RegistrarClic(anuncioId, usuarioId, ipAddress);

                return Json(new
                {
                    success = true,
                    urlDestino = anuncio.UrlDestino,
                    registrado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar clic en anuncio {AnuncioId}", anuncioId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        /// <summary>
        /// GET: /FeedPublico/RedirectAnuncio/{id} - Redirige al destino del anuncio
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RedirectAnuncio(int id)
        {
            try
            {
                var anuncio = await _context.Anuncios.FindAsync(id);
                if (anuncio == null || string.IsNullOrEmpty(anuncio.UrlDestino))
                    return RedirectToAction("Index");

                var usuarioId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Registrar el clic
                await _adService.RegistrarClic(id, usuarioId, ipAddress);

                // Validar URL antes de redirigir (prevenir Open Redirect)
                if (!Uri.TryCreate(anuncio.UrlDestino, UriKind.Absolute, out var uri))
                {
                    _logger.LogWarning("URL de anuncio inválida: {Url}", anuncio.UrlDestino);
                    return RedirectToAction("Index");
                }

                // Solo permitir HTTP/HTTPS
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    _logger.LogWarning("Esquema de URL no permitido: {Scheme}", uri.Scheme);
                    return RedirectToAction("Index");
                }

                return Redirect(uri.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al redirigir anuncio {AnuncioId}", id);
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// GET: /FeedPublico/LimpiarCache - Limpia la caché del feed público
        /// Solo accesible por administradores
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult LimpiarCache()
        {
            try
            {
                _cache.Remove(CacheKeyFeedPublico);
                _cache.Remove(CacheKeyFeedPremium);
                _cache.Remove(CacheKeyCreadoresSugeridos);
                _cache.Remove(CacheKeyCreadoresPremium);
                _cache.Remove(CacheKeyEstadisticas);

                _logger.LogInformation("FeedPublico: Caché limpiada manualmente");

                TempData["Success"] = "Caché del feed público limpiada correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché");
                TempData["Error"] = "Error al limpiar caché";
                return RedirectToAction("Index");
            }
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Configura las meta tags SEO para la página Index
        /// </summary>
        private void ConfigurarSeoIndex()
        {
            ViewData["Title"] = "Explorar Contenido Exclusivo";
            ViewData["MetaDescription"] = "Descubre creadores verificados y contenido exclusivo en Lado. Explora fotos, videos y conecta con tus creadores favoritos.";
            ViewData["MetaKeywords"] = "explorar contenido, creadores, fotos exclusivas, videos, lado, red social";
            ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/FeedPublico";
            ViewData["OgTitle"] = "Explorar Contenido - Lado";
            ViewData["OgDescription"] = "Descubre creadores verificados y contenido exclusivo en Lado.";
            ViewData["OgType"] = "website";
            ViewData["SchemaType"] = "CollectionPage";
        }

        /// <summary>
        /// Configura los datos de perfil en ViewBag según el tipo de creador
        /// </summary>
        private void ConfigurarDatosPerfilViewBag(ApplicationUser usuario, bool esCreadorLadoB)
        {
            if (esCreadorLadoB)
            {
                // Creador LadoB: mostrar seudónimo y datos premium
                ViewBag.NombreMostrar = usuario.Seudonimo ?? usuario.NombreCompleto ?? usuario.UserName ?? "Usuario";
                ViewBag.FotoMostrar = usuario.FotoPerfilLadoB ?? usuario.FotoPerfil ?? "";
                ViewBag.BioMostrar = usuario.BiografiaLadoB ?? usuario.Biografia ?? "";
                ViewBag.UsernameMostrar = usuario.Seudonimo ?? usuario.UserName ?? "";
            }
            else
            {
                // Creador LadoA: mostrar nombre real y datos normales
                ViewBag.NombreMostrar = usuario.NombreCompleto ?? usuario.UserName ?? "Usuario";
                ViewBag.FotoMostrar = usuario.FotoPerfil ?? "";
                ViewBag.BioMostrar = usuario.Biografia ?? "";
                ViewBag.UsernameMostrar = usuario.UserName ?? "";
            }
        }

        #endregion

        #region Explorar por Categoría

        /// <summary>
        /// GET: /Explorar/Categoria/{slug} - Ver contenido por categoría (SEO)
        /// </summary>
        [Route("Explorar/Categoria/{slug}")]
        [Route("Categoria/{slug}")]
        public async Task<IActionResult> Categoria(string slug, int pagina = 1)
        {
            if (string.IsNullOrEmpty(slug))
                return RedirectToAction(nameof(Index));

            // Buscar categoría
            var categoria = await _context.CategoriasIntereses
                .FirstOrDefaultAsync(c => c.Slug == slug ||
                    c.Nombre.ToLower().Replace(" ", "-") == slug.ToLower());

            if (categoria == null)
                return NotFound();

            var pageSize = 24;

            // Obtener contenido público de esta categoría
            // IMPORTANTE: EsPublicoGeneral filtra contenido "Solo para seguidores"
            var query = _context.Contenidos
                .Include(c => c.Usuario)
                .Where(c => c.CategoriaInteresId == categoria.Id &&
                            c.TipoLado == TipoLado.LadoA &&
                            c.EsPublicoGeneral &&  // Solo contenido visible en exploración
                            c.EstaActivo &&
                            !c.EsBorrador &&
                            !c.Censurado &&
                            !c.OcultoSilenciosamente &&
                            !c.EsPrivado &&
                            c.Usuario != null &&
                            c.Usuario.EstaActivo);

            var totalContenidos = await query.CountAsync();
            var totalPaginas = (int)Math.Ceiling(totalContenidos / (double)pageSize);

            var contenidos = await query
                .OrderByDescending(c => c.FechaPublicacion)
                .Skip((pagina - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Creadores destacados de esta categoría
            var creadoresDestacados = await _context.Users
                .Where(u => u.EstaActivo &&
                            u.EsCreador &&
                            u.CreadorVerificado &&
                            _context.Contenidos.Any(c => c.UsuarioId == u.Id &&
                                                         c.CategoriaInteresId == categoria.Id &&
                                                         c.TipoLado == TipoLado.LadoA))
                .OrderByDescending(u => u.NumeroSeguidores)
                .Take(6)
                .ToListAsync();

            ViewBag.Categoria = categoria;
            ViewBag.Contenidos = contenidos;
            ViewBag.CreadoresDestacados = creadoresDestacados;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalContenidos = totalContenidos;

            // SEO
            ViewData["Title"] = $"{categoria.Nombre} - Explorar Lado";
            ViewData["MetaDescription"] = !string.IsNullOrEmpty(categoria.Descripcion)
                ? categoria.Descripcion
                : $"Explora contenido de {categoria.Nombre} en Lado. Descubre creadores y publicaciones.";
            ViewData["CanonicalUrl"] = $"https://ladoapp.com/Explorar/Categoria/{slug}";
            ViewData["SchemaType"] = "CollectionPage";

            return View("Categoria");
        }

        /// <summary>
        /// GET: /Explorar/Categorias - Lista de todas las categorías
        /// </summary>
        [Route("Explorar/Categorias")]
        [Route("Categorias")]
        public async Task<IActionResult> Categorias()
        {
            var categorias = await _context.CategoriasIntereses
                .Where(c => c.CategoriaPadreId == null) // Solo categorías principales
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            // Contar contenido por categoría (solo contenido visible en exploración)
            var conteoCategoria = await _context.Contenidos
                .Where(c => c.CategoriaInteresId != null &&
                            c.TipoLado == TipoLado.LadoA &&
                            c.EsPublicoGeneral &&  // Solo contenido visible en exploración
                            c.EstaActivo &&
                            !c.EsBorrador &&
                            !c.Censurado &&
                            !c.OcultoSilenciosamente &&
                            !c.EsPrivado)
                .GroupBy(c => c.CategoriaInteresId)
                .Select(g => new { CategoriaId = g.Key, Cantidad = g.Count() })
                .ToDictionaryAsync(x => x.CategoriaId ?? 0, x => x.Cantidad);

            ViewBag.Categorias = categorias;
            ViewBag.ConteoCategoria = conteoCategoria;

            // SEO
            ViewData["Title"] = "Explorar Categorías - Lado";
            ViewData["MetaDescription"] = "Explora contenido por categorías en Lado. Encuentra creadores de fotografía, fitness, arte, música y más.";

            return View("Categorias");
        }

        #endregion
    }
}
