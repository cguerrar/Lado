using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarHistoriasSilenciadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoriasSilenciadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SilenciadoId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaSilenciado = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriasSilenciadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriasSilenciadas_AspNetUsers_SilenciadoId",
                        column: x => x.SilenciadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoriasSilenciadas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 12, 30, 23, 712, DateTimeKind.Local).AddTicks(2776));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 12, 30, 23, 712, DateTimeKind.Local).AddTicks(2844));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 12, 30, 23, 712, DateTimeKind.Local).AddTicks(2845));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 12, 30, 23, 712, DateTimeKind.Local).AddTicks(2847));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 27, 12, 30, 23, 712, DateTimeKind.Local).AddTicks(2848));

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasSilenciadas_SilenciadoId",
                table: "HistoriasSilenciadas",
                column: "SilenciadoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasSilenciadas_Usuario_Silenciado_Unique",
                table: "HistoriasSilenciadas",
                columns: new[] { "UsuarioId", "SilenciadoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoriasSilenciadas_UsuarioId",
                table: "HistoriasSilenciadas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoriasSilenciadas");

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
        }
    }
}
