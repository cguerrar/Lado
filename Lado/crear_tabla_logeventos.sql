-- Script para crear la tabla LogEventos
-- Ejecutar en la base de datos Lado

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LogEventos')
BEGIN
    CREATE TABLE [dbo].[LogEventos] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Fecha] DATETIME2(7) NOT NULL,
        [Tipo] INT NOT NULL,
        [Categoria] INT NOT NULL,
        [Mensaje] NVARCHAR(500) NOT NULL,
        [Detalle] NVARCHAR(MAX) NULL,
        [UsuarioId] NVARCHAR(450) NULL,
        [UsuarioNombre] NVARCHAR(256) NULL,
        [IpAddress] NVARCHAR(45) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [Url] NVARCHAR(2000) NULL,
        [MetodoHttp] NVARCHAR(10) NULL,
        [TipoExcepcion] NVARCHAR(200) NULL,
        CONSTRAINT [PK_LogEventos] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Indices para mejorar rendimiento de consultas
    CREATE NONCLUSTERED INDEX [IX_LogEventos_Fecha] ON [dbo].[LogEventos] ([Fecha] DESC);
    CREATE NONCLUSTERED INDEX [IX_LogEventos_Tipo] ON [dbo].[LogEventos] ([Tipo]);
    CREATE NONCLUSTERED INDEX [IX_LogEventos_Categoria] ON [dbo].[LogEventos] ([Categoria]);
    CREATE NONCLUSTERED INDEX [IX_LogEventos_UsuarioId] ON [dbo].[LogEventos] ([UsuarioId]);

    -- FK opcional a AspNetUsers (comentado porque UsuarioId puede ser null o usuario eliminado)
    -- ALTER TABLE [dbo].[LogEventos] ADD CONSTRAINT [FK_LogEventos_AspNetUsers]
    --     FOREIGN KEY ([UsuarioId]) REFERENCES [dbo].[AspNetUsers] ([Id]);

    PRINT 'Tabla LogEventos creada exitosamente';
END
ELSE
BEGIN
    PRINT 'La tabla LogEventos ya existe';
END

-- Registrar migracion en EF si usas migraciones
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251218000000_AgregarLogEventos')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251218000000_AgregarLogEventos', '8.0.0');
    PRINT 'Migracion registrada';
END
