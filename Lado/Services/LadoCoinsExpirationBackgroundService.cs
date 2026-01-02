using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que procesa vencimientos de LadoCoins cada hora.
    /// También envía notificaciones 7 días antes del vencimiento.
    /// </summary>
    public class LadoCoinsExpirationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LadoCoinsExpirationBackgroundService> _logger;
        private readonly TimeSpan _intervalo = TimeSpan.FromHours(1);

        public LadoCoinsExpirationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<LadoCoinsExpirationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LadoCoinsExpirationBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcesarVencimientosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando vencimientos de LadoCoins");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }

            _logger.LogInformation("LadoCoinsExpirationBackgroundService detenido");
        }

        private async Task ProcesarVencimientosAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var ladoCoinsService = scope.ServiceProvider.GetRequiredService<ILadoCoinsService>();

            _logger.LogDebug("Iniciando procesamiento de vencimientos de LadoCoins");

            await ladoCoinsService.ProcesarVencimientosAsync();

            _logger.LogDebug("Procesamiento de vencimientos completado");
        }
    }
}
