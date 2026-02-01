using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lado.Controllers
{
    [Authorize]
    public class SubastasController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISubastasService _subastasService;
        private readonly IRateLimitService _rateLimitService;
        private readonly ILogger<SubastasController> _logger;

        public SubastasController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISubastasService subastasService,
            IRateLimitService rateLimitService,
            ILogger<SubastasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _subastasService = subastasService;
            _rateLimitService = rateLimitService;
            _logger = logger;
        }

        // ========================================
        // LISTAR SUBASTAS ACTIVAS
        // ========================================
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(int pagina = 1)
        {
            const int porPagina = 12;

            var subastas = await _subastasService.ObtenerSubastasActivasAsync(pagina, porPagina);
            var totalSubastas = await _subastasService.ContarSubastasActivasAsync();

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = (int)Math.Ceiling((double)totalSubastas / porPagina);
            ViewBag.TotalSubastas = totalSubastas;

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(usuarioId))
            {
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                ViewBag.UsuarioActual = usuario;
                ViewBag.EsCreador = usuario?.EsCreador ?? false;
            }

            return View(subastas);
        }

        // ========================================
        // MIS SUBASTAS (CREADOR)
        // ========================================
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> MisSubastas()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario == null || !usuario.EsCreador)
            {
                TempData["Error"] = "Solo los creadores pueden acceder a esta seccion";
                return RedirectToAction("Index");
            }

            var subastas = await _subastasService.ObtenerSubastasCreadorAsync(usuarioId);

            ViewBag.UsuarioActual = usuario;

            // Estadisticas
            ViewBag.SubastasActivas = subastas.Count(s => s.Estado == EstadoSubasta.Activa);
            ViewBag.SubastasFinalizadas = subastas.Count(s => s.Estado == EstadoSubasta.Finalizada);
            ViewBag.TotalGanado = subastas
                .Where(s => s.Estado == EstadoSubasta.Finalizada)
                .Sum(s => s.PrecioActual * 0.80m); // Descontando comision

            return View(subastas);
        }

        // ========================================
        // CREAR SUBASTA (GET)
        // ========================================
        public async Task<IActionResult> Crear()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario == null || !usuario.EsCreador)
            {
                TempData["Error"] = "Solo los creadores pueden crear subastas";
                return RedirectToAction("Index");
            }

            // Obtener contenidos del creador para seleccionar
            var contenidosRaw = await _context.Contenidos
                .Include(c => c.Archivos)
                .Where(c => c.UsuarioId == usuarioId && c.EstaActivo && !c.EsBorrador)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(50)
                .ToListAsync();

            // Procesar thumbnails después de cargar los datos
            var contenidos = contenidosRaw.Select(c => {
                // Buscar thumbnail: primero del contenido, luego del primer archivo
                var primerArchivo = c.Archivos?.OrderBy(a => a.Orden).FirstOrDefault();
                var thumbnail = c.Thumbnail
                    ?? primerArchivo?.Thumbnail
                    ?? primerArchivo?.RutaArchivo
                    ?? c.RutaArchivo;

                return new
                {
                    c.Id,
                    c.Descripcion,
                    Thumbnail = thumbnail,
                    c.TipoContenido
                };
            }).ToList();

            ViewBag.Contenidos = contenidos;
            ViewBag.UsuarioActual = usuario;

            return View(new Subasta
            {
                PrecioInicial = 10.00m,
                IncrementoMinimo = 1.00m,
                ExtensionAutomatica = true
            });
        }

        // ========================================
        // CREAR SUBASTA (POST)
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            string titulo,
            string? descripcion,
            decimal precioInicial,
            decimal incrementoMinimo,
            int duracionHoras,
            int? contenidoId,
            decimal? precioCompraloYa,
            bool soloSuscriptores,
            TipoContenidoSubasta tipoContenido,
            IFormFile? imagenPreview)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return Json(new { success = false, message = "No autenticado" });

            // Rate limiting
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"subasta_crear_{usuarioId}";

            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 5, TimeSpan.FromHours(1),
                TipoAtaque.SpamContenido, "/Subastas/Crear", usuarioId))
            {
                return Json(new { success = false, message = "Has creado demasiadas subastas. Espera un momento." });
            }

            string? imagenPreviewPath = null;

            // Subir imagen preview si se proporciono
            if (imagenPreview != null && imagenPreview.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(imagenPreview.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Tipo de imagen no permitido" });
                }

                if (imagenPreview.Length > 5 * 1024 * 1024) // 5MB
                {
                    return Json(new { success = false, message = "La imagen no puede superar 5MB" });
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "subastas");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imagenPreview.CopyToAsync(stream);
                }

                imagenPreviewPath = $"/uploads/subastas/{fileName}";
            }

            var resultado = await _subastasService.CrearSubastaAsync(
                usuarioId,
                titulo,
                descripcion,
                precioInicial,
                incrementoMinimo,
                duracionHoras,
                contenidoId,
                imagenPreviewPath,
                precioCompraloYa,
                soloSuscriptores,
                tipoContenido
            );

            if (resultado.exito)
            {
                return Json(new
                {
                    success = true,
                    message = resultado.mensaje,
                    redirectUrl = Url.Action("Detalles", new { id = resultado.subasta?.Id })
                });
            }

            return Json(new { success = false, message = resultado.mensaje });
        }

        // ========================================
        // VER DETALLES DE SUBASTA
        // ========================================
        [AllowAnonymous]
        public async Task<IActionResult> Detalles(int id)
        {
            var subasta = await _subastasService.ObtenerSubastaAsync(id);

            if (subasta == null)
            {
                TempData["Error"] = "Subasta no encontrada";
                return RedirectToAction("Index");
            }

            // Incrementar vistas
            await _subastasService.IncrementarVistasAsync(id);

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ApplicationUser? usuarioActual = null;
            bool puedePujar = false;
            bool esMiSubasta = false;

            if (!string.IsNullOrEmpty(usuarioId))
            {
                usuarioActual = await _userManager.FindByIdAsync(usuarioId);
                puedePujar = await _subastasService.PuedePujarAsync(usuarioId, id);
                esMiSubasta = subasta.CreadorId == usuarioId;
            }

            ViewBag.UsuarioActual = usuarioActual;
            ViewBag.PuedePujar = puedePujar;
            ViewBag.EsMiSubasta = esMiSubasta;
            ViewBag.EsGanador = subasta.GanadorId == usuarioId;

            return View(subasta);
        }

        // ========================================
        // REALIZAR PUJA
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pujar(int subastaId, decimal monto)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return Json(new { success = false, message = "Debes iniciar sesion para pujar" });

            // Rate limiting
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var rateLimitKey = $"puja_{usuarioId}";

            if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, 10, TimeSpan.FromMinutes(1),
                TipoAtaque.SpamContenido, "/Subastas/Pujar", usuarioId))
            {
                return Json(new { success = false, message = "Demasiadas pujas. Espera un momento." });
            }

            var resultado = await _subastasService.RealizarPujaAsync(subastaId, usuarioId, monto, clientIp);

            if (resultado.exito)
            {
                // Obtener datos actualizados
                var subasta = await _subastasService.ObtenerSubastaAsync(subastaId);
                return Json(new
                {
                    success = true,
                    message = resultado.mensaje,
                    precioActual = subasta?.PrecioActual ?? monto,
                    numeroPujas = subasta?.NumeroPujas ?? 0,
                    tiempoRestanteMs = subasta?.TiempoRestante()?.TotalMilliseconds ?? 0
                });
            }

            return Json(new { success = false, message = resultado.mensaje });
        }

        // ========================================
        // COMPRALO YA
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompraloYa(int subastaId)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return Json(new { success = false, message = "Debes iniciar sesion" });

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var resultado = await _subastasService.RealizarCompraloYaAsync(subastaId, usuarioId, clientIp);

            return Json(new { success = resultado.exito, message = resultado.mensaje });
        }

        // ========================================
        // CANCELAR SUBASTA
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int subastaId)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
                return Json(new { success = false, message = "No autenticado" });

            var resultado = await _subastasService.CancelarSubastaAsync(subastaId, usuarioId);

            return Json(new { success = resultado.exito, message = resultado.mensaje });
        }

        // ========================================
        // API: OBTENER ESTADO ACTUAL (para updates en tiempo real)
        // ========================================
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Estado(int id)
        {
            var subasta = await _context.Subastas
                .Where(s => s.Id == id)
                .Select(s => new
                {
                    s.PrecioActual,
                    NumeroPujas = s.ContadorPujas,
                    s.FechaFin,
                    s.Estado,
                    TiempoRestanteMs = s.FechaFin > DateTime.Now
                        ? (s.FechaFin - DateTime.Now).TotalMilliseconds
                        : 0
                })
                .FirstOrDefaultAsync();

            if (subasta == null)
                return NotFound();

            return Json(subasta);
        }

        // ========================================
        // API: OBTENER PUJAS RECIENTES
        // ========================================
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> PujasRecientes(int id, int limit = 10)
        {
            var subasta = await _context.Subastas
                .Include(s => s.Pujas)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subasta == null)
                return NotFound();

            var pujas = subasta.Pujas
                .OrderByDescending(p => p.FechaPuja)
                .Take(limit)
                .Select(p => new
                {
                    monto = p.Monto,
                    fecha = p.FechaPuja.ToString("HH:mm:ss"),
                    usuarioInicial = subasta.MostrarHistorialPujas
                        ? (p.Usuario?.UserName?.Substring(0, 1).ToUpper() + "***")
                        : "***"
                })
                .ToList();

            return Json(pujas);
        }
    }
}
