using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoriesQuotaToConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoriesMaxPorDia",
                table: "ConfiguracionesPublicacionAutomatica",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoriesMinPorDia",
                table: "ConfiguracionesPublicacionAutomatica",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6351));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6410));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6412));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6413));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6415));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoriesMaxPorDia",
                table: "ConfiguracionesPublicacionAutomatica");

            migrationBuilder.DropColumn(
                name: "StoriesMinPorDia",
                table: "ConfiguracionesPublicacionAutomatica");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 17, 26, 2, 11, DateTimeKind.Local).AddTicks(7073));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 17, 26, 2, 11, DateTimeKind.Local).AddTicks(7135));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 17, 26, 2, 11, DateTimeKind.Local).AddTicks(7136));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 17, 26, 2, 11, DateTimeKind.Local).AddTicks(7138));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 17, 26, 2, 11, DateTimeKind.Local).AddTicks(7139));
        }
    }
}
