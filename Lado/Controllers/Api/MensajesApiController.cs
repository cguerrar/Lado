using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Lado.Data;
using Lado.Models;
using Lado.Hubs;
using Lado.DTOs.Common;
using Lado.DTOs.Usuario;
using Lado.Services;
using System.Security.Claims;

namespace Lado.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MensajesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<MensajesApiController> _logger;
        private readonly IRateLimitService _rateLimitService;

        public MensajesApiController(
            ApplicationDbContext context,
            IHubContext<ChatHub> hubContext,
            ILogger<MensajesApiController> logger,
            IRateLimitService rateLimitService)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _rateLimitService = rateLimitService;
        }

        /// <summary>
        /// Obtener lista de conversaciones
        /// </summary>
        [HttpGet("conversaciones")]
        public async Task<ActionResult<ApiResponse<List<ConversacionDto>>>> GetConversaciones()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<List<ConversacionDto>>.Fail("No autenticado"));
                }

                var bloqueadosIds = await _context.BloqueosUsuarios
                    .Where(b => b.BloqueadorId == userId || b.BloqueadoId == userId)
                    .Select(b => b.BloqueadorId == userId ? b.BloqueadoId : b.BloqueadorId)
                    .ToListAsync();

                var mensajes = await _context.ChatMensajes
                    .Include(m => m.Remitente)
                    .Include(m => m.Destinatario)
                    .Where(m => (m.RemitenteId == userId || m.DestinatarioId == userId) &&
                               !bloqueadosIds.Contains(m.RemitenteId) &&
                               !bloqueadosIds.Contains(m.DestinatarioId))
                    .OrderByDescending(m => m.FechaEnvio)
                    .ToListAsync();

                var conversaciones = mensajes
                    .GroupBy(m => m.RemitenteId == userId ? m.DestinatarioId : m.RemitenteId)
                    .Select(g =>
                    {
                        var ultimoMensaje = g.First();
                        var otroUsuario = ultimoMensaje.RemitenteId == userId
                            ? ultimoMensaje.Destinatario
                            : ultimoMensaje.Remitente;

                        return new ConversacionDto
                        {
                            Usuario = new UsuarioDto
                            {
                                Id = otroUsuario.Id,
                                UserName = otroUsuario.UserName ?? "",
                                NombreCompleto = otroUsuario.NombreCompleto ?? "",
                                FotoPerfil = otroUsuario.FotoPerfil,
                                EsCreador = otroUsuario.EsCreador,
                                EstaVerificado = otroUsuario.CreadorVerificado
                            },
                            UltimoMensaje = ultimoMensaje.Mensaje,
                            FechaUltimoMensaje = ultimoMensaje.FechaEnvio,
                            TiempoRelativo = GetTiempoRelativo(ultimoMensaje.FechaEnvio),
                            MensajesNoLeidos = g.Count(m => m.DestinatarioId == userId && !m.Leido),
                            EsMio = ultimoMensaje.RemitenteId == userId
                        };
                    })
                    .OrderByDescending(c => c.FechaUltimoMensaje)
                    .ToList();

                return Ok(ApiResponse<List<ConversacionDto>>.Ok(conversaciones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo conversaciones");
                return StatusCode(500, ApiResponse<List<ConversacionDto>>.Fail("Error interno del servidor"));
            }
        }

        [HttpGet("conversacion/{otroUserId}")]
        public async Task<ActionResult<PaginatedResponse<MensajeDto>>> GetMensajes(
            string otroUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            // Limitar paginación para prevenir DoS
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var bloqueado = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == userId && b.BloqueadoId == otroUserId) ||
                                  (b.BloqueadorId == otroUserId && b.BloqueadoId == userId));
                if (bloqueado)
                {
                    return NotFound(ApiResponse.Fail("Conversacion no disponible"));
                }

                var query = _context.ChatMensajes
                    .Include(m => m.Remitente)
                    .Where(m => (m.RemitenteId == userId && m.DestinatarioId == otroUserId) ||
                               (m.RemitenteId == otroUserId && m.DestinatarioId == userId));

                var totalItems = await query.CountAsync();

                var mensajes = await query
                    .OrderByDescending(m => m.FechaEnvio)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new MensajeDto
                    {
                        Id = m.Id,
                        Contenido = m.Mensaje,
                        FechaEnvio = m.FechaEnvio,
                        TiempoRelativo = GetTiempoRelativo(m.FechaEnvio),
                        Leido = m.Leido,
                        EsMio = m.RemitenteId == userId,
                        Remitente = new UsuarioDto
                        {
                            Id = m.Remitente!.Id,
                            UserName = m.Remitente.UserName ?? "",
                            NombreCompleto = m.Remitente.NombreCompleto ?? "",
                            FotoPerfil = m.Remitente.FotoPerfil,
                            EsCreador = m.Remitente.EsCreador,
                            EstaVerificado = m.Remitente.CreadorVerificado
                        }
                    })
                    .ToListAsync();

                var noLeidos = await _context.ChatMensajes
                    .Where(m => m.RemitenteId == otroUserId && m.DestinatarioId == userId && !m.Leido)
                    .ToListAsync();

                foreach (var mensaje in noLeidos)
                {
                    mensaje.Leido = true;
                }

                if (noLeidos.Any())
                {
                    await _context.SaveChangesAsync();
                }

                mensajes.Reverse();
                return Ok(PaginatedResponse<MensajeDto>.Create(mensajes, totalItems, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo mensajes");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        [HttpPost("enviar")]
        public async Task<ActionResult<ApiResponse<MensajeDto>>> EnviarMensaje([FromBody] EnviarMensajeRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<MensajeDto>.Fail("No autenticado"));
                }

                // ========================================
                // 🚫 RATE LIMITING - Prevenir abuso
                // ========================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var rateLimitKey = $"api_message_send_{userId}";
                var rateLimitKeyIp = $"api_message_send_ip_{clientIp}";

                // Límite por IP: máximo 60 mensajes por minuto
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKeyIp, 60, TimeSpan.FromMinutes(1),
                    TipoAtaque.SpamMensajes, "/api/Mensajes/enviar", userId))
                {
                    _logger.LogWarning("🚨 RATE LIMIT IP API MENSAJE: IP {IP} excedió límite - Usuario: {UserId}", clientIp, userId);
                    return StatusCode(429, ApiResponse<MensajeDto>.Fail("Demasiadas solicitudes. Espera un momento."));
                }

                // Límite por usuario: máximo 30 mensajes por minuto
                if (!await _rateLimitService.IsAllowedAsync(clientIp, rateLimitKey, RateLimits.Messaging_MaxRequests, RateLimits.Messaging_Window,
                    TipoAtaque.SpamMensajes, "/api/Mensajes/enviar", userId))
                {
                    _logger.LogWarning("🚫 RATE LIMIT API MENSAJE: Usuario {UserId} excedió límite - IP: {IP}", userId, clientIp);
                    return StatusCode(429, ApiResponse<MensajeDto>.Fail("Estás enviando mensajes muy rápido. Espera un momento."));
                }

                if (string.IsNullOrWhiteSpace(request.Mensaje))
                {
                    return BadRequest(ApiResponse<MensajeDto>.Fail("El mensaje no puede estar vacio"));
                }

                var destinatario = await _context.Users.FindAsync(request.DestinatarioId);
                if (destinatario == null || !destinatario.EstaActivo)
                {
                    return NotFound(ApiResponse<MensajeDto>.Fail("Destinatario no encontrado"));
                }

                var bloqueado = await _context.BloqueosUsuarios
                    .AnyAsync(b => (b.BloqueadorId == userId && b.BloqueadoId == request.DestinatarioId) ||
                                  (b.BloqueadorId == request.DestinatarioId && b.BloqueadoId == userId));
                if (bloqueado)
                {
                    return BadRequest(ApiResponse<MensajeDto>.Fail("No puedes enviar mensajes a este usuario"));
                }

                var remitente = await _context.Users.FindAsync(userId);
                if (remitente == null)
                {
                    return NotFound(ApiResponse<MensajeDto>.Fail("Usuario no encontrado"));
                }

                var mensaje = new ChatMensaje
                {
                    RemitenteId = userId,
                    DestinatarioId = request.DestinatarioId,
                    Mensaje = request.Mensaje?.Trim() ?? "",
                    FechaEnvio = DateTime.UtcNow,
                    Leido = false
                };

                _context.ChatMensajes.Add(mensaje);
                await _context.SaveChangesAsync();

                var dto = new MensajeDto
                {
                    Id = mensaje.Id,
                    Contenido = mensaje.Mensaje,
                    FechaEnvio = mensaje.FechaEnvio,
                    TiempoRelativo = "ahora",
                    Leido = false,
                    EsMio = true,
                    Remitente = new UsuarioDto
                    {
                        Id = remitente.Id,
                        UserName = remitente.UserName ?? "",
                        NombreCompleto = remitente.NombreCompleto ?? "",
                        FotoPerfil = remitente.FotoPerfil,
                        EsCreador = remitente.EsCreador,
                        EstaVerificado = remitente.CreadorVerificado
                    }
                };

                try
                {
                    await _hubContext.Clients.User(request.DestinatarioId)
                        .SendAsync("RecibirMensaje", dto);
                }
                catch (Exception signalREx)
                {
                    _logger.LogWarning(signalREx, "Error enviando notificacion SignalR");
                }

                return Ok(ApiResponse<MensajeDto>.Ok(dto, "Mensaje enviado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando mensaje");
                return StatusCode(500, ApiResponse<MensajeDto>.Fail("Error interno del servidor"));
            }
        }

        [HttpPost("conversacion/{otroUserId}/leer")]
        public async Task<ActionResult<ApiResponse>> MarcarLeidos(string otroUserId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var mensajes = await _context.ChatMensajes
                    .Where(m => m.RemitenteId == otroUserId && m.DestinatarioId == userId && !m.Leido)
                    .ToListAsync();

                foreach (var mensaje in mensajes)
                {
                    mensaje.Leido = true;
                }

                await _context.SaveChangesAsync();

                try
                {
                    await _hubContext.Clients.User(otroUserId).SendAsync("MensajesLeidos", userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al notificar mensajes leídos via SignalR al usuario {UserId}", otroUserId);
                }

                return Ok(ApiResponse.Ok("Mensajes marcados como leidos"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marcando mensajes como leidos");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        [HttpGet("no-leidos")]
        public async Task<ActionResult<ApiResponse<int>>> GetNoLeidos()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse<int>.Fail("No autenticado"));
                }

                var count = await _context.ChatMensajes
                    .CountAsync(m => m.DestinatarioId == userId && !m.Leido);

                return Ok(ApiResponse<int>.Ok(count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo mensajes no leidos");
                return StatusCode(500, ApiResponse<int>.Fail("Error interno del servidor"));
            }
        }

        [HttpDelete("{mensajeId}")]
        public async Task<ActionResult<ApiResponse>> EliminarMensaje(int mensajeId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(ApiResponse.Fail("No autenticado"));
                }

                var mensaje = await _context.ChatMensajes.FindAsync(mensajeId);
                if (mensaje == null)
                {
                    return NotFound(ApiResponse.Fail("Mensaje no encontrado"));
                }

                if (mensaje.RemitenteId != userId)
                {
                    return Forbid();
                }

                _context.ChatMensajes.Remove(mensaje);
                await _context.SaveChangesAsync();

                return Ok(ApiResponse.Ok("Mensaje eliminado"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando mensaje");
                return StatusCode(500, ApiResponse.Fail("Error interno del servidor"));
            }
        }

        private static string GetTiempoRelativo(DateTime fecha)
        {
            var diff = DateTime.UtcNow - fecha;
            if (diff.TotalMinutes < 1) return "ahora";
            if (diff.TotalMinutes < 60) return $"hace {(int)diff.TotalMinutes}m";
            if (diff.TotalHours < 24) return $"hace {(int)diff.TotalHours}h";
            if (diff.TotalDays < 7) return $"hace {(int)diff.TotalDays}d";
            return fecha.ToString("dd/MM");
        }
    }

    public class ConversacionDto
    {
        public UsuarioDto Usuario { get; set; } = new();
        public string UltimoMensaje { get; set; } = string.Empty;
        public DateTime FechaUltimoMensaje { get; set; }
        public string TiempoRelativo { get; set; } = string.Empty;
        public int MensajesNoLeidos { get; set; }
        public bool EsMio { get; set; }
    }

    public class MensajeDto
    {
        public int Id { get; set; }
        public string Contenido { get; set; } = string.Empty;
        public DateTime FechaEnvio { get; set; }
        public string TiempoRelativo { get; set; } = string.Empty;
        public bool Leido { get; set; }
        public bool EsMio { get; set; }
        public UsuarioDto Remitente { get; set; } = new();
    }

    public class EnviarMensajeRequest
    {
        public string DestinatarioId { get; set; } = string.Empty;
        public string? Mensaje { get; set; }
    }
}
