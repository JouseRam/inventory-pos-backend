# Publishes the central web (Pos:Role=Central) for IIS hosting such as somee.com.
#
# The app is self-contained (it carries the .NET 8 runtime), so the host only needs the ASP.NET Core IIS module.
# The server's web.config holds the secrets and is generated separately: upload it on the FIRST deployment only,
# and never overwrite it afterwards (the app folder is replaced, the server's web.config stays).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File build\publish-web.ps1 [-ConnectionString "<SQL Server>"] [-RedirectToHttps]
param(
    [string]$Output = (Join-Path $PSScriptRoot "..\artifacts\web"),
    [string]$ConnectionString = "",
    [switch]$RedirectToHttps
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\InventoryPOS.API\InventoryPOS.API.csproj"

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }

dotnet publish $project -c Release -r win-x64 --self-contained true -o $Output -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Development settings never ship; the publish's own web.config is replaced by the server one below.
Get-ChildItem $Output -Filter "appsettings.Development.json" | Remove-Item -Force
Remove-Item (Join-Path $Output "web.config") -Force -ErrorAction SilentlyContinue

# ── Server web.config (first deployment only) ──
$jwtBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($jwtBytes)
$jwtKey = [Convert]::ToBase64String($jwtBytes)
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { $ConnectionString = "PEGA_AQUI_LA_CADENA_DE_SQL_SERVER_DE_SOMEE" }
$redirect = if ($RedirectToHttps) { "true" } else { "false" }

function Escape([string]$value) { [System.Security.SecurityElement]::Escape($value) }

$webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath=".\InventoryPOS.API.exe" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
          <environmentVariable name="Pos__Role" value="Central" />
          <environmentVariable name="Database__Provider" value="SqlServer" />
          <environmentVariable name="Database__AutoMigrate" value="true" />
          <environmentVariable name="ConnectionStrings__DefaultConnection" value="$(Escape $ConnectionString)" />
          <environmentVariable name="Jwt__Key" value="$jwtKey" />
          <environmentVariable name="Http__RedirectToHttps" value="$redirect" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
$serverConfig = Join-Path (Split-Path $Output -Parent) "web.config.server"
Set-Content -Path $serverConfig -Value $webConfig -Encoding UTF8

$size = "{0:N0} MB" -f ((Get-ChildItem $Output -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Write-Host ""
Write-Host "Listo: $Output ($size)"
Write-Host "web.config del servidor: $serverConfig  (contiene secretos; NO se versiona; subir solo la primera vez como web.config)"
