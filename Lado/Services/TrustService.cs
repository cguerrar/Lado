using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface ITrustService
    {
        /// <summary>
        /// Actualiza la última actividad del usuario
        /// </summary>
        Task ActualizarUltimaActividadAsync(string usuarioId);

        /// <summary>
        /// Registra que un usuario recibió un mensaje
        /// </summary>
        Task RegistrarMensajeRecibidoAsync(string destinatarioId);

        /// <summary>
        /// Registra que un usuario respondió un mensaje
        /// </summary>
        Task RegistrarMensajeRespondidoAsync(string remitenteId, int tiempoRespuestaMinutos);

        /// <summary>
        /// Actualiza el contador de contenidos publicados
        /// </summary>
        Task ActualizarContadorContenidosAsync(string usuarioId);

        /// <summary>
        /// Registra un reporte contra un usuario
        /// </summary>
        Task RegistrarReporteAsync(string usuarioId);

        /// <summary>
        /// Obtiene las métricas de confianza de un usuario
        /// </summary>
        Task<TrustMetrics> ObtenerMetricasAsync(string usuarioId);

        /// <summary>
        /// Recalcula todas las métricas de un usuario
        /// </summary>
        Task RecalcularMetricasAsync(string usuarioId);

        /// <summary>
        /// Obtiene la configuración de confianza actual
        /// </summary>
        Task<ConfiguracionConfianza> ObtenerConfiguracionAsync();

        /// <summary>
        /// Calcula el nivel de confianza de un usuario basado en la configuración
        /// </summary>
        Task<int> CalcularNivelConfianzaAsync(ApplicationUser usuario);
    }

    public class TrustMetrics
    {
        public int NivelConfianza { get; set; } // 0-5
        public int TasaRespuesta { get; set; } // 0-100%
        public string EstadoActividad { get; set; } = "";
        public string TiempoRespuesta { get; set; } = "";
        public bool EsVerificado { get; set; }
        public bool EstaActivo { get; set; }
        public int ContenidosPublicados { get; set; }
        public int Seguidores { get; set; }
        public bool TieneHistorialLimpio { get; set; }

        // Badges que mostrar
        public List<TrustBadge> Badges { get; set; } = new();
    }

    public class TrustBadge
    {
        public string Icono { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Color { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }

    public class TrustService : ITrustService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TrustService> _logger;
        private static ConfiguracionConfianza? _configCache;
        private static DateTime _configCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public TrustService(ApplicationDbContext context, ILogger<TrustService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la configuración de confianza (con cache de 5 minutos)
        /// </summary>
        public async Task<ConfiguracionConfianza> ObtenerConfiguracionAsync()
        {
            // Usar cache si es válido
            if (_configCache != null && DateTime.Now - _configCacheTime < CacheDuration)
            {
                return _configCache;
            }

            // Obtener de BD o crear configuración por defecto
            var config = await _context.ConfiguracionesConfianza.FirstOrDefaultAsync();

            if (config == null)
            {
                config = new ConfiguracionConfianza();
                _context.ConfiguracionesConfianza.Add(config);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Creada configuración de confianza por defecto");
            }

            _configCache = config;
            _configCacheTime = DateTime.Now;
            return config;
        }

        /// <summary>
        /// Invalida el cache de configuración (llamar después de guardar cambios)
        /// </summary>
        public static void InvalidarCache()
        {
            _configCache = null;
            _configCacheTime = DateTime.MinValue;
        }

        /// <summary>
        /// Calcula el nivel de confianza basado en la configuración
        /// </summary>
        public async Task<int> CalcularNivelConfianzaAsync(ApplicationUser usuario)
        {
            var config = await ObtenerConfiguracionAsync();
            int nivel = 0;

            // Criterio 1: Verificación de identidad
            if (config.VerificacionIdentidadHabilitada && usuario.CreadorVerificado)
            {
                nivel += config.PuntosVerificacionIdentidad;
            }

            // Criterio 2: Verificación de edad
            if (config.VerificacionEdadHabilitada && usuario.AgeVerified)
            {
                nivel += config.PuntosVerificacionEdad;
            }

            // Criterio 3: Tasa de respuesta
            if (config.TasaRespuestaHabilitada && usuario.ObtenerTasaRespuesta() >= config.PorcentajeMinimoRespuesta)
            {
                nivel += config.PuntosTasaRespuesta;
            }

            // Criterio 4: Actividad reciente
            if (config.ActividadRecienteHabilitada)
            {
                var estaActivo = usuario.UltimaActividad.HasValue &&
                    (DateTime.Now - usuario.UltimaActividad.Value).TotalHours <= config.HorasMaximasInactividad;
                if (estaActivo)
                {
                    nivel += config.PuntosActividadReciente;
                }
            }

            // Criterio 5: Contenido publicado
            if (config.ContenidoPublicadoHabilitado && usuario.ContenidosPublicados >= config.MinimoPublicaciones)
            {
                nivel += config.PuntosContenidoPublicado;
            }

            // Limitar al máximo configurado
            return Math.Min(nivel, config.NivelMaximo);
        }

        public async Task ActualizarUltimaActividadAsync(string usuarioId)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario != null)
                {
                    usuario.UltimaActividad = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando última actividad para {UsuarioId}", usuarioId);
            }
        }

        public async Task RegistrarMensajeRecibidoAsync(string destinatarioId)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(destinatarioId);
                if (usuario != null)
                {
                    usuario.MensajesRecibidosTotal++;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando mensaje recibido para {UsuarioId}", destinatarioId);
            }
        }

        public async Task RegistrarMensajeRespondidoAsync(string remitenteId, int tiempoRespuestaMinutos)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(remitenteId);
                if (usuario != null)
                {
                    usuario.MensajesRespondidosTotal++;

                    // Calcular nuevo promedio de tiempo de respuesta
                    if (usuario.TiempoPromedioRespuesta.HasValue)
                    {
                        usuario.TiempoPromedioRespuesta =
                            (usuario.TiempoPromedioRespuesta.Value + tiempoRespuestaMinutos) / 2;
                    }
                    else
                    {
                        usuario.TiempoPromedioRespuesta = tiempoRespuestaMinutos;
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando mensaje respondido para {UsuarioId}", remitenteId);
            }
        }

        public async Task ActualizarContadorContenidosAsync(string usuarioId)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario != null)
                {
                    usuario.ContenidosPublicados = await _context.Contenidos
                        .CountAsync(c => c.UsuarioId == usuarioId && c.EstaActivo && !c.EsBorrador);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando contador de contenidos para {UsuarioId}", usuarioId);
            }
        }

        public async Task RegistrarReporteAsync(string usuarioId)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario != null)
                {
                    usuario.ReportesRecibidos++;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando reporte para {UsuarioId}", usuarioId);
            }
        }

        public async Task<TrustMetrics> ObtenerMetricasAsync(string usuarioId)
        {
            var usuario = await _context.Users.FindAsync(usuarioId);
            if (usuario == null)
            {
                return new TrustMetrics();
            }

            var config = await ObtenerConfiguracionAsync();
            var seguidores = await _context.Suscripciones
                .CountAsync(s => s.CreadorId == usuarioId && s.EstaActiva);

            // Calcular nivel usando la configuración
            var nivelConfianza = await CalcularNivelConfianzaAsync(usuario);

            var metrics = new TrustMetrics
            {
                NivelConfianza = nivelConfianza,
                TasaRespuesta = usuario.ObtenerTasaRespuesta(),
                EstadoActividad = usuario.ObtenerEstadoActividad(),
                TiempoRespuesta = usuario.ObtenerTextoTiempoRespuesta(),
                EsVerificado = usuario.CreadorVerificado,
                EstaActivo = usuario.EstaActivoReciente(),
                ContenidosPublicados = usuario.ContenidosPublicados,
                Seguidores = seguidores,
                TieneHistorialLimpio = usuario.ReportesRecibidos == 0,
                Badges = new List<TrustBadge>()
            };

            // Solo generar badges si está habilitado
            if (!config.MostrarBadgesEnPerfil)
            {
                return metrics;
            }

            // Generar badges usando la configuración
            if (config.VerificacionIdentidadHabilitada && usuario.CreadorVerificado)
            {
                metrics.Badges.Add(new TrustBadge
                {
                    Icono = "shield-check",
                    Titulo = "Verificado",
                    Color = "#10b981",
                    Descripcion = config.DescripcionVerificacionIdentidad
                });
            }

            if (config.TasaRespuestaHabilitada)
            {
                if (metrics.TasaRespuesta >= config.PorcentajeMinimoRespuesta)
                {
                    metrics.Badges.Add(new TrustBadge
                    {
                        Icono = "message-circle",
                        Titulo = $"Responde {metrics.TasaRespuesta}%",
                        Color = "#3b82f6",
                        Descripcion = config.DescripcionTasaRespuesta
                    });
                }
                else if (metrics.TasaRespuesta >= 50)
                {
                    metrics.Badges.Add(new TrustBadge
                    {
                        Icono = "message-circle",
                        Titulo = $"Responde {metrics.TasaRespuesta}%",
                        Color = "#f59e0b",
                        Descripcion = "Tasa de respuesta moderada"
                    });
                }
            }

            if (config.ActividadRecienteHabilitada)
            {
                var estaActivo = usuario.UltimaActividad.HasValue &&
                    (DateTime.Now - usuario.UltimaActividad.Value).TotalHours <= config.HorasMaximasInactividad;
                if (estaActivo)
                {
                    metrics.Badges.Add(new TrustBadge
                    {
                        Icono = "activity",
                        Titulo = "Activo",
                        Color = "#22c55e",
                        Descripcion = config.DescripcionActividadReciente
                    });
                }
            }

            if (config.ContenidoPublicadoHabilitado &&
                metrics.TieneHistorialLimpio &&
                usuario.ContenidosPublicados >= config.MinimoPublicaciones)
            {
                metrics.Badges.Add(new TrustBadge
                {
                    Icono = "star",
                    Titulo = "Confiable",
                    Color = "#8b5cf6",
                    Descripcion = config.DescripcionContenidoPublicado
                });
            }

            if (seguidores >= 100)
            {
                metrics.Badges.Add(new TrustBadge
                {
                    Icono = "users",
                    Titulo = $"{seguidores}+ confían",
                    Color = "#ec4899",
                    Descripcion = $"{seguidores} seguidores activos"
                });
            }

            return metrics;
        }

        public async Task RecalcularMetricasAsync(string usuarioId)
        {
            try
            {
                var usuario = await _context.Users.FindAsync(usuarioId);
                if (usuario == null) return;

                // Recalcular contenidos
                usuario.ContenidosPublicados = await _context.Contenidos
                    .CountAsync(c => c.UsuarioId == usuarioId && c.EstaActivo && !c.EsBorrador);

                // Recalcular mensajes recibidos
                usuario.MensajesRecibidosTotal = await _context.ChatMensajes
                    .CountAsync(m => m.DestinatarioId == usuarioId);

                // Recalcular mensajes respondidos
                usuario.MensajesRespondidosTotal = await _context.ChatMensajes
                    .CountAsync(m => m.RemitenteId == usuarioId);

                // Última actividad basada en último contenido
                var ultimoContenido = await _context.Contenidos
                    .Where(c => c.UsuarioId == usuarioId && c.EstaActivo)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .FirstOrDefaultAsync();

                if (ultimoContenido != null)
                {
                    if (!usuario.UltimaActividad.HasValue ||
                        ultimoContenido.FechaPublicacion > usuario.UltimaActividad)
                    {
                        usuario.UltimaActividad = ultimoContenido.FechaPublicacion;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Métricas recalculadas para {UsuarioId}", usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculando métricas para {UsuarioId}", usuarioId);
            }
        }
    }
}
