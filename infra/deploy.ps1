<#
    One-command Azure deploy for the Sentinel Participant Risk Response App.
    Provisions the App Service + PostgreSQL (infra/main.bicep) and deploys the app.

    Prereqs:
      - Azure CLI installed and signed in (`az login`) with an identity that has
        at least Contributor on the target resource group, AND the
        Microsoft.DBforPostgreSQL provider registered on the subscription.

    Usage:
      ./infra/deploy.ps1 -ResourceGroup rg-riskapp-test
#>
param(
    [Parameter(Mandatory = $true)] [string] $ResourceGroup,
    [string] $Location = 'eastus2',
    [string] $AppServiceSku = 'B1'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> Signed-in context" -ForegroundColor Cyan
az account show --query "{subscription:name, user:user.name}" -o table

# Ensure the PostgreSQL provider is registered (needs subscription-level rights).
$pg = az provider show -n Microsoft.DBforPostgreSQL --query registrationState -o tsv
if ($pg -ne 'Registered') {
    Write-Host "==> Registering Microsoft.DBforPostgreSQL (one-time)..." -ForegroundColor Yellow
    az provider register -n Microsoft.DBforPostgreSQL
    do { Start-Sleep 10; $pg = az provider show -n Microsoft.DBforPostgreSQL --query registrationState -o tsv; Write-Host "    state: $pg" }
    while ($pg -ne 'Registered')
}

# Generate a strong PostgreSQL admin password (kept only in the Web App setting).
$bytes = New-Object byte[] 18
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$pgPassword = ([Convert]::ToBase64String($bytes) -replace '[+/=]', '') + 'Aa1!'

Write-Host "==> Provisioning infrastructure (Bicep)..." -ForegroundColor Cyan
$deploy = az deployment group create `
    -g $ResourceGroup `
    -f "$root/infra/main.bicep" `
    -p appServiceSku=$AppServiceSku pgAdminPassword=$pgPassword `
    -o json | ConvertFrom-Json

$webApp = $deploy.properties.outputs.webAppName.value
$webUrl = $deploy.properties.outputs.webAppUrl.value
Write-Host "    Web App: $webApp" -ForegroundColor Green
Write-Host "    URL:     $webUrl" -ForegroundColor Green

Write-Host "==> Publishing the app..." -ForegroundColor Cyan
$publishDir = Join-Path $root 'publish'
$zipPath = Join-Path $root 'publish.zip'
dotnet publish "$root/src/RiskScoring.Api/RiskScoring.Api.csproj" -c Release -o $publishDir
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

Write-Host "==> Deploying package to App Service..." -ForegroundColor Cyan
az webapp deploy -g $ResourceGroup -n $webApp --src-path $zipPath --type zip

Write-Host ""
Write-Host "Done. App: $webUrl" -ForegroundColor Green
