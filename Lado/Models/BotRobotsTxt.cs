using System.ComponentModel.DataAnnotations;

namespace Lado.Models
{
    /// <summary>
    /// Bots/crawlers configurados en robots.txt con sus reglas específicas.
    /// Permite bloquear bots agresivos o configurar crawl-delays personalizados.
    /// </summary>
    public class BotRobotsTxt
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre del User-Agent (ej: Googlebot, SemrushBot, AhrefsBot)
        /// </summary>
        [Required]
        [Display(Name = "User-Agent")]
        [StringLength(100)]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Si el bot está completamente bloqueado (Disallow: /)
        /// </summary>
        [Display(Name = "Bloquear Completamente")]
        public bool Bloqueado { get; set; } = false;

        /// <summary>
        /// Crawl-delay para este bot (0 = sin límite)
        /// </summary>
        [Display(Name = "Crawl-delay (segundos)")]
        [Range(0, 120)]
        public int CrawlDelay { get; set; } = 0;

        /// <summary>
        /// Si la configuración está activa
        /// </summary>
        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        /// <summary>
        /// Descripción del bot
        /// </summary>
        [Display(Name = "Descripción")]
        [StringLength(200)]
        public string? Descripcion { get; set; }

        /// <summary>
        /// Es un bot conocido/importante (Google, Bing, etc.)
        /// </summary>
        [Display(Name = "Bot Importante")]
        public bool EsBotImportante { get; set; } = false;

        /// <summary>
        /// Orden en el archivo robots.txt
        /// </summary>
        [Display(Name = "Orden")]
        public int Orden { get; set; } = 100;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // ========================================
        // DATOS POR DEFECTO
        // ========================================

        /// <summary>
        /// Obtiene la lista de bots por defecto para seed
        /// </summary>
        public static List<BotRobotsTxt> ObtenerBotsDefault()
        {
            return new List<BotRobotsTxt>
            {
                // Bots importantes - no bloquear
                new() { UserAgent = "Googlebot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 1, Descripcion = "Bot de Google" },
                new() { UserAgent = "Googlebot-Image", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 2, Descripcion = "Bot de Google Images" },
                new() { UserAgent = "Bingbot", Bloqueado = false, CrawlDelay = 1, EsBotImportante = true, Orden = 3, Descripcion = "Bot de Bing" },
                new() { UserAgent = "Slurp", Bloqueado = false, CrawlDelay = 2, EsBotImportante = true, Orden = 4, Descripcion = "Bot de Yahoo" },
                new() { UserAgent = "DuckDuckBot", Bloqueado = false, CrawlDelay = 1, EsBotImportante = true, Orden = 5, Descripcion = "Bot de DuckDuckGo" },
                new() { UserAgent = "Yandex", Bloqueado = false, CrawlDelay = 2, EsBotImportante = true, Orden = 6, Descripcion = "Bot de Yandex" },
                new() { UserAgent = "facebookexternalhit", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 7, Descripcion = "Bot de Facebook para previews" },
                new() { UserAgent = "Twitterbot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 8, Descripcion = "Bot de Twitter para cards" },
                new() { UserAgent = "LinkedInBot", Bloqueado = false, CrawlDelay = 0, EsBotImportante = true, Orden = 9, Descripcion = "Bot de LinkedIn para previews" },

                // Bots SEO/análisis - bloquear por defecto
                new() { UserAgent = "SemrushBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 50, Descripcion = "Bot de SEMrush (scraping)" },
                new() { UserAgent = "AhrefsBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 51, Descripcion = "Bot de Ahrefs (scraping)" },
                new() { UserAgent = "MJ12bot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 52, Descripcion = "Bot de Majestic (scraping)" },
                new() { UserAgent = "DotBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 53, Descripcion = "Bot de Moz (scraping)" },
                new() { UserAgent = "BLEXBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 54, Descripcion = "Bot de BLEXBot (scraping)" },
                new() { UserAgent = "DataForSeoBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 55, Descripcion = "Bot de DataForSEO (scraping)" },
                new() { UserAgent = "PetalBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 56, Descripcion = "Bot de Huawei/Petal" },
                new() { UserAgent = "Bytespider", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 57, Descripcion = "Bot de ByteDance/TikTok (agresivo)" },

                // AI Crawlers - bloquear para proteger contenido
                new() { UserAgent = "GPTBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 60, Descripcion = "Bot de OpenAI/ChatGPT" },
                new() { UserAgent = "ChatGPT-User", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 61, Descripcion = "Usuario ChatGPT" },
                new() { UserAgent = "CCBot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 62, Descripcion = "Bot de Common Crawl" },
                new() { UserAgent = "anthropic-ai", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 63, Descripcion = "Bot de Anthropic" },
                new() { UserAgent = "Claude-Web", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 64, Descripcion = "Bot de Claude" },
                new() { UserAgent = "Google-Extended", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 65, Descripcion = "Bot de Google para AI" },
                new() { UserAgent = "Amazonbot", Bloqueado = true, CrawlDelay = 0, EsBotImportante = false, Orden = 66, Descripcion = "Bot de Amazon" },
            };
        }
    }
}
