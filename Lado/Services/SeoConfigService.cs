using Lado.Data;
using Lado.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Lado.Services
{
    public interface ISeoConfigService
    {
        /// <summary>
        /// Obtiene la configuración SEO actual (con caché)
        /// </summary>
        Task<ConfiguracionSeo> ObtenerConfiguracionAsync();

        /// <summary>
        /// Actualiza la configuración SEO
        /// </summary>
        Task<bool> ActualizarConfiguracionAsync(ConfiguracionSeo config, string modificadoPor);

        /// <summary>
        /// Obtiene todas las redirecciones activas
        /// </summary>
        Task<List<Redireccion301>> ObtenerRedireccionesActivasAsync();

        /// <summary>
        /// Obtiene todas las redirecciones (para admin)
        /// </summary>
        Task<List<Redireccion301>> ObtenerTodasRedireccionesAsync();

        /// <summary>
        /// Busca una redirección por URL de origen
        /// </summary>
        Task<Redireccion301?> BuscarRedireccionAsync(string urlOrigen);

        /// <summary>
        /// Crea una nueva redirección
        /// </summary>
        Task<Redireccion301> CrearRedireccionAsync(Redireccion301 redireccion);

        /// <summary>
        /// Actualiza una redirección existente
        /// </summary>
        Task<bool> ActualizarRedireccionAsync(Redireccion301 redireccion);

        /// <summary>
        /// Elimina una redirección
        /// </summary>
        Task<bool> EliminarRedireccionAsync(int id);

        /// <summary>
        /// Incrementa el contador de uso de una redirección
        /// </summary>
        Task IncrementarUsoRedireccionAsync(int id);

        /// <summary>
        /// Obtiene todas las rutas de robots.txt activas
        /// </summary>
        Task<List<RutaRobotsTxt>> ObtenerRutasRobotsActivasAsync();

        /// <summary>
        /// Obtiene todas las rutas de robots.txt (para admin)
        /// </summary>
        Task<List<RutaRobotsTxt>> ObtenerTodasRutasRobotsAsync();

        /// <summary>
        /// Crea una nueva ruta de robots.txt
        /// </summary>
        Task<RutaRobotsTxt> CrearRutaRobotsAsync(RutaRobotsTxt ruta);

        /// <summary>
        /// Actualiza una ruta de robots.txt
        /// </summary>
        Task<bool> ActualizarRutaRobotsAsync(RutaRobotsTxt ruta);

        /// <summary>
        /// Elimina una ruta de robots.txt
        /// </summary>
        Task<bool> EliminarRutaRobotsAsync(int id);

        /// <summary>
        /// Obtiene todos los bots de robots.txt activos
        /// </summary>
        Task<List<BotRobotsTxt>> ObtenerBotsRobotsActivosAsync();

        /// <summary>
        /// Obtiene todos los bots de robots.txt (para admin)
        /// </summary>
        Task<List<BotRobotsTxt>> ObtenerTodosBotsRobotsAsync();

        /// <summary>
        /// Crea un nuevo bot de robots.txt
        /// </summary>
        Task<BotRobotsTxt> CrearBotRobotsAsync(BotRobotsTxt bot);

        /// <summary>
        /// Actualiza un bot de robots.txt
        /// </summary>
        Task<bool> ActualizarBotRobotsAsync(BotRobotsTxt bot);

        /// <summary>
        /// Elimina un bot de robots.txt
        /// </summary>
        Task<bool> EliminarBotRobotsAsync(int id);

        /// <summary>
        /// Limpia todas las cachés de SEO
        /// </summary>
        void LimpiarCache();

        /// <summary>
        /// Genera el contenido dinámico de robots.txt
        /// </summary>
        Task<string> GenerarRobotsTxtAsync();
    }

    public class SeoConfigService : ISeoConfigService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SeoConfigService> _logger;

        // Claves de caché
        private const string CacheKeyConfigSeo = "seo_config";
        private const string CacheKeyRedirecciones = "seo_redirecciones";
        private const string CacheKeyRutasRobots = "seo_rutas_robots";
        private const string CacheKeyBotsRobots = "seo_bots_robots";
        private const string CacheKeyRobotsTxt = "seo_robots_txt";

        // Duración de caché
        private static readonly TimeSpan CacheDurationConfig = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheDurationRedirecciones = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CacheDurationRobots = TimeSpan.FromHours(1);

        public SeoConfigService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<SeoConfigService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        #region Configuración SEO

        public async Task<ConfiguracionSeo> ObtenerConfiguracionAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeyConfigSeo, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDurationConfig;

                var config = await _context.ConfiguracionesSeo.FirstOrDefaultAsync();

                if (config == null)
                {
                    // Crear configuración por defecto si no existe
                    config = new ConfiguracionSeo();
                    _context.ConfiguracionesSeo.Add(config);
                    await _context.SaveChangesAsync();
                }

                return config;
            }) ?? new ConfiguracionSeo();
        }

        public async Task<bool> ActualizarConfiguracionAsync(ConfiguracionSeo config, string modificadoPor)
        {
            try
            {
                var existente = await _context.ConfiguracionesSeo.FirstOrDefaultAsync();

                if (existente != null)
                {
                    // Actualizar todas las propiedades
                    _context.Entry(existente).CurrentValues.SetValues(config);
                    existente.FechaModificacion = DateTime.Now;
                    existente.ModificadoPor = modificadoPor;
                }
                else
                {
                    config.FechaModificacion = DateTime.Now;
                    config.ModificadoPor = modificadoPor;
                    _context.ConfiguracionesSeo.Add(config);
                }

                await _context.SaveChangesAsync();
                LimpiarCache();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración SEO");
                return false;
            }
        }

        #endregion

        #region Redirecciones

        public async Task<List<Redireccion301>> ObtenerRedireccionesActivasAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeyRedirecciones, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDurationRedirecciones;

                return await _context.Redirecciones301
                    .Where(r => r.Activa)
                    .OrderBy(r => r.UrlOrigen)
                    .ToListAsync();
            }) ?? new List<Redireccion301>();
        }

        public async Task<List<Redireccion301>> ObtenerTodasRedireccionesAsync()
        {
            return await _context.Redirecciones301
                .OrderByDescending(r => r.FechaCreacion)
                .ToListAsync();
        }

        public async Task<Redireccion301?> BuscarRedireccionAsync(string urlOrigen)
        {
            var redirecciones = await ObtenerRedireccionesActivasAsync();
            return redirecciones.FirstOrDefault(r =>
                r.UrlOrigen.Equals(urlOrigen, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Redireccion301> CrearRedireccionAsync(Redireccion301 redireccion)
        {
            redireccion.FechaCreacion = DateTime.Now;
            _context.Redirecciones301.Add(redireccion);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeyRedirecciones);
            return redireccion;
        }

        public async Task<bool> ActualizarRedireccionAsync(Redireccion301 redireccion)
        {
            try
            {
                _context.Redirecciones301.Update(redireccion);
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeyRedirecciones);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar redirección {Id}", redireccion.Id);
                return false;
            }
        }

        public async Task<bool> EliminarRedireccionAsync(int id)
        {
            try
            {
                var redireccion = await _context.Redirecciones301.FindAsync(id);
                if (redireccion != null)
                {
                    _context.Redirecciones301.Remove(redireccion);
                    await _context.SaveChangesAsync();
                    _cache.Remove(CacheKeyRedirecciones);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar redirección {Id}", id);
                return false;
            }
        }

        public async Task IncrementarUsoRedireccionAsync(int id)
        {
            try
            {
                var redireccion = await _context.Redirecciones301.FindAsync(id);
                if (redireccion != null)
                {
                    redireccion.ContadorUso++;
                    redireccion.UltimoUso = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al incrementar uso de redirección {Id}", id);
            }
        }

        #endregion

        #region Rutas Robots.txt

        public async Task<List<RutaRobotsTxt>> ObtenerRutasRobotsActivasAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeyRutasRobots, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDurationRobots;

                return await _context.RutasRobotsTxt
                    .Where(r => r.Activa)
                    .OrderBy(r => r.Orden)
                    .ToListAsync();
            }) ?? new List<RutaRobotsTxt>();
        }

        public async Task<List<RutaRobotsTxt>> ObtenerTodasRutasRobotsAsync()
        {
            return await _context.RutasRobotsTxt
                .OrderBy(r => r.Orden)
                .ToListAsync();
        }

        public async Task<RutaRobotsTxt> CrearRutaRobotsAsync(RutaRobotsTxt ruta)
        {
            ruta.FechaCreacion = DateTime.Now;
            _context.RutasRobotsTxt.Add(ruta);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeyRutasRobots);
            _cache.Remove(CacheKeyRobotsTxt);
            return ruta;
        }

        public async Task<bool> ActualizarRutaRobotsAsync(RutaRobotsTxt ruta)
        {
            try
            {
                _context.RutasRobotsTxt.Update(ruta);
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeyRutasRobots);
                _cache.Remove(CacheKeyRobotsTxt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar ruta robots {Id}", ruta.Id);
                return false;
            }
        }

        public async Task<bool> EliminarRutaRobotsAsync(int id)
        {
            try
            {
                var ruta = await _context.RutasRobotsTxt.FindAsync(id);
                if (ruta != null)
                {
                    _context.RutasRobotsTxt.Remove(ruta);
                    await _context.SaveChangesAsync();
                    _cache.Remove(CacheKeyRutasRobots);
                    _cache.Remove(CacheKeyRobotsTxt);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar ruta robots {Id}", id);
                return false;
            }
        }

        #endregion

        #region Bots Robots.txt

        public async Task<List<BotRobotsTxt>> ObtenerBotsRobotsActivosAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeyBotsRobots, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDurationRobots;

                return await _context.BotsRobotsTxt
                    .Where(b => b.Activo)
                    .OrderBy(b => b.Orden)
                    .ToListAsync();
            }) ?? new List<BotRobotsTxt>();
        }

        public async Task<List<BotRobotsTxt>> ObtenerTodosBotsRobotsAsync()
        {
            return await _context.BotsRobotsTxt
                .OrderBy(b => b.Orden)
                .ToListAsync();
        }

        public async Task<BotRobotsTxt> CrearBotRobotsAsync(BotRobotsTxt bot)
        {
            bot.FechaCreacion = DateTime.Now;
            _context.BotsRobotsTxt.Add(bot);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeyBotsRobots);
            _cache.Remove(CacheKeyRobotsTxt);
            return bot;
        }

        public async Task<bool> ActualizarBotRobotsAsync(BotRobotsTxt bot)
        {
            try
            {
                _context.BotsRobotsTxt.Update(bot);
                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeyBotsRobots);
                _cache.Remove(CacheKeyRobotsTxt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar bot robots {Id}", bot.Id);
                return false;
            }
        }

        public async Task<bool> EliminarBotRobotsAsync(int id)
        {
            try
            {
                var bot = await _context.BotsRobotsTxt.FindAsync(id);
                if (bot != null)
                {
                    _context.BotsRobotsTxt.Remove(bot);
                    await _context.SaveChangesAsync();
                    _cache.Remove(CacheKeyBotsRobots);
                    _cache.Remove(CacheKeyRobotsTxt);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar bot robots {Id}", id);
                return false;
            }
        }

        #endregion

        #region Generación Robots.txt

        public async Task<string> GenerarRobotsTxtAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKeyRobotsTxt, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDurationRobots;

                var config = await ObtenerConfiguracionAsync();
                var rutas = await ObtenerRutasRobotsActivasAsync();
                var bots = await ObtenerBotsRobotsActivosAsync();

                var sb = new System.Text.StringBuilder();

                // Comentario inicial
                sb.AppendLine("# robots.txt for " + config.UrlBase);
                sb.AppendLine("# Generated dynamically");
                sb.AppendLine();

                // Configuración para cada bot específico
                foreach (var bot in bots.Where(b => b.EsBotImportante || b.Bloqueado))
                {
                    sb.AppendLine($"User-agent: {bot.UserAgent}");

                    if (bot.Bloqueado)
                    {
                        sb.AppendLine("Disallow: /");
                    }
                    else
                    {
                        if (bot.CrawlDelay > 0)
                        {
                            sb.AppendLine($"Crawl-delay: {bot.CrawlDelay}");
                        }
                    }
                    sb.AppendLine();
                }

                // Reglas generales para todos los bots
                sb.AppendLine("User-agent: *");

                // Crawl-delay general
                if (config.RobotsCrawlDelayOtros > 0)
                {
                    sb.AppendLine($"Crawl-delay: {config.RobotsCrawlDelayOtros}");
                }

                // IMPORTANTE: Siempre permitir páginas públicas (hardcodeado para evitar bloqueos accidentales)
                sb.AppendLine("Allow: /");
                sb.AppendLine("Allow: /FeedPublico/");
                sb.AppendLine("Allow: /Feed/Detalle/");
                sb.AppendLine("Allow: /Home/");
                sb.AppendLine("Allow: /About");
                sb.AppendLine("Allow: /Privacy");
                sb.AppendLine("Allow: /Terms");
                sb.AppendLine("Allow: /Blog/");
                sb.AppendLine();

                // Rutas adicionales de la BD
                var rutasAllow = rutas.Where(r => r.Tipo == TipoReglaRobots.Allow && r.UserAgent == "*");
                var rutasDisallow = rutas.Where(r => r.Tipo == TipoReglaRobots.Disallow && r.UserAgent == "*");

                foreach (var ruta in rutasAllow)
                {
                    // Evitar duplicados
                    if (ruta.Ruta != "/" && !ruta.Ruta.StartsWith("/FeedPublico") && !ruta.Ruta.StartsWith("/Feed/Detalle"))
                    {
                        sb.AppendLine($"Allow: {ruta.Ruta}");
                    }
                }

                sb.AppendLine();

                // Bloquear áreas privadas
                foreach (var ruta in rutasDisallow)
                {
                    sb.AppendLine($"Disallow: {ruta.Ruta}");
                }

                sb.AppendLine();

                // Sitemap
                sb.AppendLine($"Sitemap: {config.UrlBase}/sitemap.xml");

                return sb.ToString();
            }) ?? string.Empty;
        }

        #endregion

        #region Cache

        public void LimpiarCache()
        {
            _cache.Remove(CacheKeyConfigSeo);
            _cache.Remove(CacheKeyRedirecciones);
            _cache.Remove(CacheKeyRutasRobots);
            _cache.Remove(CacheKeyBotsRobots);
            _cache.Remove(CacheKeyRobotsTxt);
        }

        #endregion
    }
}
