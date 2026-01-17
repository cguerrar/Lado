using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface IMantenimientoService
    {
        Task<ModoMantenimiento> ObtenerConfiguracionAsync();
        Task<bool> EstaEnMantenimientoAsync();
        Task ActivarMantenimientoAsync(string adminId, string? mensaje = null, DateTime? finEstimado = null);
        Task DesactivarMantenimientoAsync(string adminId);
        Task ProgramarMantenimientoAsync(string adminId, DateTime inicio, DateTime finEstimado, string? mensaje = null);
        Task<bool> EsRutaPermitidaAsync(string ruta);
        Task ActualizarConfiguracionAsync(ModoMantenimiento config);
        Task<List<HistorialMantenimiento>> ObtenerHistorialAsync(int cantidad = 20);
    }

    public class MantenimientoService : IMantenimientoService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<MantenimientoService> _logger;
        private readonly ILogEventoService _logService;
        private const string CACHE_KEY = "ModoMantenimiento";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        public MantenimientoService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<MantenimientoService> logger,
            ILogEventoService logService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _logService = logService;
        }

        public async Task<ModoMantenimiento> ObtenerConfiguracionAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out ModoMantenimiento? cached) && cached != null)
            {
                return cached;
            }

            var config = await _context.Set<ModoMantenimiento>().FirstOrDefaultAsync();
            if (config == null)
            {
                // Crear configuración por defecto
                config = new ModoMantenimiento();
                _context.Set<ModoMantenimiento>().Add(config);
                await _context.SaveChangesAsync();
            }

            _cache.Set(CACHE_KEY, config, CacheDuration);
            return config;
        }

        public async Task<bool> EstaEnMantenimientoAsync()
        {
            var config = await ObtenerConfiguracionAsync();

            // Si está activo manualmente
            if (config.EstaActivo) return true;

            // Si tiene mantenimiento programado y ya llegó la hora
            if (config.FechaInicio.HasValue && config.FechaInicio.Value <= DateTime.Now)
            {
                // Verificar si no ha terminado
                if (!config.FechaFinEstimado.HasValue || config.FechaFinEstimado.Value > DateTime.Now)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> EsRutaPermitidaAsync(string ruta)
        {
            var config = await ObtenerConfiguracionAsync();

            if (string.IsNullOrEmpty(config.RutasPermitidas))
                return false;

            var rutasPermitidas = config.RutasPermitidas
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim().ToLower())
                .ToList();

            var rutaNormalizada = ruta.ToLower();

            return rutasPermitidas.Any(r => rutaNormalizada.StartsWith(r));
        }

        public async Task ActivarMantenimientoAsync(string adminId, string? mensaje = null, DateTime? finEstimado = null)
        {
            var config = await ObtenerConfiguracionAsync();

            config.EstaActivo = true;
            config.FechaInicio = DateTime.Now;
            config.FechaFinEstimado = finEstimado;
            config.ActivadoPorId = adminId;
            config.FechaActualizacion = DateTime.Now;

            if (!string.IsNullOrEmpty(mensaje))
            {
                config.Mensaje = mensaje;
            }

            await _context.SaveChangesAsync();
            InvalidateCache();

            // Registrar en historial
            var historial = new HistorialMantenimiento
            {
                FechaInicio = DateTime.Now,
                Titulo = config.Titulo,
                Mensaje = config.Mensaje,
                ActivadoPorId = adminId
            };
            _context.Set<HistorialMantenimiento>().Add(historial);
            await _context.SaveChangesAsync();

            _logger.LogWarning("[Mantenimiento] ACTIVADO por admin {AdminId}", adminId);

            await _logService.RegistrarEventoAsync(
                "Modo mantenimiento ACTIVADO",
                CategoriaEvento.Admin,
                TipoLogEvento.Warning,
                adminId,
                null,
                $"Fin estimado: {finEstimado?.ToString("dd/MM/yyyy HH:mm") ?? "No especificado"}"
            );
        }

        public async Task DesactivarMantenimientoAsync(string adminId)
        {
            var config = await ObtenerConfiguracionAsync();

            var duracion = config.FechaInicio.HasValue
                ? (int)(DateTime.Now - config.FechaInicio.Value).TotalMinutes
                : 0;

            config.EstaActivo = false;
            config.FechaInicio = null;
            config.FechaFinEstimado = null;
            config.NotificacionPreviaEnviada = false;
            config.FechaActualizacion = DateTime.Now;

            await _context.SaveChangesAsync();
            InvalidateCache();

            // Actualizar historial
            var ultimoHistorial = await _context.Set<HistorialMantenimiento>()
                .OrderByDescending(h => h.FechaInicio)
                .FirstOrDefaultAsync(h => h.FechaFin == null);

            if (ultimoHistorial != null)
            {
                ultimoHistorial.FechaFin = DateTime.Now;
                ultimoHistorial.DesactivadoPorId = adminId;
                ultimoHistorial.DuracionMinutos = duracion;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("[Mantenimiento] DESACTIVADO por admin {AdminId}. Duración: {Duracion} minutos",
                adminId, duracion);

            await _logService.RegistrarEventoAsync(
                "Modo mantenimiento DESACTIVADO",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                adminId,
                null,
                $"Duración total: {duracion} minutos"
            );
        }

        public async Task ProgramarMantenimientoAsync(string adminId, DateTime inicio, DateTime finEstimado, string? mensaje = null)
        {
            var config = await ObtenerConfiguracionAsync();

            config.EstaActivo = false; // No activo aún, solo programado
            config.FechaInicio = inicio;
            config.FechaFinEstimado = finEstimado;
            config.ActivadoPorId = adminId;
            config.NotificacionPreviaEnviada = false;
            config.FechaActualizacion = DateTime.Now;

            if (!string.IsNullOrEmpty(mensaje))
            {
                config.Mensaje = mensaje;
            }

            await _context.SaveChangesAsync();
            InvalidateCache();

            _logger.LogInformation("[Mantenimiento] PROGRAMADO por admin {AdminId}. Inicio: {Inicio}, Fin: {Fin}",
                adminId, inicio, finEstimado);

            await _logService.RegistrarEventoAsync(
                "Mantenimiento PROGRAMADO",
                CategoriaEvento.Admin,
                TipoLogEvento.Info,
                adminId,
                null,
                $"Inicio: {inicio:dd/MM/yyyy HH:mm}, Fin estimado: {finEstimado:dd/MM/yyyy HH:mm}"
            );
        }

        public async Task ActualizarConfiguracionAsync(ModoMantenimiento config)
        {
            config.FechaActualizacion = DateTime.Now;
            _context.Set<ModoMantenimiento>().Update(config);
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        public async Task<List<HistorialMantenimiento>> ObtenerHistorialAsync(int cantidad = 20)
        {
            return await _context.Set<HistorialMantenimiento>()
                .Include(h => h.ActivadoPor)
                .Include(h => h.DesactivadoPor)
                .OrderByDescending(h => h.FechaInicio)
                .Take(cantidad)
                .ToListAsync();
        }

        private void InvalidateCache()
        {
            _cache.Remove(CACHE_KEY);
        }
    }

    /// <summary>
    /// Background service que verifica mantenimientos programados
    /// </summary>
    public class MantenimientoBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MantenimientoBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public MantenimientoBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MantenimientoBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[MantenimientoBackground] Servicio iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await VerificarMantenimientoProgramado(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MantenimientoBackground] Error verificando mantenimiento");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task VerificarMantenimientoProgramado(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var config = await context.Set<ModoMantenimiento>().FirstOrDefaultAsync(stoppingToken);
            if (config == null) return;

            var ahora = DateTime.Now;

            // Si hay mantenimiento programado que aún no ha empezado
            if (!config.EstaActivo && config.FechaInicio.HasValue)
            {
                // Verificar si ya es hora de activar
                if (config.FechaInicio.Value <= ahora)
                {
                    config.EstaActivo = true;
                    config.FechaActualizacion = ahora;
                    await context.SaveChangesAsync(stoppingToken);

                    // Crear registro en historial
                    var historial = new HistorialMantenimiento
                    {
                        FechaInicio = ahora,
                        Titulo = config.Titulo,
                        Mensaje = config.Mensaje,
                        ActivadoPorId = config.ActivadoPorId
                    };
                    context.Set<HistorialMantenimiento>().Add(historial);
                    await context.SaveChangesAsync(stoppingToken);

                    _logger.LogWarning("[MantenimientoBackground] Mantenimiento programado INICIADO automáticamente");
                }
            }

            // Si está activo y ya pasó la hora de fin estimado, desactivar automáticamente
            if (config.EstaActivo && config.FechaFinEstimado.HasValue && config.FechaFinEstimado.Value <= ahora)
            {
                var duracion = config.FechaInicio.HasValue
                    ? (int)(ahora - config.FechaInicio.Value).TotalMinutes
                    : 0;

                config.EstaActivo = false;
                config.FechaInicio = null;
                config.FechaFinEstimado = null;
                config.NotificacionPreviaEnviada = false;
                config.FechaActualizacion = ahora;
                await context.SaveChangesAsync(stoppingToken);

                // Actualizar historial
                var ultimoHistorial = await context.Set<HistorialMantenimiento>()
                    .OrderByDescending(h => h.FechaInicio)
                    .FirstOrDefaultAsync(h => h.FechaFin == null, stoppingToken);

                if (ultimoHistorial != null)
                {
                    ultimoHistorial.FechaFin = ahora;
                    ultimoHistorial.DuracionMinutos = duracion;
                    ultimoHistorial.Notas = "Finalizado automáticamente";
                    await context.SaveChangesAsync(stoppingToken);
                }

                _logger.LogInformation("[MantenimientoBackground] Mantenimiento FINALIZADO automáticamente. Duración: {Duracion} minutos", duracion);
            }
        }
    }
}
