using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

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

        public FeedPublicoController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<FeedPublicoController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
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

                // 1. CONTENIDO PÚBLICO LadoA - mostrar solo contenido marcado como EsPublicoGeneral
                var contenidoPublico = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoA
                            && c.EsPublicoGeneral  // Solo contenido marcado como público general
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(20)
                    .ToListAsync();

                // 2. CONTENIDO PREMIUM (LadoB) para mostrar difuminado
                var contenidoPremium = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB
                            && c.Usuario != null
                            && c.Usuario.EstaActivo)
                    .OrderByDescending(c => c.NumeroLikes)
                    .ThenByDescending(c => c.FechaPublicacion)
                    .Take(10)
                    .ToListAsync();

                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();

                // 3. SUGERENCIAS DE USUARIOS (creadores populares)
                var creadoresSugeridos = await _userManager.Users
                    .Where(u => u.EstaActivo && u.CreadorVerificado)
                    .OrderByDescending(u => u.NumeroSeguidores)
                    .Take(8)
                    .ToListAsync();

                // Si no hay suficientes verificados, agregar usuarios activos
                if (creadoresSugeridos.Count < 5)
                {
                    var usuariosAdicionales = await _userManager.Users
                        .Where(u => u.EstaActivo
                                && !creadoresSugeridos.Select(cs => cs.Id).Contains(u.Id))
                        .OrderByDescending(u => u.NumeroSeguidores)
                        .Take(8 - creadoresSugeridos.Count)
                        .ToListAsync();

                    creadoresSugeridos.AddRange(usuariosAdicionales);
                }

                ViewBag.CreadoresSugeridos = creadoresSugeridos;

                // 4. CREADORES PREMIUM (usuarios con contenido LadoB)
                var creadoresPremiumIds = await _context.Contenidos
                    .Where(c => c.TipoLado == TipoLado.LadoB && c.EstaActivo && !c.EsBorrador)
                    .Select(c => c.UsuarioId)
                    .Distinct()
                    .ToListAsync();

                var creadoresPremium = await _userManager.Users
                    .Where(u => creadoresPremiumIds.Contains(u.Id) && u.EstaActivo)
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

                return View(feedMezclado);
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

                // Contenido público del usuario
                var contenidoPublico = await _context.Contenidos
                    .Where(c => c.UsuarioId == id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoA)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(12)
                    .ToListAsync();

                // Contenido premium del usuario (para mostrar difuminado)
                var contenidoPremium = await _context.Contenidos
                    .Where(c => c.UsuarioId == id
                            && c.EstaActivo
                            && !c.EsBorrador
                            && !c.Censurado
                            && c.TipoLado == TipoLado.LadoB)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(6)
                    .ToListAsync();

                ViewBag.ContenidoPublico = contenidoPublico;
                ViewBag.ContenidoPremium = contenidoPremium;
                ViewBag.ContenidoPremiumIds = contenidoPremium.Select(c => c.Id).ToList();

                ViewBag.TotalPublicaciones = contenidoPublico.Count + contenidoPremium.Count;
                ViewBag.TotalLikes = contenidoPublico.Sum(c => c.NumeroLikes);

                ViewBag.NumeroSuscriptores = await _context.Suscripciones
                    .CountAsync(s => s.CreadorId == id && s.EstaActiva);

                ViewBag.EstaAutenticado = User.Identity?.IsAuthenticated ?? false;

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
    }
}
