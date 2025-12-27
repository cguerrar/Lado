using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarContadorIngresos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContadorIngresos",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(288));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(364));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(367));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(369));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 21, 31, 8, 541, DateTimeKind.Local).AddTicks(370));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContadorIngresos",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 20, 3, 2, 682, DateTimeKind.Local).AddTicks(7045));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 20, 3, 2, 682, DateTimeKind.Local).AddTicks(7106));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 20, 3, 2, 682, DateTimeKind.Local).AddTicks(7107));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 20, 3, 2, 682, DateTimeKind.Local).AddTicks(7108));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 20, 3, 2, 682, DateTimeKind.Local).AddTicks(7109));
        }
    }
}
