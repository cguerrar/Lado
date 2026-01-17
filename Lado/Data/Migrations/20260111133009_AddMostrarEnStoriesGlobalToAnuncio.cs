using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lado.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMostrarEnStoriesGlobalToAnuncio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Columnas Email en AspNetUsers - idempotente
            var emailColumns = new[] { "EmailComentarios", "EmailConsejos", "EmailMenciones",
                "EmailNuevasSuscripciones", "EmailNuevoContenido", "EmailNuevosMensajes",
                "EmailNuevosSeguidores", "EmailPropinas", "EmailReporteGanancias",
                "EmailResumenSemanal", "EmailStories" };

            foreach (var col in emailColumns)
            {
                migrationBuilder.Sql($@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = '{col}')
                    BEGIN
                        ALTER TABLE [AspNetUsers] ADD [{col}] bit NOT NULL DEFAULT 0;
                    END");
            }

            // MostrarEnStoriesGlobal en Anuncios
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Anuncios') AND name = 'MostrarEnStoriesGlobal')
                BEGIN
                    ALTER TABLE [Anuncios] ADD [MostrarEnStoriesGlobal] bit NOT NULL DEFAULT 0;
                END");

            // Tabla GruposDestacados
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GruposDestacados')
                BEGIN
                    CREATE TABLE [GruposDestacados] (
                        [Id] int NOT NULL IDENTITY,
                        [UsuarioId] nvarchar(450) NOT NULL,
                        [Nombre] nvarchar(50) NOT NULL,
                        [ImagenPortada] nvarchar(500) NULL,
                        [Orden] int NOT NULL,
                        [FechaCreacion] datetime2 NOT NULL,
                        [TipoLado] int NOT NULL,
                        CONSTRAINT [PK_GruposDestacados] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_GruposDestacados_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_GruposDestacados_UsuarioId] ON [GruposDestacados] ([UsuarioId]);
                END");

            // Tabla StoriesEnviadas
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StoriesEnviadas')
                BEGIN
                    CREATE TABLE [StoriesEnviadas] (
                        [Id] int NOT NULL IDENTITY,
                        [StoryId] int NOT NULL,
                        [RemitenteId] nvarchar(450) NOT NULL,
                        [DestinatarioId] nvarchar(450) NOT NULL,
                        [FechaEnvio] datetime2 NOT NULL,
                        [Visto] bit NOT NULL,
                        [FechaVisto] datetime2 NULL,
                        [Mensaje] nvarchar(500) NULL,
                        CONSTRAINT [PK_StoriesEnviadas] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_StoriesEnviadas_AspNetUsers_DestinatarioId] FOREIGN KEY ([DestinatarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_StoriesEnviadas_AspNetUsers_RemitenteId] FOREIGN KEY ([RemitenteId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_StoriesEnviadas_Stories_StoryId] FOREIGN KEY ([StoryId]) REFERENCES [Stories] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_StoriesEnviadas_DestinatarioId] ON [StoriesEnviadas] ([DestinatarioId]);
                    CREATE INDEX [IX_StoriesEnviadas_RemitenteId] ON [StoriesEnviadas] ([RemitenteId]);
                    CREATE INDEX [IX_StoriesEnviadas_StoryId] ON [StoriesEnviadas] ([StoryId]);
                END");

            // Tabla HistoriasDestacadas
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriasDestacadas')
                BEGIN
                    CREATE TABLE [HistoriasDestacadas] (
                        [Id] int NOT NULL IDENTITY,
                        [UsuarioId] nvarchar(450) NOT NULL,
                        [GrupoDestacadoId] int NULL,
                        [RutaArchivo] nvarchar(500) NOT NULL,
                        [TipoContenido] int NOT NULL,
                        [ElementosJson] nvarchar(max) NULL,
                        [StoryOriginalId] int NULL,
                        [FechaCreacion] datetime2 NOT NULL,
                        [Orden] int NOT NULL,
                        [NumeroVistas] int NOT NULL,
                        [TipoLado] int NOT NULL,
                        [PistaMusicalId] int NULL,
                        [MusicaInicioSegundos] int NOT NULL,
                        [MusicaVolumen] int NOT NULL,
                        CONSTRAINT [PK_HistoriasDestacadas] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_HistoriasDestacadas_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_HistoriasDestacadas_GruposDestacados_GrupoDestacadoId] FOREIGN KEY ([GrupoDestacadoId]) REFERENCES [GruposDestacados] ([Id]),
                        CONSTRAINT [FK_HistoriasDestacadas_PistasMusica_PistaMusicalId] FOREIGN KEY ([PistaMusicalId]) REFERENCES [PistasMusica] ([Id])
                    );
                    CREATE INDEX [IX_HistoriasDestacadas_GrupoDestacadoId] ON [HistoriasDestacadas] ([GrupoDestacadoId]);
                    CREATE INDEX [IX_HistoriasDestacadas_PistaMusicalId] ON [HistoriasDestacadas] ([PistaMusicalId]);
                    CREATE INDEX [IX_HistoriasDestacadas_UsuarioId] ON [HistoriasDestacadas] ([UsuarioId]);
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HistoriasDestacadas");
            migrationBuilder.DropTable(name: "StoriesEnviadas");
            migrationBuilder.DropTable(name: "GruposDestacados");

            migrationBuilder.DropColumn(name: "EmailComentarios", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailConsejos", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailMenciones", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailNuevasSuscripciones", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailNuevoContenido", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailNuevosMensajes", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailNuevosSeguidores", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailPropinas", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailReporteGanancias", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailResumenSemanal", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailStories", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "MostrarEnStoriesGlobal", table: "Anuncios");
        }
    }
}
