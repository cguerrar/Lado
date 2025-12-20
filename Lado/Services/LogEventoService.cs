using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    public interface ILogEventoService
    {
        Task RegistrarErrorAsync(Exception ex, CategoriaEvento categoria, string? usuarioId = null, string? usuarioNombre = null);
        Task RegistrarEventoAsync(string mensaje, CategoriaEvento categoria, TipoLogEvento tipo = TipoLogEvento.Evento, string? usuarioId = null, string? usuarioNombre = null, string? detalle = null);
        Task<LogEventosResultado> ObtenerLogsAsync(LogEventosFiltro filtro);
        Task<LogEventosEstadisticas> ObtenerEstadisticasAsync();
        Task<int> LimpiarLogsAntiguosAsync(int diasRetención = 30);
    }

    public class LogEventoService : ILogEventoService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<LogEventoService> _logger;

        public LogEventoService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<LogEventoService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task RegistrarErrorAsync(Exception ex, CategoriaEvento categoria, string? usuarioId = null, string? usuarioNombre = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                var log = new LogEvento
                {
                    Fecha = DateTime.UtcNow,
                    Tipo = TipoLogEvento.Error,
                    Categoria = categoria,
                    Mensaje = ex.Message.Length > 500 ? ex.Message.Substring(0, 500) : ex.Message,
                    Detalle = ex.ToString(),
                    TipoExcepcion = ex.GetType().Name,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Url = GetRequestUrl(httpContext),
                    MetodoHttp = httpContext?.Request?.Method
                };

                _context.LogEventos.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                // No queremos que el logging cause más errores
                _logger.LogError(logEx, "Error al registrar log de error");
            }
        }

        public async Task RegistrarEventoAsync(
            string mensaje,
            CategoriaEvento categoria,
            TipoLogEvento tipo = TipoLogEvento.Evento,
            string? usuarioId = null,
            string? usuarioNombre = null,
            string? detalle = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                var log = new LogEvento
                {
                    Fecha = DateTime.UtcNow,
                    Tipo = tipo,
                    Categoria = categoria,
                    Mensaje = mensaje.Length > 500 ? mensaje.Substring(0, 500) : mensaje,
                    Detalle = detalle,
                    UsuarioId = usuarioId,
                    UsuarioNombre = usuarioNombre,
                    IpAddress = GetClientIpAddress(httpContext),
                    UserAgent = GetUserAgent(httpContext),
                    Url = GetRequestUrl(httpContext),
                    MetodoHttp = httpContext?.Request?.Method
                };

                _context.LogEventos.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Error al registrar evento");
            }
        }

        public async Task<LogEventosResultado> ObtenerLogsAsync(LogEventosFiltro filtro)
        {
            var query = _context.LogEventos.AsQueryable();

            // Filtros
            if (filtro.Tipo.HasValue)
                query = query.Where(l => l.Tipo == filtro.Tipo.Value);

            if (filtro.Categoria.HasValue)
                query = query.Where(l => l.Categoria == filtro.Categoria.Value);

            if (filtro.FechaDesde.HasValue)
                query = query.Where(l => l.Fecha >= filtro.FechaDesde.Value);

            if (filtro.FechaHasta.HasValue)
                query = query.Where(l => l.Fecha <= filtro.FechaHasta.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                var busqueda = filtro.Busqueda.ToLower();
                query = query.Where(l =>
                    l.Mensaje.ToLower().Contains(busqueda) ||
                    (l.UsuarioNombre != null && l.UsuarioNombre.ToLower().Contains(busqueda)) ||
                    (l.Detalle != null && l.Detalle.ToLower().Contains(busqueda)));
            }

            // Total antes de paginar
            var total = await query.CountAsync();

            // Ordenar y paginar
            var logs = await query
                .OrderByDescending(l => l.Fecha)
                .Skip((filtro.Pagina - 1) * filtro.TamanoPagina)
                .Take(filtro.TamanoPagina)
                .ToListAsync();

            return new LogEventosResultado
            {
                Logs = logs,
                Total = total,
                Pagina = filtro.Pagina,
                TamanoPagina = filtro.TamanoPagina,
                TotalPaginas = (int)Math.Ceiling((double)total / filtro.TamanoPagina)
            };
        }

        public async Task<LogEventosEstadisticas> ObtenerEstadisticasAsync()
        {
            var hoy = DateTime.UtcNow.Date;
            var hace24h = DateTime.UtcNow.AddHours(-24);
            var hace7d = DateTime.UtcNow.AddDays(-7);

            var stats = new LogEventosEstadisticas
            {
                ErroresHoy = await _context.LogEventos
                    .CountAsync(l => l.Tipo == TipoLogEvento.Error && l.Fecha >= hoy),

                ErroresUltimas24h = await _context.LogEventos
                    .CountAsync(l => l.Tipo == TipoLogEvento.Error && l.Fecha >= hace24h),

                WarningsHoy = await _context.LogEventos
                    .CountAsync(l => l.Tipo == TipoLogEvento.Warning && l.Fecha >= hoy),

                EventosHoy = await _context.LogEventos
                    .CountAsync(l => l.Tipo == TipoLogEvento.Evento && l.Fecha >= hoy),

                TotalUltimos7Dias = await _context.LogEventos
                    .CountAsync(l => l.Fecha >= hace7d),

                TotalGeneral = await _context.LogEventos.CountAsync()
            };

            // Errores por categoría (últimos 7 días)
            stats.ErroresPorCategoria = await _context.LogEventos
                .Where(l => l.Tipo == TipoLogEvento.Error && l.Fecha >= hace7d)
                .GroupBy(l => l.Categoria)
                .Select(g => new { Categoria = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Categoria.ToString(), x => x.Count);

            return stats;
        }

        public async Task<int> LimpiarLogsAntiguosAsync(int diasRetención = 30)
        {
            var fechaLimite = DateTime.UtcNow.AddDays(-diasRetención);

            var logsAEliminar = await _context.LogEventos
                .Where(l => l.Fecha < fechaLimite)
                .ToListAsync();

            if (logsAEliminar.Any())
            {
                _context.LogEventos.RemoveRange(logsAEliminar);
                await _context.SaveChangesAsync();
            }

            return logsAEliminar.Count;
        }

        // Helpers para obtener info del request
        private string? GetClientIpAddress(HttpContext? context)
        {
            if (context == null) return null;

            var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
                ip = context.Connection.RemoteIpAddress?.ToString();

            return ip?.Length > 45 ? ip.Substring(0, 45) : ip;
        }

        private string? GetUserAgent(HttpContext? context)
        {
            var ua = context?.Request.Headers["User-Agent"].ToString();
            return ua?.Length > 500 ? ua.Substring(0, 500) : ua;
        }

        private string? GetRequestUrl(HttpContext? context)
        {
            if (context == null) return null;
            var url = $"{context.Request.Path}{context.Request.QueryString}";
            return url.Length > 2000 ? url.Substring(0, 2000) : url;
        }
    }

    // Clases auxiliares para filtros y resultados
    public class LogEventosFiltro
    {
        public TipoLogEvento? Tipo { get; set; }
        public CategoriaEvento? Categoria { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string? Busqueda { get; set; }
        public int Pagina { get; set; } = 1;
        public int TamanoPagina { get; set; } = 50;
    }

    public class LogEventosResultado
    {
        public List<LogEvento> Logs { get; set; } = new();
        public int Total { get; set; }
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; }
        public int TotalPaginas { get; set; }
    }

    public class LogEventosEstadisticas
    {
        public int ErroresHoy { get; set; }
        public int ErroresUltimas24h { get; set; }
        public int WarningsHoy { get; set; }
        public int EventosHoy { get; set; }
        public int TotalUltimos7Dias { get; set; }
        public int TotalGeneral { get; set; }
        public Dictionary<string, int> ErroresPorCategoria { get; set; } = new();
    }
}
