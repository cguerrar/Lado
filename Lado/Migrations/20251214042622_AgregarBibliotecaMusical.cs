using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBibliotecaMusical : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PistasMusica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Artista = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Album = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Genero = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Duracion = table.Column<int>(type: "int", nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RutaPortada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsLibreDeRegalias = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ContadorUsos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Bpm = table.Column<int>(type: "int", nullable: true),
                    Energia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstadoAnimo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PistasMusica", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PistasMusica_Activo",
                table: "PistasMusica",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_PistasMusica_Activo_Genero",
                table: "PistasMusica",
                columns: new[] { "Activo", "Genero" });

            migrationBuilder.CreateIndex(
                name: "IX_PistasMusica_ContadorUsos",
                table: "PistasMusica",
                column: "ContadorUsos");

            migrationBuilder.CreateIndex(
                name: "IX_PistasMusica_Genero",
                table: "PistasMusica",
                column: "Genero");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PistasMusica");
        }
    }
}
