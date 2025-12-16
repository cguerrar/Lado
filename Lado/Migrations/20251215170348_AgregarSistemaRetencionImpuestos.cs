using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSistemaRetencionImpuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RetencionImpuestos",
                table: "Transacciones",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RetencionImpuestos",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsarRetencionPais",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RetencionesPaises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoPais = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    NombrePais = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PorcentajeRetencion = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetencionesPaises", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetencionesPaises");

            migrationBuilder.DropColumn(
                name: "RetencionImpuestos",
                table: "Transacciones");

            migrationBuilder.DropColumn(
                name: "RetencionImpuestos",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UsarRetencionPais",
                table: "AspNetUsers");
        }
    }
}
