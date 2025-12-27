-- Script para arreglar el estado en linea de usuarios
-- La migracion creo la columna con defaultValue: false, pero deberia ser true

-- Actualizar todos los usuarios para mostrar estado en linea por defecto
UPDATE AspNetUsers SET MostrarEstadoEnLinea = 1;

-- Verificar
SELECT COUNT(*) AS 'Total usuarios',
       SUM(CASE WHEN MostrarEstadoEnLinea = 1 THEN 1 ELSE 0 END) AS 'Con estado visible'
FROM AspNetUsers;
