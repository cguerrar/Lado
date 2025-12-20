-- Agregar columna LadoPreferido a la tabla AspNetUsers
-- Ejecutar en la base de datos de producción

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'LadoPreferido')
BEGIN
    ALTER TABLE AspNetUsers ADD LadoPreferido INT NOT NULL DEFAULT 0;
    PRINT 'Columna LadoPreferido agregada exitosamente';
END
ELSE
BEGIN
    PRINT 'La columna LadoPreferido ya existe';
END

-- También registrar la migración en la tabla de EF Core (si usas migraciones)
IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = '20251217222400_AgregarLadoPreferido')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251217222400_AgregarLadoPreferido', '8.0.0');
    PRINT 'Migración registrada en historial';
END
