using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarUbicacionContenido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitud",
                table: "Contenidos",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitud",
                table: "Contenidos",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreUbicacion",
                table: "Contenidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DetectarUbicacionAutomaticamente",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitud",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "Longitud",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "NombreUbicacion",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "DetectarUbicacionAutomaticamente",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7075));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7134));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7136));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7137));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7138));
        }
    }
}
