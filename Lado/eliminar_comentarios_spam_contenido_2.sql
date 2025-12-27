-- =============================================
-- ELIMINACIÓN PERMANENTE DE SPAM EN CONTENIDO ID = 2
-- =============================================

-- 1. Ver cuántos comentarios hay y quiénes comentaron
SELECT
    c.UsuarioId,
    u.UserName,
    COUNT(*) as NumComentarios,
    MIN(c.FechaCreacion) as Primero,
    MAX(c.FechaCreacion) as Ultimo
FROM Comentarios c
JOIN AspNetUsers u ON c.UsuarioId = u.Id
WHERE c.ContenidoId = 2
GROUP BY c.UsuarioId, u.UserName
ORDER BY NumComentarios DESC;

-- 2. Total de comentarios
SELECT COUNT(*) as TotalComentarios
FROM Comentarios
WHERE ContenidoId = 2;

-- =============================================
-- EJECUTAR ELIMINACIÓN (mantiene 3 por usuario)
-- =============================================

-- Eliminar permanentemente comentarios excesivos (mantener solo los primeros 3 de cada usuario)
WITH ComentariosRanked AS (
    SELECT
        Id,
        UsuarioId,
        ROW_NUMBER() OVER (PARTITION BY UsuarioId ORDER BY FechaCreacion) as RowNum
    FROM Comentarios
    WHERE ContenidoId = 2
)
DELETE FROM Comentarios
WHERE Id IN (
    SELECT Id FROM ComentariosRanked WHERE RowNum > 3
);

-- Actualizar el contador del contenido
UPDATE Contenidos
SET NumeroComentarios = (
    SELECT COUNT(*) FROM Comentarios WHERE ContenidoId = 2
)
WHERE Id = 2;

-- Verificar resultado
SELECT
    (SELECT COUNT(*) FROM Comentarios WHERE ContenidoId = 2) as ComentariosRestantes,
    (SELECT NumeroComentarios FROM Contenidos WHERE Id = 2) as ContadorActualizado;

-- =============================================
-- OPCIONAL: Bloquear usuario spammer
-- =============================================
-- Si identificas al atacante (el que tenía miles de comentarios):
-- UPDATE AspNetUsers SET EstaActivo = 0 WHERE Id = 'ID_DEL_SPAMMER';
