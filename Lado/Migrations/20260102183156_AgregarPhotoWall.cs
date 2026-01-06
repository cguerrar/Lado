using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPhotoWall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FotosDestacadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostoPagado = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosDestacadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosDestacadas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FotosDestacadas_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 15, 31, 55, 915, DateTimeKind.Local).AddTicks(2208));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 15, 31, 55, 915, DateTimeKind.Local).AddTicks(2271));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 15, 31, 55, 915, DateTimeKind.Local).AddTicks(2272));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 15, 31, 55, 915, DateTimeKind.Local).AddTicks(2273));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 15, 31, 55, 915, DateTimeKind.Local).AddTicks(2275));

            migrationBuilder.CreateIndex(
                name: "IX_FotosDestacadas_Activos",
                table: "FotosDestacadas",
                columns: new[] { "FechaInicio", "FechaExpiracion", "Nivel" });

            migrationBuilder.CreateIndex(
                name: "IX_FotosDestacadas_ContenidoId",
                table: "FotosDestacadas",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_FotosDestacadas_FechaExpiracion",
                table: "FotosDestacadas",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_FotosDestacadas_Nivel",
                table: "FotosDestacadas",
                column: "Nivel");

            migrationBuilder.CreateIndex(
                name: "IX_FotosDestacadas_UsuarioId",
                table: "FotosDestacadas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FotosDestacadas");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 12, 38, 22, 304, DateTimeKind.Local).AddTicks(4618));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 12, 38, 22, 304, DateTimeKind.Local).AddTicks(4684));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 12, 38, 22, 304, DateTimeKind.Local).AddTicks(4685));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 12, 38, 22, 304, DateTimeKind.Local).AddTicks(4687));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 2, 12, 38, 22, 304, DateTimeKind.Local).AddTicks(4688));
        }
    }
}
