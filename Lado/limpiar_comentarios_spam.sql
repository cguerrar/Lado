-- =============================================
-- SCRIPT PARA LIMPIAR COMENTARIOS SPAM
-- =============================================

-- 1. Ver el post afectado y cuántos comentarios tiene
SELECT
    c.ContenidoId,
    co.Descripcion,
    COUNT(*) as TotalComentarios,
    MIN(c.FechaCreacion) as PrimerComentario,
    MAX(c.FechaCreacion) as UltimoComentario
FROM Comentarios c
JOIN Contenidos co ON c.ContenidoId = co.Id
GROUP BY c.ContenidoId, co.Descripcion
HAVING COUNT(*) > 100
ORDER BY TotalComentarios DESC;

-- 2. Ver los comentarios sospechosos (muchos del mismo usuario o IP en poco tiempo)
SELECT TOP 100
    c.Id,
    c.ContenidoId,
    c.UsuarioId,
    u.UserName,
    LEFT(c.Texto, 50) as TextoPreview,
    c.FechaCreacion
FROM Comentarios c
JOIN AspNetUsers u ON c.UsuarioId = u.Id
WHERE c.ContenidoId = (
    SELECT TOP 1 ContenidoId
    FROM Comentarios
    GROUP BY ContenidoId
    HAVING COUNT(*) > 100
    ORDER BY COUNT(*) DESC
)
ORDER BY c.FechaCreacion DESC;

-- 3. Contar comentarios por usuario en el post afectado
SELECT
    c.UsuarioId,
    u.UserName,
    COUNT(*) as NumComentarios
FROM Comentarios c
JOIN AspNetUsers u ON c.UsuarioId = u.Id
WHERE c.ContenidoId = (
    SELECT TOP 1 ContenidoId
    FROM Comentarios
    GROUP BY ContenidoId
    HAVING COUNT(*) > 100
    ORDER BY COUNT(*) DESC
)
GROUP BY c.UsuarioId, u.UserName
ORDER BY NumComentarios DESC;

-- =============================================
-- EJECUTAR LIMPIEZA (DESCOMENTAR CUANDO ESTÉS SEGURO)
-- =============================================

-- Opción A: Eliminar comentarios de un usuario específico en un post específico
-- DECLARE @ContenidoId INT = ???;  -- Reemplazar con el ID del contenido
-- DECLARE @UsuarioSpammer NVARCHAR(450) = '???';  -- Reemplazar con el ID del usuario

-- DELETE FROM Comentarios
-- WHERE ContenidoId = @ContenidoId
--   AND UsuarioId = @UsuarioSpammer;

-- Opción B: Eliminar todos excepto los primeros 10 comentarios de cada usuario en ese post
-- DECLARE @ContenidoId INT = ???;

-- WITH ComentariosRanked AS (
--     SELECT
--         Id,
--         ROW_NUMBER() OVER (PARTITION BY UsuarioId ORDER BY FechaCreacion) as RowNum
--     FROM Comentarios
--     WHERE ContenidoId = @ContenidoId
-- )
-- DELETE FROM Comentarios
-- WHERE Id IN (
--     SELECT Id FROM ComentariosRanked WHERE RowNum > 10
-- );

-- Opción C: Eliminar todos los comentarios creados en un rango de tiempo sospechoso
-- DECLARE @ContenidoId INT = ???;
-- DECLARE @FechaInicio DATETIME = '2025-12-20 10:00:00';
-- DECLARE @FechaFin DATETIME = '2025-12-20 11:00:00';

-- DELETE FROM Comentarios
-- WHERE ContenidoId = @ContenidoId
--   AND FechaCreacion BETWEEN @FechaInicio AND @FechaFin;

-- =============================================
-- ACTUALIZAR CONTADOR DE COMENTARIOS
-- =============================================

-- Después de limpiar, actualizar el contador:
-- UPDATE Contenidos
-- SET NumeroComentarios = (
--     SELECT COUNT(*) FROM Comentarios WHERE ContenidoId = Contenidos.Id AND EstaActivo = 1
-- )
-- WHERE Id = @ContenidoId;

-- =============================================
-- BLOQUEAR AL USUARIO ATACANTE
-- =============================================

-- Si identificas al atacante, bloquearlo:
-- UPDATE AspNetUsers
-- SET EstaActivo = 0
-- WHERE Id = @UsuarioSpammer;

-- O agregar su IP a la lista de bloqueados (si tienes la IP):
-- INSERT INTO IpsBloqueadas (DireccionIp, Razon, TipoBloqueo, TipoAtaque, EstaActivo, FechaCreacion)
-- VALUES ('X.X.X.X', 'Inyección de 10000 comentarios', 1, 5, 1, GETDATE());
