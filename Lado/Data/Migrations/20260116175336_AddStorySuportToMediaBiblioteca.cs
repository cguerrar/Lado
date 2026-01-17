using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStorySuportToMediaBiblioteca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StoryPublicadoId",
                table: "MediaBiblioteca",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoPublicacion",
                table: "MediaBiblioteca",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 53, 35, 627, DateTimeKind.Local).AddTicks(212));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 53, 35, 627, DateTimeKind.Local).AddTicks(280));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 53, 35, 627, DateTimeKind.Local).AddTicks(281));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 53, 35, 627, DateTimeKind.Local).AddTicks(283));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 53, 35, 627, DateTimeKind.Local).AddTicks(284));

            migrationBuilder.CreateIndex(
                name: "IX_MediaBiblioteca_StoryPublicadoId",
                table: "MediaBiblioteca",
                column: "StoryPublicadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaBiblioteca_Stories_StoryPublicadoId",
                table: "MediaBiblioteca",
                column: "StoryPublicadoId",
                principalTable: "Stories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaBiblioteca_Stories_StoryPublicadoId",
                table: "MediaBiblioteca");

            migrationBuilder.DropIndex(
                name: "IX_MediaBiblioteca_StoryPublicadoId",
                table: "MediaBiblioteca");

            migrationBuilder.DropColumn(
                name: "StoryPublicadoId",
                table: "MediaBiblioteca");

            migrationBuilder.DropColumn(
                name: "TipoPublicacion",
                table: "MediaBiblioteca");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 28, 13, 404, DateTimeKind.Local).AddTicks(3722));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 28, 13, 404, DateTimeKind.Local).AddTicks(3858));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 28, 13, 404, DateTimeKind.Local).AddTicks(3859));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 28, 13, 404, DateTimeKind.Local).AddTicks(3861));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 14, 28, 13, 404, DateTimeKind.Local).AddTicks(3862));
        }
    }
}
