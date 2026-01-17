using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class DesafiosRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Rating",
                table: "Desafios",
                newName: "RatingFan");

            migrationBuilder.RenameColumn(
                name: "ComentarioRating",
                table: "Desafios",
                newName: "ComentarioRatingFan");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Desafios",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioRatingCreador",
                table: "Desafios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsDestacado",
                table: "Desafios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsRelampago",
                table: "Desafios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinDestacado",
                table: "Desafios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LimitePropuestas",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoEscrow",
                table: "Desafios",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroGuardados",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumeroVistas",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PresupuestoMaximo",
                table: "Desafios",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PresupuestoMinimo",
                table: "Desafios",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RatingCreador",
                table: "Desafios",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Desafios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoContenidoRequerido",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoEspecial",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TipoPresupuesto",
                table: "Desafios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BadgesUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoBadge = table.Column<int>(type: "int", nullable: false),
                    FechaObtenido = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DatosExtra = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgesUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgesUsuario_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DesafiosGuardados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesafioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGuardado = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesafiosGuardados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesafiosGuardados_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DesafiosGuardados_Desafios_DesafioId",
                        column: x => x.DesafioId,
                        principalTable: "Desafios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EstadisticasDesafiosUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DesafiosCompletadosComoCreador = table.Column<int>(type: "int", nullable: false),
                    DesafiosEnProgresoComoCreador = table.Column<int>(type: "int", nullable: false),
                    TotalGanadoDesafios = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromedioRatingComoCreador = table.Column<double>(type: "float", nullable: false),
                    TotalRatingsComoCreador = table.Column<int>(type: "int", nullable: false),
                    TasaCompletado = table.Column<double>(type: "float", nullable: false),
                    TiempoPromedioEntrega = table.Column<double>(type: "float", nullable: false),
                    DesafiosCreadosComoFan = table.Column<int>(type: "int", nullable: false),
                    DesafiosCompletadosComoFan = table.Column<int>(type: "int", nullable: false),
                    TotalGastadoDesafios = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromedioRatingComoFan = table.Column<double>(type: "float", nullable: false),
                    TotalRatingsComoFan = table.Column<int>(type: "int", nullable: false),
                    NivelCreador = table.Column<int>(type: "int", nullable: false),
                    PuntosCreador = table.Column<int>(type: "int", nullable: false),
                    NivelFan = table.Column<int>(type: "int", nullable: false),
                    PuntosFan = table.Column<int>(type: "int", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadisticasDesafiosUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstadisticasDesafiosUsuario_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MensajesDesafio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DesafioId = table.Column<int>(type: "int", nullable: false),
                    EmisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    FechaEnvio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leido = table.Column<bool>(type: "bit", nullable: false),
                    FechaLectura = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesDesafio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensajesDesafio_AspNetUsers_EmisorId",
                        column: x => x.EmisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MensajesDesafio_Desafios_DesafioId",
                        column: x => x.DesafioId,
                        principalTable: "Desafios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificacionesDesafio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DesafioId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leida = table.Column<bool>(type: "bit", nullable: false),
                    FechaLectura = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacionesDesafio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificacionesDesafio_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificacionesDesafio_Desafios_DesafioId",
                        column: x => x.DesafioId,
                        principalTable: "Desafios",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9186));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9258));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9260));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9262));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9264));

            migrationBuilder.CreateIndex(
                name: "IX_BadgesUsuario_UsuarioId",
                table: "BadgesUsuario",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosGuardados_DesafioId",
                table: "DesafiosGuardados",
                column: "DesafioId");

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosGuardados_UsuarioId",
                table: "DesafiosGuardados",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EstadisticasDesafiosUsuario_UsuarioId",
                table: "EstadisticasDesafiosUsuario",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesDesafio_DesafioId",
                table: "MensajesDesafio",
                column: "DesafioId");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesDesafio_EmisorId",
                table: "MensajesDesafio",
                column: "EmisorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesDesafio_DesafioId",
                table: "NotificacionesDesafio",
                column: "DesafioId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesDesafio_UsuarioId",
                table: "NotificacionesDesafio",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BadgesUsuario");

            migrationBuilder.DropTable(
                name: "DesafiosGuardados");

            migrationBuilder.DropTable(
                name: "EstadisticasDesafiosUsuario");

            migrationBuilder.DropTable(
                name: "MensajesDesafio");

            migrationBuilder.DropTable(
                name: "NotificacionesDesafio");

            migrationBuilder.DropColumn(
                name: "ComentarioRatingCreador",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "EsDestacado",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "EsRelampago",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "FechaFinDestacado",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "LimitePropuestas",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "MontoEscrow",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "NumeroGuardados",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "NumeroVistas",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "PresupuestoMaximo",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "PresupuestoMinimo",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "RatingCreador",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "TipoContenidoRequerido",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "TipoEspecial",
                table: "Desafios");

            migrationBuilder.DropColumn(
                name: "TipoPresupuesto",
                table: "Desafios");

            migrationBuilder.RenameColumn(
                name: "RatingFan",
                table: "Desafios",
                newName: "Rating");

            migrationBuilder.RenameColumn(
                name: "ComentarioRatingFan",
                table: "Desafios",
                newName: "ComentarioRating");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Desafios",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(795));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(975));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(977));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(979));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 11, 23, 10, 20, 549, DateTimeKind.Local).AddTicks(981));
        }
    }
}
