using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    /// <summary>
    /// Servicio para verificar la integridad de archivos multimedia.
    /// Verifica que los archivos referenciados en la BD existan en disco.
    /// </summary>
    public interface IMediaIntegrityService
    {
        /// <summary>
        /// Verifica si un archivo existe en disco
        /// </summary>
        bool ArchivoExiste(string? rutaRelativa);

        /// <summary>
        /// Filtra una lista de contenidos, excluyendo los que tienen archivos faltantes
        /// </summary>
        List<Contenido> FiltrarContenidoValido(IEnumerable<Contenido> contenidos);

        /// <summary>
        /// Obtiene estadísticas de archivos faltantes
        /// </summary>
        Task<MediaIntegrityStats> ObtenerEstadisticasAsync();

        /// <summary>
        /// Marca contenido con archivos faltantes como inactivo (limpieza)
        /// </summary>
        Task<int> LimpiarContenidoSinArchivosAsync(bool soloSimular = true);

        /// <summary>
        /// Limpia la caché de verificación de archivos
        /// </summary>
        void LimpiarCache();
    }

    public class MediaIntegrityStats
    {
        public int TotalContenidos { get; set; }
        public int ContenidosConArchivo { get; set; }
        public int ContenidosSinArchivo { get; set; }
        public int ArchivosVerificados { get; set; }
        public int ArchivosFaltantes { get; set; }
        public List<string> RutasFaltantes { get; set; } = new();
    }

    public class MediaIntegrityService : IMediaIntegrityService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MediaIntegrityService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;

        // Caché de archivos verificados (ruta -> existe)
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private const string CacheKeyPrefix = "MediaIntegrity_";

        public MediaIntegrityService(
            IWebHostEnvironment env,
            ILogger<MediaIntegrityService> logger,
            IServiceScopeFactory scopeFactory,
            IMemoryCache cache)
        {
            _env = env;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _cache = cache;
        }

        /// <summary>
        /// Verifica si un archivo existe en disco (con caché)
        /// </summary>
        public bool ArchivoExiste(string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
                return false;

            // Limpiar la ruta de comillas o caracteres extraños
            var rutaLimpia = LimpiarRuta(rutaRelativa);
            if (string.IsNullOrEmpty(rutaLimpia))
                return false;

            // Verificar caché primero
            var cacheKey = $"{CacheKeyPrefix}{rutaLimpia}";
            if (_cache.TryGetValue(cacheKey, out bool existeEnCache))
            {
                return existeEnCache;
            }

            // Verificar en disco
            var existe = VerificarArchivoEnDisco(rutaLimpia);

            // Guardar en caché
            _cache.Set(cacheKey, existe, CacheDuration);

            return existe;
        }

        /// <summary>
        /// Filtra contenidos, excluyendo los que tienen archivos faltantes
        /// </summary>
        public List<Contenido> FiltrarContenidoValido(IEnumerable<Contenido> contenidos)
        {
            var resultado = new List<Contenido>();
            var archivosFaltantes = 0;

            foreach (var contenido in contenidos)
            {
                // Si no tiene archivo, incluirlo (puede ser solo texto)
                if (string.IsNullOrEmpty(contenido.RutaArchivo))
                {
                    resultado.Add(contenido);
                    continue;
                }

                // Verificar si el archivo principal existe
                var archivoExiste = ArchivoExiste(contenido.RutaArchivo);

                // Para videos, también verificar thumbnail si no existe el video
                if (!archivoExiste && contenido.TipoContenido == TipoContenido.Video)
                {
                    // Si hay thumbnail, usar eso en su lugar
                    if (!string.IsNullOrEmpty(contenido.Thumbnail) && ArchivoExiste(contenido.Thumbnail))
                    {
                        resultado.Add(contenido);
                        continue;
                    }
                }

                if (archivoExiste)
                {
                    resultado.Add(contenido);
                }
                else
                {
                    archivosFaltantes++;
                    _logger.LogDebug("Archivo faltante para contenido {Id}: {Ruta}",
                        contenido.Id, contenido.RutaArchivo);
                }
            }

            if (archivosFaltantes > 0)
            {
                _logger.LogWarning("Se excluyeron {Count} contenidos por archivos faltantes", archivosFaltantes);
            }

            return resultado;
        }

        /// <summary>
        /// Obtiene estadísticas de integridad de archivos
        /// </summary>
        public async Task<MediaIntegrityStats> ObtenerEstadisticasAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stats = new MediaIntegrityStats();

            var contenidos = await context.Contenidos
                .Where(c => c.EstaActivo && !c.EsBorrador)
                .Select(c => new { c.Id, c.RutaArchivo, c.Thumbnail })
                .ToListAsync();

            stats.TotalContenidos = contenidos.Count;

            foreach (var contenido in contenidos)
            {
                if (string.IsNullOrEmpty(contenido.RutaArchivo))
                {
                    stats.ContenidosSinArchivo++;
                    continue;
                }

                stats.ContenidosConArchivo++;
                stats.ArchivosVerificados++;

                if (!ArchivoExiste(contenido.RutaArchivo))
                {
                    stats.ArchivosFaltantes++;
                    if (stats.RutasFaltantes.Count < 100) // Limitar lista
                    {
                        stats.RutasFaltantes.Add(contenido.RutaArchivo);
                    }
                }
            }

            _logger.LogInformation(
                "Estadísticas de integridad: {Total} contenidos, {ConArchivo} con archivo, {Faltantes} faltantes",
                stats.TotalContenidos, stats.ContenidosConArchivo, stats.ArchivosFaltantes);

            return stats;
        }

        /// <summary>
        /// Limpia contenido que referencia archivos que ya no existen
        /// </summary>
        public async Task<int> LimpiarContenidoSinArchivosAsync(bool soloSimular = true)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var contenidosAfectados = 0;

            var contenidos = await context.Contenidos
                .Where(c => c.EstaActivo && !string.IsNullOrEmpty(c.RutaArchivo))
                .ToListAsync();

            foreach (var contenido in contenidos)
            {
                if (!ArchivoExiste(contenido.RutaArchivo))
                {
                    // Si es video y tiene thumbnail válido, no desactivar
                    if (contenido.TipoContenido == TipoContenido.Video &&
                        !string.IsNullOrEmpty(contenido.Thumbnail) &&
                        ArchivoExiste(contenido.Thumbnail))
                    {
                        continue;
                    }

                    contenidosAfectados++;

                    if (!soloSimular)
                    {
                        contenido.EstaActivo = false;
                        _logger.LogWarning(
                            "Desactivando contenido {Id} por archivo faltante: {Ruta}",
                            contenido.Id, contenido.RutaArchivo);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[SIMULACIÓN] Se desactivaría contenido {Id}: {Ruta}",
                            contenido.Id, contenido.RutaArchivo);
                    }
                }
            }

            if (!soloSimular && contenidosAfectados > 0)
            {
                await context.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Limpieza {Modo}: {Count} contenidos afectados",
                soloSimular ? "SIMULADA" : "EJECUTADA", contenidosAfectados);

            return contenidosAfectados;
        }

        /// <summary>
        /// Limpia la caché de verificación
        /// </summary>
        public void LimpiarCache()
        {
            // IMemoryCache no tiene método Clear, pero los items expirarán
            _logger.LogInformation("Solicitud de limpieza de caché de integridad");
        }

        /// <summary>
        /// Limpia caracteres extraños de la ruta
        /// </summary>
        private string LimpiarRuta(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return "";

            var limpia = ruta.Trim();

            // Quitar comillas de cualquier tipo
            while (limpia.StartsWith("\"") || limpia.StartsWith("'") || limpia.StartsWith("\\\""))
                limpia = limpia.TrimStart('"', '\'', '\\');
            while (limpia.EndsWith("\"") || limpia.EndsWith("'") || limpia.EndsWith("\\\""))
                limpia = limpia.TrimEnd('"', '\'', '\\');

            // Limpiar entidades HTML
            limpia = limpia.Replace("&quot;", "").Replace("%22", "").Replace("\"", "").Replace("'", "");

            // Normalizar barras
            limpia = limpia.Replace("\\", "/");

            // Quitar barra inicial si existe
            if (limpia.StartsWith("/"))
                limpia = limpia.Substring(1);

            return limpia;
        }

        /// <summary>
        /// Verifica físicamente si el archivo existe en disco
        /// </summary>
        private bool VerificarArchivoEnDisco(string rutaRelativa)
        {
            try
            {
                var wwwrootPath = _env.WebRootPath;
                if (string.IsNullOrEmpty(wwwrootPath))
                {
                    wwwrootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
                }

                var rutaCompleta = Path.Combine(wwwrootPath, rutaRelativa.Replace("/", Path.DirectorySeparatorChar.ToString()));

                return File.Exists(rutaCompleta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando archivo: {Ruta}", rutaRelativa);
                return false;
            }
        }
    }
}
