using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que:
    /// - Verifica si referidos se convirtieron en creadores LadoB (cada 4 horas)
    /// - Expira comisiones después de 3 meses (una vez al día)
    /// </summary>
    public class ReferidosBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReferidosBackgroundService> _logger;
        private readonly TimeSpan _intervalo = TimeSpan.FromHours(4);
        private DateTime _ultimaExpiracion = DateTime.MinValue;

        public ReferidosBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ReferidosBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReferidosBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Verificar creadores LadoB
                    await VerificarCreadoresLadoBAsync();

                    // Expirar comisiones (una vez al día)
                    var hoy = DateTime.UtcNow.Date;
                    if (_ultimaExpiracion.Date < hoy)
                    {
                        await ExpirarComisionesAsync();
                        _ultimaExpiracion = hoy;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en procesamiento de referidos");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }

            _logger.LogInformation("ReferidosBackgroundService detenido");
        }

        private async Task VerificarCreadoresLadoBAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var referidosService = scope.ServiceProvider.GetRequiredService<IReferidosService>();

            _logger.LogDebug("Verificando referidos que se convirtieron en creadores LadoB");

            // Buscar referidos que no han recibido el bono de creador LadoB
            var referidosPendientes = await context.Referidos
                .Where(r => !r.BonoCreadorLadoBEntregado)
                .Select(r => r.ReferidoUsuarioId)
                .ToListAsync();

            foreach (var usuarioId in referidosPendientes)
            {
                await referidosService.ProcesarCreadorLadoBAsync(usuarioId);
            }

            if (referidosPendientes.Count > 0)
            {
                _logger.LogInformation("Verificados {Count} referidos para bono de creador LadoB", referidosPendientes.Count);
            }
        }

        private async Task ExpirarComisionesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var referidosService = scope.ServiceProvider.GetRequiredService<IReferidosService>();

            _logger.LogInformation("Expirando comisiones de referidos vencidas");

            await referidosService.ExpirarComisionesAsync();

            _logger.LogInformation("Expiración de comisiones completada");
        }
    }
}
