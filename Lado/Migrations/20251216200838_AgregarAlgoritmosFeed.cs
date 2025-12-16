using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAlgoritmosFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlgoritmosFeed",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icono = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EsPorDefecto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ConfiguracionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalUsos = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlgoritmosFeed", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreferenciasAlgoritmoUsuario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AlgoritmoFeedId = table.Column<int>(type: "int", nullable: false),
                    FechaSeleccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenciasAlgoritmoUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreferenciasAlgoritmoUsuario_AlgoritmosFeed_AlgoritmoFeedId",
                        column: x => x.AlgoritmoFeedId,
                        principalTable: "AlgoritmosFeed",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreferenciasAlgoritmoUsuario_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlgoritmosFeed_Activo",
                table: "AlgoritmosFeed",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_AlgoritmosFeed_Codigo",
                table: "AlgoritmosFeed",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlgoritmosFeed_EsPorDefecto",
                table: "AlgoritmosFeed",
                column: "EsPorDefecto");

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasAlgoritmoUsuario_AlgoritmoFeedId",
                table: "PreferenciasAlgoritmoUsuario",
                column: "AlgoritmoFeedId");

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasAlgoritmoUsuario_UsuarioId",
                table: "PreferenciasAlgoritmoUsuario",
                column: "UsuarioId",
                unique: true);

            // Seed data: 4 algoritmos iniciales
            migrationBuilder.InsertData(
                table: "AlgoritmosFeed",
                columns: new[] { "Codigo", "Nombre", "Descripcion", "Icono", "Activo", "EsPorDefecto", "Orden", "TotalUsos", "FechaCreacion" },
                values: new object[] { "cronologico", "Cronologico", "Muestra los posts ordenados por fecha de publicacion, los mas recientes primero", "clock", true, true, 1, 0, DateTime.Now });

            migrationBuilder.InsertData(
                table: "AlgoritmosFeed",
                columns: new[] { "Codigo", "Nombre", "Descripcion", "Icono", "Activo", "EsPorDefecto", "Orden", "TotalUsos", "FechaCreacion" },
                values: new object[] { "trending", "Trending", "Prioriza contenido con alto engagement reciente (likes, comentarios, vistas)", "trending-up", true, false, 2, 0, DateTime.Now });

            migrationBuilder.InsertData(
                table: "AlgoritmosFeed",
                columns: new[] { "Codigo", "Nombre", "Descripcion", "Icono", "Activo", "EsPorDefecto", "Orden", "TotalUsos", "FechaCreacion" },
                values: new object[] { "seguidos", "Seguidos Primero", "70% contenido de creadores que sigues, 30% descubrimiento de nuevos", "users", true, false, 3, 0, DateTime.Now });

            migrationBuilder.InsertData(
                table: "AlgoritmosFeed",
                columns: new[] { "Codigo", "Nombre", "Descripcion", "Icono", "Activo", "EsPorDefecto", "Orden", "TotalUsos", "FechaCreacion" },
                values: new object[] { "para_ti", "Para Ti", "Personalizado basado en tu historial de interacciones y preferencias", "heart", true, false, 4, 0, DateTime.Now });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreferenciasAlgoritmoUsuario");

            migrationBuilder.DropTable(
                name: "AlgoritmosFeed");
        }
    }
}
