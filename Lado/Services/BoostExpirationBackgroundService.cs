using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en background que expira los boosts de visibilidad
    /// cuando ha pasado la fecha de expiración.
    /// </summary>
    public class BoostExpirationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BoostExpirationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public BoostExpirationBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<BoostExpirationBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("⭐ BoostExpirationBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpirarBoostsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error en BoostExpirationBackgroundService");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("⭐ BoostExpirationBackgroundService detenido");
        }

        private async Task ExpirarBoostsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logService = scope.ServiceProvider.GetRequiredService<ILogEventoService>();

            var ahora = DateTime.Now;

            // Buscar usuarios con boosts expirados
            var usuariosConBoostExpirado = await context.Users
                .Where(u => u.BoostMultiplicador > 1.0m &&
                           u.BoostFechaFin.HasValue &&
                           u.BoostFechaFin.Value <= ahora)
                .ToListAsync();

            if (!usuariosConBoostExpirado.Any())
            {
                return;
            }

            _logger.LogInformation("⏰ Expirando boosts de {Count} usuarios", usuariosConBoostExpirado.Count);

            foreach (var usuario in usuariosConBoostExpirado)
            {
                var boostAnterior = usuario.BoostMultiplicador;
                usuario.BoostMultiplicador = 1.0m;
                usuario.BoostFechaFin = null;

                await logService.RegistrarEventoAsync(
                    $"Boost expirado para usuario {usuario.UserName}",
                    CategoriaEvento.Sistema,
                    TipoLogEvento.Info,
                    usuario.Id,
                    usuario.NombreCompleto ?? usuario.UserName,
                    $"Multiplicador anterior: {boostAnterior}x"
                );
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("✅ {Count} boosts expirados correctamente", usuariosConBoostExpirado.Count);
        }
    }
}
