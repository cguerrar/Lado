-- Script para registrar migraciones pendientes
-- Ejecutar en SQL Server Management Studio o Azure Data Studio

-- 1. Registrar migración de LogEventos (la tabla ya existe)
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251219015841_AgregarLogEventos')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251219015841_AgregarLogEventos', '8.0.11')
    PRINT 'Migración AgregarLogEventos registrada'
END
ELSE
BEGIN
    PRINT 'Migración AgregarLogEventos ya estaba registrada'
END
GO

-- 2. Verificar migraciones actuales
SELECT * FROM [__EFMigrationsHistory] ORDER BY [MigrationId]
GO
