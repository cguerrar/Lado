using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarStoryLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroLikes",
                table: "Stories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoryId",
                table: "Notificaciones",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoryLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaLike = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryLikes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryLikes_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryLikes_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_StoryLikes_Story_Usuario_Unique",
                table: "StoryLikes",
                columns: new[] { "StoryId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryLikes_StoryId",
                table: "StoryLikes",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLikes_UsuarioId",
                table: "StoryLikes",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryLikes");

            migrationBuilder.DropColumn(
                name: "NumeroLikes",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "Notificaciones");

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
        }
    }
}
