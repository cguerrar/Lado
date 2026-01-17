-- Script para crear las tablas de Usuarios Administrados
-- Ejecutar en la base de datos de producción

-- 1. Agregar columna EsUsuarioAdministrado a AspNetUsers (si no existe)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]') AND name = 'EsUsuarioAdministrado')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers] ADD [EsUsuarioAdministrado] BIT NOT NULL DEFAULT 0;
    CREATE INDEX IX_AspNetUsers_EsUsuarioAdministrado ON [dbo].[AspNetUsers]([EsUsuarioAdministrado]);
    PRINT 'Columna EsUsuarioAdministrado agregada a AspNetUsers';
END
ELSE
BEGIN
    PRINT 'Columna EsUsuarioAdministrado ya existe';
END
GO

-- 2. Crear tabla MediaBiblioteca (si no existe)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MediaBiblioteca')
BEGIN
    CREATE TABLE [dbo].[MediaBiblioteca] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [RutaArchivo] NVARCHAR(500) NOT NULL,
        [NombreOriginal] NVARCHAR(255) NULL,
        [TipoMedia] INT NOT NULL,
        [TamanoBytes] BIGINT NOT NULL,
        [Descripcion] NVARCHAR(2000) NULL,
        [Hashtags] NVARCHAR(500) NULL,
        [Estado] INT NOT NULL DEFAULT 0,
        [FechaSubida] DATETIME2 NOT NULL,
        [FechaProgramada] DATETIME2 NULL,
        [FechaPublicado] DATETIME2 NULL,
        [ContenidoPublicadoId] INT NULL,
        [TipoLado] INT NOT NULL DEFAULT 0,
        [SoloSuscriptores] BIT NOT NULL DEFAULT 0,
        [PrecioLadoCoins] INT NULL,
        [Orden] INT NOT NULL DEFAULT 0,
        [MensajeError] NVARCHAR(1000) NULL,
        [IntentosPublicacion] INT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_MediaBiblioteca] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MediaBiblioteca_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MediaBiblioteca_Contenidos_ContenidoPublicadoId] FOREIGN KEY ([ContenidoPublicadoId])
            REFERENCES [dbo].[Contenidos] ([Id]) ON DELETE SET NULL
    );

    CREATE INDEX IX_MediaBiblioteca_UsuarioId ON [dbo].[MediaBiblioteca]([UsuarioId]);
    CREATE INDEX IX_MediaBiblioteca_Estado ON [dbo].[MediaBiblioteca]([Estado]);
    CREATE INDEX IX_MediaBiblioteca_FechaProgramada ON [dbo].[MediaBiblioteca]([FechaProgramada]);
    CREATE INDEX IX_MediaBiblioteca_ContenidoPublicadoId ON [dbo].[MediaBiblioteca]([ContenidoPublicadoId]);

    PRINT 'Tabla MediaBiblioteca creada';
END
ELSE
BEGIN
    PRINT 'Tabla MediaBiblioteca ya existe';
END
GO

-- 3. Crear tabla ConfiguracionesPublicacionAutomatica (si no existe)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConfiguracionesPublicacionAutomatica')
BEGIN
    CREATE TABLE [dbo].[ConfiguracionesPublicacionAutomatica] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UsuarioId] NVARCHAR(450) NOT NULL,
        [Activo] BIT NOT NULL DEFAULT 0,
        [PublicacionesMinPorDia] INT NOT NULL DEFAULT 1,
        [PublicacionesMaxPorDia] INT NOT NULL DEFAULT 3,
        [HoraInicio] TIME NOT NULL DEFAULT '09:00:00',
        [HoraFin] TIME NOT NULL DEFAULT '22:00:00',
        [PublicarFinesDeSemana] BIT NOT NULL DEFAULT 1,
        [VariacionMinutos] INT NOT NULL DEFAULT 30,
        [TipoLadoDefault] INT NOT NULL DEFAULT 0,
        [SoloSuscriptoresDefault] BIT NOT NULL DEFAULT 0,
        [UltimaPublicacion] DATETIME2 NULL,
        [PublicacionesHoy] INT NOT NULL DEFAULT 0,
        [FechaUltimoReset] DATETIME2 NULL,
        [ProximaPublicacion] DATETIME2 NULL,
        [FechaCreacion] DATETIME2 NOT NULL,
        [FechaModificacion] DATETIME2 NOT NULL,
        [TotalPublicaciones] INT NOT NULL DEFAULT 0,
        [DiasPermitidos] NVARCHAR(20) NULL,
        CONSTRAINT [PK_ConfiguracionesPublicacionAutomatica] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ConfiguracionesPublicacionAutomatica_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX IX_ConfiguracionesPublicacionAutomatica_UsuarioId
        ON [dbo].[ConfiguracionesPublicacionAutomatica]([UsuarioId]);

    PRINT 'Tabla ConfiguracionesPublicacionAutomatica creada';
END
ELSE
BEGIN
    PRINT 'Tabla ConfiguracionesPublicacionAutomatica ya existe';
END
GO

-- 4. Registrar la migración en __EFMigrationsHistory
IF NOT EXISTS (SELECT * FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = '20260116120000_AddUsuariosAdministrados')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260116120000_AddUsuariosAdministrados', '8.0.0');
    PRINT 'Migración registrada en __EFMigrationsHistory';
END
GO

PRINT '=== Script completado exitosamente ===';
