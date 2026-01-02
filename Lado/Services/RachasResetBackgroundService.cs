using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que resetea contadores diarios a medianoche UTC.
    /// Se ejecuta cada 30 minutos para verificar si es un nuevo día.
    /// </summary>
    public class RachasResetBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RachasResetBackgroundService> _logger;
        private readonly TimeSpan _intervalo = TimeSpan.FromMinutes(30);
        private DateTime _ultimoReset = DateTime.MinValue;

        public RachasResetBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<RachasResetBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RachasResetBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var hoyUtc = DateTime.UtcNow.Date;

                    // Solo ejecutar si es un nuevo día
                    if (_ultimoReset.Date < hoyUtc)
                    {
                        await ResetearContadoresAsync();
                        _ultimoReset = hoyUtc;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reseteando contadores de rachas");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }

            _logger.LogInformation("RachasResetBackgroundService detenido");
        }

        private async Task ResetearContadoresAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var rachasService = scope.ServiceProvider.GetRequiredService<IRachasService>();

            _logger.LogInformation("Iniciando reset de contadores diarios");

            await rachasService.ResetearContadoresDiariosAsync();

            _logger.LogInformation("Reset de contadores diarios completado");
        }
    }
}
