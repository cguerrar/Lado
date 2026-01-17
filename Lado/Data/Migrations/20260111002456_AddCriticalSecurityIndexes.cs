using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalSecurityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 21, 24, 55, 274, DateTimeKind.Local).AddTicks(7390));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 21, 24, 55, 274, DateTimeKind.Local).AddTicks(7456));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 21, 24, 55, 274, DateTimeKind.Local).AddTicks(7458));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 21, 24, 55, 274, DateTimeKind.Local).AddTicks(7459));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 21, 24, 55, 274, DateTimeKind.Local).AddTicks(7461));

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_Categoria",
                table: "LogEventos",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_Categoria_Tipo",
                table: "LogEventos",
                columns: new[] { "Categoria", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_Fecha",
                table: "LogEventos",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_Fecha_Categoria",
                table: "LogEventos",
                columns: new[] { "Fecha", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_Tipo",
                table: "LogEventos",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_LogEventos_UsuarioId_Fecha",
                table: "LogEventos",
                columns: new[] { "UsuarioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_IpsBloqueadas_DireccionIp",
                table: "IpsBloqueadas",
                column: "DireccionIp");

            migrationBuilder.CreateIndex(
                name: "IX_IpsBloqueadas_DireccionIp_EstaActivo",
                table: "IpsBloqueadas",
                columns: new[] { "DireccionIp", "EstaActivo" });

            migrationBuilder.CreateIndex(
                name: "IX_IpsBloqueadas_EstaActivo",
                table: "IpsBloqueadas",
                column: "EstaActivo");

            migrationBuilder.CreateIndex(
                name: "IX_IpsBloqueadas_EstaActivo_FechaExpiracion",
                table: "IpsBloqueadas",
                columns: new[] { "EstaActivo", "FechaExpiracion" });

            migrationBuilder.CreateIndex(
                name: "IX_IpsBloqueadas_TipoBloqueo",
                table: "IpsBloqueadas",
                column: "TipoBloqueo");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_DireccionIp",
                table: "IntentosAtaque",
                column: "DireccionIp");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_DireccionIp_Fecha",
                table: "IntentosAtaque",
                columns: new[] { "DireccionIp", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_Fecha",
                table: "IntentosAtaque",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_TipoAtaque",
                table: "IntentosAtaque",
                column: "TipoAtaque");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_TipoAtaque_Fecha",
                table: "IntentosAtaque",
                columns: new[] { "TipoAtaque", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosAtaque_UsuarioId",
                table: "IntentosAtaque",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogEventos_Categoria",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_LogEventos_Categoria_Tipo",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_LogEventos_Fecha",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_LogEventos_Fecha_Categoria",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_LogEventos_Tipo",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_LogEventos_UsuarioId_Fecha",
                table: "LogEventos");

            migrationBuilder.DropIndex(
                name: "IX_IpsBloqueadas_DireccionIp",
                table: "IpsBloqueadas");

            migrationBuilder.DropIndex(
                name: "IX_IpsBloqueadas_DireccionIp_EstaActivo",
                table: "IpsBloqueadas");

            migrationBuilder.DropIndex(
                name: "IX_IpsBloqueadas_EstaActivo",
                table: "IpsBloqueadas");

            migrationBuilder.DropIndex(
                name: "IX_IpsBloqueadas_EstaActivo_FechaExpiracion",
                table: "IpsBloqueadas");

            migrationBuilder.DropIndex(
                name: "IX_IpsBloqueadas_TipoBloqueo",
                table: "IpsBloqueadas");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_DireccionIp",
                table: "IntentosAtaque");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_DireccionIp_Fecha",
                table: "IntentosAtaque");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_Fecha",
                table: "IntentosAtaque");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_TipoAtaque",
                table: "IntentosAtaque");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_TipoAtaque_Fecha",
                table: "IntentosAtaque");

            migrationBuilder.DropIndex(
                name: "IX_IntentosAtaque_UsuarioId",
                table: "IntentosAtaque");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5875));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5990));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5992));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5993));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5994));
        }
    }
}
