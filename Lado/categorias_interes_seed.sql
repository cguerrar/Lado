-- Script para insertar categorias de interes predeterminadas
-- Ejecutar una vez para poblar la tabla CategoriasIntereses

-- Primero verificar si ya existen categorias
IF NOT EXISTS (SELECT 1 FROM CategoriasIntereses WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT CategoriasIntereses ON;

    -- Categorias principales
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (1, 'Entretenimiento', 'Contenido de entretenimiento general', 'bi-film', '#FF6B6B', NULL, 1, 1),
    (2, 'Musica', 'Artistas, covers, producciones musicales', 'bi-music-note-beamed', '#4ECDC4', NULL, 2, 1),
    (3, 'Fitness', 'Ejercicio, rutinas, vida saludable', 'bi-heart-pulse', '#45B7D1', NULL, 3, 1),
    (4, 'Moda', 'Estilo, tendencias, outfits', 'bi-bag-heart', '#F7DC6F', NULL, 4, 1),
    (5, 'Belleza', 'Maquillaje, skincare, cuidado personal', 'bi-stars', '#BB8FCE', NULL, 5, 1),
    (6, 'Cocina', 'Recetas, gastronomia, tips culinarios', 'bi-egg-fried', '#F39C12', NULL, 6, 1),
    (7, 'Viajes', 'Destinos, aventuras, experiencias', 'bi-airplane', '#1ABC9C', NULL, 7, 1),
    (8, 'Gaming', 'Videojuegos, streams, esports', 'bi-controller', '#9B59B6', NULL, 8, 1),
    (9, 'Arte', 'Dibujo, pintura, creatividad', 'bi-palette', '#E74C3C', NULL, 9, 1),
    (10, 'Educacion', 'Tutoriales, cursos, aprendizaje', 'bi-book', '#3498DB', NULL, 10, 1),
    (11, 'Comedia', 'Humor, sketches, entretenimiento', 'bi-emoji-laughing', '#F1C40F', NULL, 11, 1),
    (12, 'Lifestyle', 'Dia a dia, vlogs, estilo de vida', 'bi-house-heart', '#E91E63', NULL, 12, 1),
    (13, 'Tecnologia', 'Gadgets, apps, innovacion', 'bi-cpu', '#607D8B', NULL, 13, 1),
    (14, 'Deportes', 'Futbol, basquet, atletismo', 'bi-trophy', '#27AE60', NULL, 14, 1),
    (15, 'Mascotas', 'Perros, gatos, animales', 'bi-heart', '#FF9800', NULL, 15, 1);

    -- Subcategorias de Entretenimiento
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (101, 'Cine', 'Peliculas, reviews, trailers', 'bi-camera-reels', '#FF6B6B', 1, 1, 1),
    (102, 'Series', 'Series de TV, streaming', 'bi-tv', '#FF6B6B', 1, 2, 1),
    (103, 'Reality', 'Reality shows, drama', 'bi-broadcast', '#FF6B6B', 1, 3, 1);

    -- Subcategorias de Musica
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (201, 'Pop', 'Musica pop', 'bi-music-note', '#4ECDC4', 2, 1, 1),
    (202, 'Urbano', 'Reggaeton, trap, hip-hop', 'bi-boombox', '#4ECDC4', 2, 2, 1),
    (203, 'Rock', 'Rock, metal, alternativo', 'bi-lightning', '#4ECDC4', 2, 3, 1),
    (204, 'Electronica', 'EDM, house, techno', 'bi-soundwave', '#4ECDC4', 2, 4, 1);

    -- Subcategorias de Fitness
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (301, 'Gym', 'Entrenamiento en gimnasio', 'bi-person-arms-up', '#45B7D1', 3, 1, 1),
    (302, 'Yoga', 'Yoga, meditacion, bienestar', 'bi-peace', '#45B7D1', 3, 2, 1),
    (303, 'Cardio', 'Running, HIIT, ciclismo', 'bi-bicycle', '#45B7D1', 3, 3, 1),
    (304, 'Nutricion', 'Dietas, alimentacion saludable', 'bi-apple', '#45B7D1', 3, 4, 1);

    -- Subcategorias de Moda
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (401, 'Streetwear', 'Moda urbana, casual', 'bi-handbag', '#F7DC6F', 4, 1, 1),
    (402, 'Luxury', 'Alta moda, marcas premium', 'bi-gem', '#F7DC6F', 4, 2, 1),
    (403, 'Vintage', 'Moda retro, segunda mano', 'bi-clock-history', '#F7DC6F', 4, 3, 1);

    -- Subcategorias de Gaming
    INSERT INTO CategoriasIntereses (Id, Nombre, Descripcion, Icono, Color, CategoriaPadreId, Orden, EstaActiva) VALUES
    (801, 'FPS', 'Shooters, Call of Duty, Fortnite', 'bi-crosshair', '#9B59B6', 8, 1, 1),
    (802, 'RPG', 'Juegos de rol, aventura', 'bi-shield-shaded', '#9B59B6', 8, 2, 1),
    (803, 'Deportivos', 'FIFA, NBA, simuladores', 'bi-joystick', '#9B59B6', 8, 3, 1),
    (804, 'Mobile', 'Juegos moviles', 'bi-phone', '#9B59B6', 8, 4, 1);

    SET IDENTITY_INSERT CategoriasIntereses OFF;

    PRINT 'Categorias de interes insertadas correctamente';
END
ELSE
BEGIN
    PRINT 'Las categorias ya existen, no se insertaron nuevos registros';
END
