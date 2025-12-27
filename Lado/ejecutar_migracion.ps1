# Script para ejecutar la migración de tablas JWT
$connectionString = "Data Source=200.50.127.114;Initial Catalog=Lado;User ID=sa;Password=Password123..**;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True"

$sql = @"
-- Crear tabla RefreshTokens
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

-- Indices para RefreshTokens
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_Token' AND object_id = OBJECT_ID('RefreshTokens'))
BEGIN
    CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID('RefreshTokens'))
BEGIN
    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
END

-- Verificar ActiveTokens
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

-- Indices para ActiveTokens
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveTokens_Jti' AND object_id = OBJECT_ID('ActiveTokens'))
BEGIN
    CREATE UNIQUE INDEX IX_ActiveTokens_Jti ON ActiveTokens(Jti);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ActiveTokens_UserId' AND object_id = OBJECT_ID('ActiveTokens'))
BEGIN
    CREATE INDEX IX_ActiveTokens_UserId ON ActiveTokens(UserId);
END

-- Verificar SecurityVersion
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'SecurityVersion')
BEGIN
    ALTER TABLE AspNetUsers ADD SecurityVersion INT NOT NULL DEFAULT 1;
    PRINT 'Campo SecurityVersion agregado';
END

-- Verificacion
SELECT 'Tablas creadas:' AS Mensaje;
SELECT name FROM sys.tables WHERE name IN ('RefreshTokens', 'ActiveTokens');
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    $command = New-Object System.Data.SqlClient.SqlCommand($sql, $connection)
    $command.CommandTimeout = 120

    $result = $command.ExecuteNonQuery()
    Write-Host "Migracion ejecutada exitosamente" -ForegroundColor Green

    # Verificar tablas
    $checkSql = "SELECT name FROM sys.tables WHERE name IN ('RefreshTokens', 'ActiveTokens')"
    $command.CommandText = $checkSql
    $reader = $command.ExecuteReader()

    Write-Host "`nTablas verificadas:" -ForegroundColor Cyan
    while ($reader.Read()) {
        Write-Host "  - $($reader['name'])" -ForegroundColor Yellow
    }
    $reader.Close()

    $connection.Close()
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}
