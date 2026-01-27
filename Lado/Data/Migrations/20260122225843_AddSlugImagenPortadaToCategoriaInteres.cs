using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugImagenPortadaToCategoriaInteres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagenPortada",
                table: "CategoriasIntereses",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "CategoriasIntereses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArticulosBlog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resumen = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagenPortada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetaTitulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetaDescripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PalabrasClave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstaPublicado = table.Column<bool>(type: "bit", nullable: false),
                    EsDestacado = table.Column<bool>(type: "bit", nullable: false),
                    Vistas = table.Column<int>(type: "int", nullable: false),
                    TiempoLecturaMinutos = table.Column<int>(type: "int", nullable: false),
                    AutorId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticulosBlog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticulosBlog_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ArticulosBlog_AutorId",
                table: "ArticulosBlog",
                column: "AutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticulosBlog");

            migrationBuilder.DropColumn(
                name: "ImagenPortada",
                table: "CategoriasIntereses");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "CategoriasIntereses");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(5939));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6017));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6028));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6030));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6032));
        }
    }
}
