using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarBloqueoUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BloqueosUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BloqueadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloqueadoId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaBloqueo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Razon = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloqueosUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloqueosUsuarios_AspNetUsers_BloqueadoId",
                        column: x => x.BloqueadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BloqueosUsuarios_AspNetUsers_BloqueadorId",
                        column: x => x.BloqueadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloqueosUsuarios_BloqueadoId",
                table: "BloqueosUsuarios",
                column: "BloqueadoId");

            migrationBuilder.CreateIndex(
                name: "IX_BloqueosUsuarios_Bloqueador_Bloqueado_Unique",
                table: "BloqueosUsuarios",
                columns: new[] { "BloqueadorId", "BloqueadoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloqueosUsuarios_BloqueadorId",
                table: "BloqueosUsuarios",
                column: "BloqueadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloqueosUsuarios");
        }
    }
}
