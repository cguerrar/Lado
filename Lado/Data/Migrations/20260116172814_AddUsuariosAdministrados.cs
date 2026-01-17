using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuariosAdministrados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsUsuarioAdministrado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConfiguracionesPublicacionAutomatica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    PublicacionesMinPorDia = table.Column<int>(type: "int", nullable: false),
                    PublicacionesMaxPorDia = table.Column<int>(type: "int", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraFin = table.Column<TimeSpan>(type: "time", nullable: false),
                    PublicarFinesDeSemana = table.Column<bool>(type: "bit", nullable: false),
                    VariacionMinutos = table.Column<int>(type: "int", nullable: false),
                    TipoLadoDefault = table.Column<int>(type: "int", nullable: false),
                    SoloSuscriptoresDefault = table.Column<bool>(type: "bit", nullable: false),
                    UltimaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicacionesHoy = table.Column<int>(type: "int", nullable: false),
                    FechaUltimoReset = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalPublicaciones = table.Column<int>(type: "int", nullable: false),
                    DiasPermitidos = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPublicacionAutomatica", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesPublicacionAutomatica_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaBiblioteca",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TipoMedia = table.Column<int>(type: "int", nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Hashtags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaSubida = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaPublicado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContenidoPublicadoId = table.Column<int>(type: "int", nullable: true),
                    TipoLado = table.Column<int>(type: "int", nullable: false),
                    SoloSuscriptores = table.Column<bool>(type: "bit", nullable: false),
                    PrecioLadoCoins = table.Column<int>(type: "int", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    MensajeError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IntentosPublicacion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaBiblioteca", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaBiblioteca_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaBiblioteca_Contenidos_ContenidoPublicadoId",
                        column: x => x.ContenidoPublicadoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPublicacionAutomatica_UsuarioId",
                table: "ConfiguracionesPublicacionAutomatica",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaBiblioteca_ContenidoPublicadoId",
                table: "MediaBiblioteca",
                column: "ContenidoPublicadoId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaBiblioteca_UsuarioId",
                table: "MediaBiblioteca",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesPublicacionAutomatica");

            migrationBuilder.DropTable(
                name: "MediaBiblioteca");

            migrationBuilder.DropColumn(
                name: "EsUsuarioAdministrado",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(841));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(905));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(906));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(908));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(909));
        }
    }
}
