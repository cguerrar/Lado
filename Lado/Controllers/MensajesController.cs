using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers
{
    [Authorize]
    public class MensajesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MensajesController> _logger;

        public MensajesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MensajesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
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

            // Obtener IDs de conversaciones existentes
            var conversacionesExistentes = await _context.MensajesPrivados
                .Where(m => m.RemitenteId == usuario.Id || m.DestinatarioId == usuario.Id)
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
                    .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                               (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
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

            // Cargar mensajes
            var mensajes = await _context.MensajesPrivados
                .Include(m => m.Remitente)
                .Include(m => m.Destinatario)
                .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == id) ||
                           (m.RemitenteId == id && m.DestinatarioId == usuario.Id))
                .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                           (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                .OrderBy(m => m.FechaEnvio)
                .ToListAsync();

            _logger.LogInformation("Mensajes cargados: {Count}", mensajes.Count);

            // Marcar mensajes como leídos
            var mensajesNoLeidos = mensajes
                .Where(m => m.DestinatarioId == usuario.Id && !m.Leido)
                .ToList();

            if (mensajesNoLeidos.Any())
            {
                foreach (var mensaje in mensajesNoLeidos)
                {
                    mensaje.Leido = true;
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Marcados como leídos: {Count} mensajes", mensajesNoLeidos.Count);
            }

            ViewBag.Mensajes = mensajes;
            ViewBag.Destinatario = destinatario;

            return View(usuario);
        }

        // ========================================
        // ENVIAR MENSAJE
        // ========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensaje(string destinatarioId, string contenido)
        {
            try
            {
                _logger.LogInformation("=== ENVIAR MENSAJE - INICIO ===");
                _logger.LogInformation("DestinatarioId: {DestinatarioId}", destinatarioId);

                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    _logger.LogError("Usuario no autenticado");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                _logger.LogInformation("Usuario: {Username} (ID: {UserId})", usuario.UserName, usuario.Id);

                // Validaciones
                if (string.IsNullOrWhiteSpace(contenido))
                {
                    _logger.LogWarning("Contenido vacío");
                    return Json(new { success = false, message = "El mensaje no puede estar vacío" });
                }

                if (string.IsNullOrWhiteSpace(destinatarioId))
                {
                    _logger.LogWarning("DestinatarioId vacío");
                    return Json(new { success = false, message = "Destinatario no especificado" });
                }

                var destinatario = await _userManager.FindByIdAsync(destinatarioId);
                if (destinatario == null)
                {
                    _logger.LogError("Destinatario no encontrado: {DestinatarioId}", destinatarioId);
                    return Json(new { success = false, message = "Destinatario no encontrado" });
                }

                _logger.LogInformation("Destinatario: {Username}", destinatario.UserName);

                // ========================================
                // VERIFICAR BLOQUEO
                // ========================================
                var existeBloqueo = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == usuario.Id && b.BloqueadoId == destinatarioId) ||
                                  (b.BloqueadorId == destinatarioId && b.BloqueadoId == usuario.Id));

                if (existeBloqueo)
                {
                    _logger.LogWarning("Intento de mensaje a usuario bloqueado");
                    return Json(new { success = false, message = "No puedes enviar mensajes a este usuario" });
                }

                // ⭐ CAMBIO: Verificar relación de suscripción (bidireccional)
                var existeRelacion = await _context.Suscripciones
                    .AnyAsync(s => (s.FanId == usuario.Id && s.CreadorId == destinatarioId && s.EstaActiva) ||
                                  (s.FanId == destinatarioId && s.CreadorId == usuario.Id && s.EstaActiva));

                // Verificar si ya tienen conversación previa
                var tieneConversacion = await _context.MensajesPrivados
                    .AnyAsync(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == destinatarioId) ||
                                  (m.RemitenteId == destinatarioId && m.DestinatarioId == usuario.Id));

                if (!existeRelacion && !tieneConversacion)
                {
                    _logger.LogWarning("Sin relación de suscripción ni conversación previa");
                    return Json(new
                    {
                        success = false,
                        message = "Debes estar suscrito para iniciar una conversación con este usuario"
                    });
                }

                // Crear mensaje
                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = destinatarioId,
                    Contenido = contenido.Trim(),
                    FechaEnvio = DateTime.Now,
                    Leido = false,
                    EliminadoPorRemitente = false,
                    EliminadoPorDestinatario = false
                };

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ Mensaje enviado exitosamente. ID: {MensajeId}", mensaje.Id);

                return Json(new
                {
                    success = true,
                    mensaje = new
                    {
                        id = mensaje.Id,
                        contenido = mensaje.Contenido,
                        fechaEnvio = mensaje.FechaEnvio.ToString("HH:mm"),
                        fechaCompleta = mensaje.FechaEnvio.ToString("dd/MM/yyyy HH:mm"),
                        remitenteId = mensaje.RemitenteId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar mensaje");
                return Json(new
                {
                    success = false,
                    message = "Error al enviar el mensaje. Por favor intenta nuevamente."
                });
            }
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
                    _logger.LogWarning("Usuario no autenticado en CargarMensajes");
                    return Json(new { success = false, message = "Usuario no autenticado" });
                }

                if (string.IsNullOrWhiteSpace(contactoId))
                {
                    _logger.LogWarning("ContactoId vacío en CargarMensajes");
                    return Json(new { success = false, message = "Contacto no especificado" });
                }

                var mensajes = await _context.MensajesPrivados
                    .Where(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId) ||
                               (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id))
                    .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                               (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                    .OrderBy(m => m.FechaEnvio)
                    .Select(m => new
                    {
                        id = m.Id,
                        remitenteId = m.RemitenteId,
                        contenido = m.Contenido,
                        fechaEnvio = m.FechaEnvio.ToString("HH:mm"),
                        fechaCompleta = m.FechaEnvio.ToString("dd/MM/yyyy HH:mm"),
                        leido = m.Leido
                    })
                    .ToListAsync();

                _logger.LogInformation("Cargados {Count} mensajes para contacto {ContactoId}",
                    mensajes.Count, contactoId);

                return Json(new { success = true, mensajes });
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

                mensaje.Leido = true;
                await _context.SaveChangesAsync();

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
        // ENVIAR MENSAJE DIRECTO (desde perfil)
        // ========================================

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> EnviarMensajeDirecto([FromBody] EnviarMensajeDirectoRequest request)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Json(new { success = false, message = "Usuario no autenticado" });
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

                // Si hay propina, procesarla
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

                    // Descontar saldo
                    usuario.Saldo -= request.Monto;

                    // Calcular comisión (10%)
                    var comision = request.Monto * 0.10m;
                    var gananciaReceptor = request.Monto - comision;

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
                }

                // Crear el mensaje
                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = receptor.Id,
                    Contenido = request.Mensaje.Trim(),
                    FechaEnvio = DateTime.Now,
                    Leido = false
                };

                // Si tiene propina, agregar indicador al mensaje
                if (request.Monto > 0)
                {
                    mensaje.Contenido = $"💰 Propina de ${request.Monto:N0}\n\n{mensaje.Contenido}";
                }

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mensaje enviado de {Remitente} a {Destinatario}, propina: ${Monto}",
                    usuario.UserName, receptor.UserName, request.Monto);

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
        public ApplicationUser Contacto { get; set; }
        public MensajePrivado UltimoMensaje { get; set; }
        public int MensajesNoLeidos { get; set; }
        public bool TieneConversacion { get; set; }
    }
}