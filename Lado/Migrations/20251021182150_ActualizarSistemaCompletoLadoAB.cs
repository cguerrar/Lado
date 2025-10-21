using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class ActualizarSistemaCompletoLadoAB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================
            // CONVERSIÓN DE EstadoTransaccion: STRING → INT (ENUM)
            // ========================================

            // 1. Eliminar índice temporalmente
            migrationBuilder.DropIndex(
                name: "IX_Transacciones_EstadoTransaccion",
                table: "Transacciones");

            // 2. Agregar columna temporal para el nuevo tipo
            migrationBuilder.AddColumn<int>(
                name: "EstadoTransaccion_Temp",
                table: "Transacciones",
                type: "int",
                nullable: false,
                defaultValue: 2); // Completada por defecto

            // 3. Convertir valores string existentes a int (enum)
            migrationBuilder.Sql(@"
                UPDATE Transacciones 
                SET EstadoTransaccion_Temp = CASE 
                    WHEN EstadoTransaccion = 'Pendiente' THEN 0
                    WHEN EstadoTransaccion = 'Procesando' THEN 1
                    WHEN EstadoTransaccion = 'Completado' OR EstadoTransaccion = 'Completada' THEN 2
                    WHEN EstadoTransaccion = 'Fallida' THEN 3
                    WHEN EstadoTransaccion = 'Cancelada' THEN 4
                    WHEN EstadoTransaccion = 'Reembolsada' THEN 5
                    WHEN EstadoTransaccion IS NULL THEN 2
                    ELSE 2
                END
            ");

            // 4. Eliminar columna vieja
            migrationBuilder.DropColumn(
                name: "EstadoTransaccion",
                table: "Transacciones");

            // 5. Renombrar columna temporal a su nombre final
            migrationBuilder.RenameColumn(
                name: "EstadoTransaccion_Temp",
                table: "Transacciones",
                newName: "EstadoTransaccion");

            // 6. Recrear índice
            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_EstadoTransaccion",
                table: "Transacciones",
                column: "EstadoTransaccion");

            // ========================================
            // ACTUALIZAR TABLA CONTENIDOS
            // ========================================

            migrationBuilder.AddColumn<int>(
                name: "DuracionPreviewSegundos",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroCompartidos",
                table: "Contenidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RutaPreview",
                table: "Contenidos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TienePreview",
                table: "Contenidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ========================================
            // CREAR TABLA COLECCIONES
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
            // CREAR TABLA REACCIONES
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
                        name: "FK_Reacciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reacciones_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // CREAR TABLA STORIES
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
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
            // CREAR TABLA COMPRAS COLECCIÓN
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
                        name: "FK_ComprasColeccion_AspNetUsers_CompradorId",
                        column: x => x.CompradorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComprasColeccion_Colecciones_ColeccionId",
                        column: x => x.ColeccionId,
                        principalTable: "Colecciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // CREAR TABLA CONTENIDO COLECCIONES
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
                        name: "FK_ContenidoColecciones_Colecciones_ColeccionId",
                        column: x => x.ColeccionId,
                        principalTable: "Colecciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContenidoColecciones_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // CREAR TABLA STORY VISTAS
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
                        name: "FK_StoryVistas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryVistas_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========================================
            // CREAR ÍNDICES
            // ========================================

            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_Creador_Activa",
                table: "Colecciones",
                columns: new[] { "CreadorId", "EstaActiva" });

            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_CreadorId",
                table: "Colecciones",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Colecciones_EstaActiva",
                table: "Colecciones",
                column: "EstaActiva");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_ColeccionId",
                table: "ComprasColeccion",
                column: "ColeccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_Comprador_Coleccion_Unique",
                table: "ComprasColeccion",
                columns: new[] { "CompradorId", "ColeccionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprasColeccion_CompradorId",
                table: "ComprasColeccion",
                column: "CompradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidoColecciones_ColeccionId",
                table: "ContenidoColecciones",
                column: "ColeccionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidoColecciones_ContenidoId",
                table: "ContenidoColecciones",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_Contenido_Tipo",
                table: "Reacciones",
                columns: new[] { "ContenidoId", "TipoReaccion" });

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_ContenidoId",
                table: "Reacciones",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_Usuario_Contenido_Unique",
                table: "Reacciones",
                columns: new[] { "UsuarioId", "ContenidoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reacciones_UsuarioId",
                table: "Reacciones",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_Creador_Expiracion_Activo",
                table: "Stories",
                columns: new[] { "CreadorId", "FechaExpiracion", "EstaActivo" });

            migrationBuilder.CreateIndex(
                name: "IX_Stories_CreadorId",
                table: "Stories",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_FechaExpiracion",
                table: "Stories",
                column: "FechaExpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_Story_Usuario_Unique",
                table: "StoryVistas",
                columns: new[] { "StoryId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_StoryId",
                table: "StoryVistas",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryVistas_UsuarioId",
                table: "StoryVistas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComprasColeccion");

            migrationBuilder.DropTable(
                name: "ContenidoColecciones");

            migrationBuilder.DropTable(
                name: "Reacciones");

            migrationBuilder.DropTable(
                name: "StoryVistas");

            migrationBuilder.DropTable(
                name: "Colecciones");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropColumn(
                name: "DuracionPreviewSegundos",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "NumeroCompartidos",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "RutaPreview",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "TienePreview",
                table: "Contenidos");

            // Revertir EstadoTransaccion a string
            migrationBuilder.DropIndex(
                name: "IX_Transacciones_EstadoTransaccion",
                table: "Transacciones");

            migrationBuilder.AddColumn<string>(
                name: "EstadoTransaccion_Temp",
                table: "Transacciones",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE Transacciones 
                SET EstadoTransaccion_Temp = CASE 
                    WHEN EstadoTransaccion = 0 THEN 'Pendiente'
                    WHEN EstadoTransaccion = 1 THEN 'Procesando'
                    WHEN EstadoTransaccion = 2 THEN 'Completada'
                    WHEN EstadoTransaccion = 3 THEN 'Fallida'
                    WHEN EstadoTransaccion = 4 THEN 'Cancelada'
                    WHEN EstadoTransaccion = 5 THEN 'Reembolsada'
                    ELSE 'Completada'
                END
            ");

            migrationBuilder.DropColumn(
                name: "EstadoTransaccion",
                table: "Transacciones");

            migrationBuilder.RenameColumn(
                name: "EstadoTransaccion_Temp",
                table: "Transacciones",
                newName: "EstadoTransaccion");

            migrationBuilder.CreateIndex(
                name: "IX_Transacciones_EstadoTransaccion",
                table: "Transacciones",
                column: "EstadoTransaccion");
        }
    }
}