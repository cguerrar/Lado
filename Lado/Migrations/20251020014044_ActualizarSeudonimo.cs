using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarSeudonimo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreMostrado",
                table: "Contenidos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoLado",
                table: "Contenidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Seudonimo",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeudonimoVerificado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_TipoLado",
                table: "Contenidos",
                column: "TipoLado");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Seudonimo",
                table: "AspNetUsers",
                column: "Seudonimo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contenidos_TipoLado",
                table: "Contenidos");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Seudonimo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NombreMostrado",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "TipoLado",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "Seudonimo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SeudonimoVerificado",
                table: "AspNetUsers");
        }
    }
}
