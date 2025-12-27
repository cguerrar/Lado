# Script para registrar la migracion de LogEventos
Add-Type -AssemblyName System.Data

$connectionString = 'Data Source=200.50.127.114;Initial Catalog=Lado;User ID=sa;Password=Password123..**;TrustServerCertificate=True'

$query = @"
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251219015841_AgregarLogEventos')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251219015841_AgregarLogEventos', '8.0.11')
    SELECT 'Migracion registrada exitosamente' as Resultado
END
ELSE
BEGIN
    SELECT 'La migracion ya estaba registrada' as Resultado
END
"@

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection
    $conn.ConnectionString = $connectionString
    $conn.Open()
    Write-Host "Conexion exitosa a la base de datos"

    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
    $result = $cmd.ExecuteScalar()
    Write-Host "Resultado: $result"

    $conn.Close()
    Write-Host "Conexion cerrada"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Inner: $($_.Exception.InnerException.Message)"
}
