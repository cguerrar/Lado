using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en background que verifica suspensiones temporales expiradas
    /// y las levanta automáticamente, notificando al usuario
    /// </summary>
    public class SuspensionExpirationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SuspensionExpirationService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Verificar cada 5 minutos

        public SuspensionExpirationService(
            IServiceProvider serviceProvider,
            ILogger<SuspensionExpirationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SuspensionExpiration] Servicio iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await VerificarSuspensionesExpiradas(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SuspensionExpiration] Error verificando suspensiones");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task VerificarSuspensionesExpiradas(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logService = scope.ServiceProvider.GetRequiredService<ILogEventoService>();

            var ahora = DateTime.Now;

            // Buscar usuarios con suspensión temporal expirada
            var usuariosSuspendidos = await context.Users
                .Where(u => u.FechaSuspensionFin != null &&
                           u.FechaSuspensionFin <= ahora &&
                           u.EstaActivo) // Solo los que tienen suspensión temporal (no permanente)
                .ToListAsync(stoppingToken);

            if (usuariosSuspendidos.Count == 0)
                return;

            _logger.LogInformation("[SuspensionExpiration] Encontrados {Count} usuarios con suspensión expirada",
                usuariosSuspendidos.Count);

            foreach (var usuario in usuariosSuspendidos)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    // Guardar datos para el log antes de limpiar
                    var razonOriginal = usuario.RazonSuspension;
                    var fechaSuspension = usuario.FechaSuspension;
                    var duracion = usuario.FechaSuspensionFin.HasValue && fechaSuspension.HasValue
                        ? (usuario.FechaSuspensionFin.Value - fechaSuspension.Value).TotalDays
                        : 0;

                    // Limpiar campos de suspensión
                    usuario.FechaSuspensionFin = null;
                    usuario.RazonSuspension = null;
                    usuario.FechaSuspension = null;
                    usuario.SuspendidoPorId = null;

                    // Crear notificación para el usuario
                    var notificacion = new Notificacion
                    {
                        UsuarioId = usuario.Id,
                        Tipo = TipoNotificacion.SuspensionLevantada,
                        Titulo = "Suspensión Levantada",
                        Mensaje = "Tu suspensión temporal ha finalizado. Ya puedes usar tu cuenta normalmente.",
                        UrlDestino = "/Feed",
                        FechaCreacion = DateTime.Now,
                        Leida = false,
                        EstaActiva = true
                    };

                    context.Notificaciones.Add(notificacion);
                    await context.SaveChangesAsync(stoppingToken);

                    // Registrar en logs
                    await logService.RegistrarEventoAsync(
                        $"Suspensión temporal levantada automáticamente",
                        CategoriaEvento.Sistema,
                        TipoLogEvento.Info,
                        usuario.Id,
                        usuario.NombreCompleto ?? usuario.UserName,
                        $"Duración original: {duracion:F0} días. Razón: {razonOriginal ?? "No especificada"}"
                    );

                    _logger.LogInformation("[SuspensionExpiration] Suspensión levantada para usuario {UserId} ({UserName})",
                        usuario.Id, usuario.UserName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SuspensionExpiration] Error levantando suspensión para usuario {UserId}",
                        usuario.Id);
                }
            }
        }
    }
}
