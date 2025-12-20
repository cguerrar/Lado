-- =============================================
-- Migración: Crear tabla RefreshTokens
-- Fecha: 2024-12-20
-- Descripción: Crea la tabla para almacenar refresh tokens JWT
-- =============================================

-- 1. Crear tabla RefreshTokens
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RefreshTokens')
BEGIN
    CREATE TABLE RefreshTokens (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Token NVARCHAR(500) NOT NULL,
        UserId NVARCHAR(450) NOT NULL,
        ExpiryDate DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IsRevoked BIT NOT NULL DEFAULT 0,
        DeviceInfo NVARCHAR(500) NULL,
        IpAddress NVARCHAR(50) NULL,

        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
            REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla RefreshTokens creada';
END
ELSE
BEGIN
    PRINT 'Tabla RefreshTokens ya existe';
END
GO

-- 2. Crear índices para RefreshTokens
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Token' AND object_id = OBJECT_ID('RefreshTokens'))
BEGIN
    CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
    PRINT 'Índice IX_RefreshTokens_Token creado';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID('RefreshTokens'))
BEGIN
    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
    PRINT 'Índice IX_RefreshTokens_UserId creado';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_User_Active' AND object_id = OBJECT_ID('RefreshTokens'))
BEGIN
    CREATE INDEX IX_RefreshTokens_User_Active ON RefreshTokens(UserId, IsRevoked, ExpiryDate);
    PRINT 'Índice IX_RefreshTokens_User_Active creado';
END
GO

-- 3. Verificar tabla ActiveTokens también existe
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

-- 4. Crear índices para ActiveTokens
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

-- 5. Verificar que SecurityVersion existe en AspNetUsers
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

-- 6. Verificación final
PRINT '========================================';
PRINT 'VERIFICACIÓN DE TABLAS Y COLUMNAS:';
PRINT '========================================';

SELECT 'RefreshTokens' AS Tabla, COUNT(*) AS Registros FROM RefreshTokens;
SELECT 'ActiveTokens' AS Tabla, COUNT(*) AS Registros FROM ActiveTokens;
SELECT 'AspNetUsers con SecurityVersion' AS Verificacion,
       COUNT(*) AS TotalUsuarios
FROM AspNetUsers WHERE SecurityVersion IS NOT NULL;

PRINT 'Migración completada exitosamente';
GO
