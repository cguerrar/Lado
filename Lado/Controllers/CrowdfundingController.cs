using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Lado.Data;
using Lado.Models;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class CrowdfundingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrowdfundingService _crowdfundingService;
        private readonly ILogger<CrowdfundingController> _logger;
        private readonly ILogEventoService _logEventoService;

        public CrowdfundingController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrowdfundingService crowdfundingService,
            ILogger<CrowdfundingController> logger,
            ILogEventoService logEventoService)
        {
            _context = context;
            _userManager = userManager;
            _crowdfundingService = crowdfundingService;
            _logger = logger;
            _logEventoService = logEventoService;
        }

        // ========================================
        // INDEX - Campanas Activas
        // ========================================

        /// <summary>
        /// Listado de campanas activas de crowdfunding
        /// </summary>
        [HttpGet]
        [HttpGet("Index")]
        [AllowAnonymous]
        public async Task<IActionResult> Index(int pagina = 1, string? categoria = null)
        {
            var campanas = await _crowdfundingService.ObtenerCampanasActivasAsync(pagina, 12, categoria);

            // Obtener categorias para el filtro
            var categorias = await _context.CampanasCrowdfunding
                .Where(c => c.Estado == EstadoCampanaCrowdfunding.Activa && !string.IsNullOrEmpty(c.Categoria))
                .Select(c => c.Categoria)
                .Distinct()
                .ToListAsync();

            ViewBag.Categorias = categorias;
            ViewBag.CategoriaActual = categoria;
            ViewBag.Pagina = pagina;
            ViewBag.TotalCampanas = await _context.CampanasCrowdfunding
                .CountAsync(c => c.Estado == EstadoCampanaCrowdfunding.Activa && c.EsVisible);

            // Verificar si el usuario actual es creador
            var usuario = await _userManager.GetUserAsync(User);
            ViewBag.EsCreador = usuario?.EsCreador ?? false;

            return View(campanas);
        }

        // ========================================
        // MIS CAMPANAS (Creadores)
        // ========================================

        /// <summary>
        /// Campanas del creador actual
        /// </summary>
        [HttpGet("MisCampanas")]
        public async Task<IActionResult> MisCampanas()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = await _userManager.FindByIdAsync(usuarioId);
            if (usuario == null || !usuario.EsCreador)
            {
                TempData["Error"] = "Solo los creadores pueden acceder a esta seccion";
                return RedirectToAction("Index");
            }

            var campanas = await _crowdfundingService.ObtenerCampanasDelCreadorAsync(usuarioId);
            var estadisticas = await _crowdfundingService.ObtenerEstadisticasCreadorAsync(usuarioId);

            ViewBag.Estadisticas = estadisticas;

            return View(campanas);
        }

        /// <summary>
        /// Campanas donde el usuario ha aportado
        /// </summary>
        [HttpGet("MisAportes")]
        public async Task<IActionResult> MisAportes()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return RedirectToAction("Login", "Account");
            }

            var campanas = await _crowdfundingService.ObtenerCampanasAportadasAsync(usuarioId);
            var totalAportado = await _crowdfundingService.ObtenerTotalAportadoPorUsuarioAsync(usuarioId);

            ViewBag.TotalAportado = totalAportado;

            return View(campanas);
        }

        // ========================================
        // CREAR CAMPANA
        // ========================================

        /// <summary>
        /// Formulario para crear nueva campana
        /// </summary>
        [HttpGet("Crear")]
        public async Task<IActionResult> Crear()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var usuario = await _userManager.FindByIdAsync(usuarioId ?? "");

            if (usuario == null || !usuario.EsCreador)
            {
                TempData["Error"] = "Solo los creadores pueden crear campanas";
                return RedirectToAction("Index");
            }

            var model = new CrearCampanaViewModel
            {
                FechaLimite = DateTime.Now.AddDays(30),
                AporteMinimo = 5m
            };

            return View(model);
        }

        /// <summary>
        /// Procesar creacion de campana
        /// </summary>
        [HttpPost("Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(CrearCampanaViewModel model, IFormFile? imagenPreview)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var usuario = await _userManager.FindByIdAsync(usuarioId ?? "");

            if (usuario == null || !usuario.EsCreador)
            {
                TempData["Error"] = "Solo los creadores pueden crear campanas";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Procesar imagen si se subio
                string? rutaImagen = null;
                if (imagenPreview != null && imagenPreview.Length > 0)
                {
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "crowdfunding");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagenPreview.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imagenPreview.CopyToAsync(stream);
                    }

                    rutaImagen = $"/uploads/crowdfunding/{fileName}";
                }

                var campana = new CampanaCrowdfunding
                {
                    CreadorId = usuarioId!,
                    Titulo = model.Titulo,
                    Descripcion = model.Descripcion,
                    Meta = model.Meta,
                    AporteMinimo = model.AporteMinimo,
                    FechaLimite = model.FechaLimite,
                    ImagenPreview = rutaImagen,
                    TipoLado = model.TipoLado,
                    Categoria = model.Categoria,
                    Tags = model.Tags
                };

                var resultado = await _crowdfundingService.CrearCampanaAsync(campana);

                if (resultado.exito)
                {
                    TempData["Success"] = "Campana creada. Puedes publicarla cuando estes listo.";
                    return RedirectToAction("Editar", new { id = resultado.campanaId });
                }
                else
                {
                    ModelState.AddModelError("", resultado.mensaje);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuarioId, usuario?.NombreCompleto);
                ModelState.AddModelError("", "Error al crear la campana");
                return View(model);
            }
        }

        // ========================================
        // EDITAR CAMPANA
        // ========================================

        /// <summary>
        /// Formulario para editar campana en borrador
        /// </summary>
        [HttpGet("Editar/{id}")]
        public async Task<IActionResult> Editar(int id)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var campana = await _crowdfundingService.ObtenerCampanaAsync(id);

            if (campana == null)
            {
                TempData["Error"] = "Campana no encontrada";
                return RedirectToAction("MisCampanas");
            }

            if (campana.CreadorId != usuarioId)
            {
                TempData["Error"] = "No tienes permiso para editar esta campana";
                return RedirectToAction("MisCampanas");
            }

            if (campana.Estado != EstadoCampanaCrowdfunding.Borrador)
            {
                TempData["Error"] = "Solo puedes editar campanas en borrador";
                return RedirectToAction("Detalles", new { id });
            }

            var model = new CrearCampanaViewModel
            {
                Id = campana.Id,
                Titulo = campana.Titulo,
                Descripcion = campana.Descripcion,
                Meta = campana.Meta,
                AporteMinimo = campana.AporteMinimo,
                FechaLimite = campana.FechaLimite,
                TipoLado = campana.TipoLado,
                Categoria = campana.Categoria,
                Tags = campana.Tags,
                ImagenPreviewActual = campana.ImagenPreview
            };

            return View(model);
        }

        /// <summary>
        /// Procesar edicion de campana
        /// </summary>
        [HttpPost("Editar/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, CrearCampanaViewModel model, IFormFile? imagenPreview)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var campanaExistente = await _crowdfundingService.ObtenerCampanaAsync(id);

            if (campanaExistente == null || campanaExistente.CreadorId != usuarioId)
            {
                TempData["Error"] = "No tienes permiso para editar esta campana";
                return RedirectToAction("MisCampanas");
            }

            if (!ModelState.IsValid)
            {
                model.ImagenPreviewActual = campanaExistente.ImagenPreview;
                return View(model);
            }

            try
            {
                // Procesar nueva imagen si se subio
                string? rutaImagen = campanaExistente.ImagenPreview;
                if (imagenPreview != null && imagenPreview.Length > 0)
                {
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "crowdfunding");
                    if (!Directory.Exists(uploadsPath))
                    {
                        Directory.CreateDirectory(uploadsPath);
                    }

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(imagenPreview.FileName)}";
                    var filePath = Path.Combine(uploadsPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imagenPreview.CopyToAsync(stream);
                    }

                    rutaImagen = $"/uploads/crowdfunding/{fileName}";
                }

                campanaExistente.Titulo = model.Titulo;
                campanaExistente.Descripcion = model.Descripcion;
                campanaExistente.Meta = model.Meta;
                campanaExistente.AporteMinimo = model.AporteMinimo;
                campanaExistente.FechaLimite = model.FechaLimite;
                campanaExistente.ImagenPreview = rutaImagen;
                campanaExistente.TipoLado = model.TipoLado;
                campanaExistente.Categoria = model.Categoria;
                campanaExistente.Tags = model.Tags;

                var resultado = await _crowdfundingService.ActualizarCampanaAsync(campanaExistente);

                if (resultado.exito)
                {
                    TempData["Success"] = "Campana actualizada";
                    return RedirectToAction("Editar", new { id });
                }
                else
                {
                    ModelState.AddModelError("", resultado.mensaje);
                    model.ImagenPreviewActual = rutaImagen;
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Contenido, usuarioId, null);
                ModelState.AddModelError("", "Error al actualizar la campana");
                model.ImagenPreviewActual = campanaExistente.ImagenPreview;
                return View(model);
            }
        }

        // ========================================
        // DETALLES DE CAMPANA
        // ========================================

        /// <summary>
        /// Ver detalles de una campana
        /// </summary>
        [HttpGet("Detalles/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Detalles(int id)
        {
            var campana = await _crowdfundingService.ObtenerCampanaAsync(id);

            if (campana == null)
            {
                TempData["Error"] = "Campana no encontrada";
                return RedirectToAction("Index");
            }

            // Solo el creador puede ver campanas en borrador
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (campana.Estado == EstadoCampanaCrowdfunding.Borrador && campana.CreadorId != usuarioId)
            {
                TempData["Error"] = "Esta campana aun no esta publicada";
                return RedirectToAction("Index");
            }

            // Incrementar vistas
            await _crowdfundingService.IncrementarVistasAsync(id);

            // Verificar si el usuario ya aporto
            AporteCrowdfunding? aporteUsuario = null;
            decimal saldoUsuario = 0;
            if (!string.IsNullOrEmpty(usuarioId))
            {
                aporteUsuario = await _crowdfundingService.ObtenerAporteUsuarioAsync(id, usuarioId);
                var usuario = await _userManager.FindByIdAsync(usuarioId);
                saldoUsuario = usuario?.Saldo ?? 0;
            }

            // Obtener lista de aportantes
            var aportantes = await _crowdfundingService.ObtenerAportantesAsync(id, 20);

            ViewBag.AporteUsuario = aporteUsuario;
            ViewBag.SaldoUsuario = saldoUsuario;
            ViewBag.Aportantes = aportantes;
            ViewBag.EsCreador = campana.CreadorId == usuarioId;

            return View(campana);
        }

        // ========================================
        // APORTAR A CAMPANA
        // ========================================

        /// <summary>
        /// Realizar un aporte a una campana
        /// </summary>
        [HttpPost("Aportar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aportar(int campanaId, decimal monto, string? mensaje, bool esAnonimo = false)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return Json(new { success = false, message = "Debes iniciar sesion para aportar" });
            }

            try
            {
                var resultado = await _crowdfundingService.RealizarAporteAsync(campanaId, usuarioId, monto, mensaje, esAnonimo);

                if (resultado.exito)
                {
                    var campana = await _crowdfundingService.ObtenerCampanaAsync(campanaId);
                    return Json(new
                    {
                        success = true,
                        message = resultado.mensaje,
                        nuevoProgreso = campana?.PorcentajeProgreso ?? 0,
                        totalRecaudado = campana?.TotalRecaudado ?? 0,
                        totalAportantes = campana?.TotalAportantes ?? 0,
                        metaAlcanzada = campana?.MetaAlcanzada ?? false
                    });
                }

                return Json(new { success = false, message = resultado.mensaje });
            }
            catch (Exception ex)
            {
                await _logEventoService.RegistrarErrorAsync(ex, CategoriaEvento.Pago, usuarioId, null);
                return Json(new { success = false, message = "Error al procesar el aporte" });
            }
        }

        // ========================================
        // PUBLICAR CAMPANA
        // ========================================

        /// <summary>
        /// Publicar una campana en borrador
        /// </summary>
        [HttpPost("Publicar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(int campanaId)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            var resultado = await _crowdfundingService.PublicarCampanaAsync(campanaId, usuarioId);

            return Json(new { success = resultado.exito, message = resultado.mensaje });
        }

        // ========================================
        // CANCELAR CAMPANA
        // ========================================

        /// <summary>
        /// Cancelar una campana activa
        /// </summary>
        [HttpPost("Cancelar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int campanaId)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            var resultado = await _crowdfundingService.CancelarCampanaAsync(campanaId, usuarioId);

            return Json(new { success = resultado.exito, message = resultado.mensaje });
        }

        // ========================================
        // FINALIZAR CAMPANA (ENTREGAR CONTENIDO)
        // ========================================

        /// <summary>
        /// Vista para finalizar campana exitosa
        /// </summary>
        [HttpGet("Finalizar/{id}")]
        public async Task<IActionResult> Finalizar(int id)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var campana = await _crowdfundingService.ObtenerCampanaAsync(id);

            if (campana == null || campana.CreadorId != usuarioId)
            {
                TempData["Error"] = "No tienes permiso para finalizar esta campana";
                return RedirectToAction("MisCampanas");
            }

            if (campana.Estado != EstadoCampanaCrowdfunding.MetaAlcanzada && campana.Estado != EstadoCampanaCrowdfunding.Activa)
            {
                TempData["Error"] = "Esta campana no puede ser finalizada";
                return RedirectToAction("Detalles", new { id });
            }

            // Obtener contenidos del creador para seleccionar
            var contenidos = await _context.Contenidos
                .Where(c => c.UsuarioId == usuarioId && c.EstaActivo && !c.EsBorrador)
                .OrderByDescending(c => c.FechaPublicacion)
                .Take(50)
                .ToListAsync();

            ViewBag.Contenidos = contenidos;

            return View(campana);
        }

        /// <summary>
        /// Procesar finalizacion de campana
        /// </summary>
        [HttpPost("Finalizar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarPost(int campanaId, int contenidoId, string? mensajeAgradecimiento)
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(usuarioId))
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            var campana = await _crowdfundingService.ObtenerCampanaAsync(campanaId);
            if (campana == null || campana.CreadorId != usuarioId)
            {
                return Json(new { success = false, message = "No tienes permiso para finalizar esta campana" });
            }

            var resultado = await _crowdfundingService.FinalizarCampanaExitosaAsync(campanaId, contenidoId, mensajeAgradecimiento);

            return Json(new { success = resultado.exito, message = resultado.mensaje });
        }

        // ========================================
        // API: OBTENER PROGRESO
        // ========================================

        /// <summary>
        /// Obtener progreso actual de una campana (para actualizacion en tiempo real)
        /// </summary>
        [HttpGet("Progreso/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> ObtenerProgreso(int id)
        {
            var campana = await _crowdfundingService.ObtenerCampanaAsync(id);
            if (campana == null)
            {
                return Json(new { success = false });
            }

            return Json(new
            {
                success = true,
                progreso = campana.PorcentajeProgreso,
                totalRecaudado = campana.TotalRecaudado,
                meta = campana.Meta,
                totalAportantes = campana.TotalAportantes,
                metaAlcanzada = campana.MetaAlcanzada,
                tiempoRestante = campana.TiempoRestanteTexto,
                estado = campana.Estado.ToString()
            });
        }
    }

    // ========================================
    // VIEW MODELS
    // ========================================

    public class CrearCampanaViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El titulo es obligatorio")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "El titulo debe tener entre 10 y 200 caracteres")]
        [Display(Name = "Titulo de la campana")]
        public string Titulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripcion es obligatoria")]
        [StringLength(2000, MinimumLength = 50, ErrorMessage = "La descripcion debe tener entre 50 y 2000 caracteres")]
        [Display(Name = "Descripcion del contenido que crearas")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "La meta es obligatoria")]
        [Range(10, 100000, ErrorMessage = "La meta debe estar entre $10 y $100,000")]
        [Display(Name = "Meta a recaudar")]
        public decimal Meta { get; set; } = 100m;

        [Range(1, 1000, ErrorMessage = "El aporte minimo debe estar entre $1 y $1,000")]
        [Display(Name = "Aporte minimo")]
        public decimal AporteMinimo { get; set; } = 5m;

        [Required(ErrorMessage = "La fecha limite es obligatoria")]
        [Display(Name = "Fecha limite")]
        [DataType(DataType.Date)]
        public DateTime FechaLimite { get; set; }

        [Display(Name = "Tipo de contenido")]
        public TipoLado TipoLado { get; set; } = TipoLado.LadoA;

        [StringLength(50)]
        [Display(Name = "Categoria")]
        public string? Categoria { get; set; }

        [StringLength(500)]
        [Display(Name = "Tags (separados por coma)")]
        public string? Tags { get; set; }

        public string? ImagenPreviewActual { get; set; }
    }
}
