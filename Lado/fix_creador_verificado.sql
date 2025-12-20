-- PASO 1: Revertir TODOS los usuarios
UPDATE AspNetUsers SET CreadorVerificado = 0

-- PASO 2: Habilitar SOLO los usuarios que deben ser LadoB
-- Reemplaza 'ID-AQUI' con el ID real del usuario
-- UPDATE AspNetUsers SET CreadorVerificado = 1, EsCreador = 1 WHERE Id = 'ID-AQUI'
