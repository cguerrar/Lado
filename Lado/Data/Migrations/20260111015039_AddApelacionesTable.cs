using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApelacionesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Apelaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoContenido = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContenidoId = table.Column<int>(type: "int", nullable: true),
                    StoryId = table.Column<int>(type: "int", nullable: true),
                    ComentarioId = table.Column<int>(type: "int", nullable: true),
                    RazonRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Argumentos = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenciaAdicional = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ResolucionComentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdministradorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContenidoRestaurado = table.Column<bool>(type: "bit", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    NumeroReferencia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apelaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apelaciones_AspNetUsers_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Apelaciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Apelaciones_Comentarios_ComentarioId",
                        column: x => x.ComentarioId,
                        principalTable: "Comentarios",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Apelaciones_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Apelaciones_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 50, 38, 834, DateTimeKind.Local).AddTicks(7299));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 50, 38, 834, DateTimeKind.Local).AddTicks(7382));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 50, 38, 834, DateTimeKind.Local).AddTicks(7384));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 50, 38, 834, DateTimeKind.Local).AddTicks(7385));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 50, 38, 834, DateTimeKind.Local).AddTicks(7387));

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_AdministradorId",
                table: "Apelaciones",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_ComentarioId",
                table: "Apelaciones",
                column: "ComentarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_ContenidoId",
                table: "Apelaciones",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_StoryId",
                table: "Apelaciones",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Apelaciones_UsuarioId",
                table: "Apelaciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apelaciones");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 32, 0, 206, DateTimeKind.Local).AddTicks(7255));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 32, 0, 206, DateTimeKind.Local).AddTicks(7379));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 32, 0, 206, DateTimeKind.Local).AddTicks(7381));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 32, 0, 206, DateTimeKind.Local).AddTicks(7382));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 22, 32, 0, 206, DateTimeKind.Local).AddTicks(7383));
        }
    }
}
