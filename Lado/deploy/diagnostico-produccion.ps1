# =====================================================
# Diagnóstico de errores en producción - LADO
# Ejecutar como Administrador en el servidor
# =====================================================

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  DIAGNÓSTICO DE PRODUCCIÓN - LADO" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Verificar variables de entorno JWT
Write-Host "`n[1] Variables de entorno JWT:" -ForegroundColor Yellow
$jwtKey = [System.Environment]::GetEnvironmentVariable("Jwt__Key", "Machine")
$jwtIssuer = [System.Environment]::GetEnvironmentVariable("Jwt__Issuer", "Machine")

if ($jwtKey) {
    Write-Host "  Jwt__Key: $($jwtKey.Substring(0, 10))... (OK - $($jwtKey.Length) chars)" -ForegroundColor Green
} else {
    Write-Host "  Jwt__Key: NO CONFIGURADA!" -ForegroundColor Red
}

if ($jwtIssuer) {
    Write-Host "  Jwt__Issuer: $jwtIssuer (OK)" -ForegroundColor Green
} else {
    Write-Host "  Jwt__Issuer: NO CONFIGURADA!" -ForegroundColor Red
}

# 2. Verificar logs de IIS/ASP.NET Core
Write-Host "`n[2] Últimos errores en logs:" -ForegroundColor Yellow
$logPaths = @(
    "C:\inetpub\wwwroot\Lado\logs\stdout*.log",
    "C:\inetpub\logs\LogFiles\W3SVC1\*.log",
    "C:\Lado\logs\*.log"
)

foreach ($path in $logPaths) {
    $files = Get-ChildItem $path -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($files) {
        Write-Host "`n  Archivo: $($files.FullName)" -ForegroundColor Gray
        Write-Host "  Últimas 30 líneas:" -ForegroundColor Gray
        Get-Content $files.FullName -Tail 30 | ForEach-Object { Write-Host "    $_" }
    }
}

# 3. Verificar Event Log
Write-Host "`n[3] Errores en Event Log (últimos 5):" -ForegroundColor Yellow
Get-EventLog -LogName Application -EntryType Error -Newest 5 -Source "*ASP*", "*IIS*", "*.NET*" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  [$($_.TimeGenerated)] $($_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)))..." -ForegroundColor Red
}

# 4. Verificar conexión a BD
Write-Host "`n[4] Probando conexión a BD:" -ForegroundColor Yellow
$connString = [System.Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Machine")
if ($connString) {
    Write-Host "  Connection string encontrada" -ForegroundColor Green
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
        $conn.Open()

        # Verificar tablas JWT
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('ActiveTokens', 'RefreshTokens')"
        $result = $cmd.ExecuteScalar()

        if ($result -eq 2) {
            Write-Host "  Tablas JWT: OK (ActiveTokens y RefreshTokens existen)" -ForegroundColor Green
        } else {
            Write-Host "  Tablas JWT: FALTAN! ($result de 2 tablas)" -ForegroundColor Red
            Write-Host "  Ejecutar el SQL de creación de tablas" -ForegroundColor Yellow
        }

        $conn.Close()
    } catch {
        Write-Host "  ERROR conectando a BD: $_" -ForegroundColor Red
    }
} else {
    Write-Host "  Connection string NO encontrada!" -ForegroundColor Red
}

# 5. Verificar que el sitio responde
Write-Host "`n[5] Probando endpoint de salud:" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://www.ladoapp.com/api/auth/check-email?email=test@test.com" -UseBasicParsing -TimeoutSec 10
    Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $body = $reader.ReadToEnd()
        Write-Host "  Response: $body" -ForegroundColor Red
    }
}

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "  FIN DEL DIAGNÓSTICO" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
