[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$NoDesktopShortcut,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot 'Build.ps1'
$sourceExe = Join-Path $projectRoot 'artifacts\Raudo.exe'
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$installDirectory = Join-Path $localAppData 'Programs\Raudo'
$installedExe = Join-Path $installDirectory 'Raudo.exe'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Raudo.lnk'
$programsDirectory = [Environment]::GetFolderPath('Programs')
$startMenuShortcut = Join-Path $programsDirectory 'Raudo.lnk'

if (Get-Process -Name 'Raudo' -ErrorAction SilentlyContinue) {
    throw 'Cierra Raudo desde el icono junto al reloj antes de actualizarlo.'
}

if (-not $SkipBuild) {
    & $buildScript -Test
}

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "No existe el ejecutable esperado: $sourceExe"
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force

$shell = New-Object -ComObject WScript.Shell
$startShortcut = $shell.CreateShortcut($startMenuShortcut)
$startShortcut.TargetPath = $installedExe
$startShortcut.WorkingDirectory = $installDirectory
$startShortcut.Description = 'Utilidades locales y ligeras para Windows'
$startShortcut.IconLocation = "$installedExe,0"
$startShortcut.Save()

if (-not $NoDesktopShortcut) {
    $shortcut = $shell.CreateShortcut($desktopShortcut)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.Description = 'Utilidades locales y ligeras para Windows'
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Save()
}

Write-Output "Instalado: $installedExe"
Write-Output "Acceso directo: $startMenuShortcut"

if (-not $NoLaunch) {
    Start-Process -FilePath $installedExe
}
