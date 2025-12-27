-- =============================================
-- Script: Agregar Comentarios en Hilo
-- Fecha: 2025-12-20
-- Descripcion: Agrega columna ComentarioPadreId para respuestas anidadas
-- =============================================

-- Verificar y agregar columna ComentarioPadreId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Comentarios') AND name = 'ComentarioPadreId')
BEGIN
    ALTER TABLE Comentarios ADD ComentarioPadreId INT NULL;
    PRINT 'Columna ComentarioPadreId agregada';
END
ELSE
    PRINT 'Columna ComentarioPadreId ya existe';

GO

-- Agregar Foreign Key si no existe
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Comentarios_Comentarios_ComentarioPadreId')
BEGIN
    ALTER TABLE Comentarios
    ADD CONSTRAINT FK_Comentarios_Comentarios_ComentarioPadreId
    FOREIGN KEY (ComentarioPadreId) REFERENCES Comentarios(Id);
    PRINT 'Foreign Key agregada';
END
ELSE
    PRINT 'Foreign Key ya existe';

GO

-- Crear indice si no existe
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Comentarios_ComentarioPadreId')
BEGIN
    CREATE INDEX IX_Comentarios_ComentarioPadreId ON Comentarios(ComentarioPadreId);
    PRINT 'Indice IX_Comentarios_ComentarioPadreId creado';
END
ELSE
    PRINT 'Indice ya existe';

PRINT 'Script ejecutado correctamente';
