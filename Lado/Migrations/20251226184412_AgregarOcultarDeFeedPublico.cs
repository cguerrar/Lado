using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOcultarDeFeedPublico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OcultarDeFeedPublico",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(862));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(977));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(979));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(980));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(981));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcultarDeFeedPublico",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 14, 58, 17, 802, DateTimeKind.Local).AddTicks(9443));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 14, 58, 17, 802, DateTimeKind.Local).AddTicks(9499));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 14, 58, 17, 802, DateTimeKind.Local).AddTicks(9500));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 14, 58, 17, 802, DateTimeKind.Local).AddTicks(9501));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 14, 58, 17, 802, DateTimeKind.Local).AddTicks(9503));
        }
    }
}
