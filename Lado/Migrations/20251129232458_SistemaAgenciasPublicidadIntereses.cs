using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class SistemaAgenciasPublicidadIntereses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoLado",
                table: "Stories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CategoriaInteresId",
                table: "Contenidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Contenidos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ciudad",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Genero",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PermitePublicidadPersonalizada",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Agencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreEmpresa = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NIF = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Pais = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CodigoPostal = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SitioWeb = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SaldoPublicitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalGastado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalRecargado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstaVerificada = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaAprobacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaSuspension = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MotivoSuspension = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agencias_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoriasIntereses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Icono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CategoriaPadreId = table.Column<int>(type: "int", nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasIntereses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoriasIntereses_CategoriasIntereses_CategoriaPadreId",
                        column: x => x.CategoriaPadreId,
                        principalTable: "CategoriasIntereses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InteraccionesContenidos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    TipoInteraccion = table.Column<int>(type: "int", nullable: false),
                    FechaInteraccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SegundosVisto = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteraccionesContenidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteraccionesContenidos_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InteraccionesContenidos_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Anuncios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgenciaId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlDestino = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UrlCreativo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoCreativo = table.Column<int>(type: "int", nullable: false),
                    TextoBoton = table.Column<int>(type: "int", nullable: false),
                    TextoBotonPersonalizado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PresupuestoDiario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PresupuestoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CostoPorMilImpresiones = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CostoPorClic = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Impresiones = table.Column<long>(type: "bigint", nullable: false),
                    Clics = table.Column<long>(type: "bigint", nullable: false),
                    GastoTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    GastoHoy = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaAprobacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaPausa = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoRechazo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anuncios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anuncios_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InteresesUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CategoriaInteresId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PesoInteres = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 1.0m),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaInteraccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContadorInteracciones = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InteresesUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InteresesUsuarios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InteresesUsuarios_CategoriasIntereses_CategoriaInteresId",
                        column: x => x.CategoriaInteresId,
                        principalTable: "CategoriasIntereses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClicsAnuncios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnuncioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaClic = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostoClic = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Pais = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EsValido = table.Column<bool>(type: "bit", nullable: false),
                    MotivoInvalido = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClicsAnuncios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClicsAnuncios_Anuncios_AnuncioId",
                        column: x => x.AnuncioId,
                        principalTable: "Anuncios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClicsAnuncios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ImpresionesAnuncios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnuncioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaImpresion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostoImpresion = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Pais = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpresionesAnuncios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImpresionesAnuncios_Anuncios_AnuncioId",
                        column: x => x.AnuncioId,
                        principalTable: "Anuncios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImpresionesAnuncios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SegmentacionesAnuncios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnuncioId = table.Column<int>(type: "int", nullable: false),
                    EdadMinima = table.Column<int>(type: "int", nullable: true),
                    EdadMaxima = table.Column<int>(type: "int", nullable: true),
                    Genero = table.Column<int>(type: "int", nullable: true),
                    PaisesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CiudadesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InteresesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SoloMovil = table.Column<bool>(type: "bit", nullable: true),
                    SoloDesktop = table.Column<bool>(type: "bit", nullable: true),
                    HorariosJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DiasActivos = table.Column<int>(type: "int", nullable: true),
                    UsuariosExcluidosJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentacionesAnuncios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentacionesAnuncios_Anuncios_AnuncioId",
                        column: x => x.AnuncioId,
                        principalTable: "Anuncios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransaccionesAgencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgenciaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoAnterior = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoPosterior = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetodoPago = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdTransaccionExterna = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AnuncioId = table.Column<int>(type: "int", nullable: true),
                    FechaTransaccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransaccionesAgencias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransaccionesAgencias_Agencias_AgenciaId",
                        column: x => x.AgenciaId,
                        principalTable: "Agencias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransaccionesAgencias_Anuncios_AnuncioId",
                        column: x => x.AnuncioId,
                        principalTable: "Anuncios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contenidos_CategoriaInteresId",
                table: "Contenidos",
                column: "CategoriaInteresId");

            migrationBuilder.CreateIndex(
                name: "IX_Agencias_Estado",
                table: "Agencias",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Agencias_UsuarioId",
                table: "Agencias",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_AgenciaId",
                table: "Anuncios",
                column: "AgenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_Estado",
                table: "Anuncios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Anuncios_Estado_Fechas",
                table: "Anuncios",
                columns: new[] { "Estado", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasIntereses_CategoriaPadreId",
                table: "CategoriasIntereses",
                column: "CategoriaPadreId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasIntereses_EstaActiva",
                table: "CategoriasIntereses",
                column: "EstaActiva");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasIntereses_Nombre",
                table: "CategoriasIntereses",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_ClicsAnuncios_AnuncioId_FechaClic",
                table: "ClicsAnuncios",
                columns: new[] { "AnuncioId", "FechaClic" });

            migrationBuilder.CreateIndex(
                name: "IX_ClicsAnuncios_SessionId_AnuncioId",
                table: "ClicsAnuncios",
                columns: new[] { "SessionId", "AnuncioId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClicsAnuncios_UsuarioId_FechaClic",
                table: "ClicsAnuncios",
                columns: new[] { "UsuarioId", "FechaClic" });

            migrationBuilder.CreateIndex(
                name: "IX_ImpresionesAnuncios_AnuncioId_FechaImpresion",
                table: "ImpresionesAnuncios",
                columns: new[] { "AnuncioId", "FechaImpresion" });

            migrationBuilder.CreateIndex(
                name: "IX_ImpresionesAnuncios_UsuarioId_FechaImpresion",
                table: "ImpresionesAnuncios",
                columns: new[] { "UsuarioId", "FechaImpresion" });

            migrationBuilder.CreateIndex(
                name: "IX_InteraccionesContenidos_ContenidoId_FechaInteraccion",
                table: "InteraccionesContenidos",
                columns: new[] { "ContenidoId", "FechaInteraccion" });

            migrationBuilder.CreateIndex(
                name: "IX_InteraccionesContenidos_UsuarioId_ContenidoId_TipoInteraccion",
                table: "InteraccionesContenidos",
                columns: new[] { "UsuarioId", "ContenidoId", "TipoInteraccion" });

            migrationBuilder.CreateIndex(
                name: "IX_InteresesUsuarios_CategoriaInteresId",
                table: "InteresesUsuarios",
                column: "CategoriaInteresId");

            migrationBuilder.CreateIndex(
                name: "IX_InteresesUsuarios_UsuarioId_CategoriaInteresId",
                table: "InteresesUsuarios",
                columns: new[] { "UsuarioId", "CategoriaInteresId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentacionesAnuncios_AnuncioId",
                table: "SegmentacionesAnuncios",
                column: "AnuncioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesAgencias_AgenciaId_FechaTransaccion",
                table: "TransaccionesAgencias",
                columns: new[] { "AgenciaId", "FechaTransaccion" });

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesAgencias_AnuncioId",
                table: "TransaccionesAgencias",
                column: "AnuncioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contenidos_CategoriasIntereses_CategoriaInteresId",
                table: "Contenidos",
                column: "CategoriaInteresId",
                principalTable: "CategoriasIntereses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contenidos_CategoriasIntereses_CategoriaInteresId",
                table: "Contenidos");

            migrationBuilder.DropTable(
                name: "ClicsAnuncios");

            migrationBuilder.DropTable(
                name: "ImpresionesAnuncios");

            migrationBuilder.DropTable(
                name: "InteraccionesContenidos");

            migrationBuilder.DropTable(
                name: "InteresesUsuarios");

            migrationBuilder.DropTable(
                name: "SegmentacionesAnuncios");

            migrationBuilder.DropTable(
                name: "TransaccionesAgencias");

            migrationBuilder.DropTable(
                name: "CategoriasIntereses");

            migrationBuilder.DropTable(
                name: "Anuncios");

            migrationBuilder.DropTable(
                name: "Agencias");

            migrationBuilder.DropIndex(
                name: "IX_Contenidos_CategoriaInteresId",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "TipoLado",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "CategoriaInteresId",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Contenidos");

            migrationBuilder.DropColumn(
                name: "Ciudad",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Genero",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PermitePublicidadPersonalizada",
                table: "AspNetUsers");
        }
    }
}
