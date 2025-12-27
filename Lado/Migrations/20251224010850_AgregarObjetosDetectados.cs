using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarObjetosDetectados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObjetosContenido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    NombreObjeto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Confianza = table.Column<float>(type: "real", nullable: false, defaultValue: 0.8f),
                    FechaDeteccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjetosContenido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjetosContenido_Contenidos_ContenidoId",
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
                value: new DateTime(2025, 12, 23, 22, 8, 49, 902, DateTimeKind.Local).AddTicks(6323));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 22, 8, 49, 902, DateTimeKind.Local).AddTicks(6385));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 22, 8, 49, 902, DateTimeKind.Local).AddTicks(6387));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 22, 8, 49, 902, DateTimeKind.Local).AddTicks(6388));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 22, 8, 49, 902, DateTimeKind.Local).AddTicks(6390));

            migrationBuilder.CreateIndex(
                name: "IX_ObjetosContenido_ContenidoId",
                table: "ObjetosContenido",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ObjetosContenido_NombreObjeto",
                table: "ObjetosContenido",
                column: "NombreObjeto");

            migrationBuilder.CreateIndex(
                name: "IX_ObjetosContenido_Objeto_Confianza",
                table: "ObjetosContenido",
                columns: new[] { "NombreObjeto", "Confianza" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObjetosContenido");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 44, 9, 164, DateTimeKind.Local).AddTicks(5161));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 44, 9, 164, DateTimeKind.Local).AddTicks(5227));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 44, 9, 164, DateTimeKind.Local).AddTicks(5229));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 44, 9, 164, DateTimeKind.Local).AddTicks(5230));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 44, 9, 164, DateTimeKind.Local).AddTicks(5231));
        }
    }
}
