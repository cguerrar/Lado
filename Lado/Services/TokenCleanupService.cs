using Lado.Data;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en segundo plano que limpia tokens expirados periódicamente
    /// Evita el crecimiento infinito de las tablas ActiveTokens y RefreshTokens
    /// </summary>
    public class TokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1); // Ejecutar cada hora

        public TokenCleanupService(
            IServiceProvider serviceProvider,
            ILogger<TokenCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TokenCleanupService iniciado - Intervalo: {Interval}", _interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredTokensAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en limpieza de tokens");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CleanupExpiredTokensAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            // Limpiar ActiveTokens expirados (más de 24 horas de antigüedad para dar margen)
            var cutoffDate = now.AddHours(-24);

            var expiredActiveTokens = await context.ActiveTokens
                .Where(t => t.ExpiresAt < cutoffDate)
                .ToListAsync();

            if (expiredActiveTokens.Any())
            {
                context.ActiveTokens.RemoveRange(expiredActiveTokens);
                _logger.LogInformation("Eliminados {Count} ActiveTokens expirados", expiredActiveTokens.Count);
            }

            // Limpiar RefreshTokens expirados o revocados (más de 7 días de antigüedad)
            var refreshCutoffDate = now.AddDays(-7);

            var expiredRefreshTokens = await context.RefreshTokens
                .Where(t => t.ExpiryDate < refreshCutoffDate || (t.IsRevoked && t.CreatedAt < refreshCutoffDate))
                .ToListAsync();

            if (expiredRefreshTokens.Any())
            {
                context.RefreshTokens.RemoveRange(expiredRefreshTokens);
                _logger.LogInformation("Eliminados {Count} RefreshTokens expirados/revocados", expiredRefreshTokens.Count);
            }

            if (expiredActiveTokens.Any() || expiredRefreshTokens.Any())
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Limpieza de tokens completada - ActiveTokens: {Active}, RefreshTokens: {Refresh}",
                    expiredActiveTokens.Count, expiredRefreshTokens.Count);
            }
        }
    }
}
