using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMusicaAContenido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioDuracion",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AudioOriginalVolumen",
                table: "Contenidos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AudioTrimInicio",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MusicaVolumen",
                table: "Contenidos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PistaMusicalId",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_PistaMusicalId",
                table: "Contenidos",
                column: "PistaMusicalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contenidos_PistasMusica_PistaMusicalId",
                table: "Contenidos",
                column: "PistaMusicalId",
                principalTable: "PistasMusica",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contenidos_PistasMusica_PistaMusicalId",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_PistaMusicalId",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "AudioDuracion",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "AudioOriginalVolumen",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "AudioTrimInicio",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "MusicaVolumen",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "PistaMusicalId",
                table: "Contenidos");
        }
    }
}
