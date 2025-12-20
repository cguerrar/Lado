-- Script para arreglar la tabla Notificaciones
-- Ejecutar en SQL Server Management Studio

-- Primero verificar si la tabla existe
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Notificaciones')
BEGIN
    PRINT 'Tabla Notificaciones existe, verificando columnas...'

    -- Agregar columnas faltantes si no existen
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'EstaActiva')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [EstaActiva] bit NOT NULL DEFAULT 1
        PRINT 'Columna EstaActiva agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ComentarioId')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [ComentarioId] int NULL
        PRINT 'Columna ComentarioId agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ContenidoId')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [ContenidoId] int NULL
        PRINT 'Columna ContenidoId agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'DesafioId')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [DesafioId] int NULL
        PRINT 'Columna DesafioId agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'FechaLectura')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [FechaLectura] datetime2 NULL
        PRINT 'Columna FechaLectura agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'ImagenUrl')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [ImagenUrl] nvarchar(500) NULL
        PRINT 'Columna ImagenUrl agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'MensajeId')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [MensajeId] int NULL
        PRINT 'Columna MensajeId agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'UrlDestino')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [UrlDestino] nvarchar(500) NULL
        PRINT 'Columna UrlDestino agregada'
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Notificaciones' AND COLUMN_NAME = 'UsuarioOrigenId')
    BEGIN
        ALTER TABLE [Notificaciones] ADD [UsuarioOrigenId] nvarchar(450) NULL
        PRINT 'Columna UsuarioOrigenId agregada'

        -- Agregar la foreign key
        ALTER TABLE [Notificaciones] ADD CONSTRAINT [FK_Notificaciones_AspNetUsers_UsuarioOrigenId]
            FOREIGN KEY ([UsuarioOrigenId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
        PRINT 'Foreign key UsuarioOrigenId agregada'
    END

    -- Crear índices si no existen
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notificaciones_UsuarioId' AND object_id = OBJECT_ID('Notificaciones'))
    BEGIN
        CREATE INDEX [IX_Notificaciones_UsuarioId] ON [Notificaciones] ([UsuarioId])
        PRINT 'Indice IX_Notificaciones_UsuarioId creado'
    END

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notificaciones_UsuarioOrigenId' AND object_id = OBJECT_ID('Notificaciones'))
    BEGIN
        CREATE INDEX [IX_Notificaciones_UsuarioOrigenId] ON [Notificaciones] ([UsuarioOrigenId])
        PRINT 'Indice IX_Notificaciones_UsuarioOrigenId creado'
    END

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notificaciones_FechaCreacion' AND object_id = OBJECT_ID('Notificaciones'))
    BEGIN
        CREATE INDEX [IX_Notificaciones_FechaCreacion] ON [Notificaciones] ([FechaCreacion] DESC)
        PRINT 'Indice IX_Notificaciones_FechaCreacion creado'
    END

    PRINT 'Tabla Notificaciones actualizada correctamente'
END
ELSE
BEGIN
    PRINT 'Tabla Notificaciones NO existe, creandola...'

    CREATE TABLE [Notificaciones] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] nvarchar(450) NOT NULL,
        [Tipo] int NOT NULL,
        [Mensaje] nvarchar(500) NOT NULL,
        [Titulo] nvarchar(200) NULL,
        [UsuarioOrigenId] nvarchar(450) NULL,
        [ContenidoId] int NULL,
        [MensajeId] int NULL,
        [DesafioId] int NULL,
        [ComentarioId] int NULL,
        [UrlDestino] nvarchar(500) NULL,
        [ImagenUrl] nvarchar(500) NULL,
        [FechaCreacion] datetime2 NOT NULL,
        [Leida] bit NOT NULL DEFAULT 0,
        [FechaLectura] datetime2 NULL,
        [EstaActiva] bit NOT NULL DEFAULT 1,
        CONSTRAINT [PK_Notificaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notificaciones_AspNetUsers_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Notificaciones_AspNetUsers_UsuarioOrigenId] FOREIGN KEY ([UsuarioOrigenId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
    )

    CREATE INDEX [IX_Notificaciones_UsuarioId] ON [Notificaciones] ([UsuarioId])
    CREATE INDEX [IX_Notificaciones_UsuarioOrigenId] ON [Notificaciones] ([UsuarioOrigenId])
    CREATE INDEX [IX_Notificaciones_FechaCreacion] ON [Notificaciones] ([FechaCreacion] DESC)

    PRINT 'Tabla Notificaciones creada correctamente'
END

-- Registrar la migración si no está registrada
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251216175610_AgregarNotificaciones')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251216175610_AgregarNotificaciones', '8.0.0')
    PRINT 'Migración registrada'
END
ELSE
BEGIN
    PRINT 'Migración ya estaba registrada'
END

PRINT '¡Script completado!'
