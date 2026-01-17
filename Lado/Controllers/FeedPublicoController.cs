using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para el Feed Público - accesible para usuarios anónimos
    /// Muestra contenido público, sugerencias de creadores y contenido premium difuminado
    /// </summary>
    public class FeedPublicoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FeedPublicoController> _logger;
        private readonly IAdService _adService;
        private readonly IMediaIntegrityService _mediaIntegrity;

        // Constantes de configuración
        private const int MaxContenidoPublico = 300;
        private const int MaxContenidoPremium = 10;
        private const int MaxCreadoresSugeridos = 8;
        private const int MaxCreadoresPremium = 6;
        private const int MaxAnuncios = 2;
        private const long MaxTamanoVideoBytes = 20 * 1024 * 1024; // 20MB

        public FeedPublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedPublicoController> logger,
            IAdService adService,
            IMediaIntegrityService mediaIntegrity)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _adService = adService;
            _mediaIntegrity = mediaIntegrity;
        }

        #region Acciones Públicas

        /// <summary>
        /// GET: /FeedPublico - Página principal del feed público con mosaico
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var estaAutenticado = User.Identity?.IsAuthenticated ?? false;
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var userName = User?.Identity?.Name;

                // ⚠️ CRÍTICO: Desactivar cache del navegador para usuarios autenticados
                // Esto previene que el bfcache muestre datos del usuario anterior
                if (estaAutenticado)
                {
                    Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
                    Response.Headers["Pragma"] = "no-cache";
                    Response.Headers["Expires"] = "0";
                }

                // DEBUG: Log de autenticación al entrar al FeedPublico
                _logger.LogWarning("🔐 FEEDPUBLICO INDEX: EstaAutenticado={EstaAuth}, UserId={UserId}, UserName={UserName}",
                    estaAutenticado, userId ?? "NULL", userName ?? "NULL");

                ViewBag.EstaAutenticado = estaAutenticado;

                // Configurar SEO
                ConfigurarSeoIndex();

                // Obtener IDs de usuarios que quieren ocultar su contenido del feed público
                var usuariosOcultos = await _userManager.Users
                    .Where(u => u.OcultarDeFeedPublico)
                    .Select(u => u.Id)
                    .ToListAsync();

                // 1. CONTENIDO PÚBLICO para el mosaico
                // IMPORTANTE: Solo LadoA (público) - NO mostrar LadoB en feed público
                var contenidoPublicoRaw = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && !c.EsContenidoSensible
                            && c.TipoLado == TipoLado.LadoA
                            && !string.IsNullOrEmpty(c.RutaArchivo)
                            && c.Usuario != null
                            && c.Usuario.EstaActivo
                            && !usuariosOcultos.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroVistas)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(400)
                    .ToListAsync();

                _logger.LogInformation("FeedPublico: Query BD retornó {Count} contenidos raw", contenidoPublicoRaw.Count);

                // Filtrar contenido: excluir archivos faltantes Y videos muy grandes (>20MB)
                List<Contenido> contenidoPublico;
                try
                {
                    contenidoPublico = _mediaIntegrity
                        .FiltrarContenidoParaFeedPublico(contenidoPublicoRaw, MaxTamanoVideoBytes)
                        .Take(MaxContenidoPublico)
                        .ToList();
                    _logger.LogInformation("FeedPublico: Después de filtrar: {Count} contenidos válidos", contenidoPublico.Count);
                }
                catch (Exception exFiltro)
                {
                    _logger.LogError(exFiltro, "FeedPublico: Error en FiltrarContenidoParaFeedPublico, usando contenido sin filtrar");
                    contenidoPublico = contenidoPublicoRaw.Take(MaxContenidoPublico).ToList();
                }

                // FALLBACK: Si no hay contenido, intentar query más simple
                if (!contenidoPublico.Any())
                {
                    _logger.LogWarning("FeedPublico: No hay contenido con filtros normales, intentando fallback");
                    contenidoPublico = await _context.Contenidos
                        .Include(c => c.Usuario)
                        .Where(c => c.EstaActivo
                                && !c.EsBorrador
                                && !string.IsNullOrEmpty(c.RutaArchivo)
                                && c.Usuario != null
                                && c.Usuario.EstaActivo)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(100)
                        .ToListAsync();
                    _logger.LogInformation("FeedPublico: Fallback retornó {Count} contenidos", contenidoPublico.Count);
                }

                // 2. CONTENIDO PREMIUM (LadoB) para mostrar difuminado
                var contenidoPremiumRaw = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.OcultoSilenciosamente // Shadow hide
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.Usuario.EstaActivo
                            && !usuariosOcultos.Contains(c.UsuarioId))
                    .OrderByDescending(c => c.NumeroLikes)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(20)
                    .ToListAsync();

                var contenidoPremium = _mediaIntegrity.FiltrarContenidoValido(contenidoPremiumRaw)
                    .Take(MaxContenidoPremium)
                    .ToList();

                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();

                // 3. SUGERENCIAS DE USUARIOS (creadores populares)
                var creadoresSugeridos = await _userManager.Users
                    .Where(u => u.EstaActivo
                            && u.CreadorVerificado
                            && u.EsCreador
                            && u.UserName != "admin"
                            && !u.Email.ToLower().Contains("admin")
                            && !usuariosOcultos.Contains(u.Id))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(MaxCreadoresSugeridos)
                    .ToListAsync();

                // Si no hay suficientes verificados, agregar creadores activos
                if (creadoresSugeridos.Count < 5)
                {
                    var usuariosAdicionales = await _userManager.Users
                        .Where(u => u.EstaActivo
                                && u.EsCreador
                                && u.UserName != "admin"
                                && !u.Email.ToLower().Contains("admin")
                                && !usuariosOcultos.Contains(u.Id)
                                && !creadoresSugeridos.Select(cs => cs.Id).Contains(u.Id))
                        .OrderByDescending(u => u.NumeroSeguidores)
                        .Take(MaxCreadoresSugeridos - creadoresSugeridos.Count)
                        .ToListAsync();

                    creadoresSugeridos.AddRange(usuariosAdicionales);
                }

                ViewBag.CreadoresSugeridos = creadoresSugeridos;

                // 4. CREADORES PREMIUM (usuarios con contenido LadoB)
                var creadoresPremiumIds = await _context.Contenidos
                    .Where(c => c.TipoLado == TipoLado.LadoB && c.EstaActivo && !c.EsBorrador && !c.EsPrivado
                            && !usuariosOcultos.Contains(c.UsuarioId))
                    .Select(c => c.UsuarioId)
                    .Distinct()
                    .ToListAsync();

                var creadoresPremium = await _userManager.Users
                    .Where(u => creadoresPremiumIds.Contains(u.Id)
                            && u.EstaActivo
                            && u.UserName != "admin"
                            && !u.Email.ToLower().Contains("admin")
                            && !usuariosOcultos.Contains(u.Id))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(MaxCreadoresPremium)
                    .ToListAsync();

                ViewBag.CreadoresPremium = creadoresPremium;

                // 5. CARGAR ANUNCIOS PUBLICITARIOS
                var usuarioId = estaAutenticado ? _userManager.GetUserId(User) : null;
                var anuncios = await _adService.ObtenerAnunciosActivos(MaxAnuncios, usuarioId);
                ViewBag.Anuncios = anuncios;

                // Registrar impresiones de los anuncios mostrados
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                foreach (var anuncio in anuncios)
                {
                    await _adService.RegistrarImpresion(anuncio.Id, usuarioId, ipAddress);
                }

                // 6. ESTADÍSTICAS PARA EL MOSAICO
                var totalCreadores = await _userManager.Users
                    .CountAsync(u => u.EstaActivo &&
                        (u.EsCreador || u.TipoUsuario == 1 || !string.IsNullOrEmpty(u.Seudonimo)));
                var totalUsuarios = await _userManager.Users
                    .CountAsync(u => u.EstaActivo);
                var totalContenido = await _context.Contenidos
                    .CountAsync(c => c.EstaActivo && !c.EsBorrador);

                ViewBag.TotalCreadores = totalCreadores;
                ViewBag.TotalUsuarios = totalUsuarios;
                ViewBag.TotalContenido = totalContenido;

                _logger.LogInformation("FeedPublico: {TotalPublico} públicos, {TotalPremium} premium, {TotalSugeridos} sugeridos",
                    contenidoPublico.Count, contenidoPremium.Count, creadoresSugeridos.Count);

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
        /// GET: /FeedPublico/VerPerfil/{id} - Ver perfil público de un creador
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VerPerfil(string id)
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

                var estaAutenticado = User.Identity?.IsAuthenticated ?? false;
                var esCreadorLadoB = usuario.TieneLadoB();

                ViewBag.EsCreadorLadoB = esCreadorLadoB;

                var contenidoPublico = new List<Contenido>();
                var contenidoPremium = new List<Contenido>();

                // LÓGICA DE CONTENIDO:
                // - Creador LadoB: SOLO mostrar contenido LadoB (bloqueado/difuminado)
                // - Creador LadoA: mostrar contenido LadoA público

                if (esCreadorLadoB)
                {
                    // Creador LadoB: SOLO mostrar contenido LadoB (bloqueado para no suscriptores)
                    // NO mezclar con contenido LadoA
                    contenidoPremium = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.OcultoSilenciosamente
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoB)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(18)
                        .ToListAsync();

                    // NO cargar contenido LadoA para creadores LadoB
                    contenidoPublico = new List<Contenido>();
                }
                else if (!estaAutenticado)
                {
                    // Usuario anónimo viendo creador LadoA: solo contenido público general
                    contenidoPublico = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.OcultoSilenciosamente
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoA
                                && c.EsPublicoGeneral)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(18)
                        .ToListAsync();
                }
                else
                {
                    // Usuario autenticado viendo creador LadoA: todo el contenido LadoA
                    contenidoPublico = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.OcultoSilenciosamente
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoA)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(18)
                        .ToListAsync();
                }

                ViewBag.ContenidoPublico = contenidoPublico;
                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();
                ViewBag.TotalPublicaciones = contenidoPublico.Count + contenidoPremium.Count;
                ViewBag.TotalLikes = contenidoPublico.Sum(c => c.NumeroLikes) + contenidoPremium.Sum(c => c.NumeroLikes);

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                ViewBag.EstaAutenticado = estaAutenticado;

                // Datos adicionales para creadores LadoB
                if (esCreadorLadoB)
                {
                    ViewBag.PrecioSuscripcion = usuario.PrecioSuscripcionLadoB ?? 0;

                    // Contar total de contenido por tipo
                    var conteoTipos = await _context.Contenidos
                        .Where(c => c.UsuarioId == id &&
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

                // Determinar datos de perfil a mostrar
                ConfigurarDatosPerfilViewBag(usuario, esCreadorLadoB);

                _logger.LogInformation("VerPerfil: Usuario={Id}, EsLadoB={EsLadoB}", id, esCreadorLadoB);

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
    }
}
