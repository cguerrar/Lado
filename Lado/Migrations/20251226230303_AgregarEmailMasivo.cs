using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEmailMasivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecibirEmailsComunicados",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecibirEmailsMarketing",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlantillasEmail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContenidoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Marketing"),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasEmail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampanasEmail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PlantillaId = table.Column<int>(type: "int", nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContenidoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoDestinatario = table.Column<int>(type: "int", nullable: false),
                    EmailsEspecificos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiltroAdicional = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaInicioEnvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFinEnvio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDestinatarios = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Enviados = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Fallidos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DetalleErrores = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampanasEmail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampanasEmail_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CampanasEmail_PlantillasEmail_PlantillaId",
                        column: x => x.PlantillaId,
                        principalTable: "PlantillasEmail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_CampanasEmail_CreadoPorId",
                table: "CampanasEmail",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasEmail_Estado",
                table: "CampanasEmail",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasEmail_Estado_Fecha",
                table: "CampanasEmail",
                columns: new[] { "Estado", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_CampanasEmail_FechaCreacion",
                table: "CampanasEmail",
                column: "FechaCreacion");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasEmail_PlantillaId",
                table: "CampanasEmail",
                column: "PlantillaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasEmail_Activa_Categoria",
                table: "PlantillasEmail",
                columns: new[] { "EstaActiva", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasEmail_Categoria",
                table: "PlantillasEmail",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasEmail_EstaActiva",
                table: "PlantillasEmail",
                column: "EstaActiva");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampanasEmail");

            migrationBuilder.DropTable(
                name: "PlantillasEmail");

            migrationBuilder.DropColumn(
                name: "RecibirEmailsComunicados",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RecibirEmailsMarketing",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(862));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(977));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(979));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(980));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 26, 15, 44, 11, 665, DateTimeKind.Local).AddTicks(981));
        }
    }
}
