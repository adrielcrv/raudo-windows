[CmdletBinding()]
param(
    [string]$Version = '1.3.0',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versión no válida: $Version"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactsPath = Join-Path $projectRoot 'artifacts'
$sourceExe = Join-Path $artifactsPath 'Raudo.exe'
$stagingPath = Join-Path $artifactsPath 'package'
$zipPath = Join-Path $artifactsPath "Raudo-v$Version-win.zip"
$zipHashPath = "$zipPath.sha256"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -Test
}

if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "No existe el ejecutable esperado: $sourceExe"
}

$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($sourceExe).Version.ToString(3)
if (-not [string]::Equals($assemblyVersion, $Version, [StringComparison]::Ordinal)) {
    throw "La versión del ejecutable ($assemblyVersion) no coincide con el paquete solicitado ($Version)."
}

if (Test-Path -LiteralPath $stagingPath) {
    $resolvedStaging = (Resolve-Path -LiteralPath $stagingPath).Path
    $expectedStaging = Join-Path $artifactsPath 'package'
    if (-not [string]::Equals($resolvedStaging, $expectedStaging, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta temporal inesperada: $resolvedStaging"
    }
    Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination (Join-Path $stagingPath 'Raudo.exe')
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\README.txt') -Destination (Join-Path $stagingPath 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $stagingPath 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $stagingPath 'THIRD-PARTY-NOTICES.txt')

$exeHash = Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $stagingPath 'Raudo.exe')
Set-Content -LiteralPath (Join-Path $stagingPath 'SHA256SUMS.txt') -Encoding ascii -Value (
    "$($exeHash.Hash.ToLowerInvariant())  Raudo.exe")

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
Set-Content -LiteralPath $zipHashPath -Encoding ascii -Value (
    "$($zipHash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)")

Remove-Item -LiteralPath $stagingPath -Recurse -Force
Write-Output "Paquete: $zipPath"
Write-Output "SHA256: $($zipHash.Hash)"
