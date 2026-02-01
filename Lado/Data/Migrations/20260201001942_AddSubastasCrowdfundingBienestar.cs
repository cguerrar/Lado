using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubastasCrowdfundingBienestar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlertaBurnoutMostrada",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DiasConsecutivosActivo",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinVacaciones",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioVacaciones",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimaAlertaBurnout",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimoResetHoras",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HorasTrabajadasHoy",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LimiteHorasDiarias",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensajeAutorespuesta",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ModoVacaciones",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaActividadCreacion",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampanasCrowdfunding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Meta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AporteMinimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 1.00m),
                    TotalRecaudado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalAportantes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ImagenPreview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VideoPreview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaLimite = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFinalizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TipoLado = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContenidoEntregadoId = table.Column<int>(type: "int", nullable: true),
                    MensajeAgradecimiento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EsVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Vistas = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampanasCrowdfunding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampanasCrowdfunding_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampanasCrowdfunding_Contenidos_ContenidoEntregadoId",
                        column: x => x.ContenidoEntregadoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesVacaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MensajeAutorespuesta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AutoResponderMensajes = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ProtegerSuscriptores = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    MensajePerfilPublico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesVacaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracionesVacaciones_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContenidosProgramados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ContenidoBorradorId = table.Column<int>(type: "int", nullable: true),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Publicado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaPublicacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Cancelado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContenidosProgramados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContenidosProgramados_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContenidosProgramados_Contenidos_ContenidoBorradorId",
                        column: x => x.ContenidoBorradorId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LogrosCelebrados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TipoLogro = table.Column<int>(type: "int", nullable: false),
                    ValorHito = table.Column<int>(type: "int", nullable: false),
                    Visto = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaLogro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogrosCelebrados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogrosCelebrados_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subastas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: true),
                    ImagenPreview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoContenidoSubasta = table.Column<int>(type: "int", nullable: false),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrecioInicial = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioActual = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    IncrementoMinimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 1.00m),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ContadorPujas = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NumeroVistas = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SoloSuscriptores = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PrecioCompraloYa = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MostrarHistorialPujas = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    GanadorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExtensionAutomatica = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ExtensionesRealizadas = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaximoExtensiones = table.Column<int>(type: "int", nullable: false, defaultValue: 5)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subastas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subastas_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subastas_AspNetUsers_GanadorId",
                        column: x => x.GanadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Subastas_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SuscripcionesGrupales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NombreGrupo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CodigoInvitacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaxMiembros = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    MiembrosActuales = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PrecioTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioIndividual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DescuentoPorcentaje = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    TipoLado = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActivacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaLimiteFormacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccesoAbierto = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ActivacionAutomatica = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DuracionDias = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    RenovacionAutomatica = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SuscripcionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesGrupales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscripcionesGrupales_AspNetUsers_CreadorId",
                        column: x => x.CreadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SuscripcionesGrupales_Suscripciones_SuscripcionId",
                        column: x => x.SuscripcionId,
                        principalTable: "Suscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AportesCrowdfunding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampanaId = table.Column<int>(type: "int", nullable: false),
                    AportanteId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaAporte = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaDevolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsAnonimo = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AccesoOtorgado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaAcceso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransaccionId = table.Column<int>(type: "int", nullable: true),
                    TransaccionDevolucionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AportesCrowdfunding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AportesCrowdfunding_AspNetUsers_AportanteId",
                        column: x => x.AportanteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AportesCrowdfunding_CampanasCrowdfunding_CampanaId",
                        column: x => x.CampanaId,
                        principalTable: "CampanasCrowdfunding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubastasPujas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubastaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaPuja = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EsSuperada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EsGanadora = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubastasPujas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubastasPujas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubastasPujas_Subastas_SubastaId",
                        column: x => x.SubastaId,
                        principalTable: "Subastas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MiembrosGruposSuscripcion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SuscripcionGrupalId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EsLider = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaUnion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaSalida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    HaPagado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaPago = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MontoPagado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TransaccionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosGruposSuscripcion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiembrosGruposSuscripcion_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiembrosGruposSuscripcion_SuscripcionesGrupales_SuscripcionGrupalId",
                        column: x => x.SuscripcionGrupalId,
                        principalTable: "SuscripcionesGrupales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MiembrosGruposSuscripcion_Transacciones_TransaccionId",
                        column: x => x.TransaccionId,
                        principalTable: "Transacciones",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 31, 21, 19, 41, 179, DateTimeKind.Local).AddTicks(2036));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 31, 21, 19, 41, 179, DateTimeKind.Local).AddTicks(2112));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 31, 21, 19, 41, 179, DateTimeKind.Local).AddTicks(2115));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 31, 21, 19, 41, 179, DateTimeKind.Local).AddTicks(2117));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 31, 21, 19, 41, 179, DateTimeKind.Local).AddTicks(2119));

            migrationBuilder.CreateIndex(
                name: "IX_AportesCrowdfunding_AportanteId",
                table: "AportesCrowdfunding",
                column: "AportanteId");

            migrationBuilder.CreateIndex(
                name: "IX_AportesCrowdfunding_CampanaId",
                table: "AportesCrowdfunding",
                column: "CampanaId");

            migrationBuilder.CreateIndex(
                name: "IX_AportesCrowdfunding_CampanaId_Estado",
                table: "AportesCrowdfunding",
                columns: new[] { "CampanaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_AportesCrowdfunding_CampanaId_FechaAporte",
                table: "AportesCrowdfunding",
                columns: new[] { "CampanaId", "FechaAporte" });

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_ContenidoEntregadoId",
                table: "CampanasCrowdfunding",
                column: "ContenidoEntregadoId");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_CreadorId",
                table: "CampanasCrowdfunding",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_CreadorId_Estado",
                table: "CampanasCrowdfunding",
                columns: new[] { "CreadorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_Estado",
                table: "CampanasCrowdfunding",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_Estado_FechaLimite",
                table: "CampanasCrowdfunding",
                columns: new[] { "Estado", "FechaLimite" });

            migrationBuilder.CreateIndex(
                name: "IX_CampanasCrowdfunding_FechaLimite",
                table: "CampanasCrowdfunding",
                column: "FechaLimite");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesVacaciones_CreadorId",
                table: "ConfiguracionesVacaciones",
                column: "CreadorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContenidosProgramados_ContenidoBorradorId",
                table: "ContenidosProgramados",
                column: "ContenidoBorradorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidosProgramados_CreadorId",
                table: "ContenidosProgramados",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidosProgramados_FechaProgramada",
                table: "ContenidosProgramados",
                column: "FechaProgramada");

            migrationBuilder.CreateIndex(
                name: "IX_ContenidosProgramados_Pendientes",
                table: "ContenidosProgramados",
                columns: new[] { "Publicado", "Cancelado", "FechaProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_LogrosCelebrados_Creador_Tipo_Valor",
                table: "LogrosCelebrados",
                columns: new[] { "CreadorId", "TipoLogro", "ValorHito" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogrosCelebrados_Creador_Visto",
                table: "LogrosCelebrados",
                columns: new[] { "CreadorId", "Visto" });

            migrationBuilder.CreateIndex(
                name: "IX_LogrosCelebrados_CreadorId",
                table: "LogrosCelebrados",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosGruposSuscripcion_Grupo_Estado",
                table: "MiembrosGruposSuscripcion",
                columns: new[] { "SuscripcionGrupalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosGruposSuscripcion_Grupo_Usuario_Unique",
                table: "MiembrosGruposSuscripcion",
                columns: new[] { "SuscripcionGrupalId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosGruposSuscripcion_SuscripcionGrupalId",
                table: "MiembrosGruposSuscripcion",
                column: "SuscripcionGrupalId");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosGruposSuscripcion_TransaccionId",
                table: "MiembrosGruposSuscripcion",
                column: "TransaccionId");

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosGruposSuscripcion_UsuarioId",
                table: "MiembrosGruposSuscripcion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_ContenidoId",
                table: "Subastas",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_CreadorId",
                table: "Subastas",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_Estado",
                table: "Subastas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_Estado_FechaFin",
                table: "Subastas",
                columns: new[] { "Estado", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_FechaFin",
                table: "Subastas",
                column: "FechaFin");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_GanadorId",
                table: "Subastas",
                column: "GanadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Subastas_TipoContenido",
                table: "Subastas",
                column: "TipoContenidoSubasta");

            migrationBuilder.CreateIndex(
                name: "IX_SubastasPujas_SubastaId",
                table: "SubastasPujas",
                column: "SubastaId");

            migrationBuilder.CreateIndex(
                name: "IX_SubastasPujas_SubastaId_FechaPuja",
                table: "SubastasPujas",
                columns: new[] { "SubastaId", "FechaPuja" });

            migrationBuilder.CreateIndex(
                name: "IX_SubastasPujas_SubastaId_Monto",
                table: "SubastasPujas",
                columns: new[] { "SubastaId", "Monto" });

            migrationBuilder.CreateIndex(
                name: "IX_SubastasPujas_UsuarioId",
                table: "SubastasPujas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesGrupales_CodigoInvitacion",
                table: "SuscripcionesGrupales",
                column: "CodigoInvitacion");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesGrupales_CreadorId",
                table: "SuscripcionesGrupales",
                column: "CreadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesGrupales_CreadorId_Estado",
                table: "SuscripcionesGrupales",
                columns: new[] { "CreadorId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesGrupales_Estado",
                table: "SuscripcionesGrupales",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesGrupales_SuscripcionId",
                table: "SuscripcionesGrupales",
                column: "SuscripcionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AportesCrowdfunding");

            migrationBuilder.DropTable(
                name: "ConfiguracionesVacaciones");

            migrationBuilder.DropTable(
                name: "ContenidosProgramados");

            migrationBuilder.DropTable(
                name: "LogrosCelebrados");

            migrationBuilder.DropTable(
                name: "MiembrosGruposSuscripcion");

            migrationBuilder.DropTable(
                name: "SubastasPujas");

            migrationBuilder.DropTable(
                name: "CampanasCrowdfunding");

            migrationBuilder.DropTable(
                name: "SuscripcionesGrupales");

            migrationBuilder.DropTable(
                name: "Subastas");

            migrationBuilder.DropColumn(
                name: "AlertaBurnoutMostrada",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DiasConsecutivosActivo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaFinVacaciones",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaInicioVacaciones",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaUltimaAlertaBurnout",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaUltimoResetHoras",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HorasTrabajadasHoy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LimiteHorasDiarias",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MensajeAutorespuesta",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ModoVacaciones",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UltimaActividadCreacion",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 30, 23, 2, 10, 40, DateTimeKind.Local).AddTicks(7654));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 30, 23, 2, 10, 40, DateTimeKind.Local).AddTicks(7715));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 30, 23, 2, 10, 40, DateTimeKind.Local).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 30, 23, 2, 10, 40, DateTimeKind.Local).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 30, 23, 2, 10, 40, DateTimeKind.Local).AddTicks(7720));
        }
    }
}
