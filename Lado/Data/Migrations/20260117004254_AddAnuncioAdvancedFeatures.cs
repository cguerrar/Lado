using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnuncioAdvancedFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ClicsHoy",
                table: "Anuncios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "EsCarrusel",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaUltimoResetDiario",
                table: "Anuncios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrecuenciaEnFeed",
                table: "Anuncios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImagenesCarruselJson",
                table: "Anuncios",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImpresionesHoy",
                table: "Anuncios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "MaxImpresionesUsuario",
                table: "Anuncios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxImpresionesUsuarioDia",
                table: "Anuncios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinutosEntreImpresiones",
                table: "Anuncios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarBannerInferior",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarBannerSuperior",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarEnExplorar",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarEnFeed",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarEnPerfiles",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarEnStories",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NombreInterno",
                table: "Anuncios",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotasInternas",
                table: "Anuncios",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SoloConSuscripciones",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloCreadores",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloFans",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoloVerificados",
                table: "Anuncios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "UsuariosUnicos",
                table: "Anuncios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "VistasAnunciosUsuarios",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnuncioId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TotalImpresiones = table.Column<int>(type: "int", nullable: false),
                    ImpresionesHoy = table.Column<int>(type: "int", nullable: false),
                    PrimeraImpresion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaImpresion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaUltimoReset = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HizoClic = table.Column<bool>(type: "bit", nullable: false),
                    TotalClics = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VistasAnunciosUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VistasAnunciosUsuarios_Anuncios_AnuncioId",
                        column: x => x.AnuncioId,
                        principalTable: "Anuncios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VistasAnunciosUsuarios_AspNetUsers_UsuarioId",
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
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(5939));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6017));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6028));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6030));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 42, 53, 552, DateTimeKind.Local).AddTicks(6032));

            migrationBuilder.CreateIndex(
                name: "IX_VistasAnunciosUsuarios_AnuncioId",
                table: "VistasAnunciosUsuarios",
                column: "AnuncioId");

            migrationBuilder.CreateIndex(
                name: "IX_VistasAnunciosUsuarios_UsuarioId",
                table: "VistasAnunciosUsuarios",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VistasAnunciosUsuarios");

            migrationBuilder.DropColumn(
                name: "ClicsHoy",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "EsCarrusel",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "FechaUltimoResetDiario",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "FrecuenciaEnFeed",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "ImagenesCarruselJson",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "ImpresionesHoy",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MaxImpresionesUsuario",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MaxImpresionesUsuarioDia",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MinutosEntreImpresiones",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarBannerInferior",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarBannerSuperior",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarEnExplorar",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarEnFeed",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarEnPerfiles",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "MostrarEnStories",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "NombreInterno",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "NotasInternas",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "SoloConSuscripciones",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "SoloCreadores",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "SoloFans",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "SoloVerificados",
                table: "Anuncios");

            migrationBuilder.DropColumn(
                name: "UsuariosUnicos",
                table: "Anuncios");

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 1,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6351));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 2,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6410));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 3,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6412));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 4,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6413));

            migrationBuilder.UpdateData(
                table: "ConfiguracionesPlataforma",
                keyColumn: "Id",
                keyValue: 5,
                column: "UltimaModificacion",
                value: new DateTime(2026, 1, 16, 21, 1, 18, 959, DateTimeKind.Local).AddTicks(6415));
        }
    }
}
