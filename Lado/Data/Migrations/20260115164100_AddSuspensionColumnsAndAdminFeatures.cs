using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspensionColumnsAndAdminFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSuspension",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSuspensionFin",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSuspension",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendidoPorId",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditoriasConfiguracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoConfiguracion = table.Column<int>(type: "int", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValorNuevo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntidadId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModificadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpOrigen = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasConfiguracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriasConfiguracion_AspNetUsers_ModificadoPorId",
                        column: x => x.ModificadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosAdmin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TodoElDia = table.Column<bool>(type: "bit", nullable: false),
                    EsRecurrente = table.Column<bool>(type: "bit", nullable: false),
                    Recurrencia = table.Column<int>(type: "int", nullable: true),
                    FinRecurrencia = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ubicacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiereConfirmacion = table.Column<bool>(type: "bit", nullable: false),
                    EnviarRecordatorio = table.Column<bool>(type: "bit", nullable: false),
                    MinutosAnteRecordatorio = table.Column<int>(type: "int", nullable: false),
                    RecordatorioEnviado = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cancelado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAdmin", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosAdmin_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialMantenimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActivadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DesactivadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialMantenimiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialMantenimiento_AspNetUsers_ActivadoPorId",
                        column: x => x.ActivadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialMantenimiento_AspNetUsers_DesactivadoPorId",
                        column: x => x.DesactivadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModoMantenimiento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFinEstimado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MostrarCuentaRegresiva = table.Column<bool>(type: "bit", nullable: false),
                    PermitirCreadoresVerificados = table.Column<bool>(type: "bit", nullable: false),
                    RutasPermitidas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActivadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotificarMinutosAntes = table.Column<int>(type: "int", nullable: false),
                    NotificacionPreviaEnviada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModoMantenimiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModoMantenimiento_AspNetUsers_ActivadoPorId",
                        column: x => x.ActivadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotasInternas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoEntidad = table.Column<int>(type: "int", nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    EsFijada = table.Column<bool>(type: "bit", nullable: false),
                    EstaActiva = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEdicion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasInternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasInternas_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasInternas_AspNetUsers_EditadoPorId",
                        column: x => x.EditadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesRespuesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Atajo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VecesUsado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesRespuesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplatesRespuesta_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketsInternos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AsignadoAId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ItemRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    TipoItemRelacionado = table.Column<int>(type: "int", nullable: true),
                    Etiquetas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketsInternos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketsInternos_AspNetUsers_AsignadoAId",
                        column: x => x.AsignadoAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketsInternos_AspNetUsers_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesEventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantesEventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantesEventos_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantesEventos_EventosAdmin_EventoId",
                        column: x => x.EventoId,
                        principalTable: "EventosAdmin",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RespuestasTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EsNotaInterna = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespuestasTickets_AspNetUsers_AutorId",
                        column: x => x.AutorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespuestasTickets_TicketsInternos_TicketId",
                        column: x => x.TicketId,
                        principalTable: "TicketsInternos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(841));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(905));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(906));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(908));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 15, 13, 40, 59, 558, DateTimeKind.Local).AddTicks(909));

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriasConfiguracion_ModificadoPorId",
                table: "AuditoriasConfiguracion",
                column: "ModificadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAdmin_CreadoPorId",
                table: "EventosAdmin",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMantenimiento_ActivadoPorId",
                table: "HistorialMantenimiento",
                column: "ActivadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMantenimiento_DesactivadoPorId",
                table: "HistorialMantenimiento",
                column: "DesactivadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModoMantenimiento_ActivadoPorId",
                table: "ModoMantenimiento",
                column: "ActivadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasInternas_CreadoPorId",
                table: "NotasInternas",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasInternas_EditadoPorId",
                table: "NotasInternas",
                column: "EditadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesEventos_EventoId",
                table: "ParticipantesEventos",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesEventos_UsuarioId",
                table: "ParticipantesEventos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasTickets_AutorId",
                table: "RespuestasTickets",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestasTickets_TicketId",
                table: "RespuestasTickets",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesRespuesta_CreadoPorId",
                table: "TemplatesRespuesta",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsInternos_AsignadoAId",
                table: "TicketsInternos",
                column: "AsignadoAId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketsInternos_CreadoPorId",
                table: "TicketsInternos",
                column: "CreadoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasConfiguracion");

            migrationBuilder.DropTable(
                name: "HistorialMantenimiento");

            migrationBuilder.DropTable(
                name: "ModoMantenimiento");

            migrationBuilder.DropTable(
                name: "NotasInternas");

            migrationBuilder.DropTable(
                name: "ParticipantesEventos");

            migrationBuilder.DropTable(
                name: "RespuestasTickets");

            migrationBuilder.DropTable(
                name: "TemplatesRespuesta");

            migrationBuilder.DropTable(
                name: "EventosAdmin");

            migrationBuilder.DropTable(
                name: "TicketsInternos");

            migrationBuilder.DropColumn(
                name: "FechaSuspension",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FechaSuspensionFin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RazonSuspension",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SuspendidoPorId",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9186));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9258));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9260));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9262));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 12, 16, 2, 15, 552, DateTimeKind.Local).AddTicks(9264));
        }
    }
}
