# =====================================================
# Script para configurar Azure App Service
# =====================================================
# Ejecutar: .\azure-app-settings.ps1 -ResourceGroup "tu-rg" -AppName "lado-app"
# =====================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$AppName
)

Write-Host "Configurando variables de entorno para Azure App Service..." -ForegroundColor Cyan

# Solicitar valores sensibles
$dbPassword = Read-Host "Password de Base de Datos" -AsSecureString
$dbPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($dbPassword))

$smtpPassword = Read-Host "Password SMTP" -AsSecureString
$smtpPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($smtpPassword))

$googleClientSecret = Read-Host "Google Client Secret" -AsSecureString
$googleClientSecretPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($googleClientSecret))

$claudeApiKey = Read-Host "Claude API Key" -AsSecureString
$claudeApiKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($claudeApiKey))

$mercadoPagoToken = Read-Host "MercadoPago Access Token" -AsSecureString
$mercadoPagoTokenPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($mercadoPagoToken))

# Configurar App Settings
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $AppName `
    --settings `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "ConnectionStrings__DefaultConnection=Data Source=tu-servidor.database.windows.net;Initial Catalog=Lado;User ID=lado-admin;Password=$dbPasswordPlain;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=True" `
        "Authentication__Google__ClientId=985201886726-um2rscj5od9eanu3neutnmjmfur2ng7p.apps.googleusercontent.com" `
        "Authentication__Google__ClientSecret=$googleClientSecretPlain" `
        "EmailSettings__SmtpServer=ladoapp.com" `
        "EmailSettings__SmtpPort=465" `
        "EmailSettings__SmtpUsername=noreply@ladoapp.com" `
        "EmailSettings__SmtpPassword=$smtpPasswordPlain" `
        "EmailSettings__FromEmail=noreply@ladoapp.com" `
        "EmailSettings__FromName=Lado" `
        "EmailSettings__EnableSsl=true" `
        "MercadoPago__AccessToken=$mercadoPagoTokenPlain" `
        "MercadoPago__PublicKey=TU_PUBLIC_KEY" `
        "Claude__ApiKey=$claudeApiKeyPlain" `
        "AppSettings__BaseUrl=https://www.ladoapp.com" `
        "AppSettings__MaxFileSize=104857600" `
        "AppSettings__CreatorCommission=0.20" `
        "AppSettings__TipCommission=0.10"

Write-Host "Variables configuradas exitosamente!" -ForegroundColor Green
