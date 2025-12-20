-- Script para registrar la migración de notificaciones si la tabla ya existe
-- Ejecutar este script si la migración falla porque la tabla ya existe

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251216175610_AgregarNotificaciones')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251216175610_AgregarNotificaciones', '8.0.0');
    PRINT 'Migración registrada correctamente';
END
ELSE
BEGIN
    PRINT 'La migración ya está registrada';
END
