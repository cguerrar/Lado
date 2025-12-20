-- Script para agregar el algoritmo "Por Intereses" a la tabla AlgoritmosFeed
-- Ejecutar si los algoritmos ya existen en la base de datos

IF NOT EXISTS (SELECT 1 FROM AlgoritmosFeed WHERE Codigo = 'por_intereses')
BEGIN
    INSERT INTO AlgoritmosFeed (Codigo, Nombre, Descripcion, Icono, Activo, EsPorDefecto, Orden, TotalUsos, FechaCreacion)
    VALUES (
        'por_intereses',
        'Por Intereses',
        'Prioriza contenido basado en tus intereses seleccionados y aprendidos',
        'star',
        1,  -- Activo
        0,  -- No es por defecto
        5,  -- Orden
        0,  -- TotalUsos
        GETDATE()
    );

    PRINT 'Algoritmo "Por Intereses" agregado correctamente';
END
ELSE
BEGIN
    PRINT 'El algoritmo "Por Intereses" ya existe';
END
