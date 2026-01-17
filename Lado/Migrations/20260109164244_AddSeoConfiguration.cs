using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesPayPalPendientes_AspNetUsers_UsuarioId",
                table: "OrdenesPayPalPendientes");

            migrationBuilder.CreateTable(
                name: "BotsRobotsTxt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Bloqueado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CrawlDelay = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EsBotImportante = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotsRobotsTxt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesSeo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TituloSitio = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescripcionMeta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PalabrasClave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IndexarSitio = table.Column<bool>(type: "bit", nullable: false),
                    OgSiteName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OgImagenDefault = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OgImagenAncho = table.Column<int>(type: "int", nullable: false),
                    OgImagenAlto = table.Column<int>(type: "int", nullable: false),
                    OgTypeDefault = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OgLocale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TwitterSite = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TwitterCardType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FacebookUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InstagramUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TwitterUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TikTokUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizacionNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizacionDescripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrganizacionLogo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OrganizacionFundacion = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    OrganizacionEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SitemapLimitePerfiles = table.Column<int>(type: "int", nullable: false),
                    SitemapLimiteContenido = table.Column<int>(type: "int", nullable: false),
                    SitemapCacheIndexHoras = table.Column<int>(type: "int", nullable: false),
                    SitemapCachePaginasHoras = table.Column<int>(type: "int", nullable: false),
                    SitemapCachePerfilesHoras = table.Column<int>(type: "int", nullable: false),
                    SitemapCacheContenidoHoras = table.Column<int>(type: "int", nullable: false),
                    SitemapPrioridadHome = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    SitemapPrioridadFeedPublico = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    SitemapPrioridadPerfiles = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    SitemapPrioridadContenidoVideo = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    SitemapPrioridadContenidoNormal = table.Column<decimal>(type: "decimal(2,1)", nullable: false),
                    RobotsCrawlDelayGoogle = table.Column<int>(type: "int", nullable: false),
                    RobotsCrawlDelayBing = table.Column<int>(type: "int", nullable: false),
                    RobotsCrawlDelayOtros = table.Column<int>(type: "int", nullable: false),
                    UrlBase = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GoogleSiteVerification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BingSiteVerification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PinterestSiteVerification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesSeo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Redirecciones301",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrlOrigen = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UrlDestino = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PreservarQueryString = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ContadorUso = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimoUso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redirecciones301", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RutasRobotsTxt",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ruta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "*"),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RutasRobotsTxt", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "BotsRobotsTxt",
                columns: new[] { "Id", "Activo", "Descripcion", "EsBotImportante", "FechaCreacion", "Orden", "UserAgent" },
                values: new object[,]
                {
                    { 1, true, "Bot de Google", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "Googlebot" },
                    { 2, true, "Bot de Google Images", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "Googlebot-Image" }
                });

            migrationBuilder.InsertData(
                table: "BotsRobotsTxt",
                columns: new[] { "Id", "Activo", "CrawlDelay", "Descripcion", "EsBotImportante", "FechaCreacion", "Orden", "UserAgent" },
                values: new object[,]
                {
                    { 3, true, 1, "Bot de Bing", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Bingbot" },
                    { 4, true, 2, "Bot de Yahoo", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "Slurp" },
                    { 5, true, 1, "Bot de DuckDuckGo", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 5, "DuckDuckBot" },
                    { 6, true, 2, "Bot de Yandex", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 6, "Yandex" }
                });

            migrationBuilder.InsertData(
                table: "BotsRobotsTxt",
                columns: new[] { "Id", "Activo", "Descripcion", "EsBotImportante", "FechaCreacion", "Orden", "UserAgent" },
                values: new object[,]
                {
                    { 7, true, "Bot de Facebook para previews", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 7, "facebookexternalhit" },
                    { 8, true, "Bot de Twitter para cards", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 8, "Twitterbot" },
                    { 9, true, "Bot de LinkedIn para previews", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 9, "LinkedInBot" }
                });

            migrationBuilder.InsertData(
                table: "BotsRobotsTxt",
                columns: new[] { "Id", "Activo", "Bloqueado", "Descripcion", "FechaCreacion", "Orden", "UserAgent" },
                values: new object[,]
                {
                    { 10, true, true, "Bot de SEMrush (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 50, "SemrushBot" },
                    { 11, true, true, "Bot de Ahrefs (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 51, "AhrefsBot" },
                    { 12, true, true, "Bot de Majestic (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 52, "MJ12bot" },
                    { 13, true, true, "Bot de Moz (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 53, "DotBot" },
                    { 14, true, true, "Bot de BLEXBot (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 54, "BLEXBot" },
                    { 15, true, true, "Bot de DataForSEO (scraping)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 55, "DataForSeoBot" },
                    { 16, true, true, "Bot de Huawei/Petal", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 56, "PetalBot" },
                    { 17, true, true, "Bot de ByteDance/TikTok (agresivo)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 57, "Bytespider" },
                    { 18, true, true, "Bot de OpenAI/ChatGPT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 60, "GPTBot" },
                    { 19, true, true, "Usuario ChatGPT", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 61, "ChatGPT-User" },
                    { 20, true, true, "Bot de Common Crawl", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 62, "CCBot" },
                    { 21, true, true, "Bot de Anthropic", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 63, "anthropic-ai" },
                    { 22, true, true, "Bot de Claude", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 64, "Claude-Web" },
                    { 23, true, true, "Bot de Google para AI", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 65, "Google-Extended" },
                    { 24, true, true, "Bot de Amazon", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 66, "Amazonbot" }
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5857));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5917));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5919));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5920));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5921));

            migrationBuilder.InsertData(
                table: "ConfiguracionesSeo",
                columns: new[] { "Id", "BingSiteVerification", "DescripcionMeta", "FacebookUrl", "FechaModificacion", "GoogleSiteVerification", "IndexarSitio", "InstagramUrl", "ModificadoPor", "OgImagenAlto", "OgImagenAncho", "OgImagenDefault", "OgLocale", "OgSiteName", "OgTypeDefault", "OrganizacionDescripcion", "OrganizacionEmail", "OrganizacionFundacion", "OrganizacionLogo", "OrganizacionNombre", "PalabrasClave", "PinterestSiteVerification", "RobotsCrawlDelayBing", "RobotsCrawlDelayGoogle", "RobotsCrawlDelayOtros", "SitemapCacheContenidoHoras", "SitemapCacheIndexHoras", "SitemapCachePaginasHoras", "SitemapCachePerfilesHoras", "SitemapLimiteContenido", "SitemapLimitePerfiles", "SitemapPrioridadContenidoNormal", "SitemapPrioridadContenidoVideo", "SitemapPrioridadFeedPublico", "SitemapPrioridadHome", "SitemapPrioridadPerfiles", "TikTokUrl", "TituloSitio", "TwitterCardType", "TwitterSite", "TwitterUrl", "UrlBase", "YouTubeUrl" },
                values: new object[] { 1, null, "Lado es la plataforma donde creadores y fans se conectan. Crea contenido exclusivo, monetiza tu creatividad y conecta con tu audiencia.", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, "https://instagram.com/ladoapp", null, 630, 1200, "/images/og-default.jpg", "es_ES", "Lado", "website", "Plataforma de contenido exclusivo para creadores", "soporte@ladoapp.com", "2024", "/images/logo-512.png", "Lado", "creadores, contenido exclusivo, monetización, fans, suscripciones, creadores de contenido", null, 1, 0, 2, 1, 1, 24, 1, 1000, 500, 0.5m, 0.6m, 0.9m, 1.0m, 0.7m, null, "Lado - Crea, Comparte y Monetiza", "summary_large_image", "@ladoapp", "https://twitter.com/ladoapp", "https://ladoapp.com", null });

            migrationBuilder.InsertData(
                table: "RutasRobotsTxt",
                columns: new[] { "Id", "Activa", "Descripcion", "FechaCreacion", "Orden", "Ruta", "Tipo", "UserAgent" },
                values: new object[,]
                {
                    { 1, true, "Permitir raíz", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 1, "/", 0, "*" },
                    { 2, true, "Feed público indexable", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, "/FeedPublico", 0, "*" },
                    { 3, true, "Panel de administración", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 10, "/Admin/", 1, "*" },
                    { 4, true, "Cuentas de usuario", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 11, "/Account/", 1, "*" },
                    { 5, true, "API endpoints", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 12, "/api/", 1, "*" },
                    { 6, true, "Identity pages", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 13, "/Identity/", 1, "*" },
                    { 7, true, "Feed privado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 20, "/Feed/", 1, "*" },
                    { 8, true, "Mensajes privados", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 21, "/Mensajes/", 1, "*" },
                    { 9, true, "Billetera", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 22, "/Billetera/", 1, "*" },
                    { 10, true, "Suscripciones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 23, "/Suscripciones/", 1, "*" },
                    { 11, true, "Stories", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 24, "/Stories/", 1, "*" },
                    { 12, true, "Dashboard", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 25, "/Dashboard/", 1, "*" },
                    { 13, true, "Configuración usuario", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 26, "/Configuracion/", 1, "*" },
                    { 14, true, "Archivos temporales", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 30, "/Content/temp/", 1, "*" },
                    { 15, true, "Framework files", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 31, "/_framework/", 1, "*" },
                    { 16, true, "Blazor files", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 32, "/_blazor/", 1, "*" },
                    { 17, true, "Evita duplicados por sort", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 40, "/*?sort=", 1, "*" },
                    { 18, true, "Evita duplicados por filter", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 41, "/*?filter=", 1, "*" },
                    { 19, true, "Evita duplicados por paginación", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 42, "/*?page=", 1, "*" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPayPalPendientes_CaptureId_Unique",
                table: "OrdenesPayPalPendientes",
                column: "CaptureId",
                unique: true,
                filter: "[CaptureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPayPalPendientes_Estado",
                table: "OrdenesPayPalPendientes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesPayPalPendientes_OrderId_Unique",
                table: "OrdenesPayPalPendientes",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotsRobotsTxt_Activo",
                table: "BotsRobotsTxt",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_BotsRobotsTxt_Activo_Orden",
                table: "BotsRobotsTxt",
                columns: new[] { "Activo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_BotsRobotsTxt_UserAgent",
                table: "BotsRobotsTxt",
                column: "UserAgent",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Redirecciones301_Activa",
                table: "Redirecciones301",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_Redirecciones301_Activa_UrlOrigen",
                table: "Redirecciones301",
                columns: new[] { "Activa", "UrlOrigen" });

            migrationBuilder.CreateIndex(
                name: "IX_Redirecciones301_UrlOrigen",
                table: "Redirecciones301",
                column: "UrlOrigen",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RutasRobotsTxt_Activa",
                table: "RutasRobotsTxt",
                column: "Activa");

            migrationBuilder.CreateIndex(
                name: "IX_RutasRobotsTxt_Activa_Orden",
                table: "RutasRobotsTxt",
                columns: new[] { "Activa", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_RutasRobotsTxt_UserAgent",
                table: "RutasRobotsTxt",
                column: "UserAgent");

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesPayPalPendientes_AspNetUsers_UsuarioId",
                table: "OrdenesPayPalPendientes",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrdenesPayPalPendientes_AspNetUsers_UsuarioId",
                table: "OrdenesPayPalPendientes");

            migrationBuilder.DropTable(
                name: "BotsRobotsTxt");

            migrationBuilder.DropTable(
                name: "ConfiguracionesSeo");

            migrationBuilder.DropTable(
                name: "Redirecciones301");

            migrationBuilder.DropTable(
                name: "RutasRobotsTxt");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesPayPalPendientes_CaptureId_Unique",
                table: "OrdenesPayPalPendientes");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesPayPalPendientes_Estado",
                table: "OrdenesPayPalPendientes");

            migrationBuilder.DropIndex(
                name: "IX_OrdenesPayPalPendientes_OrderId_Unique",
                table: "OrdenesPayPalPendientes");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 6, 13, 49, 16, 764, DateTimeKind.Local).AddTicks(4889));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 6, 13, 49, 16, 764, DateTimeKind.Local).AddTicks(5012));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 6, 13, 49, 16, 764, DateTimeKind.Local).AddTicks(5013));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 6, 13, 49, 16, 764, DateTimeKind.Local).AddTicks(5015));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 6, 13, 49, 16, 764, DateTimeKind.Local).AddTicks(5016));

            migrationBuilder.AddForeignKey(
                name: "FK_OrdenesPayPalPendientes_AspNetUsers_UsuarioId",
                table: "OrdenesPayPalPendientes",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
