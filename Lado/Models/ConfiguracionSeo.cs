using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lado.Models
{
    /// <summary>
    /// Configuración global de SEO de la plataforma, editable desde Admin.
    /// Solo debe existir un registro en la tabla.
    /// </summary>
    public class ConfiguracionSeo
    {
        [Key]
        public int Id { get; set; }

        // ========================================
        // META TAGS GLOBALES
        // ========================================

        [Display(Name = "Título del Sitio")]
        [Required]
        [StringLength(200)]
        public string TituloSitio { get; set; } = "Lado - Crea, Comparte y Monetiza";

        [Display(Name = "Descripción Meta")]
        [StringLength(500)]
        public string DescripcionMeta { get; set; } = "Lado es la plataforma donde creadores y fans se conectan. Crea contenido exclusivo, monetiza tu creatividad y conecta con tu audiencia.";

        [Display(Name = "Palabras Clave")]
        [StringLength(500)]
        public string PalabrasClave { get; set; } = "creadores, contenido exclusivo, monetización, fans, suscripciones, creadores de contenido";

        [Display(Name = "Indexar Sitio")]
        public bool IndexarSitio { get; set; } = true;

        // ========================================
        // OPEN GRAPH
        // ========================================

        [Display(Name = "Nombre del Sitio (OG)")]
        [StringLength(100)]
        public string OgSiteName { get; set; } = "Lado";

        [Display(Name = "Imagen OG por Defecto")]
        [StringLength(500)]
        public string OgImagenDefault { get; set; } = "/images/og-default.jpg";

        [Display(Name = "Ancho Imagen OG")]
        public int OgImagenAncho { get; set; } = 1200;

        [Display(Name = "Alto Imagen OG")]
        public int OgImagenAlto { get; set; } = 630;

        [Display(Name = "Tipo OG por Defecto")]
        [StringLength(50)]
        public string OgTypeDefault { get; set; } = "website";

        [Display(Name = "Locale")]
        [StringLength(20)]
        public string OgLocale { get; set; } = "es_ES";

        // ========================================
        // TWITTER CARDS
        // ========================================

        [Display(Name = "Usuario Twitter")]
        [StringLength(100)]
        public string TwitterSite { get; set; } = "@ladoapp";

        [Display(Name = "Tipo de Card Twitter")]
        [StringLength(50)]
        public string TwitterCardType { get; set; } = "summary_large_image";

        // ========================================
        // REDES SOCIALES (para Schema.org)
        // ========================================

        [Display(Name = "URL Facebook")]
        [StringLength(200)]
        public string? FacebookUrl { get; set; }

        [Display(Name = "URL Instagram")]
        [StringLength(200)]
        public string? InstagramUrl { get; set; } = "https://instagram.com/ladoapp";

        [Display(Name = "URL Twitter/X")]
        [StringLength(200)]
        public string? TwitterUrl { get; set; } = "https://twitter.com/ladoapp";

        [Display(Name = "URL TikTok")]
        [StringLength(200)]
        public string? TikTokUrl { get; set; }

        [Display(Name = "URL YouTube")]
        [StringLength(200)]
        public string? YouTubeUrl { get; set; }

        // ========================================
        // SCHEMA.ORG - ORGANIZACIÓN
        // ========================================

        [Display(Name = "Nombre de la Organización")]
        [StringLength(200)]
        public string OrganizacionNombre { get; set; } = "Lado";

        [Display(Name = "Descripción de la Organización")]
        [StringLength(500)]
        public string OrganizacionDescripcion { get; set; } = "Plataforma de contenido exclusivo para creadores";

        [Display(Name = "Logo de la Organización")]
        [StringLength(500)]
        public string OrganizacionLogo { get; set; } = "/images/logo-512.png";

        [Display(Name = "Año de Fundación")]
        [StringLength(4)]
        public string OrganizacionFundacion { get; set; } = "2024";

        [Display(Name = "Email de Contacto")]
        [StringLength(200)]
        public string OrganizacionEmail { get; set; } = "soporte@ladoapp.com";

        // ========================================
        // SITEMAPS
        // ========================================

        [Display(Name = "Límite de Perfiles en Sitemap")]
        [Range(10, 50000)]
        public int SitemapLimitePerfiles { get; set; } = 500;

        [Display(Name = "Límite de Contenido en Sitemap")]
        [Range(10, 50000)]
        public int SitemapLimiteContenido { get; set; } = 1000;

        [Display(Name = "Caché Sitemap Index (horas)")]
        [Range(1, 168)]
        public int SitemapCacheIndexHoras { get; set; } = 1;

        [Display(Name = "Caché Sitemap Páginas (horas)")]
        [Range(1, 168)]
        public int SitemapCachePaginasHoras { get; set; } = 24;

        [Display(Name = "Caché Sitemap Perfiles (horas)")]
        [Range(1, 168)]
        public int SitemapCachePerfilesHoras { get; set; } = 1;

        [Display(Name = "Caché Sitemap Contenido (horas)")]
        [Range(1, 168)]
        public int SitemapCacheContenidoHoras { get; set; } = 1;

        [Display(Name = "Prioridad Home")]
        [Column(TypeName = "decimal(2,1)")]
        public decimal SitemapPrioridadHome { get; set; } = 1.0m;

        [Display(Name = "Prioridad Feed Público")]
        [Column(TypeName = "decimal(2,1)")]
        public decimal SitemapPrioridadFeedPublico { get; set; } = 0.9m;

        [Display(Name = "Prioridad Perfiles")]
        [Column(TypeName = "decimal(2,1)")]
        public decimal SitemapPrioridadPerfiles { get; set; } = 0.7m;

        [Display(Name = "Prioridad Contenido con Video")]
        [Column(TypeName = "decimal(2,1)")]
        public decimal SitemapPrioridadContenidoVideo { get; set; } = 0.6m;

        [Display(Name = "Prioridad Contenido sin Video")]
        [Column(TypeName = "decimal(2,1)")]
        public decimal SitemapPrioridadContenidoNormal { get; set; } = 0.5m;

        // ========================================
        // ROBOTS.TXT - CONFIGURACIÓN GENERAL
        // ========================================

        [Display(Name = "Crawl-delay Googlebot")]
        [Range(0, 60)]
        public int RobotsCrawlDelayGoogle { get; set; } = 0;

        [Display(Name = "Crawl-delay Bingbot")]
        [Range(0, 60)]
        public int RobotsCrawlDelayBing { get; set; } = 1;

        [Display(Name = "Crawl-delay Otros Bots")]
        [Range(0, 60)]
        public int RobotsCrawlDelayOtros { get; set; } = 2;

        // ========================================
        // URL BASE
        // ========================================

        [Display(Name = "URL Base del Sitio")]
        [StringLength(200)]
        public string UrlBase { get; set; } = "https://ladoapp.com";

        // ========================================
        // VERIFICACIÓN DE SITIO
        // ========================================

        [Display(Name = "Google Site Verification")]
        [StringLength(100)]
        public string? GoogleSiteVerification { get; set; }

        [Display(Name = "Bing Site Verification")]
        [StringLength(100)]
        public string? BingSiteVerification { get; set; }

        [Display(Name = "Pinterest Site Verification")]
        [StringLength(100)]
        public string? PinterestSiteVerification { get; set; }

        // ========================================
        // AUDITORÍA
        // ========================================

        [Display(Name = "Fecha de Última Modificación")]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        [Display(Name = "Modificado Por")]
        [StringLength(100)]
        public string? ModificadoPor { get; set; }
    }
}
