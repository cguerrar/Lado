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
    public class DesafiosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DesafiosController> _logger;
        private readonly IRateLimitService _rateLimitService;

        public DesafiosController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<DesafiosController> logger,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _rateLimitService = rateLimitService;
        }

        // ========================================
        // ENTREGAR CONTENIDO (AGREGAR AL CONTROLADOR)
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EntregarContenido(int desafioId, IFormFile archivo, string notasCreador)
        {
            try
            {
                _logger.LogInformation($"EntregarContenido llamado: DesafioId={desafioId}");

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"desafio_entrega_{usuarioId}";
                var rateLimitKeyIp = $"desafio_entrega_ip_{clientIp}";

                // Límite por IP
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP DESAFIO: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuarioId);
                    return Json(new { success = false, message = "Demasiadas solicitudes. Espera unos minutos." });
                }

                // Límite por usuario
                if (!_rateLimitService.IsAllowed(rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window))
                {
                    _logger.LogWarning("🚫 RATE LIMIT DESAFIO: Usuario {UserId} excedió límite - IP: {IP}", usuarioId, clientIp);
                    return Json(new { success = false, message = "Has enviado demasiadas entregas. Espera unos minutos." });
                }

                var desafio = await _context.Desafios
                    .Include(d => d.Fan)
                    .FirstOrDefaultAsync(d => d.Id == desafioId);

                if (desafio == null)
                {
                    _logger.LogWarning($"Desafío {desafioId} no encontrado");
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                // Verificar que el usuario es el creador asignado
                if (desafio.CreadorAsignadoId != usuarioId)
                {
                    _logger.LogWarning($"Usuario {usuarioId} no autorizado para entregar desafío {desafioId}");
                    return Json(new { success = false, message = "No estás autorizado para entregar este desafío" });
                }

                // Verificar estado
                if (desafio.Estado != EstadoDesafio.Asignado && desafio.Estado != EstadoDesafio.EnProgreso)
                {
                    _logger.LogWarning($"Desafío {desafioId} en estado incorrecto: {desafio.Estado}");
                    return Json(new { success = false, message = "Este desafío no puede recibir entregas en su estado actual" });
                }

                // Validar archivo
                if (archivo == null || archivo.Length == 0)
                {
                    return Json(new { success = false, message = "Debes subir un archivo" });
                }

                // Validar tamaño (50MB máximo)
                if (archivo.Length > 52428800)
                {
                    return Json(new { success = false, message = "El archivo no puede superar los 50MB" });
                }

                // Guardar archivo
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "desafios");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{desafioId}_{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await archivo.CopyToAsync(fileStream);
                }

                // Actualizar desafío
                desafio.ArchivoEntregaUrl = $"/uploads/desafios/{uniqueFileName}";
                desafio.NotasCreador = notasCreador?.Trim();
                desafio.Estado = EstadoDesafio.ContenidoSubido;
                desafio.FechaEntrega = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Contenido entregado exitosamente para desafío {desafioId}");

                return Json(new
                {
                    success = true,
                    message = "¡Contenido entregado exitosamente! El cliente lo revisará pronto."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al entregar contenido para desafío {desafioId}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // CREAR DESAFÍO PÚBLICO
        // ========================================
        [HttpGet]
        public IActionResult CrearPublico()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPublico(Desafio model)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"desafio_crear_{usuarioId}";
                var rateLimitKeyIp = $"desafio_crear_ip_{clientIp}";

                // Límite por IP
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, RateLimits.ContentCreation_IP_MaxRequests, RateLimits.ContentCreation_IP_Window))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP CREAR DESAFIO: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuarioId);
                    TempData["Error"] = "Demasiadas solicitudes. Espera unos minutos.";
                    return View(model);
                }

                // Límite por usuario
                if (!_rateLimitService.IsAllowed(rateLimitKey, RateLimits.ContentCreation_MaxRequests, RateLimits.ContentCreation_Window))
                {
                    _logger.LogWarning("🚫 RATE LIMIT CREAR DESAFIO: Usuario {UserId} excedió límite - IP: {IP}", usuarioId, clientIp);
                    TempData["Error"] = "Has creado demasiados desafíos. Espera unos minutos.";
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Titulo) ||
                    string.IsNullOrWhiteSpace(model.Descripcion) ||
                    string.IsNullOrWhiteSpace(model.Categoria))
                {
                    TempData["Error"] = "Todos los campos son obligatorios";
                    return View(model);
                }

                var desafio = new Desafio
                {
                    FanId = usuarioId!,
                    TipoDesafio = TipoDesafio.Publico,
                    Titulo = model.Titulo.Trim(),
                    Descripcion = model.Descripcion.Trim(),
                    Presupuesto = 10.00m,
                    DiasPlazoPlazo = 3,
                    Categoria = model.Categoria,
                    TipoContenido = model.TipoContenido,
                    Visibilidad = VisibilidadDesafio.Publico,
                    Estado = EstadoDesafio.RecibiendoPropuestas,
                    FechaCreacion = DateTime.Now,
                    FechaExpiracion = DateTime.Now.AddDays(7),
                    EstadoPago = EstadoPago.Hold
                };

                _context.Desafios.Add(desafio);
                await _context.SaveChangesAsync();

                TempData["Success"] = "🎮 ¡Desafío creado! Los creadores comenzarán a enviar propuestas.";
                return RedirectToAction(nameof(MisDesafios));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear desafío público");
                TempData["Error"] = $"Error: {ex.Message}";
                return View(model);
            }
        }

        // ========================================
        // CREAR DESAFÍO DIRECTO
        // ========================================
        [HttpGet]
        public async Task<IActionResult> CrearDirecto(string creadorId)
        {
            if (string.IsNullOrEmpty(creadorId))
            {
                TempData["Error"] = "Creador no especificado";
                return RedirectToAction("Index", "Home");
            }

            var creador = await _userManager.FindByIdAsync(creadorId);
           
            ViewBag.Creador = creador;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearDirecto(string creadorId, Desafio model)
        {
            try
            {
                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var creador = await _userManager.FindByIdAsync(creadorId);

          
                var desafio = new Desafio
                {
                    FanId = usuarioId!,
                    CreadorObjetivoId = creadorId,
                    TipoDesafio = TipoDesafio.Directo,
                    Titulo = model.Titulo.Trim(),
                    Descripcion = model.Descripcion.Trim(),
                    Presupuesto = model.Presupuesto,
                    DiasPlazoPlazo = model.DiasPlazoPlazo,
                    Categoria = model.Categoria ?? "General",
                    TipoContenido = model.TipoContenido,
                    Visibilidad = model.Visibilidad,
                    Estado = EstadoDesafio.Pendiente, // Esperando aceptación
                    FechaCreacion = DateTime.Now,
                    EstadoPago = EstadoPago.Hold
                };

                _context.Desafios.Add(desafio);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Desafío enviado a {creador.NombreCompleto}";
                return RedirectToAction(nameof(MisDesafios));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear desafío directo");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(CrearDirecto), new { creadorId });
            }
        }

        // ========================================
        // GESTIÓN DE DESAFÍOS
        // ========================================
        public async Task<IActionResult> MisDesafios()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var desafios = await _context.Desafios
                .Include(d => d.CreadorObjetivo)
                .Include(d => d.CreadorAsignado)
                .Include(d => d.Propuestas)
                .Where(d => d.FanId == usuarioId)
                .OrderByDescending(d => d.FechaCreacion)
                .ToListAsync();

            return View(desafios);
        }

        public async Task<IActionResult> MisPropuestas()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var propuestas = await _context.PropuestasDesafios
                .Include(p => p.Desafio)
                    .ThenInclude(d => d.Fan)
                .Include(p => p.Desafio)
                    .ThenInclude(d => d.Propuestas)
                .Where(p => p.CreadorId == usuarioId)
                .OrderByDescending(p => p.FechaPropuesta)
                .ToListAsync();

            return View(propuestas);
        }

        public async Task<IActionResult> DesafiosRecibidos()
        {
            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var desafiosDirectos = await _context.Desafios
                .Include(d => d.Fan)
                .Where(d => d.CreadorObjetivoId == usuarioId &&
                           d.Estado == EstadoDesafio.Pendiente)
                .OrderByDescending(d => d.FechaCreacion)
                .ToListAsync();

            var desafiosAsignados = await _context.Desafios
                .Include(d => d.Fan)
                .Where(d => d.CreadorAsignadoId == usuarioId &&
                           (d.Estado == EstadoDesafio.Asignado ||
                            d.Estado == EstadoDesafio.EnProgreso))
                .OrderByDescending(d => d.FechaAsignacion)
                .ToListAsync();

            ViewBag.DesafiosDirectos = desafiosDirectos;
            ViewBag.DesafiosAsignados = desafiosAsignados;

            return View();
        }

        // ========================================
        // FEED PÚBLICO (SOLO CREADORES)
        // ========================================
        public async Task<IActionResult> FeedPublico(string? categoria, decimal? presupuestoMin, decimal? presupuestoMax)
        {
            // Obtener usuario actual
            var usuario = await _userManager.GetUserAsync(User);

        
            // Solo los creadores pueden ver el feed público
            var query = _context.Desafios
                .Include(d => d.Fan)
                .Include(d => d.Propuestas)
                .Where(d => d.TipoDesafio == TipoDesafio.Publico &&
                           d.Estado == EstadoDesafio.RecibiendoPropuestas &&
                           d.FechaExpiracion > DateTime.Now);

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(categoria))
            {
                query = query.Where(d => d.Categoria == categoria);
            }

            if (presupuestoMin.HasValue)
            {
                query = query.Where(d => d.Presupuesto >= presupuestoMin.Value);
            }

            if (presupuestoMax.HasValue)
            {
                query = query.Where(d => d.Presupuesto <= presupuestoMax.Value);
            }

            var desafios = await query
                .OrderByDescending(d => d.FechaCreacion)
                .ToListAsync();

            return View(desafios);
        }

        // ========================================
        // DETALLES
        // ========================================
        public async Task<IActionResult> Detalles(int id)
        {
            var desafio = await _context.Desafios
                .Include(d => d.Fan)
                .Include(d => d.CreadorObjetivo)
                .Include(d => d.CreadorAsignado)
                .Include(d => d.Propuestas)
                    .ThenInclude(p => p.Creador)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (desafio == null)
            {
                TempData["Error"] = "Desafío no encontrado";
                return RedirectToAction(nameof(FeedPublico));
            }

            return View(desafio);
        }

        // ========================================
        // ENVIAR PROPUESTA
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarPropuesta(int desafioId, decimal precioPropuesto, int diasEntrega, string mensaje)
        {
            try
            {
                _logger.LogInformation($"EnviarPropuesta llamado: DesafioId={desafioId}, Precio={precioPropuesto}, Dias={diasEntrega}");

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    _logger.LogWarning("Usuario no autenticado intentando enviar propuesta");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

          
                var desafio = await _context.Desafios
                    .Include(d => d.Propuestas)
                    .FirstOrDefaultAsync(d => d.Id == desafioId);

                if (desafio == null)
                {
                    _logger.LogWarning($"Desafío {desafioId} no encontrado");
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                if (desafio.Estado != EstadoDesafio.RecibiendoPropuestas)
                {
                    _logger.LogWarning($"Desafío {desafioId} no está recibiendo propuestas. Estado actual: {desafio.Estado}");
                    return Json(new { success = false, message = "Este desafío ya no está recibiendo propuestas" });
                }

                // Verificar si ya envió propuesta
                var propuestaExistente = await _context.PropuestasDesafios
                    .AnyAsync(p => p.DesafioId == desafioId && p.CreadorId == usuarioId);

                if (propuestaExistente)
                {
                    _logger.LogWarning($"Usuario {usuarioId} ya tiene una propuesta para desafío {desafioId}");
                    return Json(new { success = false, message = "Ya enviaste una propuesta para este desafío" });
                }

                // Validaciones
                if (precioPropuesto <= 0)
                {
                    return Json(new { success = false, message = "El precio debe ser mayor a 0" });
                }

                if (diasEntrega <= 0 || diasEntrega > 30)
                {
                    return Json(new { success = false, message = "Los días de entrega deben estar entre 1 y 30" });
                }

                if (string.IsNullOrWhiteSpace(mensaje))
                {
                    return Json(new { success = false, message = "Debes incluir un mensaje en tu propuesta" });
                }

                // Crear propuesta
                var propuesta = new PropuestaDesafio
                {
                    DesafioId = desafioId,
                    CreadorId = usuarioId,
                    PrecioPropuesto = precioPropuesto,
                    DiasEntrega = diasEntrega,
                    MensajePropuesta = mensaje.Trim(),
                    Estado = EstadoPropuesta.Pendiente,
                    FechaPropuesta = DateTime.Now
                };

                _context.PropuestasDesafios.Add(propuesta);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Propuesta creada exitosamente: Id={propuesta.Id}, DesafioId={desafioId}, CreadorId={usuarioId}");

                return Json(new
                {
                    success = true,
                    message = "¡Propuesta enviada exitosamente! El cliente la revisará pronto.",
                    propuestaId = propuesta.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al enviar propuesta para desafío {desafioId}");
                return Json(new { success = false, message = $"Error al enviar propuesta: {ex.Message}" });
            }
        }

        // ========================================
        // ACEPTAR PROPUESTA
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AceptarPropuesta(int propuestaId)
        {
            try
            {
                _logger.LogInformation($"AceptarPropuesta llamado: PropuestaId={propuestaId}");

                var propuesta = await _context.PropuestasDesafios
                    .Include(p => p.Desafio)
                    .Include(p => p.Creador)
                    .FirstOrDefaultAsync(p => p.Id == propuestaId);

                if (propuesta == null)
                {
                    _logger.LogWarning($"Propuesta {propuestaId} no encontrada");
                    return Json(new { success = false, message = "Propuesta no encontrada" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (propuesta.Desafio.FanId != usuarioId)
                {
                    _logger.LogWarning($"Usuario {usuarioId} no autorizado para aceptar propuesta {propuestaId}");
                    return Json(new { success = false, message = "No autorizado" });
                }

                if (propuesta.Estado != EstadoPropuesta.Pendiente)
                {
                    _logger.LogWarning($"Propuesta {propuestaId} ya no está pendiente. Estado: {propuesta.Estado}");
                    return Json(new { success = false, message = "Esta propuesta ya fue procesada" });
                }

                if (propuesta.Desafio.Estado != EstadoDesafio.RecibiendoPropuestas)
                {
                    _logger.LogWarning($"Desafío {propuesta.DesafioId} ya no está recibiendo propuestas. Estado: {propuesta.Desafio.Estado}");
                    return Json(new { success = false, message = "Este desafío ya no está recibiendo propuestas" });
                }

                // Actualizar propuesta
                propuesta.Estado = EstadoPropuesta.Aceptada;
                propuesta.FechaRespuesta = DateTime.Now;

                // Actualizar desafío
                propuesta.Desafio.CreadorAsignadoId = propuesta.CreadorId;
                propuesta.Desafio.PrecioFinal = propuesta.PrecioPropuesto;
                propuesta.Desafio.Estado = EstadoDesafio.Asignado;
                propuesta.Desafio.FechaAsignacion = DateTime.Now;

                // Rechazar otras propuestas
                var otrasPropuestas = await _context.PropuestasDesafios
                    .Where(p => p.DesafioId == propuesta.DesafioId &&
                               p.Id != propuestaId &&
                               p.Estado == EstadoPropuesta.Pendiente)
                    .ToListAsync();

                foreach (var otra in otrasPropuestas)
                {
                    otra.Estado = EstadoPropuesta.Rechazada;
                    otra.FechaRespuesta = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Propuesta {propuestaId} aceptada exitosamente. Desafío {propuesta.DesafioId} asignado a {propuesta.CreadorId}");

                return Json(new
                {
                    success = true,
                    message = $"¡Propuesta aceptada! {propuesta.Creador?.NombreCompleto} comenzará a trabajar en tu desafío."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al aceptar propuesta {propuestaId}");
                return Json(new { success = false, message = $"Error al aceptar propuesta: {ex.Message}" });
            }
        }

        // ========================================
        // APROBAR CONTENIDO (AGREGAR AL CONTROLADOR)
        // ========================================
        [HttpPost]
        public async Task<IActionResult> AprobarContenido([FromBody] AprobarContenidoRequest request)
        {
            try
            {
                _logger.LogInformation($"AprobarContenido llamado: DesafioId={request.DesafioId}");

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var desafio = await _context.Desafios
                    .Include(d => d.CreadorAsignado)
                    .FirstOrDefaultAsync(d => d.Id == request.DesafioId);

                if (desafio == null)
                {
                    _logger.LogWarning($"Desafío {request.DesafioId} no encontrado");
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                // Verificar que el usuario es el fan/cliente
                if (desafio.FanId != usuarioId)
                {
                    _logger.LogWarning($"Usuario {usuarioId} no autorizado para aprobar desafío {request.DesafioId}");
                    return Json(new { success = false, message = "No estás autorizado" });
                }

                // Verificar estado
                if (desafio.Estado != EstadoDesafio.ContenidoSubido)
                {
                    _logger.LogWarning($"Desafío {request.DesafioId} en estado incorrecto: {desafio.Estado}");
                    return Json(new { success = false, message = "El contenido no está listo para aprobación" });
                }

                // Actualizar desafío
                desafio.Estado = EstadoDesafio.Completado;
                desafio.FechaCompletado = DateTime.Now;
                desafio.EstadoPago = EstadoPago.Liberado;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Contenido aprobado para desafío {request.DesafioId}");

                return Json(new
                {
                    success = true,
                    message = $"¡Misión completada! El pago ha sido procesado para {desafio.CreadorAsignado?.NombreCompleto}."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al aprobar contenido para desafío {request.DesafioId}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // RECHAZAR CONTENIDO (SOLICITAR CORRECCIÓN)
        // ========================================
        [HttpPost]
        public async Task<IActionResult> RechazarContenido([FromBody] RechazarContenidoRequest request)
        {
            try
            {
                _logger.LogInformation($"RechazarContenido llamado: DesafioId={request.DesafioId}");

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(usuarioId))
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var desafio = await _context.Desafios
                    .Include(d => d.CreadorAsignado)
                    .FirstOrDefaultAsync(d => d.Id == request.DesafioId);

                if (desafio == null)
                {
                    _logger.LogWarning($"Desafío {request.DesafioId} no encontrado");
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                // Verificar que el usuario es el fan/cliente
                if (desafio.FanId != usuarioId)
                {
                    _logger.LogWarning($"Usuario {usuarioId} no autorizado para rechazar desafío {request.DesafioId}");
                    return Json(new { success = false, message = "No estás autorizado" });
                }

                // Verificar estado
                if (desafio.Estado != EstadoDesafio.ContenidoSubido)
                {
                    _logger.LogWarning($"Desafío {request.DesafioId} en estado incorrecto: {desafio.Estado}");
                    return Json(new { success = false, message = "El contenido no está listo para revisión" });
                }

                // Volver a estado EnProgreso para que el creador corrija
                desafio.Estado = EstadoDesafio.EnProgreso;

                // Agregar nota sobre qué corregir
                var notaRechazo = $"\n\n--- CORRECCIÓN SOLICITADA ({DateTime.Now:dd/MM/yyyy HH:mm}) ---\n{request.Motivo}";
                desafio.NotasCreador = (desafio.NotasCreador ?? "") + notaRechazo;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Contenido rechazado para desafío {request.DesafioId}, motivo: {request.Motivo}");

                return Json(new
                {
                    success = true,
                    message = "Corrección solicitada. El creador recibirá tu feedback y podrá hacer una nueva entrega."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al rechazar contenido para desafío {request.DesafioId}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========================================
        // CLASES REQUEST (AGREGAR AL FINAL DEL ARCHIVO)
        // ========================================
        public class AprobarContenidoRequest
        {
            public int DesafioId { get; set; }
        }

        public class RechazarContenidoRequest
        {
            public int DesafioId { get; set; }
            public string Motivo { get; set; }
        }



        // ========================================
        // ACEPTAR/RECHAZAR DESAFÍO DIRECTO
        // ========================================
        [HttpPost]
        public async Task<IActionResult> AceptarDesafioDirecto(int desafioId)
        {
            try
            {
                var desafio = await _context.Desafios.FindAsync(desafioId);
                if (desafio == null)
                {
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (desafio.CreadorObjetivoId != usuarioId)
                {
                    return Json(new { success = false, message = "No autorizado" });
                }

                desafio.CreadorAsignadoId = usuarioId;
                desafio.PrecioFinal = desafio.Presupuesto;
                desafio.Estado = EstadoDesafio.Asignado;
                desafio.FechaAsignacion = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Desafío aceptado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aceptar desafío");
                return Json(new { success = false, message = "Error" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RechazarDesafioDirecto(int desafioId, string razon)
        {
            try
            {
                var desafio = await _context.Desafios.FindAsync(desafioId);
                if (desafio == null)
                {
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (desafio.CreadorObjetivoId != usuarioId)
                {
                    return Json(new { success = false, message = "No autorizado" });
                }

                desafio.Estado = EstadoDesafio.Rechazado;
                desafio.NotasCreador = $"Rechazado: {razon}";

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Desafío rechazado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al rechazar desafío");
                return Json(new { success = false, message = "Error" });
            }
        }




        // ========================================
        // CANCELAR DESAFÍO
        // ========================================
        [HttpPost]
        public async Task<IActionResult> CancelarDesafio(int desafioId)
        {
            try
            {
                var desafio = await _context.Desafios.FindAsync(desafioId);
                if (desafio == null)
                {
                    return Json(new { success = false, message = "Desafío no encontrado" });
                }

                var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (desafio.FanId != usuarioId)
                {
                    return Json(new { success = false, message = "No autorizado" });
                }

                if (desafio.Estado != EstadoDesafio.Pendiente &&
                    desafio.Estado != EstadoDesafio.RecibiendoPropuestas)
                {
                    return Json(new { success = false, message = "No se puede cancelar en este estado" });
                }

                desafio.Estado = EstadoDesafio.Cancelado;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Desafío cancelado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar desafío");
                return Json(new { success = false, message = "Error" });
            }
        }
    }
}