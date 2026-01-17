using Lado.Data;
using Lado.Models;
using Lado.Models.Moderacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface IDashboardMetricsService
    {
        Task<DashboardMetrics> ObtenerMetricasAsync();
        Task<List<DataPoint>> ObtenerSerieTemporalAsync(string tipo, int dias = 30);
        Task<DashboardResumen> ObtenerResumenRapidoAsync();
    }

    public class DashboardMetricsService : IDashboardMetricsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DashboardMetricsService> _logger;
        private const string CACHE_KEY = "DashboardMetrics";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public DashboardMetricsService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<DashboardMetricsService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<DashboardMetrics> ObtenerMetricasAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out DashboardMetrics? cached) && cached != null)
            {
                return cached;
            }

            var ahora = DateTime.Now;
            var hace24h = ahora.AddHours(-24);
            var hace7d = ahora.AddDays(-7);
            var hace30d = ahora.AddDays(-30);
            var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);

            try
            {
                var metrics = new DashboardMetrics
                {
                    // === USUARIOS ===
                    TotalUsuarios = await _context.Users.CountAsync(),
                    UsuariosActivos24h = await _context.Users.CountAsync(u => u.UltimaActividad >= hace24h),
                    UsuariosActivos7d = await _context.Users.CountAsync(u => u.UltimaActividad >= hace7d),
                    UsuariosNuevos24h = await _context.Users.CountAsync(u => u.FechaRegistro >= hace24h),
                    UsuariosNuevosSemana = await _context.Users.CountAsync(u => u.FechaRegistro >= hace7d),
                    UsuariosNuevosMes = await _context.Users.CountAsync(u => u.FechaRegistro >= hace30d),

                    // Creadores
                    TotalCreadores = await _context.Users.CountAsync(u => u.EsCreador),
                    CreadoresVerificados = await _context.Users.CountAsync(u => u.CreadorVerificado),
                    CreadoresNuevosMes = await _context.Users.CountAsync(u => u.EsCreador && u.FechaRegistro >= hace30d),

                    // === CONTENIDO ===
                    TotalContenidos = await _context.Contenidos.CountAsync(c => c.EstaActivo),
                    ContenidosHoy = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= hace24h),
                    ContenidosSemana = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= hace7d),
                    ContenidosMes = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= hace30d),

                    // Stories
                    StoriesActivas = await _context.Stories.CountAsync(s => s.EstaActivo && s.FechaExpiracion > ahora),
                    StoriesHoy = await _context.Stories.CountAsync(s => s.FechaPublicacion >= hace24h),

                    // === ENGAGEMENT ===
                    TotalLikes = await _context.Likes.CountAsync(),
                    LikesHoy = await _context.Likes.CountAsync(l => l.FechaLike >= hace24h),
                    TotalComentarios = await _context.Comentarios.CountAsync(),
                    ComentariosHoy = await _context.Comentarios.CountAsync(c => c.FechaCreacion >= hace24h),

                    // === MONETIZACIÓN ===
                    TotalSuscripciones = await _context.Suscripciones.CountAsync(s => s.EstaActiva),
                    SuscripcionesNuevasMes = await _context.Suscripciones.CountAsync(s => s.FechaInicio >= hace30d),

                    // Ingresos
                    IngresosHoy = await _context.Transacciones
                        .Where(t => t.FechaTransaccion >= hace24h && t.EstadoTransaccion == EstadoTransaccion.Completada)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0,
                    IngresosSemana = await _context.Transacciones
                        .Where(t => t.FechaTransaccion >= hace7d && t.EstadoTransaccion == EstadoTransaccion.Completada)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0,
                    IngresosMes = await _context.Transacciones
                        .Where(t => t.FechaTransaccion >= hace30d && t.EstadoTransaccion == EstadoTransaccion.Completada)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0,

                    // Tips
                    TotalTips = await _context.Tips.CountAsync(),
                    TipsHoy = await _context.Tips.CountAsync(t => t.FechaEnvio >= hace24h),
                    MontoTipsHoy = await _context.Tips
                        .Where(t => t.FechaEnvio >= hace24h)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0,

                    // === MODERACIÓN ===
                    ReportesPendientes = await _context.Reportes.CountAsync(r => r.Estado == "Pendiente"),
                    ReportesResueltos7d = await _context.Reportes.CountAsync(r => r.FechaResolucion >= hace7d),
                    ApelacionesPendientes = await _context.Apelaciones.CountAsync(a => a.Estado == EstadoApelacion.Pendiente),
                    ColaModeraciónPendiente = await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.Pendiente),

                    // Verificaciones
                    VerificacionesPendientes = await _context.CreatorVerificationRequests
                        .CountAsync(v => v.Estado == "Pendiente"),

                    // === FINANCIERO ===
                    RetirosPendientes = await _context.Transacciones
                        .CountAsync(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoTransaccion == EstadoTransaccion.Pendiente),
                    MontoRetirosPendientes = await _context.Transacciones
                        .Where(t => t.TipoTransaccion == TipoTransaccion.Retiro && t.EstadoTransaccion == EstadoTransaccion.Pendiente)
                        .SumAsync(t => (decimal?)t.Monto) ?? 0,

                    // === SEGURIDAD ===
                    IpsBloqueadas = await _context.IpsBloqueadas.CountAsync(i => i.EstaActivo),
                    IntentosAtaque24h = await _context.IntentosAtaque.CountAsync(i => i.Fecha >= hace24h),
                    UsuariosSuspendidos = await _context.Users.CountAsync(u => !u.EstaActivo || u.FechaSuspensionFin != null),

                    FechaActualizacion = ahora
                };

                _cache.Set(CACHE_KEY, metrics, CacheDuration);
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dashboard] Error obteniendo métricas");
                return new DashboardMetrics { FechaActualizacion = ahora };
            }
        }

        public async Task<List<DataPoint>> ObtenerSerieTemporalAsync(string tipo, int dias = 30)
        {
            var cacheKey = $"Dashboard_Serie_{tipo}_{dias}";
            if (_cache.TryGetValue(cacheKey, out List<DataPoint>? cached) && cached != null)
            {
                return cached;
            }

            var resultado = new List<DataPoint>();
            var ahora = DateTime.Now.Date;

            try
            {
                for (int i = dias - 1; i >= 0; i--)
                {
                    var fecha = ahora.AddDays(-i);
                    var fechaFin = fecha.AddDays(1);
                    double valor = 0;

                    switch (tipo.ToLower())
                    {
                        case "usuarios":
                            valor = await _context.Users.CountAsync(u => u.FechaRegistro >= fecha && u.FechaRegistro < fechaFin);
                            break;
                        case "contenidos":
                            valor = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= fecha && c.FechaPublicacion < fechaFin);
                            break;
                        case "ingresos":
                            valor = (double)(await _context.Transacciones
                                .Where(t => t.FechaTransaccion >= fecha && t.FechaTransaccion < fechaFin && t.EstadoTransaccion == EstadoTransaccion.Completada)
                                .SumAsync(t => (decimal?)t.Monto) ?? 0);
                            break;
                        case "likes":
                            valor = await _context.Likes.CountAsync(l => l.FechaLike >= fecha && l.FechaLike < fechaFin);
                            break;
                        case "comentarios":
                            valor = await _context.Comentarios.CountAsync(c => c.FechaCreacion >= fecha && c.FechaCreacion < fechaFin);
                            break;
                        case "suscripciones":
                            valor = await _context.Suscripciones.CountAsync(s => s.FechaInicio >= fecha && s.FechaInicio < fechaFin);
                            break;
                        case "reportes":
                            valor = await _context.Reportes.CountAsync(r => r.FechaReporte >= fecha && r.FechaReporte < fechaFin);
                            break;
                        case "stories":
                            valor = await _context.Stories.CountAsync(s => s.FechaPublicacion >= fecha && s.FechaPublicacion < fechaFin);
                            break;
                    }

                    resultado.Add(new DataPoint
                    {
                        Fecha = fecha,
                        Valor = valor,
                        Label = fecha.ToString("dd/MM")
                    });
                }

                _cache.Set(cacheKey, resultado, TimeSpan.FromMinutes(15));
                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dashboard] Error obteniendo serie temporal {Tipo}", tipo);
                return resultado;
            }
        }

        public async Task<DashboardResumen> ObtenerResumenRapidoAsync()
        {
            var ahora = DateTime.Now;
            var hace24h = ahora.AddHours(-24);

            return new DashboardResumen
            {
                UsuariosOnline = await _context.Users.CountAsync(u => u.UltimaActividad >= ahora.AddMinutes(-5)),
                ContenidosHoy = await _context.Contenidos.CountAsync(c => c.FechaPublicacion >= hace24h),
                IngresosHoy = await _context.Transacciones
                    .Where(t => t.FechaTransaccion >= hace24h && t.EstadoTransaccion == EstadoTransaccion.Completada)
                    .SumAsync(t => (decimal?)t.Monto) ?? 0,
                AlertasPendientes = await _context.Reportes.CountAsync(r => r.Estado == "Pendiente") +
                                    await _context.ColaModeracion.CountAsync(c => c.Estado == EstadoModeracion.Pendiente)
            };
        }
    }

    public class DashboardMetrics
    {
        // Usuarios
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos24h { get; set; }
        public int UsuariosActivos7d { get; set; }
        public int UsuariosNuevos24h { get; set; }
        public int UsuariosNuevosSemana { get; set; }
        public int UsuariosNuevosMes { get; set; }
        public int TotalCreadores { get; set; }
        public int CreadoresVerificados { get; set; }
        public int CreadoresNuevosMes { get; set; }

        // Contenido
        public int TotalContenidos { get; set; }
        public int ContenidosHoy { get; set; }
        public int ContenidosSemana { get; set; }
        public int ContenidosMes { get; set; }
        public int StoriesActivas { get; set; }
        public int StoriesHoy { get; set; }

        // Engagement
        public int TotalLikes { get; set; }
        public int LikesHoy { get; set; }
        public int TotalComentarios { get; set; }
        public int ComentariosHoy { get; set; }

        // Monetización
        public int TotalSuscripciones { get; set; }
        public int SuscripcionesNuevasMes { get; set; }
        public decimal IngresosHoy { get; set; }
        public decimal IngresosSemana { get; set; }
        public decimal IngresosMes { get; set; }
        public int TotalTips { get; set; }
        public int TipsHoy { get; set; }
        public decimal MontoTipsHoy { get; set; }

        // Moderación
        public int ReportesPendientes { get; set; }
        public int ReportesResueltos7d { get; set; }
        public int ApelacionesPendientes { get; set; }
        public int ColaModeraciónPendiente { get; set; }
        public int VerificacionesPendientes { get; set; }

        // Financiero
        public int RetirosPendientes { get; set; }
        public decimal MontoRetirosPendientes { get; set; }

        // Seguridad
        public int IpsBloqueadas { get; set; }
        public int IntentosAtaque24h { get; set; }
        public int UsuariosSuspendidos { get; set; }

        public DateTime FechaActualizacion { get; set; }
    }

    public class DataPoint
    {
        public DateTime Fecha { get; set; }
        public double Valor { get; set; }
        public string Label { get; set; } = "";
    }

    public class DashboardResumen
    {
        public int UsuariosOnline { get; set; }
        public int ContenidosHoy { get; set; }
        public decimal IngresosHoy { get; set; }
        public int AlertasPendientes { get; set; }
    }
}
