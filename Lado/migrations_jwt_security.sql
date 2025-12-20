-- =============================================
-- Migración: Seguridad JWT - Revocación Inmediata de Tokens
-- Fecha: 2024-12-20
-- Descripción: Agrega tabla ActiveTokens y campo SecurityVersion
-- =============================================

-- 1. Agregar campo SecurityVersion a AspNetUsers
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'SecurityVersion')
BEGIN
    ALTER TABLE AspNetUsers ADD SecurityVersion INT NOT NULL DEFAULT 1;
    PRINT 'Campo SecurityVersion agregado a AspNetUsers';
END
ELSE
BEGIN
    PRINT 'Campo SecurityVersion ya existe en AspNetUsers';
END
GO

-- 2. Crear tabla ActiveTokens
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ActiveTokens')
BEGIN
    CREATE TABLE ActiveTokens (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Jti NVARCHAR(100) NOT NULL,
        UserId NVARCHAR(450) NOT NULL,
        ExpiresAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IsRevoked BIT NOT NULL DEFAULT 0,
        DeviceInfo NVARCHAR(500) NULL,
        IpAddress NVARCHAR(50) NULL,

        CONSTRAINT FK_ActiveTokens_Users FOREIGN KEY (UserId)
            REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla ActiveTokens creada';
END
ELSE
BEGIN
    PRINT 'Tabla ActiveTokens ya existe';
END
GO

-- 3. Crear índices para ActiveTokens
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveTokens_Jti' AND object_id = OBJECT_ID('ActiveTokens'))
BEGIN
    CREATE UNIQUE INDEX IX_ActiveTokens_Jti ON ActiveTokens(Jti);
    PRINT 'Índice IX_ActiveTokens_Jti creado';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveTokens_UserId' AND object_id = OBJECT_ID('ActiveTokens'))
BEGIN
    CREATE INDEX IX_ActiveTokens_UserId ON ActiveTokens(UserId);
    PRINT 'Índice IX_ActiveTokens_UserId creado';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveTokens_Cleanup' AND object_id = OBJECT_ID('ActiveTokens'))
BEGIN
    CREATE INDEX IX_ActiveTokens_Cleanup ON ActiveTokens(ExpiresAt, IsRevoked);
    PRINT 'Índice IX_ActiveTokens_Cleanup creado';
END
GO

-- 4. Verificar que la migración se aplicó correctamente
SELECT
    'ActiveTokens' AS Tabla,
    COUNT(*) AS Registros
FROM ActiveTokens
UNION ALL
SELECT
    'AspNetUsers con SecurityVersion',
    COUNT(*)
FROM AspNetUsers
WHERE SecurityVersion IS NOT NULL;

PRINT 'Migración de seguridad JWT completada exitosamente';
GO
