using Lado.Data;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que limpia logs antiguos cada 24 horas
    /// </summary>
    public class LogCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LogCleanupService> _logger;
        private readonly TimeSpan _intervalo = TimeSpan.FromHours(24);
        private readonly int _diasRetencion = 30;

        public LogCleanupService(IServiceProvider serviceProvider, ILogger<LogCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogCleanupService iniciado. Limpieza cada {Horas} horas, retencion: {Dias} dias",
                _intervalo.TotalHours, _diasRetencion);

            // Esperar un poco antes de la primera ejecución (5 minutos después del inicio)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await LimpiarLogsAntiguosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en LogCleanupService al limpiar logs");
                }

                // Esperar hasta la próxima ejecución
                await Task.Delay(_intervalo, stoppingToken);
            }
        }

        private async Task LimpiarLogsAntiguosAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var fechaLimite = DateTime.UtcNow.AddDays(-_diasRetencion);

            var eliminados = await context.LogEventos
                .Where(l => l.Fecha < fechaLimite)
                .ExecuteDeleteAsync();

            if (eliminados > 0)
            {
                _logger.LogInformation("LogCleanupService: Eliminados {Count} logs anteriores a {Fecha}",
                    eliminados, fechaLimite);
            }
        }
    }
}
