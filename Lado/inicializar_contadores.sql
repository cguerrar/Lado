-- Script para inicializar ContadorIngresos de usuarios existentes
-- Ejecutar una sola vez después de agregar el campo

-- Poner contador en 1 para usuarios que ya tienen actividad registrada
UPDATE AspNetUsers
SET ContadorIngresos = 1
WHERE UltimaActividad IS NOT NULL
  AND ContadorIngresos = 0;

-- Mostrar resultado
SELECT
    COUNT(*) as TotalUsuarios,
    SUM(CASE WHEN ContadorIngresos > 0 THEN 1 ELSE 0 END) as ConContador,
    SUM(CASE WHEN ContadorIngresos = 0 THEN 1 ELSE 0 END) as SinContador
FROM AspNetUsers;
