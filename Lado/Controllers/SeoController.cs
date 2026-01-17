using Lado.Data;
using Lado.Models;
using Lado.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Xml;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para SEO - Sitemap XML dinamico, robots.txt y otras funciones SEO
    /// </summary>
    public class SeoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SeoController> _logger;
        private readonly ISeoConfigService _seoConfigService;

        public SeoController(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<SeoController> logger,
            ISeoConfigService seoConfigService)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
            _seoConfigService = seoConfigService;
        }

        /// <summary>
        /// Robots.txt dinamico - /robots.txt
        /// </summary>
        [Route("robots.txt")]
        [ResponseCache(Duration = 3600)] // Cache 1 hora
        public async Task<IActionResult> RobotsTxt()
        {
            var robotsTxt = await _seoConfigService.GenerarRobotsTxtAsync();
            return Content(robotsTxt, "text/plain", Encoding.UTF8);
        }

        /// <summary>
        /// Sitemap Index - /sitemap.xml
        /// Referencia a todos los sub-sitemaps
        /// </summary>
        [Route("sitemap.xml")]
        [ResponseCache(Duration = 3600)] // Cache 1 hora
        public async Task<IActionResult> Sitemap()
        {
            var config = await _seoConfigService.ObtenerConfiguracionAsync();
            var cacheKey = "sitemap_index";
            var cacheDuration = TimeSpan.FromHours(config.SitemapCacheIndexHoras);

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = GenerateSitemapIndex(config.UrlBase);
                _cache.Set(cacheKey, sitemapXml, cacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Sitemap de paginas estaticas - /sitemap-paginas.xml
        /// </summary>
        [Route("sitemap-paginas.xml")]
        [ResponseCache(Duration = 86400)] // Cache 24 horas
        public async Task<IActionResult> SitemapPaginas()
        {
            var config = await _seoConfigService.ObtenerConfiguracionAsync();
            var cacheKey = "sitemap_paginas";
            var cacheDuration = TimeSpan.FromHours(config.SitemapCachePaginasHoras);

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = GenerateSitemapPaginas(config);
                _cache.Set(cacheKey, sitemapXml, cacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Sitemap de perfiles publicos - /sitemap-perfiles.xml
        /// </summary>
        [Route("sitemap-perfiles.xml")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> SitemapPerfiles()
        {
            var config = await _seoConfigService.ObtenerConfiguracionAsync();
            var cacheKey = "sitemap_perfiles";
            var cacheDuration = TimeSpan.FromHours(config.SitemapCachePerfilesHoras);

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = await GenerateSitemapPerfilesAsync(config);
                _cache.Set(cacheKey, sitemapXml, cacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Sitemap de contenido publico - /sitemap-contenido.xml
        /// </summary>
        [Route("sitemap-contenido.xml")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> SitemapContenido()
        {
            var config = await _seoConfigService.ObtenerConfiguracionAsync();
            var cacheKey = "sitemap_contenido";
            var cacheDuration = TimeSpan.FromHours(config.SitemapCacheContenidoHoras);

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = await GenerateSitemapContenidoAsync(config);
                _cache.Set(cacheKey, sitemapXml, cacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        #region Generadores de Sitemap

        private string GenerateSitemapIndex(string baseUrl)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("sitemapindex", "http://www.sitemaps.org/schemas/sitemap/0.9");

                // Sitemap de paginas estaticas
                WriteSitemapRef(writer, $"{baseUrl}/sitemap-paginas.xml", DateTime.UtcNow);

                // Sitemap de perfiles
                WriteSitemapRef(writer, $"{baseUrl}/sitemap-perfiles.xml", DateTime.UtcNow);

                // Sitemap de contenido
                WriteSitemapRef(writer, $"{baseUrl}/sitemap-contenido.xml", DateTime.UtcNow);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        private string GenerateSitemapPaginas(ConfiguracionSeo config)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                // Pagina principal
                WriteUrl(writer, config.UrlBase, DateTime.UtcNow, "daily", config.SitemapPrioridadHome.ToString("0.0"));

                // Feed publico
                WriteUrl(writer, $"{config.UrlBase}/FeedPublico", DateTime.UtcNow, "hourly", config.SitemapPrioridadFeedPublico.ToString("0.0"));

                // Paginas estaticas
                WriteUrl(writer, $"{config.UrlBase}/Home/About", DateTime.UtcNow.AddDays(-30), "monthly", "0.7");
                WriteUrl(writer, $"{config.UrlBase}/Home/Privacy", DateTime.UtcNow.AddDays(-30), "monthly", "0.5");
                WriteUrl(writer, $"{config.UrlBase}/Home/Terms", DateTime.UtcNow.AddDays(-30), "monthly", "0.5");
                WriteUrl(writer, $"{config.UrlBase}/Home/Contact", DateTime.UtcNow.AddDays(-30), "monthly", "0.6");
                WriteUrl(writer, $"{config.UrlBase}/Home/Cookies", DateTime.UtcNow.AddDays(-30), "monthly", "0.4");

                // Paginas de ayuda
                WriteUrl(writer, $"{config.UrlBase}/Ayuda", DateTime.UtcNow.AddDays(-7), "weekly", "0.6");
                WriteUrl(writer, $"{config.UrlBase}/Ayuda/FAQ", DateTime.UtcNow.AddDays(-7), "weekly", "0.6");

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        private async Task<string> GenerateSitemapPerfilesAsync(ConfiguracionSeo config)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            try
            {
                // Obtener perfiles publicos verificados y activos
                var perfiles = await _context.Users
                    .Where(u => u.EstaActivo &&
                               u.EsCreador &&
                               u.CreadorVerificado &&
                               !u.OcultarDeFeedPublico)
                    .OrderByDescending(u => u.VisitasPerfil)
                    .ThenByDescending(u => u.UltimaActividad)
                    .Take(config.SitemapLimitePerfiles)
                    .Select(u => new
                    {
                        u.UserName,
                        u.Seudonimo,
                        UltimaActividad = u.UltimaActividad ?? u.FechaRegistro
                    })
                    .ToListAsync();

                using (var writer = XmlWriter.Create(sb, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                    foreach (var perfil in perfiles)
                    {
                        // URL principal del perfil
                        var username = !string.IsNullOrEmpty(perfil.UserName) ? perfil.UserName : perfil.Seudonimo;
                        if (!string.IsNullOrEmpty(username))
                        {
                            WriteUrl(writer,
                                $"{config.UrlBase}/@{username}",
                                perfil.UltimaActividad,
                                "weekly",
                                config.SitemapPrioridadPerfiles.ToString("0.0"));
                        }
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                _logger.LogInformation("Sitemap perfiles generado con {Count} perfiles", perfiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando sitemap de perfiles");
                // Retornar sitemap vacio en caso de error
                using (var writer = XmlWriter.Create(sb, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }

            return sb.ToString();
        }

        private async Task<string> GenerateSitemapContenidoAsync(ConfiguracionSeo config)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            try
            {
                // Obtener contenido publico reciente (solo LadoA, no sensible)
                var contenidos = await _context.Contenidos
                    .Include(c => c.Usuario)
                    .Where(c => c.EstaActivo &&
                               !c.EsBorrador &&
                               !c.Censurado &&
                               !c.EsPrivado &&
                               !c.EsContenidoSensible &&
                               c.TipoLado == TipoLado.LadoA &&
                               c.EsPublicoGeneral &&
                               c.Usuario != null &&
                               c.Usuario.EstaActivo &&
                               c.Usuario.CreadorVerificado)
                    .OrderByDescending(c => c.FechaPublicacion)
                    .Take(config.SitemapLimiteContenido)
                    .Select(c => new
                    {
                        c.Id,
                        c.FechaPublicacion,
                        TieneVideo = c.TipoContenido == TipoContenido.Video
                    })
                    .ToListAsync();

                using (var writer = XmlWriter.Create(sb, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

                    foreach (var contenido in contenidos)
                    {
                        // Prioridad mayor para contenido con video
                        var priority = contenido.TieneVideo
                            ? config.SitemapPrioridadContenidoVideo.ToString("0.0")
                            : config.SitemapPrioridadContenidoNormal.ToString("0.0");

                        WriteUrl(writer,
                            $"{config.UrlBase}/Feed/Detalle/{contenido.Id}",
                            contenido.FechaPublicacion,
                            "monthly",
                            priority);
                    }

                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                _logger.LogInformation("Sitemap contenido generado con {Count} items", contenidos.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando sitemap de contenido");
                using (var writer = XmlWriter.Create(sb, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Helpers

        private static void WriteSitemapRef(XmlWriter writer, string loc, DateTime lastMod)
        {
            writer.WriteStartElement("sitemap");
            writer.WriteElementString("loc", loc);
            writer.WriteElementString("lastmod", lastMod.ToString("yyyy-MM-dd"));
            writer.WriteEndElement();
        }

        private static void WriteUrl(XmlWriter writer, string loc, DateTime lastMod,
                                     string changeFreq, string priority)
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", loc);
            writer.WriteElementString("lastmod", lastMod.ToString("yyyy-MM-dd"));
            writer.WriteElementString("changefreq", changeFreq);
            writer.WriteElementString("priority", priority);
            writer.WriteEndElement();
        }

        #endregion

        /// <summary>
        /// Limpiar cache de sitemaps y SEO (para uso administrativo)
        /// </summary>
        [HttpPost]
        [Route("api/seo/clear-cache")]
        public IActionResult ClearSitemapCache()
        {
            // Solo permitir si es admin
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            _cache.Remove("sitemap_index");
            _cache.Remove("sitemap_paginas");
            _cache.Remove("sitemap_perfiles");
            _cache.Remove("sitemap_contenido");

            // Limpiar cache del servicio SEO
            _seoConfigService.LimpiarCache();

            _logger.LogInformation("Cache de SEO y sitemaps limpiado por {User}", User.Identity?.Name);

            return Ok(new { success = true, message = "Cache de SEO y sitemaps limpiado" });
        }
    }
}
