using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreferenciasNotificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NotificarMensajes = table.Column<bool>(type: "bit", nullable: false),
                    NotificarLikes = table.Column<bool>(type: "bit", nullable: false),
                    NotificarComentarios = table.Column<bool>(type: "bit", nullable: false),
                    NotificarSeguidores = table.Column<bool>(type: "bit", nullable: false),
                    NotificarSuscripciones = table.Column<bool>(type: "bit", nullable: false),
                    NotificarPropinas = table.Column<bool>(type: "bit", nullable: false),
                    NotificarMenciones = table.Column<bool>(type: "bit", nullable: false),
                    HoraSilencioInicio = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraSilencioFin = table.Column<TimeOnly>(type: "time", nullable: true),
                    ZonaHoraria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenciasNotificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreferenciasNotificaciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaNotificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FallosConsecutivos = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushSubscriptions_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 22, 8, 20, 498, DateTimeKind.Local).AddTicks(6353));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 22, 8, 20, 498, DateTimeKind.Local).AddTicks(6423));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 22, 8, 20, 498, DateTimeKind.Local).AddTicks(6425));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 22, 8, 20, 498, DateTimeKind.Local).AddTicks(6426));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 22, 8, 20, 498, DateTimeKind.Local).AddTicks(6427));

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasNotificaciones_UsuarioId",
                table: "PreferenciasNotificaciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UsuarioId",
                table: "PushSubscriptions",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferenciasNotificaciones");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 0, 59, 11, 86, DateTimeKind.Local).AddTicks(8986));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 0, 59, 11, 86, DateTimeKind.Local).AddTicks(9055));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 0, 59, 11, 86, DateTimeKind.Local).AddTicks(9056));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 0, 59, 11, 86, DateTimeKind.Local).AddTicks(9058));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 5, 0, 59, 11, 86, DateTimeKind.Local).AddTicks(9059));
        }
    }
}
