using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lado.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Notificar a los seguidores cuando un creador publica nuevo contenido
        /// </summary>
        Task NotificarNuevoContenidoAsync(string creadorId, int contenidoId, string descripcion, TipoLado tipoLado);

        /// <summary>
        /// Notificar cuando alguien te sigue
        /// </summary>
        Task NotificarNuevoSeguidorAsync(string seguidoId, string seguidorId);

        /// <summary>
        /// Notificar cuando alguien da like a tu contenido
        /// </summary>
        Task NotificarNuevoLikeAsync(string propietarioContenidoId, string usuarioLikeId, int contenidoId);

        /// <summary>
        /// Notificar cuando alguien comenta en tu contenido
        /// </summary>
        Task NotificarNuevoComentarioAsync(string propietarioContenidoId, string usuarioComentarioId, int contenidoId, int comentarioId);

        /// <summary>
        /// Notificar cuando alguien te envía un mensaje
        /// </summary>
        Task NotificarNuevoMensajeAsync(string destinatarioId, string remitenteId, int mensajeId);

        /// <summary>
        /// Notificar cuando alguien se suscribe a ti (pago)
        /// </summary>
        Task NotificarNuevaSuscripcionAsync(string creadorId, string suscriptorId);

        /// <summary>
        /// Notificar pago recibido
        /// </summary>
        Task NotificarPagoRecibidoAsync(string usuarioId, decimal monto, string concepto);

        /// <summary>
        /// Crear notificación del sistema
        /// </summary>
        Task CrearNotificacionSistemaAsync(string usuarioId, string titulo, string mensaje, string? urlDestino = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IServiceScopeFactory scopeFactory, ILogger<NotificationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task NotificarNuevoContenidoAsync(string creadorId, int contenidoId, string descripcion, TipoLado tipoLado)
        {
            try
            {
                _logger.LogInformation("🔔 Iniciando notificación de nuevo contenido. CreadorId: {CreadorId}, ContenidoId: {ContenidoId}", creadorId, contenidoId);

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Obtener el creador
                var creador = await context.Users.FindAsync(creadorId);
                if (creador == null)
                {
                    _logger.LogWarning("🔔 Creador no encontrado: {CreadorId}", creadorId);
                    return;
                }

                // Obtener los seguidores del creador (suscripciones activas)
                var seguidoresIds = await context.Suscripciones
                    .Where(s => s.CreadorId == creadorId && s.EstaActiva)
                    .Select(s => s.FanId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("🔔 Seguidores encontrados: {Count}", seguidoresIds.Count);

                if (!seguidoresIds.Any())
                {
                    _logger.LogInformation("🔔 No hay seguidores para notificar");
                    return;
                }

                var nombreMostrar = $"@{creador.UserName}";

                var descripcionCorta = descripcion?.Length > 50
                    ? descripcion.Substring(0, 47) + "..."
                    : descripcion ?? "Nuevo contenido";

                var notificaciones = seguidoresIds.Select(seguidorId => new Notificacion
                {
                    UsuarioId = seguidorId,
                    UsuarioOrigenId = creadorId,
                    Tipo = TipoNotificacion.NuevoContenido,
                    Titulo = "Nueva publicación",
                    Mensaje = $"{nombreMostrar} publicó: {descripcionCorta}",
                    ContenidoId = contenidoId,
                    UrlDestino = $"/Feed/Detalle/{contenidoId}",
                    ImagenUrl = creador.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                }).ToList();

                context.Notificaciones.AddRange(notificaciones);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificaciones de nuevo contenido creadas para {Count} seguidores", seguidoresIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificaciones de nuevo contenido");
            }
        }

        public async Task NotificarNuevoSeguidorAsync(string seguidoId, string seguidorId)
        {
            try
            {
                if (seguidoId == seguidorId) return; // No notificar si se sigue a sí mismo

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var seguidor = await context.Users.FindAsync(seguidorId);
                if (seguidor == null) return;

                var notificacion = new Notificacion
                {
                    UsuarioId = seguidoId,
                    UsuarioOrigenId = seguidorId,
                    Tipo = TipoNotificacion.NuevoSeguidor,
                    Titulo = "Nuevo seguidor",
                    Mensaje = $"@{seguidor.UserName} comenzó a seguirte",
                    UrlDestino = $"/Feed/Perfil/{seguidor.UserName}",
                    ImagenUrl = seguidor.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación de nuevo seguidor creada: {Seguidor} -> {Seguido}", seguidorId, seguidoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de nuevo seguidor");
            }
        }

        public async Task NotificarNuevoLikeAsync(string propietarioContenidoId, string usuarioLikeId, int contenidoId)
        {
            try
            {
                _logger.LogInformation("🔔 NotificarNuevoLikeAsync - Inicio: propietario={Propietario}, usuario={Usuario}, contenido={Contenido}",
                    propietarioContenidoId, usuarioLikeId, contenidoId);

                if (propietarioContenidoId == usuarioLikeId)
                {
                    _logger.LogInformation("🔔 Like propio, no se notifica");
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var usuarioLike = await context.Users.FindAsync(usuarioLikeId);
                if (usuarioLike == null)
                {
                    _logger.LogWarning("🔔 Usuario que dio like no encontrado: {Id}", usuarioLikeId);
                    return;
                }

                _logger.LogInformation("🔔 Usuario encontrado: {Nombre}", usuarioLike.NombreCompleto);

                // Verificar si ya existe una notificación reciente de like del mismo usuario
                bool notificacionExistente = false;
                try
                {
                    notificacionExistente = await context.Notificaciones
                        .AnyAsync(n => n.UsuarioId == propietarioContenidoId
                                    && n.UsuarioOrigenId == usuarioLikeId
                                    && n.ContenidoId == contenidoId
                                    && n.Tipo == TipoNotificacion.NuevoLike
                                    && n.FechaCreacion > DateTime.Now.AddHours(-1));
                }
                catch (Exception checkEx)
                {
                    _logger.LogWarning("🔔 Error verificando notificación existente (ignorando): {Error}", checkEx.Message);
                    notificacionExistente = false;
                }

                if (notificacionExistente)
                {
                    _logger.LogInformation("🔔 Ya existe notificación reciente, no se crea otra");
                    return;
                }

                _logger.LogInformation("🔔 Creando notificación de like...");

                var notificacion = new Notificacion
                {
                    UsuarioId = propietarioContenidoId,
                    UsuarioOrigenId = usuarioLikeId,
                    Tipo = TipoNotificacion.NuevoLike,
                    Titulo = "Nuevo like",
                    Mensaje = $"A @{usuarioLike.UserName} le gustó tu publicación",
                    ContenidoId = contenidoId,
                    UrlDestino = $"/Feed/Detalle/{contenidoId}",
                    ImagenUrl = usuarioLike.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);

                _logger.LogInformation("🔔 Guardando notificación en base de datos...");
                await context.SaveChangesAsync();

                _logger.LogInformation("🔔 ✅ Notificación de like creada exitosamente: {Usuario} -> contenido {ContenidoId}", usuarioLikeId, contenidoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 ❌ Error al crear notificación de like: {Message}", ex.Message);
            }
        }

        public async Task NotificarNuevoComentarioAsync(string propietarioContenidoId, string usuarioComentarioId, int contenidoId, int comentarioId)
        {
            try
            {
                if (propietarioContenidoId == usuarioComentarioId) return; // No notificar comentario propio

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var usuarioComentario = await context.Users.FindAsync(usuarioComentarioId);
                if (usuarioComentario == null) return;

                var notificacion = new Notificacion
                {
                    UsuarioId = propietarioContenidoId,
                    UsuarioOrigenId = usuarioComentarioId,
                    Tipo = TipoNotificacion.NuevoComentario,
                    Titulo = "Nuevo comentario",
                    Mensaje = $"@{usuarioComentario.UserName} comentó en tu publicación",
                    ContenidoId = contenidoId,
                    ComentarioId = comentarioId,
                    UrlDestino = $"/Feed/Detalle/{contenidoId}",
                    ImagenUrl = usuarioComentario.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación de comentario creada: {Usuario} -> contenido {ContenidoId}", usuarioComentarioId, contenidoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de comentario");
            }
        }

        public async Task NotificarNuevoMensajeAsync(string destinatarioId, string remitenteId, int mensajeId)
        {
            try
            {
                if (destinatarioId == remitenteId) return;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var remitente = await context.Users.FindAsync(remitenteId);
                if (remitente == null) return;

                // Verificar si ya existe una notificación de mensaje no leída del mismo remitente
                var notificacionExistente = await context.Notificaciones
                    .AnyAsync(n => n.UsuarioId == destinatarioId
                                && n.UsuarioOrigenId == remitenteId
                                && n.Tipo == TipoNotificacion.NuevoMensaje
                                && !n.Leida
                                && n.FechaCreacion > DateTime.Now.AddMinutes(-5));

                if (notificacionExistente) return;

                var notificacion = new Notificacion
                {
                    UsuarioId = destinatarioId,
                    UsuarioOrigenId = remitenteId,
                    Tipo = TipoNotificacion.NuevoMensaje,
                    Titulo = "Nuevo mensaje",
                    Mensaje = $"@{remitente.UserName} te envió un mensaje",
                    MensajeId = mensajeId,
                    UrlDestino = $"/Mensajes?chat={remitenteId}",
                    ImagenUrl = remitente.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación de mensaje creada: {Remitente} -> {Destinatario}", remitenteId, destinatarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de mensaje");
            }
        }

        public async Task NotificarNuevaSuscripcionAsync(string creadorId, string suscriptorId)
        {
            try
            {
                if (creadorId == suscriptorId) return;

                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var suscriptor = await context.Users.FindAsync(suscriptorId);
                if (suscriptor == null) return;

                var notificacion = new Notificacion
                {
                    UsuarioId = creadorId,
                    UsuarioOrigenId = suscriptorId,
                    Tipo = TipoNotificacion.NuevaSuscripcion,
                    Titulo = "Nueva suscripción",
                    Mensaje = $"@{suscriptor.UserName} se suscribió a tu contenido",
                    UrlDestino = $"/Feed/Perfil/{suscriptor.UserName}",
                    ImagenUrl = suscriptor.FotoPerfil,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación de suscripción creada: {Suscriptor} -> {Creador}", suscriptorId, creadorId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de suscripción");
            }
        }

        public async Task NotificarPagoRecibidoAsync(string usuarioId, decimal monto, string concepto)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var notificacion = new Notificacion
                {
                    UsuarioId = usuarioId,
                    Tipo = TipoNotificacion.PagoRecibido,
                    Titulo = "Pago recibido",
                    Mensaje = $"Recibiste ${monto:N0} por {concepto}",
                    UrlDestino = "/Billetera",
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación de pago creada: {Usuario} - ${Monto}", usuarioId, monto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de pago");
            }
        }

        public async Task CrearNotificacionSistemaAsync(string usuarioId, string titulo, string mensaje, string? urlDestino = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var notificacion = new Notificacion
                {
                    UsuarioId = usuarioId,
                    Tipo = TipoNotificacion.Sistema,
                    Titulo = titulo,
                    Mensaje = mensaje,
                    UrlDestino = urlDestino,
                    FechaCreacion = DateTime.Now,
                    Leida = false,
                    EstaActiva = true
                };

                context.Notificaciones.Add(notificacion);
                await context.SaveChangesAsync();

                _logger.LogInformation("Notificación del sistema creada para: {Usuario}", usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación del sistema");
            }
        }
    }
}
