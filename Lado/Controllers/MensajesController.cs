using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Hubs;
using Lado.Services;

namespace Lado.Controllers
{
    [Authorize]
    public class MensajesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MensajesController> _logger;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IWebHostEnvironment _environment;
        private readonly IDateTimeService _dateTimeService;
        private readonly IRateLimitService _rateLimitService;

        // Límite de archivo: 10 MB
        private const long MaxFileSize = 10 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".mov", ".webm" };

        public MensajesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MensajesController> logger,
            IHubContext<ChatHub> hubContext,
            IWebHostEnvironment environment,
            IDateTimeService dateTimeService,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _hubContext = hubContext;
            _environment = environment;
            _dateTimeService = dateTimeService;
            _rateLimitService = rateLimitService;
        }

        // ========================================
        // INDEX - LISTA DE CONVERSACIONES
        // ========================================

        public async Task<IActionResult> Index()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                _logger.LogWarning("Usuario no autenticado en Index");
                return RedirectToAction("Login", "Account");
            }

            _logger.LogInformation("Cargando conversaciones para usuario: {Username}", usuario.UserName);

            // ⭐ CAMBIO: Ya no diferenciamos por TipoUsuario
            // Todos los usuarios pueden tener conversaciones con cualquiera

            // Obtener IDs de usuarios con los que hay suscripciones (en ambas direcciones)
            var creadoresSuscritos = await _context.Suscripciones
                .Where(s => s.FanId == usuario.Id && s.EstaActiva)
                .Select(s => s.CreadorId)
                .ToListAsync();

            var suscriptoresPropios = await _context.Suscripciones
                .Where(s => s.CreadorId == usuario.Id && s.EstaActiva)
                .Select(s => s.FanId)
                .ToListAsync();

            // Obtener IDs de conversaciones existentes (excluyendo mensajes eliminados por el usuario)
            var conversacionesExistentes = await _context.MensajesPrivados
                .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                           (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                .Select(m => m.RemitenteId == usuario.Id ? m.DestinatarioId : m.RemitenteId)
                .Distinct()
                .ToListAsync();

            // ========================================
            // FILTRAR USUARIOS BLOQUEADOS
            // ========================================
            var usuariosBloqueadosIds = await _context.BloqueosUsuarios
                .Where(b => b.BloqueadorId == usuario.Id || b.BloqueadoId == usuario.Id)
                .Select(b => b.BloqueadorId == usuario.Id ? b.BloqueadoId : b.BloqueadorId)
                .Distinct()
                .ToListAsync();

            // CAMBIO: Solo mostrar conversaciones que ya tienen mensajes
            // Eliminar contactos sin conversación iniciada Y filtrar bloqueados
            var contactosIds = conversacionesExistentes
                .Where(id => !usuariosBloqueadosIds.Contains(id))
                .Distinct()
                .ToList();

            _logger.LogInformation("Total de contactos encontrados: {Count} (excluidos {Bloqueados} bloqueados)",
                contactosIds.Count, usuariosBloqueadosIds.Count);

            var conversaciones = new List<ConversacionViewModel>();

            foreach (var contactoId in contactosIds)
            {
                var contacto = await _userManager.FindByIdAsync(contactoId);
                if (contacto == null)
                {
                    _logger.LogWarning("Contacto no encontrado: {ContactoId}", contactoId);
                    continue;
                }

                var ultimoMensaje = await _context.MensajesPrivados
                    .Where(m => ((m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId && !m.EliminadoPorRemitente) ||
                                (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario)))
                    .OrderByDescending(m => m.FechaEnvio)
                    .FirstOrDefaultAsync();

                var mensajesNoLeidos = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == contactoId &&
                               m.DestinatarioId == usuario.Id &&
                               !m.Leido &&
                               !m.EliminadoPorDestinatario)
                    .CountAsync();

                conversaciones.Add(new ConversacionViewModel
                {
                    Contacto = contacto,
                    UltimoMensaje = ultimoMensaje,
                    MensajesNoLeidos = mensajesNoLeidos,
                    TieneConversacion = ultimoMensaje != null
                });
            }

            // Ordenar: primero los no leídos, luego por fecha
            ViewBag.Conversaciones = conversaciones
                .OrderByDescending(c => c.MensajesNoLeidos > 0)
                .ThenByDescending(c => c.UltimoMensaje?.FechaEnvio ?? DateTime.MinValue)
                .ToList();

            _logger.LogInformation("Conversaciones cargadas: {Count}", conversaciones.Count);

            return View(usuario);
        }

        // ========================================
        // CHAT - VISTA DE CONVERSACIÓN
        // ========================================

        public async Task<IActionResult> Chat(string id)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                _logger.LogWarning("Usuario no autenticado en Chat");
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                _logger.LogWarning("ID de destinatario vacío");
                return RedirectToAction("Index");
            }

            var destinatario = await _userManager.FindByIdAsync(id);
            if (destinatario == null)
            {
                _logger.LogWarning("Destinatario no encontrado: {DestinatarioId}", id);
                return NotFound();
            }

            _logger.LogInformation("Abriendo chat entre {Usuario} y {Destinatario}",
                usuario.UserName, destinatario.UserName);

            // ========================================
            // VERIFICAR BLOQUEO
            // ========================================
            var existeBloqueo = await _context.BloqueosUsuarios
                .AnyAsync(b => (b.BloqueadorId == usuario.Id && b.BloqueadoId == id) ||
                              (b.BloqueadorId == id && b.BloqueadoId == usuario.Id));

            if (existeBloqueo)
            {
                TempData["Error"] = "No puedes enviar mensajes a este usuario";
                return RedirectToAction("Index");
            }

            // ⭐ CAMBIO: Verificar si existe una relación de suscripción (en cualquier dirección)
            var existeRelacion = await _context.Suscripciones
                .AnyAsync(s => (s.FanId == usuario.Id && s.CreadorId == id && s.EstaActiva) ||
                              (s.FanId == id && s.CreadorId == usuario.Id && s.EstaActiva));

            if (!existeRelacion)
            {
                // Verificar si ya tienen una conversación previa
                var tieneConversacion = await _context.MensajesPrivados
                    .AnyAsync(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == id) ||
                                  (m.RemitenteId == id && m.DestinatarioId == usuario.Id));

                if (!tieneConversacion)
                {
                    _logger.LogWarning("Usuario {Usuario} intentó chatear sin suscripción con {Destinatario}",
                        usuario.UserName, destinatario.UserName);
                    TempData["Error"] = "Debes estar suscrito para enviar mensajes a este usuario.";
                    return RedirectToAction("Index");
                }
            }

            // Cargar mensajes con respuestas incluidas
            var mensajes = await _context.MensajesPrivados
                .Include(m => m.Remitente)
                .Include(m => m.Destinatario)
                .Include(m => m.MensajeRespondido)
                    .ThenInclude(mr => mr!.Remitente)
                .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == id) ||
                           (m.RemitenteId == id && m.DestinatarioId == usuario.Id))
                .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                           (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();

            _logger.LogInformation("Mensajes cargados: {Count}", mensajes.Count);

            // Marcar mensajes como leídos y guardar FechaLectura
            var mensajesNoLeidos = mensajes
                .Where(m => m.DestinatarioId == usuario.Id && !m.Leido)
                .ToList();

            if (mensajesNoLeidos.Any())
            {
                var ahora = DateTime.Now;
                var mensajeIds = new List<int>();
                foreach (var mensaje in mensajesNoLeidos)
                {
                    mensaje.Leido = true;
                    mensaje.FechaLectura = ahora;
                    mensajeIds.Add(mensaje.Id);
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marcados como leídos: {Count} mensajes", mensajesNoLeidos.Count);

                // Notificar via SignalR al remitente que sus mensajes fueron leídos
                await _hubContext.Clients.Group($"user_{id}").SendAsync("MensajesLeidos", new
                {
                    LectorId = usuario.Id,
                    MensajeIds = mensajeIds,
                    FechaLectura = ahora
                });
            }

            ViewBag.Mensajes = mensajes;
            ViewBag.Destinatario = destinatario;

            return View(usuario);
        }

        // ========================================
        // ENVIAR MENSAJE CON ARCHIVO (Principal)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensajeConArchivo(
            string destinatarioId,
            string? contenido,
            IFormFile? archivo,
            int? mensajeRespondidoId)
        {
            try
            {
                _logger.LogInformation("=== ENVIAR MENSAJE CON ARCHIVO - INICIO ===");

                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"message_send_{usuario.Id}";
                var rateLimitKeyIp = $"message_send_ip_{clientIp}";

                // Límite por IP: máximo 60 mensajes por minuto
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 60, TimeSpan.FromMinutes(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP MENSAJE: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuario.Id);
                    return Json(new { success = false, message = "Demasiados mensajes enviados. Espera un momento." });
                }

                // Límite por usuario: máximo 30 mensajes por minuto
                if (!_rateLimitService.IsAllowed(rateLimitKey, RateLimits.Messaging_MaxRequests, RateLimits.Messaging_Window))
                {
                    _logger.LogWarning("🚫 RATE LIMIT MENSAJE: Usuario {UserId} excedió límite - IP: {IP}", usuario.Id, clientIp);
                    return Json(new { success = false, message = "Estás enviando mensajes muy rápido. Espera un momento." });
                }

                if (string.IsNullOrWhiteSpace(destinatarioId))
                {
                    return Json(new { success = false, message = "Destinatario no especificado" });
                }

                // Validar que haya contenido o archivo
                if (string.IsNullOrWhiteSpace(contenido) && archivo == null)
                {
                    return Json(new { success = false, message = "Debes enviar un mensaje o un archivo" });
                }

                var destinatario = await _userManager.FindByIdAsync(destinatarioId);
                if (destinatario == null)
                {
                    return Json(new { success = false, message = "Destinatario no encontrado" });
                }

                // Verificar bloqueo
                var existeBloqueo = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == usuario.Id && b.BloqueadoId == destinatarioId) ||
                                  (b.BloqueadorId == destinatarioId && b.BloqueadoId == usuario.Id));

                if (existeBloqueo)
                {
                    return Json(new { success = false, message = "No puedes enviar mensajes a este usuario" });
                }

                // Verificar relación de suscripción o conversación previa
                var existeRelacion = await _context.Suscripciones
                    .AnyAsync(s => (s.FanId == usuario.Id && s.CreadorId == destinatarioId && s.EstaActiva) ||
                                  (s.FanId == destinatarioId && s.CreadorId == usuario.Id && s.EstaActiva));

                var tieneConversacion = await _context.MensajesPrivados
                    .AnyAsync(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == destinatarioId) ||
                                  (m.RemitenteId == destinatarioId && m.DestinatarioId == usuario.Id));

                if (!existeRelacion && !tieneConversacion)
                {
                    return Json(new { success = false, message = "Debes estar suscrito para iniciar una conversación" });
                }

                // Crear mensaje
                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = destinatarioId,
                    Contenido = contenido?.Trim() ?? "",
                    FechaEnvio = DateTime.Now,
                    Leido = false,
                    TipoMensaje = TipoMensaje.Texto
                };

                // Procesar archivo si existe
                if (archivo != null && archivo.Length > 0)
                {
                    if (archivo.Length > MaxFileSize)
                    {
                        return Json(new { success = false, message = "El archivo no puede superar 10 MB" });
                    }

                    var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();

                    // Determinar tipo de mensaje
                    if (AllowedImageExtensions.Contains(extension))
                    {
                        mensaje.TipoMensaje = TipoMensaje.Imagen;
                    }
                    else if (AllowedVideoExtensions.Contains(extension))
                    {
                        mensaje.TipoMensaje = TipoMensaje.Video;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Tipo de archivo no permitido. Solo imágenes y videos." });
                    }

                    // Guardar archivo
                    var nombreArchivo = $"{Guid.NewGuid()}{extension}";
                    var carpeta = Path.Combine(_environment.WebRootPath, "uploads", "mensajes", usuario.UserName ?? usuario.Id);

                    if (!Directory.Exists(carpeta))
                    {
                        Directory.CreateDirectory(carpeta);
                    }

                    var rutaCompleta = Path.Combine(carpeta, nombreArchivo);
                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await archivo.CopyToAsync(stream);
                    }

                    mensaje.RutaArchivo = $"/uploads/mensajes/{usuario.UserName ?? usuario.Id}/{nombreArchivo}";
                    mensaje.NombreArchivoOriginal = archivo.FileName;
                    mensaje.TamanoArchivo = archivo.Length;

                    _logger.LogInformation("Archivo guardado: {Ruta}", mensaje.RutaArchivo);
                }

                // Agregar respuesta si existe
                if (mensajeRespondidoId.HasValue && mensajeRespondidoId > 0)
                {
                    var mensajeOriginal = await _context.MensajesPrivados.FindAsync(mensajeRespondidoId);
                    if (mensajeOriginal != null)
                    {
                        mensaje.MensajeRespondidoId = mensajeRespondidoId;
                    }
                }

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Mensaje enviado. ID: {MensajeId}, Tipo: {Tipo}", mensaje.Id, mensaje.TipoMensaje);

                // Cargar datos del mensaje respondido si existe
                MensajePrivado? mensajeRespondido = null;
                if (mensaje.MensajeRespondidoId.HasValue)
                {
                    mensajeRespondido = await _context.MensajesPrivados
                        .Include(m => m.Remitente)
                        .FirstOrDefaultAsync(m => m.Id == mensaje.MensajeRespondidoId);
                }

                // Preparar DTO del mensaje - enviar timestamp para que el cliente formatee en su zona horaria
                var fechaUtc = mensaje.FechaEnvio.Kind == DateTimeKind.Utc
                    ? mensaje.FechaEnvio
                    : DateTime.SpecifyKind(mensaje.FechaEnvio, DateTimeKind.Local).ToUniversalTime();
                var timestamp = new DateTimeOffset(fechaUtc).ToUnixTimeMilliseconds();

                var mensajeDto = new
                {
                    id = mensaje.Id,
                    contenido = mensaje.Contenido,
                    fechaEnvioTimestamp = timestamp,
                    remitenteId = mensaje.RemitenteId,
                    remitenteNombre = usuario.NombreCompleto ?? usuario.UserName,
                    remitenteFoto = usuario.FotoPerfil,
                    tipoMensaje = (int)mensaje.TipoMensaje,
                    rutaArchivo = mensaje.RutaArchivo,
                    leido = false,
                    mensajeRespondido = mensajeRespondido != null ? new
                    {
                        id = mensajeRespondido.Id,
                        contenido = mensajeRespondido.Contenido,
                        remitenteNombre = mensajeRespondido.Remitente?.NombreCompleto ?? mensajeRespondido.Remitente?.UserName ?? "Usuario",
                        tipoMensaje = (int)mensajeRespondido.TipoMensaje
                    } : null
                };

                // Notificar via SignalR al destinatario
                await _hubContext.Clients.Group($"user_{destinatarioId}").SendAsync("RecibirMensaje", mensajeDto);

                return Json(new { success = true, mensaje = mensajeDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar mensaje con archivo");
                return Json(new { success = false, message = "Error al enviar el mensaje" });
            }
        }

        // ========================================
        // ENVIAR MENSAJE (Compatibilidad)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensaje(string destinatarioId, string contenido)
        {
            return await EnviarMensajeConArchivo(destinatarioId, contenido, null, null);
        }

        // ========================================
        // ELIMINAR CONVERSACIÓN
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarConversacion(string contactoId)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    _logger.LogWarning("Usuario no autenticado en EliminarConversacion");
                    return RedirectToAction("Login", "Account");
                }

                if (string.IsNullOrWhiteSpace(contactoId))
                {
                    _logger.LogWarning("ContactoId vacío en EliminarConversacion");
                    TempData["Error"] = "Contacto no especificado";
                    return RedirectToAction("Index");
                }

                _logger.LogInformation("Eliminando conversación entre {Usuario} y {Contacto}",
                    usuario.UserName, contactoId);

                var mensajes = await _context.MensajesPrivados
                    .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                               (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                    .ToListAsync();

                foreach (var mensaje in mensajes)
                {
                    if (mensaje.RemitenteId == usuario.Id)
                    {
                        mensaje.EliminadoPorRemitente = true;
                    }
                    else
                    {
                        mensaje.EliminadoPorDestinatario = true;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Conversación eliminada. {Count} mensajes afectados", mensajes.Count);
                TempData["Success"] = "Conversación eliminada correctamente";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar conversación");
                TempData["Error"] = "Error al eliminar la conversación";
                return RedirectToAction("Index");
            }
        }

        // ========================================
        // CARGAR MENSAJES (AJAX)
        // ========================================

        [HttpGet]
        public async Task<IActionResult> CargarMensajes(string contactoId)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                if (string.IsNullOrWhiteSpace(contactoId))
                {
                    return Json(new { success = false, message = "Contacto no especificado" });
                }

                var mensajesDb = await _context.MensajesPrivados
                    .Include(m => m.Remitente)
                    .Include(m => m.MensajeRespondido)
                        .ThenInclude(mr => mr!.Remitente)
                    .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                               (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                    .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                               (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                    .OrderBy(m => m.FechaEnvio)
                    .ToListAsync();

                // Convertir a DTOs con timestamp para que el cliente formatee en su zona horaria
                var mensajes = mensajesDb.Select(m => {
                    var fechaUtc = m.FechaEnvio.Kind == DateTimeKind.Utc
                        ? m.FechaEnvio
                        : DateTime.SpecifyKind(m.FechaEnvio, DateTimeKind.Local).ToUniversalTime();
                    return new
                    {
                        id = m.Id,
                        remitenteId = m.RemitenteId,
                        remitenteNombre = m.Remitente != null ? (m.Remitente.NombreCompleto ?? m.Remitente.UserName) : "Usuario",
                        contenido = m.Contenido,
                        fechaEnvioTimestamp = new DateTimeOffset(fechaUtc).ToUnixTimeMilliseconds(),
                        leido = m.Leido,
                        fechaLectura = m.FechaLectura,
                        tipoMensaje = (int)m.TipoMensaje,
                        rutaArchivo = m.RutaArchivo,
                        mensajeRespondido = m.MensajeRespondido != null ? new
                        {
                            id = m.MensajeRespondido.Id,
                            contenido = m.MensajeRespondido.Contenido,
                            remitenteNombre = m.MensajeRespondido.Remitente != null
                                ? (m.MensajeRespondido.Remitente.NombreCompleto ?? m.MensajeRespondido.Remitente.UserName)
                                : "Usuario",
                            tipoMensaje = (int)m.MensajeRespondido.TipoMensaje
                        } : null
                    };
                }).ToList();

                _logger.LogInformation("Cargados {Count} mensajes para contacto {ContactoId}",
                    mensajes.Count, contactoId);

                return Json(new { success = true, mensajes, usuarioId = usuario.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar mensajes");
                return Json(new { success = false, message = "Error al cargar mensajes" });
            }
        }

        // ========================================
        // MARCAR COMO LEÍDO (AJAX)
        // ========================================

        [HttpPost]
        public async Task<IActionResult> MarcarComoLeido(int mensajeId)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                var mensaje = await _context.MensajesPrivados
                    .FirstOrDefaultAsync(m => m.Id == mensajeId && m.DestinatarioId == usuario.Id);

                if (mensaje == null)
                {
                    return Json(new { success = false, message = "Mensaje no encontrado" });
                }

                var ahora = DateTime.Now;
                mensaje.Leido = true;
                mensaje.FechaLectura = ahora;
                await _context.SaveChangesAsync();

                // Notificar al remitente via SignalR
                await _hubContext.Clients.Group($"user_{mensaje.RemitenteId}").SendAsync("MensajesLeidos", new
                {
                    LectorId = usuario.Id,
                    MensajeIds = new[] { mensajeId },
                    FechaLectura = ahora
                });

                _logger.LogInformation("Mensaje {MensajeId} marcado como leído", mensajeId);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar mensaje como leído");
                return Json(new { success = false, message = "Error al marcar como leído" });
            }
        }

        // ========================================
        // MARCAR MÚLTIPLES COMO LEÍDOS (AJAX)
        // ========================================

        [HttpPost]
        public async Task<IActionResult> MarcarMensajesComoLeidos([FromBody] MarcarLeidosRequest request)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                if (request?.MensajeIds == null || !request.MensajeIds.Any())
                {
                    return Json(new { success = true }); // Nada que marcar
                }

                var mensajes = await _context.MensajesPrivados
                    .Where(m => request.MensajeIds.Contains(m.Id) && m.DestinatarioId == usuario.Id && !m.Leido)
                    .ToListAsync();

                if (!mensajes.Any())
                {
                    return Json(new { success = true });
                }

                var ahora = DateTime.Now;
                var remitenteIds = new HashSet<string>();

                foreach (var mensaje in mensajes)
                {
                    mensaje.Leido = true;
                    mensaje.FechaLectura = ahora;
                    remitenteIds.Add(mensaje.RemitenteId);
                }

                await _context.SaveChangesAsync();

                // Notificar a cada remitente via SignalR
                foreach (var remitenteId in remitenteIds)
                {
                    var idsDelRemitente = mensajes.Where(m => m.RemitenteId == remitenteId).Select(m => m.Id).ToArray();
                    await _hubContext.Clients.Group($"user_{remitenteId}").SendAsync("MensajesLeidos", new
                    {
                        LectorId = usuario.Id,
                        MensajeIds = idsDelRemitente,
                        FechaLectura = ahora
                    });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar mensajes como leídos");
                return Json(new { success = false, message = "Error al marcar como leídos" });
            }
        }

        // ========================================
        // ENVIAR MENSAJE DIRECTO (desde perfil)
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensajeDirecto([FromBody] EnviarMensajeDirectoRequest request)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"message_send_{usuario.Id}";
                var rateLimitKeyIp = $"message_send_ip_{clientIp}";

                // Límite por IP
                if (!_rateLimitService.IsAllowed(rateLimitKeyIp, 60, TimeSpan.FromMinutes(1)))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP MENSAJE DIRECTO: IP {IP} excedió límite - Usuario: {UserId}", clientIp, usuario.Id);
                    return Json(new { success = false, message = "Demasiados mensajes enviados. Espera un momento." });
                }

                // Límite por usuario
                if (!_rateLimitService.IsAllowed(rateLimitKey, RateLimits.Messaging_MaxRequests, RateLimits.Messaging_Window))
                {
                    _logger.LogWarning("🚫 RATE LIMIT MENSAJE DIRECTO: Usuario {UserId} excedió límite - IP: {IP}", usuario.Id, clientIp);
                    return Json(new { success = false, message = "Estás enviando mensajes muy rápido. Espera un momento." });
                }

                if (string.IsNullOrEmpty(request?.ReceptorId))
                {
                    return Json(new { success = false, message = "Receptor no especificado" });
                }

                if (string.IsNullOrWhiteSpace(request.Mensaje))
                {
                    return Json(new { success = false, message = "El mensaje no puede estar vacío" });
                }

                var receptor = await _userManager.FindByIdAsync(request.ReceptorId);
                if (receptor == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado" });
                }

                if (receptor.Id == usuario.Id)
                {
                    return Json(new { success = false, message = "No puedes enviarte un mensaje a ti mismo" });
                }

                // Verificar si hay bloqueo
                var hayBloqueo = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == usuario.Id && b.BloqueadoId == receptor.Id) ||
                                   (b.BloqueadorId == receptor.Id && b.BloqueadoId == usuario.Id));

                if (hayBloqueo)
                {
                    return Json(new { success = false, message = "No puedes enviar mensajes a este usuario" });
                }

                // Si hay propina, procesarla con transacción atómica
                if (request.Monto > 0)
                {
                    // Verificar si el receptor puede recibir propinas (tiene contenido LadoB o es creador verificado)
                    var puedeRecibirPropinas = await _context.Contenidos
                        .AnyAsync(c => c.UsuarioId == receptor.Id
                                    && c.TipoLado == TipoLado.LadoB
                                    && c.EstaActivo
                                    && !c.EsBorrador)
                        || receptor.CreadorVerificado;

                    if (!puedeRecibirPropinas)
                    {
                        return Json(new { success = false, message = "Este usuario no puede recibir propinas" });
                    }

                    if (usuario.Saldo < request.Monto)
                    {
                        return Json(new
                        {
                            success = false,
                            requiereRecarga = true,
                            message = $"Saldo insuficiente. Necesitas ${request.Monto - usuario.Saldo:N2} más."
                        });
                    }

                    // Calcular comisión (10%)
                    var comision = request.Monto * 0.10m;
                    var gananciaReceptor = request.Monto - comision;

                    // TRANSACCIÓN ATÓMICA - todas las operaciones o ninguna
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        // Descontar saldo
                        usuario.Saldo -= request.Monto;

                        // Agregar al receptor
                        receptor.Saldo += gananciaReceptor;
                        receptor.TotalGanancias += gananciaReceptor;

                        // Registrar tip
                        var tip = new Tip
                        {
                            FanId = usuario.Id,
                            CreadorId = receptor.Id,
                            Monto = request.Monto,
                            Mensaje = request.Mensaje,
                            FechaEnvio = DateTime.Now
                        };
                        _context.Tips.Add(tip);

                        // Registrar transacciones
                        _context.Transacciones.Add(new Transaccion
                        {
                            UsuarioId = usuario.Id,
                            TipoTransaccion = TipoTransaccion.Tip,
                            Monto = -request.Monto,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = $"Propina enviada a {receptor.NombreCompleto}"
                        });

                        _context.Transacciones.Add(new Transaccion
                        {
                            UsuarioId = receptor.Id,
                            TipoTransaccion = TipoTransaccion.IngresoPropina,
                            Monto = gananciaReceptor,
                            Comision = comision,
                            MontoNeto = gananciaReceptor,
                            FechaTransaccion = DateTime.Now,
                            Descripcion = $"Propina de @{usuario.UserName}"
                        });

                        // Crear el mensaje con indicador de propina
                        var mensaje = new MensajePrivado
                        {
                            RemitenteId = usuario.Id,
                            DestinatarioId = receptor.Id,
                            Contenido = $"💰 Propina de ${request.Monto:N0}\n\n{request.Mensaje.Trim()}",
                            FechaEnvio = DateTime.Now,
                            Leido = false
                        };

                        _context.MensajesPrivados.Add(mensaje);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("Mensaje con propina enviado de {Remitente} a {Destinatario}, monto: ${Monto} (comisión: ${Comision})",
                            usuario.UserName, receptor.UserName, request.Monto, comision);

                        return Json(new { success = true });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error en transacción de propina");
                        return Json(new { success = false, message = "Error al procesar la propina" });
                    }
                }

                // Mensaje sin propina (no requiere transacción)
                var mensajeSinPropina = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = receptor.Id,
                    Contenido = request.Mensaje.Trim(),
                    FechaEnvio = DateTime.Now,
                    Leido = false
                };

                _context.MensajesPrivados.Add(mensajeSinPropina);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mensaje enviado de {Remitente} a {Destinatario}",
                    usuario.UserName, receptor.UserName);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar mensaje directo");
                return Json(new { success = false, message = "Error al enviar el mensaje" });
            }
        }
    }

    // ========================================
    // VIEW MODEL
    // ========================================

    public class EnviarMensajeDirectoRequest
    {
        public string ReceptorId { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public decimal Monto { get; set; } = 0;
    }

    public class ConversacionViewModel
    {
        public ApplicationUser Contacto { get; set; } = null!;
        public MensajePrivado? UltimoMensaje { get; set; }
        public int MensajesNoLeidos { get; set; }
        public bool TieneConversacion { get; set; }
    }

    public class MarcarLeidosRequest
    {
        public int[] MensajeIds { get; set; } = Array.Empty<int>();
    }
}