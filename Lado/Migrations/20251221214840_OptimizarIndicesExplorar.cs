using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class OptimizarIndicesExplorar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 18, 48, 39, 405, DateTimeKind.Local).AddTicks(5550));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 18, 48, 39, 405, DateTimeKind.Local).AddTicks(5629));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 18, 48, 39, 405, DateTimeKind.Local).AddTicks(5632));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 18, 48, 39, 405, DateTimeKind.Local).AddTicks(5634));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 18, 48, 39, 405, DateTimeKind.Local).AddTicks(5635));

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_Explorar_Optimizado",
                table: "Contenidos",
                columns: new[] { "EstaActivo", "EsBorrador", "Censurado", "EsPrivado", "TipoLado", "FechaPublicacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_Mapa_Optimizado",
                table: "Contenidos",
                columns: new[] { "TipoLado", "EstaActivo", "Latitud", "Longitud" });

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_Popularidad",
                table: "Contenidos",
                columns: new[] { "NumeroLikes", "NumeroComentarios", "FechaPublicacion" });

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_Usuario_Activo",
                table: "Contenidos",
                columns: new[] { "EstaActivo", "EsBorrador", "Censurado", "EsPrivado", "UsuarioId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contenidos_Explorar_Optimizado",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_Mapa_Optimizado",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_Popularidad",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_Usuario_Activo",
                table: "Contenidos");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 22, 50, 3, 476, DateTimeKind.Local).AddTicks(1570));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 22, 50, 3, 476, DateTimeKind.Local).AddTicks(1633));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 22, 50, 3, 476, DateTimeKind.Local).AddTicks(1635));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 22, 50, 3, 476, DateTimeKind.Local).AddTicks(1636));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 22, 50, 3, 476, DateTimeKind.Local).AddTicks(1637));
        }
    }
}
