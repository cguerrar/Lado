-- Script para limpiar categorías y que se creen desde cero con IA
-- Ejecutar en la base de datos de Lado

-- 1. Primero quitar referencias de contenidos a categorías
UPDATE Contenidos SET CategoriaInteresId = NULL;

-- 2. Eliminar intereses de usuarios (tabla intermedia)
DELETE FROM InteresesUsuarios;

-- 3. Eliminar todas las categorías (subcategorías primero por FK)
DELETE FROM CategoriasIntereses WHERE CategoriaPadreId IS NOT NULL;
DELETE FROM CategoriasIntereses;

-- Verificar que quedó limpio
SELECT COUNT(*) as 'Categorias restantes' FROM CategoriasIntereses;
SELECT COUNT(*) as 'Contenidos sin categoria' FROM Contenidos WHERE CategoriaInteresId IS NULL;

PRINT 'Categorías eliminadas. Se crearán automáticamente al subir nuevo contenido.';
