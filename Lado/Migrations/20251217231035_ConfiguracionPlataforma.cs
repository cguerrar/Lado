using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionPlataforma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesPlataforma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UltimaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPlataforma", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesPlataforma",
                columns: new[] { "Id", "Categoria", "Clave", "Descripcion", "UltimaModificacion", "Valor" },
                values: new object[,]
                {
                    { 1, "Billetera", "ComisionBilleteraElectronica", "Comision por usar billetera electronica (%)", new DateTime(2025, 12, 17, 20, 10, 34, 820, DateTimeKind.Local).AddTicks(4325), "2.5" },
                    { 2, "Billetera", "TiempoProcesoRetiro", "Tiempo estimado para procesar retiros", new DateTime(2025, 12, 17, 20, 10, 34, 820, DateTimeKind.Local).AddTicks(4393), "3-5 dias habiles" },
                    { 3, "Billetera", "MontoMinimoRecarga", "Monto minimo para recargar saldo", new DateTime(2025, 12, 17, 20, 10, 34, 820, DateTimeKind.Local).AddTicks(4394), "5" },
                    { 4, "Billetera", "MontoMaximoRecarga", "Monto maximo para recargar saldo", new DateTime(2025, 12, 17, 20, 10, 34, 820, DateTimeKind.Local).AddTicks(4396), "1000" },
                    { 5, "General", "ComisionPlataforma", "Comision general de la plataforma (%)", new DateTime(2025, 12, 17, 20, 10, 34, 820, DateTimeKind.Local).AddTicks(4397), "20" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionPlataforma_Categoria",
                table: "ConfiguracionesPlataforma",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionPlataforma_Clave",
                table: "ConfiguracionesPlataforma",
                column: "Clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesPlataforma");
        }
    }
}
