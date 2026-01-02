using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSistemaLadoCoins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceptaLadoCoins",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BonoBienvenidaEntregado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BonoEmailVerificadoEntregado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BonoPerfilCompletoEntregado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BonoPrimerContenidoEntregado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CodigoReferido",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PorcentajeMaxLadoCoinsSuscripcion",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateTable(
                name: "ConfiguracionesLadoCoins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesLadoCoins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LadoCoins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SaldoDisponible = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    SaldoPorVencer = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalGanado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalGastado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalQuemado = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    TotalRecibido = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadoCoins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LadoCoins_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RachasUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RachaActual = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RachaMaxima = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UltimoLoginPremio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LikesHoy = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ComentariosHoy = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ContenidosHoy = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Premio5LikesHoy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Premio3ComentariosHoy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PremioContenidoHoy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PremioLoginHoy = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FechaReset = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RachasUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RachasUsuarios_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Referidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferidorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferidoUsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CodigoUsado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaExpiracionComision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalComisionGanada = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m),
                    BonoReferidorEntregado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BonoReferidoEntregado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    BonoCreadorLadoBEntregado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ComisionActiva = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UltimaComision = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referidos_AspNetUsers_ReferidoUsuarioId",
                        column: x => x.ReferidoUsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referidos_AspNetUsers_ReferidorId",
                        column: x => x.ReferidorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransaccionesLadoCoins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontoQuemado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SaldoAnterior = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoPosterior = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenciaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TipoReferencia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FechaTransaccion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Vencido = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MontoRestante = table.Column<decimal>(type: "decimal(18,2)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransaccionesLadoCoins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransaccionesLadoCoins_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1030));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1146));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1148));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1149));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 1, 15, 52, 33, 69, DateTimeKind.Local).AddTicks(1150));

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CodigoReferido",
                table: "AspNetUsers",
                column: "CodigoReferido",
                unique: true,
                filter: "[CodigoReferido] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesLadoCoins_Activo",
                table: "ConfiguracionesLadoCoins",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesLadoCoins_Categoria",
                table: "ConfiguracionesLadoCoins",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesLadoCoins_Clave",
                table: "ConfiguracionesLadoCoins",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LadoCoins_SaldoDisponible",
                table: "LadoCoins",
                column: "SaldoDisponible");

            migrationBuilder.CreateIndex(
                name: "IX_LadoCoins_UsuarioId",
                table: "LadoCoins",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RachasUsuarios_FechaReset",
                table: "RachasUsuarios",
                column: "FechaReset");

            migrationBuilder.CreateIndex(
                name: "IX_RachasUsuarios_RachaActual",
                table: "RachasUsuarios",
                column: "RachaActual");

            migrationBuilder.CreateIndex(
                name: "IX_RachasUsuarios_UsuarioId",
                table: "RachasUsuarios",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_CodigoUsado",
                table: "Referidos",
                column: "CodigoUsado");

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_ComisionActiva",
                table: "Referidos",
                column: "ComisionActiva");

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_Comisiones_Activas",
                table: "Referidos",
                columns: new[] { "ReferidorId", "ComisionActiva", "FechaExpiracionComision" });

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_FechaExpiracionComision",
                table: "Referidos",
                column: "FechaExpiracionComision");

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_ReferidorId",
                table: "Referidos",
                column: "ReferidorId");

            migrationBuilder.CreateIndex(
                name: "IX_Referidos_ReferidoUsuarioId",
                table: "Referidos",
                column: "ReferidoUsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_FechaTransaccion",
                table: "TransaccionesLadoCoins",
                column: "FechaTransaccion");

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_FechaVencimiento",
                table: "TransaccionesLadoCoins",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_Tipo",
                table: "TransaccionesLadoCoins",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_Usuario_Fecha",
                table: "TransaccionesLadoCoins",
                columns: new[] { "UsuarioId", "FechaTransaccion" });

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_UsuarioId",
                table: "TransaccionesLadoCoins",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_Vencido",
                table: "TransaccionesLadoCoins",
                column: "Vencido");

            migrationBuilder.CreateIndex(
                name: "IX_TransaccionesLadoCoins_Vencimiento_FIFO",
                table: "TransaccionesLadoCoins",
                columns: new[] { "UsuarioId", "Vencido", "FechaVencimiento", "MontoRestante" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesLadoCoins");

            migrationBuilder.DropTable(
                name: "LadoCoins");

            migrationBuilder.DropTable(
                name: "RachasUsuarios");

            migrationBuilder.DropTable(
                name: "Referidos");

            migrationBuilder.DropTable(
                name: "TransaccionesLadoCoins");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CodigoReferido",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AceptaLadoCoins",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BonoBienvenidaEntregado",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BonoEmailVerificadoEntregado",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BonoPerfilCompletoEntregado",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BonoPrimerContenidoEntregado",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CodigoReferido",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PorcentajeMaxLadoCoinsSuscripcion",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9618));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9740));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9741));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9743));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2025, 12, 28, 22, 40, 17, 687, DateTimeKind.Local).AddTicks(9744));
        }
    }
}
