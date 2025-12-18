using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class MensajesWhatsAppFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaLectura",
                table: "MensajesPrivados",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MensajeRespondidoId",
                table: "MensajesPrivados",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreArchivoOriginal",
                table: "MensajesPrivados",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RutaArchivo",
                table: "MensajesPrivados",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TamanoArchivo",
                table: "MensajesPrivados",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoMensaje",
                table: "MensajesPrivados",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesPrivados_MensajeRespondidoId",
                table: "MensajesPrivados",
                column: "MensajeRespondidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_MensajesPrivados_MensajesPrivados_MensajeRespondidoId",
                table: "MensajesPrivados",
                column: "MensajeRespondidoId",
                principalTable: "MensajesPrivados",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MensajesPrivados_MensajesPrivados_MensajeRespondidoId",
                table: "MensajesPrivados");

            migrationBuilder.DropIndex(
                name: "IX_MensajesPrivados_MensajeRespondidoId",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "FechaLectura",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "MensajeRespondidoId",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "NombreArchivoOriginal",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "RutaArchivo",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "TamanoArchivo",
                table: "MensajesPrivados");

            migrationBuilder.DropColumn(
                name: "TipoMensaje",
                table: "MensajesPrivados");
        }
    }
}
