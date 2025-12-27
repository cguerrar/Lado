-- =============================================
-- Script: Agregar Sistema de Confianza
-- Fecha: 2025-12-20
-- Descripción: Agrega campos para el sistema de confianza visible
-- =============================================

-- Verificar y agregar columna UltimaActividad
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'UltimaActividad')
BEGIN
    ALTER TABLE AspNetUsers ADD UltimaActividad DATETIME2 NULL;
    PRINT 'Columna UltimaActividad agregada';
END
ELSE
    PRINT 'Columna UltimaActividad ya existe';

-- Verificar y agregar columna MensajesRecibidosTotal
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'MensajesRecibidosTotal')
BEGIN
    ALTER TABLE AspNetUsers ADD MensajesRecibidosTotal INT NOT NULL DEFAULT 0;
    PRINT 'Columna MensajesRecibidosTotal agregada';
END
ELSE
    PRINT 'Columna MensajesRecibidosTotal ya existe';

-- Verificar y agregar columna MensajesRespondidosTotal
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'MensajesRespondidosTotal')
BEGIN
    ALTER TABLE AspNetUsers ADD MensajesRespondidosTotal INT NOT NULL DEFAULT 0;
    PRINT 'Columna MensajesRespondidosTotal agregada';
END
ELSE
    PRINT 'Columna MensajesRespondidosTotal ya existe';

-- Verificar y agregar columna TiempoPromedioRespuesta
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'TiempoPromedioRespuesta')
BEGIN
    ALTER TABLE AspNetUsers ADD TiempoPromedioRespuesta INT NULL;
    PRINT 'Columna TiempoPromedioRespuesta agregada';
END
ELSE
    PRINT 'Columna TiempoPromedioRespuesta ya existe';

-- Verificar y agregar columna ReportesRecibidos
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'ReportesRecibidos')
BEGIN
    ALTER TABLE AspNetUsers ADD ReportesRecibidos INT NOT NULL DEFAULT 0;
    PRINT 'Columna ReportesRecibidos agregada';
END
ELSE
    PRINT 'Columna ReportesRecibidos ya existe';

-- Verificar y agregar columna ContenidosPublicados
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'AspNetUsers') AND name = 'ContenidosPublicados')
BEGIN
    ALTER TABLE AspNetUsers ADD ContenidosPublicados INT NOT NULL DEFAULT 0;
    PRINT 'Columna ContenidosPublicados agregada';
END
ELSE
    PRINT 'Columna ContenidosPublicados ya existe';

GO

-- =============================================
-- Actualizar datos existentes
-- =============================================

-- Actualizar UltimaActividad para usuarios activos (basado en último contenido)
UPDATE u
SET u.UltimaActividad = (
    SELECT MAX(c.FechaPublicacion)
    FROM Contenidos c
    WHERE c.UsuarioId = u.Id AND c.EstaActivo = 1
)
FROM AspNetUsers u
WHERE u.UltimaActividad IS NULL;

-- Actualizar ContenidosPublicados contando contenidos activos
UPDATE u
SET u.ContenidosPublicados = (
    SELECT COUNT(*)
    FROM Contenidos c
    WHERE c.UsuarioId = u.Id AND c.EstaActivo = 1 AND c.EsBorrador = 0
)
FROM AspNetUsers u;

-- Actualizar MensajesRecibidosTotal (mensajes donde el usuario es destinatario)
UPDATE u
SET u.MensajesRecibidosTotal = (
    SELECT COUNT(*)
    FROM ChatMensajes m
    WHERE m.DestinatarioId = u.Id
)
FROM AspNetUsers u
WHERE u.EsCreador = 1;

-- Actualizar MensajesRespondidosTotal (mensajes enviados por el creador en respuesta)
UPDATE u
SET u.MensajesRespondidosTotal = (
    SELECT COUNT(*)
    FROM ChatMensajes m
    WHERE m.RemitenteId = u.Id
)
FROM AspNetUsers u
WHERE u.EsCreador = 1;

PRINT 'Script ejecutado correctamente';
