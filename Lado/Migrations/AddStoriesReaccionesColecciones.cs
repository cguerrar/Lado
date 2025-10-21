using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Lado.Migrations
{
    /// <summary>
    /// Migración para agregar Stories, Reacciones y Colecciones
    /// </summary>
    public partial class AddStoriesReaccionesColecciones : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================
            // TABLA: Stories
            // ========================================
            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TipoContenido = table.Column<int>(type: "int", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    NumeroVistas = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stories_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // TABLA: StoryVistas
            // ========================================
            migrationBuilder.CreateTable(
                name: "StoryVistas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaVista = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryVistas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryVistas_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoryVistas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ========================================
            // TABLA: Reacciones
            // ========================================
            migrationBuilder.CreateTable(
                name: "Reacciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoReaccion = table.Column<int>(type: "int", nullable: false),
                    FechaReaccion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reacciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reacciones_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reacciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ========================================
            // TABLA: Colecciones
            // ========================================
            migrationBuilder.CreateTable(
                name: "Colecciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImagenPortada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioOriginal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DescuentoPorcentaje = table.Column<int>(type: "int", nullable: true),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colecciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Colecciones_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // TABLA: ContenidoColecciones (tabla intermedia)
            // ========================================
            migrationBuilder.CreateTable(
                name: "ContenidoColecciones",
                columns: table => new
                {
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    ColeccionId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContenidoColecciones", x => new { x.ContenidoId, x.ColeccionId });
                    table.ForeignKey(
                        name: "FK_ContenidoColecciones_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContenidoColecciones_Colecciones_ColeccionId",
                        column: x => x.ColeccionId,
                        principalTable: "Colecciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ========================================
            // TABLA: ComprasColeccion
            // ========================================
            migrationBuilder.CreateTable(
                name: "ComprasColeccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColeccionId = table.Column<int>(type: "int", nullable: false),
                    CompradorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaCompra = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasColeccion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasColeccion_Colecciones_ColeccionId",
                        column: x => x.ColeccionId,
                        principalTable: "Colecciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComprasColeccion_AspNetUsers_CompradorId",
                        column: x => x.CompradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // ========================================
            // AGREGAR CAMPOS A TABLA CONTENIDOS (Preview)
            // ========================================
            migrationBuilder.AddColumn<bool>(
                name: "TienePreview",
                table: "Contenidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DuracionPreviewSegundos",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RutaPreview",
                table: "Contenidos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // ========================================
            // ÍNDICES PARA STORIES
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_Stories_CreadorId",
                table: "Stories",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_FechaExpiracion",
                table: "Stories",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_CreadorId_FechaExpiracion_EstaActivo",
                table: "Stories",
                columns: new[] { "CreadorId", "FechaExpiracion", "EstaActivo" });

            // ========================================
            // ÍNDICES PARA STORY VISTAS
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_StoryId",
                table: "StoryVistas",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_UsuarioId",
                table: "StoryVistas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_StoryId_UsuarioId",
                table: "StoryVistas",
                columns: new[] { "StoryId", "UsuarioId" },
                unique: true);

            // ========================================
            // ÍNDICES PARA REACCIONES
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_ContenidoId",
                table: "Reacciones",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_UsuarioId",
                table: "Reacciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_UsuarioId_ContenidoId",
                table: "Reacciones",
                columns: new[] { "UsuarioId", "ContenidoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_ContenidoId_TipoReaccion",
                table: "Reacciones",
                columns: new[] { "ContenidoId", "TipoReaccion" });

            // ========================================
            // ÍNDICES PARA COLECCIONES
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_CreadorId",
                table: "Colecciones",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_EstaActiva",
                table: "Colecciones",
                column: "EstaActiva");

            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_CreadorId_EstaActiva",
                table: "Colecciones",
                columns: new[] { "CreadorId", "EstaActiva" });

            // ========================================
            // ÍNDICES PARA CONTENIDO COLECCIONES
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_ContenidoColecciones_ColeccionId",
                table: "ContenidoColecciones",
                column: "ColeccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidoColecciones_ContenidoId",
                table: "ContenidoColecciones",
                column: "ContenidoId");

            // ========================================
            // ÍNDICES PARA COMPRAS COLECCION
            // ========================================
            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_ColeccionId",
                table: "ComprasColeccion",
                column: "ColeccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_CompradorId",
                table: "ComprasColeccion",
                column: "CompradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_CompradorId_ColeccionId",
                table: "ComprasColeccion",
                columns: new[] { "CompradorId", "ColeccionId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar tablas en orden inverso
            migrationBuilder.DropTable(name: "StoryVistas");
            migrationBuilder.DropTable(name: "Stories");
            migrationBuilder.DropTable(name: "Reacciones");
            migrationBuilder.DropTable(name: "ComprasColeccion");
            migrationBuilder.DropTable(name: "ContenidoColecciones");
            migrationBuilder.DropTable(name: "Colecciones");

            // Eliminar columnas de Contenidos
            migrationBuilder.DropColumn(name: "TienePreview", table: "Contenidos");
            migrationBuilder.DropColumn(name: "DuracionPreviewSegundos", table: "Contenidos");
            migrationBuilder.DropColumn(name: "RutaPreview", table: "Contenidos");
        }
    }
}