using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColaModeracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Prioridad = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    SupervisorAsignadoId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeoutAsignacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClasificadoPorIA = table.Column<bool>(type: "bit", nullable: false),
                    ResultadoClasificacionIA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfianzaIA = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    IADetectoProblema = table.Column<bool>(type: "bit", nullable: true),
                    CategoriasIA = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsDeReporte = table.Column<bool>(type: "bit", nullable: false),
                    ReporteId = table.Column<int>(type: "int", nullable: true),
                    VecesReasignado = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NotasInternas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecisionFinal = table.Column<int>(type: "int", nullable: true),
                    RazonRechazo = table.Column<int>(type: "int", nullable: true),
                    DetalleRazon = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TiempoRevisionSegundos = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColaModeracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColaModeracion_AspNetUsers_SupervisorAsignadoId",
                        column: x => x.SupervisorAsignadoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ColaModeracion_Contenidos_ContenidoId",
                        column: x => x.ContenidoId,
                        principalTable: "Contenidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ColaModeracion_Reportes_ReporteId",
                        column: x => x.ReporteId,
                        principalTable: "Reportes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetricasSupervisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupervisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    TotalRevisados = table.Column<int>(type: "int", nullable: false),
                    Aprobados = table.Column<int>(type: "int", nullable: false),
                    Rechazados = table.Column<int>(type: "int", nullable: false),
                    Censurados = table.Column<int>(type: "int", nullable: false),
                    Escalados = table.Column<int>(type: "int", nullable: false),
                    Revertidos = table.Column<int>(type: "int", nullable: false),
                    TiempoPromedioSegundos = table.Column<decimal>(type: "decimal(10,2)", nullable: false, defaultValue: 0m),
                    TiempoTotalSegundos = table.Column<int>(type: "int", nullable: false),
                    TiempoMinimoSegundos = table.Column<int>(type: "int", nullable: true),
                    TiempoMaximoSegundos = table.Column<int>(type: "int", nullable: true),
                    HoraInicioActividad = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HoraUltimaActividad = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroSesiones = table.Column<int>(type: "int", nullable: false),
                    TasaAprobacion = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    TasaEscalamiento = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 0m),
                    ConcordanciaIA = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricasSupervisor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricasSupervisor_AspNetUsers_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermisosSupervisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Modulo = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosSupervisor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolesSupervisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ColorBadge = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "#4682B4"),
                    Icono = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "fa-user-shield"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaxItemsSimultaneos = table.Column<int>(type: "int", nullable: false, defaultValue: 5)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesSupervisor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionesModeracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColaModeracionId = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    RazonRechazo = table.Column<int>(type: "int", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaDecision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TiempoRevisionSegundos = table.Column<int>(type: "int", nullable: false),
                    FueRevertida = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RazonReversion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevertidoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaReversion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionesModeracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionesModeracion_AspNetUsers_RevertidoPorId",
                        column: x => x.RevertidoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DecisionesModeracion_AspNetUsers_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DecisionesModeracion_ColaModeracion_ColaModeracionId",
                        column: x => x.ColaModeracionId,
                        principalTable: "ColaModeracion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolesSupervisorPermisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RolSupervisorId = table.Column<int>(type: "int", nullable: false),
                    PermisoSupervisorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesSupervisorPermisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolesSupervisorPermisos_PermisosSupervisor_PermisoSupervisorId",
                        column: x => x.PermisoSupervisorId,
                        principalTable: "PermisosSupervisor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolesSupervisorPermisos_RolesSupervisor_RolSupervisorId",
                        column: x => x.RolSupervisorId,
                        principalTable: "RolesSupervisor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosSupervisor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RolSupervisorId = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaDesactivacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AsignadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Turno = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UltimaActividad = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstaDisponible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ItemsAsignados = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosSupervisor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuariosSupervisor_AspNetUsers_AsignadoPorId",
                        column: x => x.AsignadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosSupervisor_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosSupervisor_RolesSupervisor_RolSupervisorId",
                        column: x => x.RolSupervisorId,
                        principalTable: "RolesSupervisor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5875));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5990));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5992));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5993));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 10, 16, 48, 40, 76, DateTimeKind.Local).AddTicks(5994));

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_ContenidoId",
                table: "ColaModeracion",
                column: "ContenidoId");

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_Estado",
                table: "ColaModeracion",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_Estado_Prioridad_Fecha",
                table: "ColaModeracion",
                columns: new[] { "Estado", "Prioridad", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_Prioridad",
                table: "ColaModeracion",
                column: "Prioridad");

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_ReporteId",
                table: "ColaModeracion",
                column: "ReporteId");

            migrationBuilder.CreateIndex(
                name: "IX_ColaModeracion_SupervisorAsignado",
                table: "ColaModeracion",
                column: "SupervisorAsignadoId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesModeracion_ColaModeracionId",
                table: "DecisionesModeracion",
                column: "ColaModeracionId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesModeracion_FechaDecision",
                table: "DecisionesModeracion",
                column: "FechaDecision");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesModeracion_RevertidoPorId",
                table: "DecisionesModeracion",
                column: "RevertidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesModeracion_Supervisor_Fecha",
                table: "DecisionesModeracion",
                columns: new[] { "SupervisorId", "FechaDecision" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionesModeracion_SupervisorId",
                table: "DecisionesModeracion",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasSupervisor_Fecha",
                table: "MetricasSupervisor",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MetricasSupervisor_Supervisor_Fecha",
                table: "MetricasSupervisor",
                columns: new[] { "SupervisorId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosSupervisor_Codigo",
                table: "PermisosSupervisor",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosSupervisor_Modulo",
                table: "PermisosSupervisor",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_RolesSupervisor_Nombre",
                table: "RolesSupervisor",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesSupervisorPermisos_PermisoSupervisorId",
                table: "RolesSupervisorPermisos",
                column: "PermisoSupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_RolesSupervisorPermisos_Rol_Permiso",
                table: "RolesSupervisorPermisos",
                columns: new[] { "RolSupervisorId", "PermisoSupervisorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSupervisor_Activo_Disponible",
                table: "UsuariosSupervisor",
                columns: new[] { "EstaActivo", "EstaDisponible" });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSupervisor_AsignadoPorId",
                table: "UsuariosSupervisor",
                column: "AsignadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSupervisor_RolSupervisorId",
                table: "UsuariosSupervisor",
                column: "RolSupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSupervisor_UsuarioId",
                table: "UsuariosSupervisor",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionesModeracion");

            migrationBuilder.DropTable(
                name: "MetricasSupervisor");

            migrationBuilder.DropTable(
                name: "RolesSupervisorPermisos");

            migrationBuilder.DropTable(
                name: "UsuariosSupervisor");

            migrationBuilder.DropTable(
                name: "ColaModeracion");

            migrationBuilder.DropTable(
                name: "PermisosSupervisor");

            migrationBuilder.DropTable(
                name: "RolesSupervisor");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5857));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5917));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5919));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5920));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 9, 13, 42, 43, 593, DateTimeKind.Local).AddTicks(5921));
        }
    }
}
