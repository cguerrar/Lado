using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class SeedConfiguracionLadoCoins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesLadoCoins",
                columns: new[] { "Id", "Activo", "Categoria", "Clave", "Descripcion", "FechaModificacion", "ModificadoPor", "Valor" },
                values: new object[,]
                {
                    { 1, true, "Registro", "BonoBienvenida", "Bono de bienvenida al registrarse", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 20m },
                    { 2, true, "Registro", "BonoPrimerContenido", "Bono por primera publicación", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5m },
                    { 3, true, "Registro", "BonoVerificarEmail", "Bono por verificar email", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2m },
                    { 4, true, "Registro", "BonoCompletarPerfil", "Bono por completar perfil", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3m },
                    { 5, true, "Diario", "BonoLoginDiario", "Bono por login diario", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0.50m },
                    { 6, true, "Diario", "BonoContenidoDiario", "Bono por subir contenido (1/día)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1m },
                    { 7, true, "Diario", "Bono5Likes", "Bono por dar 5 likes", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0.25m },
                    { 8, true, "Diario", "Bono3Comentarios", "Bono por 3 comentarios", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0.50m },
                    { 9, true, "Diario", "BonoRacha7Dias", "Bono por racha de 7 días", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5m },
                    { 10, true, "Referidos", "BonoReferidor", "Bono para quien invita", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 10m },
                    { 11, true, "Referidos", "BonoReferido", "Bono para quien es invitado", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 15m },
                    { 12, true, "Referidos", "BonoReferidoCreador", "Bono cuando referido crea en LadoB", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 50m },
                    { 13, true, "Referidos", "ComisionReferidoPorcentaje", "% de comisión de premios del referido", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 10m },
                    { 14, true, "Referidos", "ComisionReferidoMeses", "Meses de duración de comisión", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 3m },
                    { 15, true, "Sistema", "PorcentajeQuema", "% de quema por transacción", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 5m },
                    { 16, true, "Sistema", "DiasVencimiento", "Días hasta vencimiento", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 30m },
                    { 17, true, "Sistema", "MaxPorcentajeSuscripcion", "% máximo de LC en suscripciones", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 30m },
                    { 18, true, "Sistema", "MaxPorcentajePropina", "% máximo de LC en propinas", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 100m },
                    { 19, true, "Canje", "MultiplicadorPublicidad", "$1 LC = $1.50 en ads", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1.5m },
                    { 20, true, "Canje", "MultiplicadorBoost", "$1 LC = $2 en boost", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 2m },
                    { 21, true, "Limites", "MaxPremioDiario", "Máximo LC ganables por día", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 50m },
                    { 22, true, "Limites", "MaxPremioMensual", "Máximo LC ganables por mes", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 500m }
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 16, 8, 33, 512, DateTimeKind.Local).AddTicks(3691));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 16, 8, 33, 512, DateTimeKind.Local).AddTicks(3822));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 16, 8, 33, 512, DateTimeKind.Local).AddTicks(3823));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 16, 8, 33, 512, DateTimeKind.Local).AddTicks(3825));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 16, 8, 33, 512, DateTimeKind.Local).AddTicks(3826));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesLadoCoins",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1030));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1146));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1148));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1149));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1150));
        }
    }
}
