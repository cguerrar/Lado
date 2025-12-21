using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConfianza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Las columnas Duracion, ContenidosPublicados, etc. ya existen en la BD
            // Solo crear la nueva tabla ConfiguracionesConfianza

            migrationBuilder.CreateTable(
                name: "ConfiguracionesConfianza",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VerificacionIdentidadHabilitada = table.Column<bool>(type: "bit", nullable: false),
                    PuntosVerificacionIdentidad = table.Column<int>(type: "int", nullable: false),
                    DescripcionVerificacionIdentidad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerificacionEdadHabilitada = table.Column<bool>(type: "bit", nullable: false),
                    PuntosVerificacionEdad = table.Column<int>(type: "int", nullable: false),
                    DescripcionVerificacionEdad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TasaRespuestaHabilitada = table.Column<bool>(type: "bit", nullable: false),
                    PuntosTasaRespuesta = table.Column<int>(type: "int", nullable: false),
                    PorcentajeMinimoRespuesta = table.Column<int>(type: "int", nullable: false),
                    DescripcionTasaRespuesta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActividadRecienteHabilitada = table.Column<bool>(type: "bit", nullable: false),
                    PuntosActividadReciente = table.Column<int>(type: "int", nullable: false),
                    HorasMaximasInactividad = table.Column<int>(type: "int", nullable: false),
                    DescripcionActividadReciente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContenidoPublicadoHabilitado = table.Column<bool>(type: "bit", nullable: false),
                    PuntosContenidoPublicado = table.Column<int>(type: "int", nullable: false),
                    MinimoPublicaciones = table.Column<int>(type: "int", nullable: false),
                    DescripcionContenidoPublicado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NivelMaximo = table.Column<int>(type: "int", nullable: false),
                    MostrarBadgesEnPerfil = table.Column<bool>(type: "bit", nullable: false),
                    MostrarEstrellasEnPerfil = table.Column<bool>(type: "bit", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesConfianza", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7075));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7134));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7136));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7137));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 20, 53, 51, 158, DateTimeKind.Local).AddTicks(7138));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesConfianza");

            // No eliminar las otras columnas ya que existían antes de esta migración

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 17, 54, 11, 843, DateTimeKind.Local).AddTicks(9306));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 17, 54, 11, 843, DateTimeKind.Local).AddTicks(9415));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 17, 54, 11, 843, DateTimeKind.Local).AddTicks(9417));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 17, 54, 11, 843, DateTimeKind.Local).AddTicks(9419));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 20, 17, 54, 11, 843, DateTimeKind.Local).AddTicks(9420));
        }
    }
}
