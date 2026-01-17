using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShadowHideToContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaOcultoSilenciosamente",
                table: "Contenidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcultadoPorAdminId",
                table: "Contenidos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OcultoSilenciosamente",
                table: "Contenidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaOcultoSilenciosamente",
                table: "ArchivosContenido",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OcultoSilenciosamente",
                table: "ArchivosContenido",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(795));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(975));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(977));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(979));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(981));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaOcultoSilenciosamente",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "OcultadoPorAdminId",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "OcultoSilenciosamente",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "FechaOcultoSilenciosamente",
                table: "ArchivosContenido");

            migrationBuilder.DropColumn(
                name: "OcultoSilenciosamente",
                table: "ArchivosContenido");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 10, 30, 8, 849, DateTimeKind.Local).AddTicks(9644));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 10, 30, 8, 849, DateTimeKind.Local).AddTicks(9710));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 10, 30, 8, 849, DateTimeKind.Local).AddTicks(9711));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 10, 30, 8, 849, DateTimeKind.Local).AddTicks(9713));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 10, 30, 8, 849, DateTimeKind.Local).AddTicks(9714));
        }
    }
}
