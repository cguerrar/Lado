using Lado.Data;
using Lado.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Xml;

namespace Lado.Controllers
{
    /// <summary>
    /// Controlador para SEO - Sitemap XML dinamico y otras funciones SEO
    /// </summary>
    public class SeoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SeoController> _logger;
        private const string BaseUrl = "https://ladoapp.com";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public SeoController(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<SeoController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Sitemap Index - /sitemap.xml
        /// Referencia a todos los sub-sitemaps
        /// </summary>
        [Route("sitemap.xml")]
        [ResponseCache(Duration = 3600)] // Cache 1 hora
        public IActionResult Sitemap()
        {
            var cacheKey = "sitemap_index";

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = GenerateSitemapIndex();
                _cache.Set(cacheKey, sitemapXml, CacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Sitemap de paginas estaticas - /sitemap-paginas.xml
        /// </summary>
        [Route("sitemap-paginas.xml")]
        [ResponseCache(Duration = 86400)] // Cache 24 horas
        public IActionResult SitemapPaginas()
        {
            var cacheKey = "sitemap_paginas";

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = GenerateSitemapPaginas();
                _cache.Set(cacheKey, sitemapXml, TimeSpan.FromDays(1));
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
            var cacheKey = "sitemap_perfiles";

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = await GenerateSitemapPerfilesAsync();
                _cache.Set(cacheKey, sitemapXml, CacheDuration);
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
            var cacheKey = "sitemap_contenido";

            if (!_cache.TryGetValue(cacheKey, out string? sitemapXml))
            {
                sitemapXml = await GenerateSitemapContenidoAsync();
                _cache.Set(cacheKey, sitemapXml, CacheDuration);
            }

            return Content(sitemapXml!, "application/xml", Encoding.UTF8);
        }

        #region Generadores de Sitemap

        private string GenerateSitemapIndex()
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
                WriteSitemapRef(writer, $"{BaseUrl}/sitemap-paginas.xml", DateTime.UtcNow);

                // Sitemap de perfiles
                WriteSitemapRef(writer, $"{BaseUrl}/sitemap-perfiles.xml", DateTime.UtcNow);

                // Sitemap de contenido
                WriteSitemapRef(writer, $"{BaseUrl}/sitemap-contenido.xml", DateTime.UtcNow);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        private string GenerateSitemapPaginas()
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
                WriteUrl(writer, BaseUrl, DateTime.UtcNow, "daily", "1.0");

                // Feed publico
                WriteUrl(writer, $"{BaseUrl}/FeedPublico", DateTime.UtcNow, "hourly", "0.9");

                // Paginas estaticas
                WriteUrl(writer, $"{BaseUrl}/Home/About", DateTime.UtcNow.AddDays(-30), "monthly", "0.7");
                WriteUrl(writer, $"{BaseUrl}/Home/Privacy", DateTime.UtcNow.AddDays(-30), "monthly", "0.5");
                WriteUrl(writer, $"{BaseUrl}/Home/Terms", DateTime.UtcNow.AddDays(-30), "monthly", "0.5");
                WriteUrl(writer, $"{BaseUrl}/Home/Contact", DateTime.UtcNow.AddDays(-30), "monthly", "0.6");
                WriteUrl(writer, $"{BaseUrl}/Home/Cookies", DateTime.UtcNow.AddDays(-30), "monthly", "0.4");

                // Paginas de ayuda
                WriteUrl(writer, $"{BaseUrl}/Ayuda", DateTime.UtcNow.AddDays(-7), "weekly", "0.6");
                WriteUrl(writer, $"{BaseUrl}/Ayuda/FAQ", DateTime.UtcNow.AddDays(-7), "weekly", "0.6");

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
        }

        private async Task<string> GenerateSitemapPerfilesAsync()
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
                    .Take(500)
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
                                $"{BaseUrl}/@{username}",
                                perfil.UltimaActividad,
                                "weekly",
                                "0.7");
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

        private async Task<string> GenerateSitemapContenidoAsync()
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
                    .Take(1000)
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
                        var priority = contenido.TieneVideo ? "0.6" : "0.5";

                        WriteUrl(writer,
                            $"{BaseUrl}/Feed/Detalle/{contenido.Id}",
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
        /// Limpiar cache de sitemaps (para uso administrativo)
        /// </summary>
        [HttpPost]
        [Route("api/seo/clear-cache")]
        public IActionResult ClearSitemapCache()
        {
            // Solo permitir si es admin (verificar en produccion)
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            _cache.Remove("sitemap_index");
            _cache.Remove("sitemap_paginas");
            _cache.Remove("sitemap_perfiles");
            _cache.Remove("sitemap_contenido");

            _logger.LogInformation("Cache de sitemaps limpiado por {User}", User.Identity?.Name);

            return Ok(new { success = true, message = "Cache de sitemaps limpiado" });
        }
    }
}
