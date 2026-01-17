using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;

namespace Lado.Services
{
    /// <summary>
    /// Servicio en background que procesa la publicación automática de contenido
    /// para usuarios administrados. Revisa periódicamente si hay contenido
    /// programado que deba publicarse.
    /// </summary>
    public class PublicacionAutomaticaBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PublicacionAutomaticaBackgroundService> _logger;
        private readonly TimeSpan _intervalo = TimeSpan.FromMinutes(5); // Revisar cada 5 minutos

        public PublicacionAutomaticaBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PublicacionAutomaticaBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PublicacionAutomaticaBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcesarPublicacionesProgramadas(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en PublicacionAutomaticaBackgroundService");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }

            _logger.LogInformation("PublicacionAutomaticaBackgroundService detenido");
        }

        private async Task ProcesarPublicacionesProgramadas(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var ahora = DateTime.Now;

            // Buscar contenido programado que ya debió publicarse
            var mediasPendientes = await context.MediaBiblioteca
                .Include(m => m.Usuario)
                .Where(m => m.Estado == EstadoMediaBiblioteca.Programado &&
                           m.FechaProgramada != null &&
                           m.FechaProgramada <= ahora &&
                           m.IntentosPublicacion < 3) // Máximo 3 intentos
                .OrderBy(m => m.FechaProgramada)
                .Take(10) // Procesar de a 10 para no sobrecargar
                .ToListAsync(stoppingToken);

            if (!mediasPendientes.Any())
            {
                return;
            }

            _logger.LogInformation("Procesando {Count} publicaciones programadas", mediasPendientes.Count);

            foreach (var media in mediasPendientes)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await PublicarMedia(context, media);
                    _logger.LogInformation("Publicado contenido ID {MediaId} para usuario {UserId}",
                        media.Id, media.UsuarioId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al publicar media ID {MediaId}", media.Id);

                    media.IntentosPublicacion++;
                    media.MensajeError = ex.Message;

                    if (media.IntentosPublicacion >= 3)
                    {
                        media.Estado = EstadoMediaBiblioteca.Error;
                    }
                }
            }

            await context.SaveChangesAsync(stoppingToken);

            // Actualizar contadores de publicaciones diarias
            await ActualizarContadoresDiarios(context, stoppingToken);
        }

        private async Task PublicarMedia(ApplicationDbContext context, MediaBiblioteca media)
        {
            if (media.TipoPublicacion == TipoPublicacionMedia.Story)
            {
                await PublicarComoStory(context, media);
            }
            else
            {
                await PublicarComoContenido(context, media);
            }

            // Actualizar configuración de publicación
            var config = await context.ConfiguracionesPublicacionAutomatica
                .FirstOrDefaultAsync(c => c.UsuarioId == media.UsuarioId);

            if (config != null)
            {
                config.UltimaPublicacion = DateTime.Now;
                config.TotalPublicaciones++;

                // Incrementar contador diario
                var hoy = DateTime.Today;
                if (config.FechaUltimoReset?.Date != hoy)
                {
                    config.PublicacionesHoy = 1;
                    config.FechaUltimoReset = hoy;
                }
                else
                {
                    config.PublicacionesHoy++;
                }
            }

            await context.SaveChangesAsync();
        }

        private async Task PublicarComoContenido(ApplicationDbContext context, MediaBiblioteca media)
        {
            // Crear el contenido
            var contenido = new Contenido
            {
                UsuarioId = media.UsuarioId,
                Descripcion = ConstruirTextoPublicacion(media),
                TipoContenido = media.TipoMedia == TipoMediaBiblioteca.Video
                    ? TipoContenido.Video
                    : TipoContenido.Imagen,
                RutaArchivo = media.RutaArchivo, // Asignar también aquí para compatibilidad
                FechaPublicacion = DateTime.Now,
                EstaActivo = true,
                TipoLado = media.TipoLado,
                SoloSuscriptores = media.SoloSuscriptores,
                PrecioDesbloqueo = media.PrecioLadoCoins ?? 0
            };

            context.Contenidos.Add(contenido);
            await context.SaveChangesAsync();

            // Crear el archivo de contenido
            var archivo = new ArchivoContenido
            {
                ContenidoId = contenido.Id,
                RutaArchivo = media.RutaArchivo,
                TipoArchivo = media.TipoMedia == TipoMediaBiblioteca.Video
                    ? TipoArchivo.Video
                    : TipoArchivo.Foto,
                Orden = 0,
                FechaCreacion = DateTime.Now
            };

            context.ArchivosContenido.Add(archivo);

            // Actualizar el media como publicado
            media.Estado = EstadoMediaBiblioteca.Publicado;
            media.FechaPublicado = DateTime.Now;
            media.ContenidoPublicadoId = contenido.Id;
        }

        private async Task PublicarComoStory(ApplicationDbContext context, MediaBiblioteca media)
        {
            var ahora = DateTime.Now;

            // Crear la story
            var story = new Story
            {
                CreadorId = media.UsuarioId,
                RutaArchivo = media.RutaArchivo,
                TipoContenido = media.TipoMedia == TipoMediaBiblioteca.Video
                    ? TipoContenido.Video
                    : TipoContenido.Imagen,
                FechaPublicacion = ahora,
                FechaExpiracion = ahora.AddHours(24), // Expira en 24 horas
                EstaActivo = true,
                TipoLado = media.TipoLado,
                Texto = media.Descripcion
            };

            context.Stories.Add(story);
            await context.SaveChangesAsync();

            // Actualizar el media como publicado
            media.Estado = EstadoMediaBiblioteca.Publicado;
            media.FechaPublicado = ahora;
            media.StoryPublicadoId = story.Id;
        }

        private string ConstruirTextoPublicacion(MediaBiblioteca media)
        {
            var texto = media.Descripcion ?? "";

            if (!string.IsNullOrWhiteSpace(media.Hashtags))
            {
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    texto += "\n\n";
                }
                texto += media.Hashtags;
            }

            return texto;
        }

        private async Task ActualizarContadoresDiarios(ApplicationDbContext context, CancellationToken stoppingToken)
        {
            var hoy = DateTime.Today;

            // Reset de contadores diarios para configuraciones que no se han actualizado hoy
            var configuraciones = await context.ConfiguracionesPublicacionAutomatica
                .Where(c => c.FechaUltimoReset == null || c.FechaUltimoReset.Value.Date < hoy)
                .ToListAsync(stoppingToken);

            foreach (var config in configuraciones)
            {
                config.PublicacionesHoy = 0;
                config.FechaUltimoReset = hoy;
            }

            if (configuraciones.Any())
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
