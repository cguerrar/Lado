using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRespuestaStoryEnMensajes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoryReferenciaId",
                table: "MensajesPrivados",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoRespuestaStory",
                table: "MensajesPrivados",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 11, 27, 13, 713, DateTimeKind.Local).AddTicks(5587));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 11, 27, 13, 713, DateTimeKind.Local).AddTicks(5658));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 11, 27, 13, 713, DateTimeKind.Local).AddTicks(5661));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 11, 27, 13, 713, DateTimeKind.Local).AddTicks(5663));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 11, 27, 13, 713, DateTimeKind.Local).AddTicks(5665));

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_StoryReferenciaId",
                table: "MensajesPrivados",
                column: "StoryReferenciaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MensajesPrivados_Stories_StoryReferenciaId",
                table: "MensajesPrivados",
                column: "StoryReferenciaId",
                principalTable: "Stories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MensajesPrivados_Stories_StoryReferenciaId",
                table: "MensajesPrivados");

            migrationBuilder.DropIndex(
                name: "IX_MensajesPrivados_StoryReferenciaId",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "StoryReferenciaId",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "TipoRespuestaStory",
                table: "MensajesPrivados");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 0, 13, 26, 654, DateTimeKind.Local).AddTicks(8250));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 0, 13, 26, 654, DateTimeKind.Local).AddTicks(8320));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 0, 13, 26, 654, DateTimeKind.Local).AddTicks(8322));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 0, 13, 26, 654, DateTimeKind.Local).AddTicks(8323));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 0, 13, 26, 654, DateTimeKind.Local).AddTicks(8324));
        }
    }
}
