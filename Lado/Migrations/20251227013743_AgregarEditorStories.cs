using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEditorStories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ElementosJson",
                table: "Stories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MencionesIds",
                table: "Stories",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MusicaInicioSegundos",
                table: "Stories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PistaMusicalId",
                table: "Stories",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 22, 37, 43, 327, DateTimeKind.Local).AddTicks(5161));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 22, 37, 43, 327, DateTimeKind.Local).AddTicks(5238));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 22, 37, 43, 327, DateTimeKind.Local).AddTicks(5239));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 22, 37, 43, 327, DateTimeKind.Local).AddTicks(5241));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 22, 37, 43, 327, DateTimeKind.Local).AddTicks(5242));

            migrationBuilder.CreateIndex(
                name: "IX_Stories_PistaMusicalId",
                table: "Stories",
                column: "PistaMusicalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stories_PistasMusica_PistaMusicalId",
                table: "Stories",
                column: "PistaMusicalId",
                principalTable: "PistasMusica",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stories_PistasMusica_PistaMusicalId",
                table: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Stories_PistaMusicalId",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "ElementosJson",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MencionesIds",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MusicaInicioSegundos",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "PistaMusicalId",
                table: "Stories");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(288));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(364));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(367));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(369));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(370));
        }
    }
}
