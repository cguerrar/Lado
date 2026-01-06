using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lado.Controllers
{
    /// <summary>
    /// Controller para gestionar suscripciones y preferencias de Push Notifications
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificacionesPushController : ControllerBase
    {
        private readonly IPushNotificationService _pushService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificacionesPushController> _logger;

        public NotificacionesPushController(
            IPushNotificationService pushService,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificacionesPushController> logger)
        {
            _pushService = pushService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Obtener la clave pública VAPID para el frontend
        /// </summary>
        [HttpGet("vapid-public-key")]
        [AllowAnonymous]
        public IActionResult GetVapidPublicKey()
        {
            var publicKey = _pushService.ObtenerVapidPublicKey();
            return Ok(new { publicKey });
        }

        /// <summary>
        /// Suscribir el dispositivo actual a notificaciones push
        /// </summary>
        [HttpPost("suscribir")]
        public async Task<IActionResult> Suscribir([FromBody] SuscripcionPushRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Endpoint))
            {
                return BadRequest(new { success = false, error = "Datos de suscripción inválidos" });
            }

            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            var deviceId = request.DeviceId ?? GenerarDeviceId(userAgent);

            var resultado = await _pushService.SuscribirAsync(
                usuario.Id,
                request.Endpoint,
                request.Keys?.P256dh ?? "",
                request.Keys?.Auth ?? "",
                deviceId,
                userAgent
            );

            if (resultado)
            {
                _logger.LogInformation("Usuario {UserId} suscrito a push notifications", usuario.Id);
                return Ok(new { success = true, message = "Notificaciones activadas" });
            }

            return BadRequest(new { success = false, error = "No se pudo activar las notificaciones" });
        }

        /// <summary>
        /// Desuscribir el dispositivo actual de notificaciones push
        /// </summary>
        [HttpPost("desuscribir")]
        public async Task<IActionResult> Desuscribir([FromBody] DesuscripcionRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var resultado = await _pushService.DesuscribirAsync(usuario.Id, request.Endpoint);

            if (resultado)
            {
                return Ok(new { success = true, message = "Notificaciones desactivadas" });
            }

            return BadRequest(new { success = false, error = "No se pudo desactivar las notificaciones" });
        }

        /// <summary>
        /// Desuscribir todos los dispositivos del usuario
        /// </summary>
        [HttpPost("desuscribir-todos")]
        public async Task<IActionResult> DesuscribirTodos()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var resultado = await _pushService.DesuscribirTodosAsync(usuario.Id);

            if (resultado)
            {
                return Ok(new { success = true, message = "Todas las notificaciones desactivadas" });
            }

            return BadRequest(new { success = false, error = "No se pudo desactivar las notificaciones" });
        }

        /// <summary>
        /// Verificar si el usuario tiene notificaciones activas
        /// </summary>
        [HttpGet("estado")]
        public async Task<IActionResult> ObtenerEstado()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var tieneActivas = await _pushService.TieneSuscripcionActivaAsync(usuario.Id);
            var suscripciones = await _pushService.ObtenerSuscripcionesAsync(usuario.Id);
            var preferencias = await _pushService.ObtenerPreferenciasAsync(usuario.Id);

            return Ok(new
            {
                success = true,
                activo = tieneActivas,
                dispositivos = suscripciones.Count,
                preferencias = new
                {
                    mensajes = preferencias.NotificarMensajes,
                    likes = preferencias.NotificarLikes,
                    comentarios = preferencias.NotificarComentarios,
                    seguidores = preferencias.NotificarSeguidores,
                    suscripciones = preferencias.NotificarSuscripciones,
                    propinas = preferencias.NotificarPropinas,
                    menciones = preferencias.NotificarMenciones,
                    horaSilencioInicio = preferencias.HoraSilencioInicio?.ToString("HH:mm"),
                    horaSilencioFin = preferencias.HoraSilencioFin?.ToString("HH:mm"),
                    zonaHoraria = preferencias.ZonaHoraria
                }
            });
        }

        /// <summary>
        /// Actualizar preferencias de notificación
        /// </summary>
        [HttpPost("preferencias")]
        public async Task<IActionResult> ActualizarPreferencias([FromBody] PreferenciasRequest request)
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var preferencias = new PreferenciasNotificacion
            {
                UsuarioId = usuario.Id,
                NotificarMensajes = request.Mensajes ?? true,
                NotificarLikes = request.Likes ?? true,
                NotificarComentarios = request.Comentarios ?? true,
                NotificarSeguidores = request.Seguidores ?? true,
                NotificarSuscripciones = request.Suscripciones ?? true,
                NotificarPropinas = request.Propinas ?? true,
                NotificarMenciones = request.Menciones ?? true,
                HoraSilencioInicio = ParseTimeOnly(request.HoraSilencioInicio),
                HoraSilencioFin = ParseTimeOnly(request.HoraSilencioFin),
                ZonaHoraria = request.ZonaHoraria
            };

            var resultado = await _pushService.ActualizarPreferenciasAsync(usuario.Id, preferencias);

            if (resultado)
            {
                return Ok(new { success = true, message = "Preferencias actualizadas" });
            }

            return BadRequest(new { success = false, error = "No se pudo actualizar las preferencias" });
        }

        /// <summary>
        /// Enviar notificación de prueba al usuario actual
        /// </summary>
        [HttpPost("test")]
        public async Task<IActionResult> EnviarPrueba()
        {
            var usuario = await _userManager.GetUserAsync(User);
            if (usuario == null)
            {
                return Unauthorized(new { success = false, error = "Usuario no autenticado" });
            }

            var enviados = await _pushService.EnviarNotificacionAsync(
                usuario.Id,
                "¡Notificaciones activas! 🔔",
                "Las notificaciones push están funcionando correctamente en tu dispositivo.",
                "/Usuario/Configuracion",
                TipoNotificacionPush.Sistema
            );

            if (enviados > 0)
            {
                return Ok(new { success = true, message = $"Notificación enviada a {enviados} dispositivo(s)" });
            }

            return Ok(new { success = false, message = "No hay dispositivos suscritos" });
        }

        #region DTOs

        public class SuscripcionPushRequest
        {
            public string Endpoint { get; set; } = string.Empty;
            public PushKeys? Keys { get; set; }
            public string? DeviceId { get; set; }
        }

        public class PushKeys
        {
            public string P256dh { get; set; } = string.Empty;
            public string Auth { get; set; } = string.Empty;
        }

        public class DesuscripcionRequest
        {
            public string Endpoint { get; set; } = string.Empty;
        }

        public class PreferenciasRequest
        {
            public bool? Mensajes { get; set; }
            public bool? Likes { get; set; }
            public bool? Comentarios { get; set; }
            public bool? Seguidores { get; set; }
            public bool? Suscripciones { get; set; }
            public bool? Propinas { get; set; }
            public bool? Menciones { get; set; }
            public string? HoraSilencioInicio { get; set; }
            public string? HoraSilencioFin { get; set; }
            public string? ZonaHoraria { get; set; }
        }

        #endregion

        #region Helpers

        private string GenerarDeviceId(string? userAgent)
        {
            var baseString = $"{userAgent ?? "unknown"}-{DateTime.UtcNow.Ticks}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(baseString));
            return Convert.ToBase64String(hash)[..20];
        }

        private TimeOnly? ParseTimeOnly(string? time)
        {
            if (string.IsNullOrEmpty(time)) return null;
            if (TimeOnly.TryParse(time, out var result))
            {
                return result;
            }
            return null;
        }

        #endregion
    }
}
