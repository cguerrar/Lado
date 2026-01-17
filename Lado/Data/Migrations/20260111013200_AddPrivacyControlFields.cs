using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyControlFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reportes - ComentarioId
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Reportes') AND name = 'ComentarioId')
                BEGIN
                    ALTER TABLE [Reportes] ADD [ComentarioId] int NULL;
                END");

            // Reportes - StoryId
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Reportes') AND name = 'StoryId')
                BEGIN
                    ALTER TABLE [Reportes] ADD [StoryId] int NULL;
                END");

            // AspNetUsers - BoostActivo
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'BoostActivo')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [BoostActivo] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - BoostCredito
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'BoostCredito')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [BoostCredito] decimal(18,2) NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - BoostFechaFin
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'BoostFechaFin')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [BoostFechaFin] datetime2 NULL;
                END");

            // AspNetUsers - BoostMultiplicador
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'BoostMultiplicador')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [BoostMultiplicador] decimal(18,2) NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - MostrarEnBusquedas
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'MostrarEnBusquedas')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [MostrarEnBusquedas] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - MostrarSeguidores
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'MostrarSeguidores')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [MostrarSeguidores] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - MostrarSiguiendo
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'MostrarSiguiendo')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [MostrarSiguiendo] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - PerfilPrivado
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'PerfilPrivado')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [PerfilPrivado] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - PermitirEtiquetas
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'PermitirEtiquetas')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [PermitirEtiquetas] bit NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - QuienPuedeComentar
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'QuienPuedeComentar')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [QuienPuedeComentar] int NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - QuienPuedeMensajear
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'QuienPuedeMensajear')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [QuienPuedeMensajear] int NOT NULL DEFAULT 0;
                END");

            // AspNetUsers - SaldoPublicitario
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'SaldoPublicitario')
                BEGIN
                    ALTER TABLE [AspNetUsers] ADD [SaldoPublicitario] decimal(18,2) NOT NULL DEFAULT 0;
                END");

            // Indices
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reportes_ComentarioId')
                BEGIN
                    CREATE INDEX [IX_Reportes_ComentarioId] ON [Reportes] ([ComentarioId]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Reportes_StoryId')
                BEGIN
                    CREATE INDEX [IX_Reportes_StoryId] ON [Reportes] ([StoryId]);
                END");

            // Foreign Keys
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Reportes_Comentarios_ComentarioId')
                BEGIN
                    ALTER TABLE [Reportes] ADD CONSTRAINT [FK_Reportes_Comentarios_ComentarioId]
                    FOREIGN KEY ([ComentarioId]) REFERENCES [Comentarios] ([Id]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Reportes_Stories_StoryId')
                BEGIN
                    ALTER TABLE [Reportes] ADD CONSTRAINT [FK_Reportes_Stories_StoryId]
                    FOREIGN KEY ([StoryId]) REFERENCES [Stories] ([Id]);
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reportes_Comentarios_ComentarioId",
                table: "Reportes");

            migrationBuilder.DropForeignKey(
                name: "FK_Reportes_Stories_StoryId",
                table: "Reportes");

            migrationBuilder.DropIndex(
                name: "IX_Reportes_ComentarioId",
                table: "Reportes");

            migrationBuilder.DropIndex(
                name: "IX_Reportes_StoryId",
                table: "Reportes");

            migrationBuilder.DropColumn(
                name: "ComentarioId",
                table: "Reportes");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "Reportes");

            migrationBuilder.DropColumn(
                name: "BoostActivo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BoostCredito",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BoostFechaFin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BoostMultiplicador",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MostrarEnBusquedas",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MostrarSeguidores",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MostrarSiguiendo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PerfilPrivado",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PermitirEtiquetas",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QuienPuedeComentar",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "QuienPuedeMensajear",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SaldoPublicitario",
                table: "AspNetUsers");
        }
    }
}
