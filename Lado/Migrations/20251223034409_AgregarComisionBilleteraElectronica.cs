using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarComisionBilleteraElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ComisionBilleteraElectronica",
                table: "Transacciones",
                type: "decimal(18,2)",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComisionBilleteraElectronica",
                table: "Transacciones");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 13, 40, 995, DateTimeKind.Local).AddTicks(141));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 13, 40, 995, DateTimeKind.Local).AddTicks(228));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 13, 40, 995, DateTimeKind.Local).AddTicks(229));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 13, 40, 995, DateTimeKind.Local).AddTicks(231));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 23, 0, 13, 40, 995, DateTimeKind.Local).AddTicks(232));
        }
    }
}
