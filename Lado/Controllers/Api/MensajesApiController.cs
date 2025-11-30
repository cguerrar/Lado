using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;

namespace Lado.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/mensajes")]
    public class MensajesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MensajesApiController> _logger;

        public MensajesApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<MensajesApiController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Obtener lista de conversaciones para el chat flotante
        /// </summary>
        [HttpGet("conversaciones")]
        public async Task<IActionResult> GetConversaciones()
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                // Obtener IDs de usuarios con conversaciones
                var conversacionesIds = await _context.MensajesPrivados
                    .Where(m => (m.RemitenteId == usuario.Id && !m.EliminadoPorRemitente) ||
                               (m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario))
                    .Select(m => m.RemitenteId == usuario.Id ? m.DestinatarioId : m.RemitenteId)
                    .Distinct()
                    .ToListAsync();

                var conversaciones = new List<object>();

                foreach (var contactoId in conversacionesIds)
                {
                    var contacto = await _userManager.FindByIdAsync(contactoId);
                    if (contacto == null) continue;

                    var ultimoMensaje = await _context.MensajesPrivados
                        .Where(m => ((m.RemitenteId == usuario.Id && m.DestinatarioId == contactoId && !m.EliminadoPorRemitente) ||
                                    (m.RemitenteId == contactoId && m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario)))
                        .OrderByDescending(m => m.FechaEnvio)
                        .FirstOrDefaultAsync();

                    var noLeidos = await _context.MensajesPrivados
                        .Where(m => m.RemitenteId == contactoId &&
                                   m.DestinatarioId == usuario.Id &&
                                   !m.Leido &&
                                   !m.EliminadoPorDestinatario)
                        .CountAsync();

                    conversaciones.Add(new
                    {
                        usuarioId = contacto.Id,
                        nombreUsuario = contacto.NombreCompleto ?? contacto.UserName,
                        seudonimo = contacto.Seudonimo ?? contacto.UserName,
                        fotoPerfil = contacto.FotoPerfil,
                        ultimoMensaje = ultimoMensaje?.Contenido?.Length > 50
                            ? ultimoMensaje.Contenido.Substring(0, 47) + "..."
                            : ultimoMensaje?.Contenido,
                        fechaUltimoMensaje = ultimoMensaje?.FechaEnvio,
                        noLeidos = noLeidos,
                        enLinea = false // Por ahora no implementamos estado en línea
                    });
                }

                // Ordenar por fecha de último mensaje
                var ordenadas = conversaciones
                    .OrderByDescending(c => ((dynamic)c).noLeidos > 0)
                    .ThenByDescending(c => ((dynamic)c).fechaUltimoMensaje)
                    .ToList();

                return Ok(ordenadas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener conversaciones");
                return StatusCode(500, new { error = "Error al cargar conversaciones" });
            }
        }

        /// <summary>
        /// Obtener mensajes de una conversación específica
        /// </summary>
        [HttpGet("conversacion/{userId}")]
        public async Task<IActionResult> GetMensajes(string userId)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return BadRequest(new { error = "ID de usuario requerido" });
                }

                var mensajes = await _context.MensajesPrivados
                    .Where(m => ((m.RemitenteId == usuario.Id && m.DestinatarioId == userId && !m.EliminadoPorRemitente) ||
                                (m.RemitenteId == userId && m.DestinatarioId == usuario.Id && !m.EliminadoPorDestinatario)))
                    .OrderBy(m => m.FechaEnvio)
                    .Select(m => new
                    {
                        id = m.Id,
                        remitenteId = m.RemitenteId,
                        contenido = m.Contenido,
                        fechaEnvio = m.FechaEnvio,
                        leido = m.Leido
                    })
                    .ToListAsync();

                return Ok(mensajes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener mensajes");
                return StatusCode(500, new { error = "Error al cargar mensajes" });
            }
        }

        /// <summary>
        /// Enviar un mensaje
        /// </summary>
        [HttpPost("enviar")]
        public async Task<IActionResult> EnviarMensaje([FromBody] EnviarMensajeRequest request)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                if (string.IsNullOrWhiteSpace(request.DestinatarioId))
                {
                    return BadRequest(new { error = "Destinatario requerido" });
                }

                if (string.IsNullOrWhiteSpace(request.Contenido))
                {
                    return BadRequest(new { error = "El mensaje no puede estar vacío" });
                }

                var destinatario = await _userManager.FindByIdAsync(request.DestinatarioId);
                if (destinatario == null)
                {
                    return NotFound(new { error = "Destinatario no encontrado" });
                }

                // Verificar si existe relación de suscripción o conversación previa
                var existeRelacion = await _context.Suscripciones
                    .AnyAsync(s => (s.FanId == usuario.Id && s.CreadorId == request.DestinatarioId && s.EstaActiva) ||
                                  (s.FanId == request.DestinatarioId && s.CreadorId == usuario.Id && s.EstaActiva));

                var tieneConversacion = await _context.MensajesPrivados
                    .AnyAsync(m => (m.RemitenteId == usuario.Id && m.DestinatarioId == request.DestinatarioId) ||
                                  (m.RemitenteId == request.DestinatarioId && m.DestinatarioId == usuario.Id));

                if (!existeRelacion && !tieneConversacion)
                {
                    return BadRequest(new { error = "Debes estar suscrito para iniciar una conversación con este usuario" });
                }

                var mensaje = new MensajePrivado
                {
                    RemitenteId = usuario.Id,
                    DestinatarioId = request.DestinatarioId,
                    Contenido = request.Contenido.Trim(),
                    FechaEnvio = DateTime.Now,
                    Leido = false,
                    EliminadoPorRemitente = false,
                    EliminadoPorDestinatario = false
                };

                _context.MensajesPrivados.Add(mensaje);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Mensaje enviado: {MensajeId} de {Remitente} a {Destinatario}",
                    mensaje.Id, usuario.UserName, destinatario.UserName);

                return Ok(new
                {
                    success = true,
                    mensaje = new
                    {
                        id = mensaje.Id,
                        remitenteId = mensaje.RemitenteId,
                        contenido = mensaje.Contenido,
                        fechaEnvio = mensaje.FechaEnvio
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar mensaje");
                return StatusCode(500, new { error = "Error al enviar el mensaje" });
            }
        }

        /// <summary>
        /// Marcar mensajes de una conversación como leídos
        /// </summary>
        [HttpPost("marcar-leidos/{userId}")]
        public async Task<IActionResult> MarcarComoLeidos(string userId)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                var mensajesNoLeidos = await _context.MensajesPrivados
                    .Where(m => m.RemitenteId == userId &&
                               m.DestinatarioId == usuario.Id &&
                               !m.Leido)
                    .ToListAsync();

                foreach (var mensaje in mensajesNoLeidos)
                {
                    mensaje.Leido = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Marcados {Count} mensajes como leídos para usuario {Usuario}",
                    mensajesNoLeidos.Count, usuario.UserName);

                return Ok(new { success = true, marcados = mensajesNoLeidos.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar mensajes como leídos");
                return StatusCode(500, new { error = "Error al marcar como leídos" });
            }
        }

        /// <summary>
        /// Obtener el contador de mensajes no leídos
        /// </summary>
        [HttpGet("no-leidos-count")]
        public async Task<IActionResult> GetNoLeidosCount()
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                var count = await _context.MensajesPrivados
                    .Where(m => m.DestinatarioId == usuario.Id &&
                               !m.Leido &&
                               !m.EliminadoPorDestinatario)
                    .CountAsync();

                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener contador de no leídos");
                return StatusCode(500, new { error = "Error al obtener contador" });
            }
        }

        /// <summary>
        /// Buscar usuarios para iniciar conversación
        /// </summary>
        [HttpGet("buscar-usuarios")]
        public async Task<IActionResult> BuscarUsuarios([FromQuery] string query)
        {
            try
            {
                var usuario = await _userManager.GetUserAsync(User);
                if (usuario == null)
                {
                    return Unauthorized(new { error = "Usuario no autenticado" });
                }

                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Ok(new List<object>());
                }

                // Buscar usuarios que tengan suscripción activa con el usuario actual
                var usuariosConSuscripcion = await _context.Suscripciones
                    .Where(s => (s.FanId == usuario.Id || s.CreadorId == usuario.Id) && s.EstaActiva)
                    .Select(s => s.FanId == usuario.Id ? s.CreadorId : s.FanId)
                    .Distinct()
                    .ToListAsync();

                var usuarios = await _userManager.Users
                    .Where(u => u.Id != usuario.Id &&
                               usuariosConSuscripcion.Contains(u.Id) &&
                               (u.NombreCompleto.Contains(query) ||
                                u.UserName.Contains(query) ||
                                (u.Seudonimo != null && u.Seudonimo.Contains(query))))
                    .Take(10)
                    .Select(u => new
                    {
                        id = u.Id,
                        nombre = u.NombreCompleto ?? u.UserName,
                        seudonimo = u.Seudonimo ?? u.UserName,
                        fotoPerfil = u.FotoPerfil
                    })
                    .ToListAsync();

                return Ok(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios");
                return StatusCode(500, new { error = "Error al buscar usuarios" });
            }
        }
    }

    /// <summary>
    /// Request model para enviar mensaje
    /// </summary>
    public class EnviarMensajeRequest
    {
        public string DestinatarioId { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
    }
}
