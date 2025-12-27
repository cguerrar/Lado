-- Script para limpiar comillas de RutaArchivo y Thumbnail
-- Datos legacy pueden tener comillas encapsulando las rutas

-- Ver cuántos registros tienen comillas en RutaArchivo
SELECT COUNT(*) as ContenidoConComillas
FROM Contenidos
WHERE RutaArchivo LIKE '"%' OR RutaArchivo LIKE '%"';

-- Ver cuántos registros tienen comillas en Thumbnail
SELECT COUNT(*) as ThumbnailConComillas
FROM Contenidos
WHERE Thumbnail LIKE '"%' OR Thumbnail LIKE '%"';

-- Ver ejemplos de datos afectados
SELECT TOP 10 Id, RutaArchivo, Thumbnail
FROM Contenidos
WHERE RutaArchivo LIKE '"%' OR Thumbnail LIKE '"%';

-- Limpiar comillas de RutaArchivo (descomentar para ejecutar)
-- UPDATE Contenidos
-- SET RutaArchivo = TRIM('"' FROM RutaArchivo)
-- WHERE RutaArchivo LIKE '"%' OR RutaArchivo LIKE '%"';

-- Limpiar comillas de Thumbnail (descomentar para ejecutar)
-- UPDATE Contenidos
-- SET Thumbnail = TRIM('"' FROM Thumbnail)
-- WHERE Thumbnail LIKE '"%' OR Thumbnail LIKE '%"';

-- Verificar después de la limpieza
-- SELECT COUNT(*) as RegistrosRestantes
-- FROM Contenidos
-- WHERE RutaArchivo LIKE '"%' OR Thumbnail LIKE '"%';
