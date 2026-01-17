using Lado.Data;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que expira suscripciones temporales (24h, 7 días)
    /// y procesa renovaciones automáticas para suscripciones mensuales.
    /// </summary>
    public class SuscripcionExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SuscripcionExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30); // Verificar cada 30 minutos

        public SuscripcionExpirationService(
            IServiceProvider serviceProvider,
            ILogger<SuscripcionExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SuscripcionExpirationService iniciado");

            // Esperar 1 minuto antes de la primera ejecución para dar tiempo a la app de iniciar
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcesarSuscripcionesExpiradas(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar suscripciones expiradas");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("SuscripcionExpirationService detenido");
        }

        private async Task ProcesarSuscripcionesExpiradas(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var suscripcionCache = scope.ServiceProvider.GetRequiredService<ISuscripcionCacheService>();

            var ahora = DateTime.Now;

            // Buscar suscripciones activas que han expirado
            var suscripcionesExpiradas = await context.Suscripciones
                .Include(s => s.Creador)
                .Where(s => s.EstaActiva
                    && s.FechaFin.HasValue
                    && s.FechaFin.Value < ahora)
                .ToListAsync(stoppingToken);

            if (suscripcionesExpiradas.Count == 0)
            {
                _logger.LogDebug("No hay suscripciones expiradas para procesar");
                return;
            }

            _logger.LogInformation("Procesando {Count} suscripciones expiradas", suscripcionesExpiradas.Count);

            foreach (var suscripcion in suscripcionesExpiradas)
            {
                try
                {
                    // Desactivar suscripción
                    suscripcion.EstaActiva = false;
                    suscripcion.FechaCancelacion = ahora;

                    // Decrementar contador de seguidores del creador
                    if (suscripcion.Creador != null && suscripcion.Creador.NumeroSeguidores > 0)
                    {
                        suscripcion.Creador.NumeroSeguidores--;
                    }

                    _logger.LogInformation(
                        "Suscripción {SuscripcionId} expirada. Fan: {FanId}, Creador: {CreadorId}, Duración: {Duracion}",
                        suscripcion.Id,
                        suscripcion.FanId,
                        suscripcion.CreadorId,
                        suscripcion.Duracion);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al procesar suscripción {SuscripcionId}", suscripcion.Id);
                }
            }

            await context.SaveChangesAsync(stoppingToken);

            // Invalidar caché de suscripciones expiradas
            foreach (var suscripcion in suscripcionesExpiradas)
            {
                suscripcionCache.InvalidarCache(suscripcion.FanId, suscripcion.CreadorId);
            }

            _logger.LogInformation("Se expiraron {Count} suscripciones correctamente", suscripcionesExpiradas.Count);
        }
    }
}
