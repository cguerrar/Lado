using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSistemaPopups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Popups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImagenUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IconoClase = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BotonesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Posicion = table.Column<int>(type: "int", nullable: false),
                    ColorFondo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ColorTexto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ColorBotonPrimario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CssPersonalizado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Animacion = table.Column<int>(type: "int", nullable: false),
                    AnchoMaximo = table.Column<int>(type: "int", nullable: false, defaultValue: 400),
                    MostrarBotonCerrar = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CerrarAlClickFuera = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    DelaySegundos = table.Column<int>(type: "int", nullable: true),
                    ScrollPorcentaje = table.Column<int>(type: "int", nullable: true),
                    VisitasRequeridas = table.Column<int>(type: "int", nullable: true),
                    SelectorClick = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MostrarUsuariosLogueados = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MostrarUsuariosAnonimos = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MostrarEnMovil = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MostrarEnDesktop = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PaginasIncluidas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PaginasExcluidas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Frecuencia = table.Column<int>(type: "int", nullable: false),
                    DiasFrecuencia = table.Column<int>(type: "int", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Prioridad = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    Impresiones = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Clics = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Cierres = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Popups", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 24, 39, 968, DateTimeKind.Local).AddTicks(8604));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 24, 39, 968, DateTimeKind.Local).AddTicks(8677));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 24, 39, 968, DateTimeKind.Local).AddTicks(8679));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 24, 39, 968, DateTimeKind.Local).AddTicks(8680));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 24, 39, 968, DateTimeKind.Local).AddTicks(8682));

            migrationBuilder.CreateIndex(
                name: "IX_Popups_Activo_Fechas",
                table: "Popups",
                columns: new[] { "EstaActivo", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Popups_Activo_Prioridad",
                table: "Popups",
                columns: new[] { "EstaActivo", "Prioridad" });

            migrationBuilder.CreateIndex(
                name: "IX_Popups_EstaActivo",
                table: "Popups",
                column: "EstaActivo");

            migrationBuilder.CreateIndex(
                name: "IX_Popups_Tipo",
                table: "Popups",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Popups");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 18, 50, 4, 79, DateTimeKind.Local).AddTicks(7149));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 18, 50, 4, 79, DateTimeKind.Local).AddTicks(7212));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 18, 50, 4, 79, DateTimeKind.Local).AddTicks(7214));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 18, 50, 4, 79, DateTimeKind.Local).AddTicks(7215));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 18, 50, 4, 79, DateTimeKind.Local).AddTicks(7216));
        }
    }
}
