# =====================================================
# Script para configurar IIS en Windows Server
# =====================================================
# Ejecutar como Administrador
# =====================================================

param(
    [string]$SiteName = "Lado",
    [string]$AppPoolName = "LadoAppPool"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Configuracion de IIS para Lado" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Verificar si se ejecuta como admin
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "ERROR: Este script debe ejecutarse como Administrador" -ForegroundColor Red
    exit 1
}

# Importar modulo de IIS
Import-Module WebAdministration -ErrorAction SilentlyContinue

# Variables de entorno para el Application Pool
$envVars = @{
    "ASPNETCORE_ENVIRONMENT" = "Production"

    # Base de datos - CAMBIAR ESTOS VALORES
    "ConnectionStrings__DefaultConnection" = "Data Source=TU_SERVIDOR;Initial Catalog=Lado;User ID=TU_USUARIO;Password=TU_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True"

    # Google OAuth
    "Authentication__Google__ClientId" = "985201886726-um2rscj5od9eanu3neutnmjmfur2ng7p.apps.googleusercontent.com"
    "Authentication__Google__ClientSecret" = "TU_GOOGLE_SECRET"

    # Email SMTP
    "EmailSettings__SmtpServer" = "ladoapp.com"
    "EmailSettings__SmtpPort" = "465"
    "EmailSettings__SmtpUsername" = "noreply@ladoapp.com"
    "EmailSettings__SmtpPassword" = "TU_SMTP_PASSWORD"
    "EmailSettings__FromEmail" = "noreply@ladoapp.com"
    "EmailSettings__FromName" = "Lado"
    "EmailSettings__EnableSsl" = "true"

    # MercadoPago
    "MercadoPago__AccessToken" = "TU_MERCADOPAGO_TOKEN"
    "MercadoPago__PublicKey" = "TU_MERCADOPAGO_PUBLIC_KEY"

    # Claude API
    "Claude__ApiKey" = "TU_CLAUDE_API_KEY"

    # App Settings
    "AppSettings__BaseUrl" = "https://www.ladoapp.com"
    "AppSettings__MaxFileSize" = "104857600"
    "AppSettings__CreatorCommission" = "0.20"
    "AppSettings__TipCommission" = "0.10"
}

Write-Host "`nConfigurando variables de entorno del sistema..." -ForegroundColor Yellow

foreach ($key in $envVars.Keys) {
    [System.Environment]::SetEnvironmentVariable($key, $envVars[$key], [System.EnvironmentVariableTarget]::Machine)
    Write-Host "  + $key" -ForegroundColor Gray
}

Write-Host "`nVariables configuradas como variables de entorno del sistema." -ForegroundColor Green

# Crear archivo web.config con variables
$webConfigPath = "C:\inetpub\wwwroot\$SiteName\web.config"
Write-Host "`nNota: Asegurate de que el archivo web.config tenga:" -ForegroundColor Yellow
Write-Host @"
<aspNetCore processPath="dotnet"
            arguments=".\Lado.dll"
            stdoutLogEnabled="true"
            stdoutLogFile=".\logs\stdout"
            hostingModel="inprocess">
    <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    </environmentVariables>
</aspNetCore>
"@ -ForegroundColor Gray

Write-Host "`nReiniciando IIS..." -ForegroundColor Yellow
iisreset

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  Configuracion completada!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "`nIMPORTANTE: Edita este script con tus valores reales antes de ejecutar." -ForegroundColor Red
