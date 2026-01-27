using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGaleriaPrivada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar tabla Albums si existe (de migración fallida anterior)
            migrationBuilder.Sql("IF OBJECT_ID('dbo.Albums', 'U') IS NOT NULL DROP TABLE dbo.Albums");

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagenPortada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsPrivado = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediasGaleria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AlbumId = table.Column<int>(type: "int", nullable: true),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Thumbnail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TipoMedia = table.Column<int>(type: "int", nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    DuracionSegundos = table.Column<int>(type: "int", nullable: true),
                    Ancho = table.Column<int>(type: "int", nullable: true),
                    Alto = table.Column<int>(type: "int", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaSubida = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContenidoAsociadoId = table.Column<int>(type: "int", nullable: true),
                    MensajeAsociadoId = table.Column<int>(type: "int", nullable: true),
                    EsFavorito = table.Column<bool>(type: "bit", nullable: false),
                    HashArchivo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediasGaleria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediasGaleria_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediasGaleria_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MediasGaleria_ChatMensajes_MensajeAsociadoId",
                        column: x => x.MensajeAsociadoId,
                        principalTable: "ChatMensajes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediasGaleria_Contenidos_ContenidoAsociadoId",
                        column: x => x.ContenidoAsociadoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 23, 22, 46, 14, 32, DateTimeKind.Local).AddTicks(5432));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 23, 22, 46, 14, 32, DateTimeKind.Local).AddTicks(5514));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 23, 22, 46, 14, 32, DateTimeKind.Local).AddTicks(5517));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 23, 22, 46, 14, 32, DateTimeKind.Local).AddTicks(5518));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 23, 22, 46, 14, 32, DateTimeKind.Local).AddTicks(5520));

            migrationBuilder.CreateIndex(
                name: "IX_Albums_UsuarioId",
                table: "Albums",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_UsuarioId_Orden",
                table: "Albums",
                columns: new[] { "UsuarioId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_AlbumId",
                table: "MediasGaleria",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_ContenidoAsociadoId",
                table: "MediasGaleria",
                column: "ContenidoAsociadoId");

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_HashArchivo",
                table: "MediasGaleria",
                column: "HashArchivo");

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_MensajeAsociadoId",
                table: "MediasGaleria",
                column: "MensajeAsociadoId");

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_UsuarioId",
                table: "MediasGaleria",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_UsuarioId_EsFavorito",
                table: "MediasGaleria",
                columns: new[] { "UsuarioId", "EsFavorito" });

            migrationBuilder.CreateIndex(
                name: "IX_MediasGaleria_UsuarioId_FechaSubida",
                table: "MediasGaleria",
                columns: new[] { "UsuarioId", "FechaSubida" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediasGaleria");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 22, 19, 58, 42, 301, DateTimeKind.Local).AddTicks(1727));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 22, 19, 58, 42, 301, DateTimeKind.Local).AddTicks(1902));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 22, 19, 58, 42, 301, DateTimeKind.Local).AddTicks(1906));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 22, 19, 58, 42, 301, DateTimeKind.Local).AddTicks(1908));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 22, 19, 58, 42, 301, DateTimeKind.Local).AddTicks(1911));
        }
    }
}
