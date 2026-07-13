[CmdletBinding()]
param(
    [switch]$PurgeSettings
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$installDirectory = Join-Path $localAppData 'Programs\Raudo'
$settingsDirectory = Join-Path $localAppData 'Raudo'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Raudo.lnk'
$startMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Raudo.lnk'

if (Get-Process -Name 'Raudo' -ErrorAction SilentlyContinue) {
    throw 'Cierra Raudo desde el icono junto al reloj antes de desinstalarlo.'
}

foreach ($shortcut in @($desktopShortcut, $startMenuShortcut)) {
    if (Test-Path -LiteralPath $shortcut) {
        Remove-Item -LiteralPath $shortcut -Force
    }
}

if (Test-Path -LiteralPath $installDirectory) {
    $resolvedInstall = (Resolve-Path -LiteralPath $installDirectory).Path
    $expectedInstall = Join-Path $localAppData 'Programs\Raudo'
    if (-not [string]::Equals($resolvedInstall, $expectedInstall, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta de instalación inesperada: $resolvedInstall"
    }
    Remove-Item -LiteralPath $resolvedInstall -Recurse -Force
}

if ($PurgeSettings -and (Test-Path -LiteralPath $settingsDirectory)) {
    $resolvedSettings = (Resolve-Path -LiteralPath $settingsDirectory).Path
    $expectedSettings = Join-Path $localAppData 'Raudo'
    if (-not [string]::Equals($resolvedSettings, $expectedSettings, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta de configuración inesperada: $resolvedSettings"
    }
    Remove-Item -LiteralPath $resolvedSettings -Recurse -Force
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'Raudo' -ErrorAction SilentlyContinue
Write-Output 'Raudo fue desinstalado.'
