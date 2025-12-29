using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposPWAPopup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContenidoIOS",
                table: "Popups",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsPWA",
                table: "Popups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloAndroid",
                table: "Popups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloIOS",
                table: "Popups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloSiInstalable",
                table: "Popups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TextoBotonInstalar",
                table: "Popups",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9618));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9740));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9741));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9743));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9744));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContenidoIOS",
                table: "Popups");

            migrationBuilder.DropColumn(
                name: "EsPWA",
                table: "Popups");

            migrationBuilder.DropColumn(
                name: "SoloAndroid",
                table: "Popups");

            migrationBuilder.DropColumn(
                name: "SoloIOS",
                table: "Popups");

            migrationBuilder.DropColumn(
                name: "SoloSiInstalable",
                table: "Popups");

            migrationBuilder.DropColumn(
                name: "TextoBotonInstalar",
                table: "Popups");

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
        }
    }
}
