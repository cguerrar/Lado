-- =============================================
-- Script: Agregar Duracion a Suscripciones
-- Fecha: 2025-12-20
-- Descripcion: Agrega columna Duracion para suscripciones temporales (24h, 7 dias, mensual)
-- =============================================

-- Verificar y agregar columna Duracion (1=Dia, 7=Semana, 30=Mes)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Suscripciones') AND name = 'Duracion')
BEGIN
    ALTER TABLE Suscripciones ADD Duracion INT NOT NULL DEFAULT 30;
    PRINT 'Columna Duracion agregada con valor default 30 (mensual)';
END
ELSE
    PRINT 'Columna Duracion ya existe';

GO

-- Actualizar FechaFin para suscripciones existentes que no la tengan
UPDATE Suscripciones
SET FechaFin = DATEADD(MONTH, 1, FechaInicio)
WHERE FechaFin IS NULL AND EstaActiva = 1;

-- Actualizar ProximaRenovacion para suscripciones existentes
UPDATE Suscripciones
SET ProximaRenovacion = DATEADD(MONTH, 1, FechaInicio)
WHERE ProximaRenovacion IS NULL OR ProximaRenovacion = '0001-01-01';

PRINT 'Script ejecutado correctamente';
