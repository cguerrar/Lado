using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPromocionLadoB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContenidoOriginalLadoBId",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsPreviewBlurDeLadoB",
                table: "Contenidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TipoCensuraPreview",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarTeaserLadoB",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitirPreviewBlurLadoB",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 21, 24, 31, 956, DateTimeKind.Local).AddTicks(4030));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 21, 24, 31, 956, DateTimeKind.Local).AddTicks(4155));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 21, 24, 31, 956, DateTimeKind.Local).AddTicks(4156));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 21, 24, 31, 956, DateTimeKind.Local).AddTicks(4157));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 21, 24, 31, 956, DateTimeKind.Local).AddTicks(4159));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContenidoOriginalLadoBId",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "EsPreviewBlurDeLadoB",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "TipoCensuraPreview",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "MostrarTeaserLadoB",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PermitirPreviewBlurLadoB",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 19, 20, 24, 914, DateTimeKind.Local).AddTicks(2894));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 19, 20, 24, 914, DateTimeKind.Local).AddTicks(3010));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 19, 20, 24, 914, DateTimeKind.Local).AddTicks(3011));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 19, 20, 24, 914, DateTimeKind.Local).AddTicks(3013));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 21, 19, 20, 24, 914, DateTimeKind.Local).AddTicks(3014));
        }
    }
}
