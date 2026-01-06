using System.Text.Json;
using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

// Alias para evitar conflicto de nombres
using LadoPushSubscription = Lado.Models.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace Lado.Services
{
    public interface IPushNotificationService
    {
        /// <summary>
        /// Suscribir un dispositivo a notificaciones push
        /// </summary>
        Task<bool> SuscribirAsync(string usuarioId, string endpoint, string p256dh, string auth, string? deviceId, string? userAgent);

        /// <summary>
        /// Desuscribir un dispositivo
        /// </summary>
        Task<bool> DesuscribirAsync(string usuarioId, string endpoint);

        /// <summary>
        /// Desuscribir todos los dispositivos de un usuario
        /// </summary>
        Task<bool> DesuscribirTodosAsync(string usuarioId);

        /// <summary>
        /// Enviar notificación push a un usuario específico
        /// </summary>
        Task<int> EnviarNotificacionAsync(string usuarioId, string titulo, string cuerpo, string? url = null, TipoNotificacionPush tipo = TipoNotificacionPush.Sistema, string? icono = null);

        /// <summary>
        /// Enviar notificación push a múltiples usuarios
        /// </summary>
        Task<int> EnviarNotificacionMasivaAsync(IEnumerable<string> usuarioIds, string titulo, string cuerpo, string? url = null, TipoNotificacionPush tipo = TipoNotificacionPush.Sistema);

        /// <summary>
        /// Obtener las suscripciones activas de un usuario
        /// </summary>
        Task<List<LadoPushSubscription>> ObtenerSuscripcionesAsync(string usuarioId);

        /// <summary>
        /// Verificar si un usuario tiene notificaciones push activas
        /// </summary>
        Task<bool> TieneSuscripcionActivaAsync(string usuarioId);

        /// <summary>
        /// Obtener preferencias de notificación de un usuario
        /// </summary>
        Task<PreferenciasNotificacion> ObtenerPreferenciasAsync(string usuarioId);

        /// <summary>
        /// Actualizar preferencias de notificación
        /// </summary>
        Task<bool> ActualizarPreferenciasAsync(string usuarioId, PreferenciasNotificacion preferencias);

        /// <summary>
        /// Obtener la clave pública VAPID para el frontend
        /// </summary>
        string ObtenerVapidPublicKey();

        /// <summary>
        /// Generar nuevas claves VAPID (solo para configuración inicial)
        /// </summary>
        VapidDetails GenerarVapidKeys();
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly WebPushClient _webPushClient;
        private readonly VapidDetails _vapidDetails;

        public PushNotificationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _webPushClient = new WebPushClient();

            // Cargar configuración VAPID
            var vapidSubject = _configuration["Vapid:Subject"] ?? "mailto:admin@ladoapp.com";
            var vapidPublicKey = _configuration["Vapid:PublicKey"] ?? "";
            var vapidPrivateKey = _configuration["Vapid:PrivateKey"] ?? "";

            if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
            {
                _logger.LogWarning("VAPID keys no configuradas. Push notifications no funcionarán.");
                // Generar claves temporales para evitar errores (no usar en producción)
                var keys = VapidHelper.GenerateVapidKeys();
                vapidPublicKey = keys.PublicKey;
                vapidPrivateKey = keys.PrivateKey;
                _logger.LogWarning("Claves VAPID temporales generadas. PublicKey: {PublicKey}", vapidPublicKey);
            }

            _vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        }

        public async Task<bool> SuscribirAsync(string usuarioId, string endpoint, string p256dh, string auth, string? deviceId, string? userAgent)
        {
            try
            {
                // Verificar si ya existe esta suscripción
                var existente = await _context.PushSubscriptions
                    .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Endpoint == endpoint);

                if (existente != null)
                {
                    // Actualizar suscripción existente
                    existente.P256dh = p256dh;
                    existente.Auth = auth;
                    existente.DeviceId = deviceId;
                    existente.UserAgent = userAgent;
                    existente.Activa = true;
                    existente.FallosConsecutivos = 0;
                }
                else
                {
                    // Crear nueva suscripción
                    var suscripcion = new LadoPushSubscription
                    {
                        UsuarioId = usuarioId,
                        Endpoint = endpoint,
                        P256dh = p256dh,
                        Auth = auth,
                        DeviceId = deviceId,
                        UserAgent = userAgent,
                        FechaCreacion = DateTime.UtcNow,
                        Activa = true
                    };
                    _context.PushSubscriptions.Add(suscripcion);
                }

                // Crear preferencias por defecto si no existen
                var preferencias = await _context.PreferenciasNotificaciones
                    .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

                if (preferencias == null)
                {
                    _context.PreferenciasNotificaciones.Add(new PreferenciasNotificacion
                    {
                        UsuarioId = usuarioId
                    });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Usuario {UsuarioId} suscrito a push notifications", usuarioId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al suscribir usuario {UsuarioId} a push notifications", usuarioId);
                return false;
            }
        }

        public async Task<bool> DesuscribirAsync(string usuarioId, string endpoint)
        {
            try
            {
                var suscripcion = await _context.PushSubscriptions
                    .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId && s.Endpoint == endpoint);

                if (suscripcion != null)
                {
                    _context.PushSubscriptions.Remove(suscripcion);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Suscripción eliminada para usuario {UsuarioId}", usuarioId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desuscribir usuario {UsuarioId}", usuarioId);
                return false;
            }
        }

        public async Task<bool> DesuscribirTodosAsync(string usuarioId)
        {
            try
            {
                var suscripciones = await _context.PushSubscriptions
                    .Where(s => s.UsuarioId == usuarioId)
                    .ToListAsync();

                _context.PushSubscriptions.RemoveRange(suscripciones);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Todas las suscripciones eliminadas para usuario {UsuarioId}", usuarioId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desuscribir todos los dispositivos de usuario {UsuarioId}", usuarioId);
                return false;
            }
        }

        public async Task<int> EnviarNotificacionAsync(string usuarioId, string titulo, string cuerpo, string? url = null, TipoNotificacionPush tipo = TipoNotificacionPush.Sistema, string? icono = null)
        {
            try
            {
                // Verificar preferencias del usuario
                var preferencias = await _context.PreferenciasNotificaciones
                    .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

                if (preferencias != null && !DebeEnviarNotificacion(preferencias, tipo))
                {
                    _logger.LogDebug("Notificación {Tipo} no enviada a {UsuarioId} por preferencias", tipo, usuarioId);
                    return 0;
                }

                // Verificar horario de silencio
                if (preferencias != null && EstaEnHorarioSilencio(preferencias))
                {
                    _logger.LogDebug("Notificación no enviada a {UsuarioId} - horario de silencio", usuarioId);
                    return 0;
                }

                // Obtener suscripciones activas
                var suscripciones = await _context.PushSubscriptions
                    .Where(s => s.UsuarioId == usuarioId && s.Activa)
                    .ToListAsync();

                if (!suscripciones.Any())
                {
                    return 0;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    title = titulo,
                    body = cuerpo,
                    url = url ?? "/",
                    icon = icono ?? "/images/icons/icon-192x192.png",
                    badge = "/images/icons/icon-72x72.png",
                    tipo = tipo.ToString(),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    actions = GetAccionesParaTipo(tipo, url)
                });

                int enviados = 0;
                var suscripcionesAEliminar = new List<LadoPushSubscription>();

                foreach (var suscripcion in suscripciones)
                {
                    try
                    {
                        var pushSubscription = new WebPush.PushSubscription(
                            suscripcion.Endpoint,
                            suscripcion.P256dh,
                            suscripcion.Auth
                        );

                        await _webPushClient.SendNotificationAsync(pushSubscription, payload, _vapidDetails);

                        suscripcion.UltimaNotificacion = DateTime.UtcNow;
                        suscripcion.FallosConsecutivos = 0;
                        enviados++;
                    }
                    catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                                       ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Suscripción expirada o inválida - marcar para eliminar
                        _logger.LogWarning("Suscripción expirada para usuario {UsuarioId}, endpoint: {Endpoint}",
                            usuarioId, suscripcion.Endpoint);
                        suscripcionesAEliminar.Add(suscripcion);
                    }
                    catch (Exception ex)
                    {
                        suscripcion.FallosConsecutivos++;
                        if (suscripcion.FallosConsecutivos >= 5)
                        {
                            suscripcion.Activa = false;
                            _logger.LogWarning("Suscripción desactivada por múltiples fallos para usuario {UsuarioId}", usuarioId);
                        }
                        _logger.LogError(ex, "Error al enviar push a usuario {UsuarioId}", usuarioId);
                    }
                }

                // Eliminar suscripciones expiradas
                if (suscripcionesAEliminar.Any())
                {
                    _context.PushSubscriptions.RemoveRange(suscripcionesAEliminar);
                }

                await _context.SaveChangesAsync();

                if (enviados > 0)
                {
                    _logger.LogInformation("Push notification enviada a {UsuarioId}: {Titulo} ({Enviados} dispositivos)",
                        usuarioId, titulo, enviados);
                }

                return enviados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación push a usuario {UsuarioId}", usuarioId);
                return 0;
            }
        }

        public async Task<int> EnviarNotificacionMasivaAsync(IEnumerable<string> usuarioIds, string titulo, string cuerpo, string? url = null, TipoNotificacionPush tipo = TipoNotificacionPush.Sistema)
        {
            int totalEnviados = 0;

            // Procesar en lotes para no sobrecargar
            var lotes = usuarioIds.Chunk(100);

            foreach (var lote in lotes)
            {
                var tareas = lote.Select(id => EnviarNotificacionAsync(id, titulo, cuerpo, url, tipo));
                var resultados = await Task.WhenAll(tareas);
                totalEnviados += resultados.Sum();
            }

            _logger.LogInformation("Notificación masiva enviada: {Titulo} - {Total} dispositivos", titulo, totalEnviados);
            return totalEnviados;
        }

        public async Task<List<LadoPushSubscription>> ObtenerSuscripcionesAsync(string usuarioId)
        {
            return await _context.PushSubscriptions
                .Where(s => s.UsuarioId == usuarioId && s.Activa)
                .ToListAsync();
        }

        public async Task<bool> TieneSuscripcionActivaAsync(string usuarioId)
        {
            return await _context.PushSubscriptions
                .AnyAsync(s => s.UsuarioId == usuarioId && s.Activa);
        }

        public async Task<PreferenciasNotificacion> ObtenerPreferenciasAsync(string usuarioId)
        {
            var preferencias = await _context.PreferenciasNotificaciones
                .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

            if (preferencias == null)
            {
                preferencias = new PreferenciasNotificacion { UsuarioId = usuarioId };
                _context.PreferenciasNotificaciones.Add(preferencias);
                await _context.SaveChangesAsync();
            }

            return preferencias;
        }

        public async Task<bool> ActualizarPreferenciasAsync(string usuarioId, PreferenciasNotificacion preferencias)
        {
            try
            {
                var existente = await _context.PreferenciasNotificaciones
                    .FirstOrDefaultAsync(p => p.UsuarioId == usuarioId);

                if (existente != null)
                {
                    existente.NotificarMensajes = preferencias.NotificarMensajes;
                    existente.NotificarLikes = preferencias.NotificarLikes;
                    existente.NotificarComentarios = preferencias.NotificarComentarios;
                    existente.NotificarSeguidores = preferencias.NotificarSeguidores;
                    existente.NotificarSuscripciones = preferencias.NotificarSuscripciones;
                    existente.NotificarPropinas = preferencias.NotificarPropinas;
                    existente.NotificarMenciones = preferencias.NotificarMenciones;
                    existente.HoraSilencioInicio = preferencias.HoraSilencioInicio;
                    existente.HoraSilencioFin = preferencias.HoraSilencioFin;
                    existente.ZonaHoraria = preferencias.ZonaHoraria;
                }
                else
                {
                    preferencias.UsuarioId = usuarioId;
                    _context.PreferenciasNotificaciones.Add(preferencias);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar preferencias de notificación para usuario {UsuarioId}", usuarioId);
                return false;
            }
        }

        public string ObtenerVapidPublicKey()
        {
            return _vapidDetails.PublicKey;
        }

        public VapidDetails GenerarVapidKeys()
        {
            var keys = VapidHelper.GenerateVapidKeys();
            _logger.LogInformation("Nuevas claves VAPID generadas. Agregar a appsettings.json:");
            _logger.LogInformation("PublicKey: {PublicKey}", keys.PublicKey);
            _logger.LogInformation("PrivateKey: {PrivateKey}", keys.PrivateKey);
            return keys;
        }

        #region Helpers privados

        private bool DebeEnviarNotificacion(PreferenciasNotificacion pref, TipoNotificacionPush tipo)
        {
            return tipo switch
            {
                TipoNotificacionPush.NuevoMensaje => pref.NotificarMensajes,
                TipoNotificacionPush.NuevoLike => pref.NotificarLikes,
                TipoNotificacionPush.NuevoComentario => pref.NotificarComentarios,
                TipoNotificacionPush.NuevoSeguidor => pref.NotificarSeguidores,
                TipoNotificacionPush.NuevaSuscripcion => pref.NotificarSuscripciones,
                TipoNotificacionPush.NuevaPropina => pref.NotificarPropinas,
                TipoNotificacionPush.Mencion => pref.NotificarMenciones,
                TipoNotificacionPush.Sistema => true,
                TipoNotificacionPush.Promocion => true,
                _ => true
            };
        }

        private bool EstaEnHorarioSilencio(PreferenciasNotificacion pref)
        {
            if (!pref.HoraSilencioInicio.HasValue || !pref.HoraSilencioFin.HasValue)
                return false;

            try
            {
                var zonaHoraria = !string.IsNullOrEmpty(pref.ZonaHoraria)
                    ? TimeZoneInfo.FindSystemTimeZoneById(pref.ZonaHoraria)
                    : TimeZoneInfo.Local;

                var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaHoraria);
                var horaActual = TimeOnly.FromDateTime(ahora);

                var inicio = pref.HoraSilencioInicio.Value;
                var fin = pref.HoraSilencioFin.Value;

                // Manejar caso donde el horario cruza medianoche (ej: 22:00 - 08:00)
                if (inicio > fin)
                {
                    return horaActual >= inicio || horaActual <= fin;
                }
                else
                {
                    return horaActual >= inicio && horaActual <= fin;
                }
            }
            catch
            {
                return false;
            }
        }

        private object[] GetAccionesParaTipo(TipoNotificacionPush tipo, string? url)
        {
            return tipo switch
            {
                TipoNotificacionPush.NuevoMensaje => new object[]
                {
                    new { action = "view", title = "Ver mensaje" },
                    new { action = "dismiss", title = "Ignorar" }
                },
                TipoNotificacionPush.NuevoSeguidor => new object[]
                {
                    new { action = "view", title = "Ver perfil" }
                },
                _ => new object[]
                {
                    new { action = "view", title = "Ver" }
                }
            };
        }

        #endregion
    }
}
