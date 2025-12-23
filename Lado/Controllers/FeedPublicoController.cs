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

        public FeedPublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedPublicoController> logger,
            IAdService adService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _adService = adService;
        }

        // GET: /FeedPublico o /FeedPublico/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Verificar si el usuario está autenticado
                var estaAutenticado = User.Identity?.IsAuthenticated ?? false;
                ViewBag.EstaAutenticado = estaAutenticado;

                // 1. CONTENIDO PÚBLICO para el mosaico - obtener mas contenido
                // IMPORTANTE: Solo LadoA (público) - NO mostrar LadoB en feed público
                var contenidoPublico = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && !c.EsContenidoSensible  // Excluir contenido sensible del mosaico publico
                            && c.TipoLado == TipoLado.LadoA  // Solo contenido público LadoA
                            && !string.IsNullOrEmpty(c.RutaArchivo)  // Solo contenido con media
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.NumeroLikes + c.NumeroVistas)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(300)  // Obtener suficiente contenido para llenar toda la pantalla
                    .ToListAsync();

                // 2. CONTENIDO PREMIUM (LadoB) para mostrar difuminado
                var contenidoPremium = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && !c.EsPrivado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.NumeroLikes)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(10)
                    .ToListAsync();

                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();

                // 3. SUGERENCIAS DE USUARIOS (creadores populares, excluyendo admin)
                var creadoresSugeridos = await _userManager.Users
                    .Where(u => u.EstaActivo
                            && u.CreadorVerificado
                            && u.EsCreador
                            && u.UserName != "admin"
                            && !u.Email.ToLower().Contains("admin"))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(8)
                    .ToListAsync();

                // Si no hay suficientes verificados, agregar creadores activos (no admin)
                if (creadoresSugeridos.Count < 5)
                {
                    var usuariosAdicionales = await _userManager.Users
                        .Where(u => u.EstaActivo
                                && u.EsCreador
                                && u.UserName != "admin"
                                && !u.Email.ToLower().Contains("admin")
                                && !creadoresSugeridos.Select(cs => cs.Id).Contains(u.Id))
                        .OrderByDescending(u => u.NumeroSeguidores)
                        .Take(8 - creadoresSugeridos.Count)
                        .ToListAsync();

                    creadoresSugeridos.AddRange(usuariosAdicionales);
                }

                ViewBag.CreadoresSugeridos = creadoresSugeridos;

                // 4. CREADORES PREMIUM (usuarios con contenido LadoB público)
                var creadoresPremiumIds = await _context.Contenidos
                    .Where(c => c.TipoLado == TipoLado.LadoB && c.EstaActivo && !c.EsBorrador && !c.EsPrivado)
                    .Select(c => c.UsuarioId)
                    .Distinct()
                    .ToListAsync();

                var creadoresPremium = await _userManager.Users
                    .Where(u => creadoresPremiumIds.Contains(u.Id)
                            && u.EstaActivo
                            && u.UserName != "admin"
                            && !u.Email.ToLower().Contains("admin"))
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(6)
                    .ToListAsync();

                ViewBag.CreadoresPremium = creadoresPremium;

                // 5. Mezclar contenido público y premium para el feed
                var feedMezclado = new List<Contenido>();
                var indexPublico = 0;
                var indexPremium = 0;

                // Intercalar contenido: cada 3 posts públicos, mostrar 1 premium
                while (indexPublico < contenidoPublico.Count || indexPremium < contenidoPremium.Count)
                {
                    // Agregar hasta 3 posts públicos
                    for (int i = 0; i < 3 && indexPublico < contenidoPublico.Count; i++)
                    {
                        feedMezclado.Add(contenidoPublico[indexPublico]);
                        indexPublico++;
                    }

                    // Agregar 1 post premium (difuminado)
                    if (indexPremium < contenidoPremium.Count)
                    {
                        feedMezclado.Add(contenidoPremium[indexPremium]);
                        indexPremium++;
                    }
                }

                _logger.LogInformation("Feed público: {TotalPublico} públicos, {TotalPremium} premium, {TotalSugeridos} creadores sugeridos",
                    contenidoPublico.Count, contenidoPremium.Count, creadoresSugeridos.Count);

                // 6. CARGAR ANUNCIOS PUBLICITARIOS
                var usuarioId = estaAutenticado ? _userManager.GetUserId(User) : null;
                var anuncios = await _adService.ObtenerAnunciosActivos(2, usuarioId);
                ViewBag.Anuncios = anuncios;

                // Registrar impresiones de los anuncios mostrados
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                foreach (var anuncio in anuncios)
                {
                    await _adService.RegistrarImpresion(anuncio.Id, usuarioId, ipAddress);
                }

                // 7. ESTADISTICAS PARA EL MOSAICO
                // Contar creadores: LadoA (EsCreador o TipoUsuario=1) + LadoB (tiene seudónimo)
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

                // Para el mosaico, devolver todo el contenido sin mezclar
                return View(contenidoPublico);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el feed público");
                TempData["Error"] = "Error al cargar el feed. Por favor, intenta nuevamente.";
                return View(new List<Contenido>());
            }
        }

        // GET: /FeedPublico/VerPerfil/{id}
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

                // Verificar si el creador es LadoB (tiene seudónimo = contenido premium)
                var esCreadorLadoB = usuario.TieneLadoB();
                ViewBag.EsCreadorLadoB = esCreadorLadoB;

                var contenidoPublico = new List<Contenido>();
                var contenidoPremium = new List<Contenido>();

                // Si es creador LadoB y usuario NO está autenticado, NO mostrar contenido
                if (esCreadorLadoB && !estaAutenticado)
                {
                    // Creador LadoB + usuario anónimo = NO mostrar nada
                    // contenidoPublico y contenidoPremium quedan vacíos
                }
                else if (!estaAutenticado)
                {
                    // Usuario anónimo viendo creador LadoA: solo contenido marcado como público general
                    contenidoPublico = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoA
                                && c.EsPublicoGeneral)  // Solo contenido público general
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(12)
                        .ToListAsync();
                }
                else
                {
                    // Usuario autenticado: ve todo el contenido LadoA
                    contenidoPublico = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoA)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(12)
                        .ToListAsync();

                    // Contenido premium borroso
                    contenidoPremium = await _context.Contenidos
                        .Where(c => c.UsuarioId == id
                                && c.EstaActivo
                                && !c.EsBorrador
                                && !c.Censurado
                                && !c.EsPrivado
                                && c.TipoLado == TipoLado.LadoB)
                        .OrderByDescending(c => c.FechaPublicacion)
                        .Take(6)
                        .ToListAsync();
                }

                ViewBag.ContenidoPublico = contenidoPublico;
                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();

                ViewBag.TotalPublicaciones = contenidoPublico.Count + contenidoPremium.Count;
                ViewBag.TotalLikes = contenidoPublico.Sum(c => c.NumeroLikes);

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                ViewBag.EstaAutenticado = estaAutenticado;

                // ========================================
                // DETERMINAR DATOS DE PERFIL A MOSTRAR
                // ========================================
                // Regla:
                // - Creador LadoB (tiene seudónimo): mostrar perfil LadoB (seudónimo, foto premium)
                // - Creador LadoA (sin seudónimo): mostrar perfil LadoA (nombre real, foto normal)

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

                _logger.LogInformation("VerPerfil: Usuario={Id}, EsLadoB={EsLadoB}, Nombre={Nombre}",
                    id, esCreadorLadoB, (string)ViewBag.NombreMostrar);

                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar perfil público {Id}", id);
                TempData["Error"] = "Error al cargar el perfil";
                return RedirectToAction("Index");
            }
        }

        // POST: /FeedPublico/RequiereLogin
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

        // POST: /FeedPublico/RegistrarClicAnuncio
        /// <summary>
        /// Registra un clic en un anuncio y retorna la URL de destino
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RegistrarClicAnuncio(int anuncioId)
        {
            try
            {
                var anuncio = await _context.Anuncios.FindAsync(anuncioId);
                if (anuncio == null)
                {
                    return Json(new { success = false, message = "Anuncio no encontrado" });
                }

                var usuarioId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var registrado = await _adService.RegistrarClic(anuncioId, usuarioId, ipAddress);

                return Json(new
                {
                    success = true,
                    urlDestino = anuncio.UrlDestino,
                    registrado = registrado
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar clic en anuncio {AnuncioId}", anuncioId);
                return Json(new { success = false, message = "Error al procesar" });
            }
        }

        // GET: /FeedPublico/RedirectAnuncio/{id}
        /// <summary>
        /// Redirige al usuario al destino del anuncio despues de registrar el clic
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RedirectAnuncio(int id)
        {
            try
            {
                var anuncio = await _context.Anuncios.FindAsync(id);
                if (anuncio == null || string.IsNullOrEmpty(anuncio.UrlDestino))
                {
                    return RedirectToAction("Index");
                }

                var usuarioId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                // Registrar el clic
                await _adService.RegistrarClic(id, usuarioId, ipAddress);

                // Redirigir al destino
                return Redirect(anuncio.UrlDestino);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al redirigir anuncio {AnuncioId}", id);
                return RedirectToAction("Index");
            }
        }
    }
}
