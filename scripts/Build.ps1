[CmdletBinding()]
param(
    [switch]$Test,
    [switch]$IntegrationTest,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'src\Raudo'
$testsDirectory = Join-Path $projectRoot 'tests\Raudo.Tests'
$artifactsPath = Join-Path $projectRoot 'artifacts'
$assetsPath = Join-Path $projectRoot 'assets'
$iconPath = Join-Path $assetsPath 'Raudo.ico'
$outputPath = Join-Path $artifactsPath 'Raudo.exe'
$testOutputPath = Join-Path $artifactsPath 'Raudo.Tests.exe'
$manifestPath = Join-Path $sourceDirectory 'app.manifest'
$assetGeneratorSource = Join-Path $PSScriptRoot 'GenerateAssets.cs'
$assetGeneratorExe = Join-Path ([IO.Path]::GetTempPath()) "Raudo-GenerateAssets-$PID.exe"

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)

$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'No se encontró el compilador de .NET Framework incluido en Windows.'
}

if ($Clean -and (Test-Path -LiteralPath $artifactsPath)) {
    $resolvedArtifacts = (Resolve-Path -LiteralPath $artifactsPath).Path
    $expectedArtifacts = Join-Path $projectRoot 'artifacts'
    if (-not [string]::Equals($resolvedArtifacts, $expectedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Ruta de artefactos inesperada: $resolvedArtifacts"
    }
    Remove-Item -LiteralPath $resolvedArtifacts -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null
New-Item -ItemType Directory -Path $assetsPath -Force | Out-Null
$windowsContractsPath = & (Join-Path $PSScriptRoot 'RestoreWindowsContracts.ps1') `
    -ArtifactsPath $artifactsPath

try {
    & $compiler `
        /nologo `
        /target:exe `
        /optimize+ `
        /warn:4 `
        /warnaserror+ `
        /reference:System.dll `
        /reference:System.Drawing.dll `
        "/out:$assetGeneratorExe" `
        $assetGeneratorSource

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudo compilar el generador de recursos: $LASTEXITCODE."
    }

    & $assetGeneratorExe $iconPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $iconPath)) {
        throw 'No se pudo generar el icono de Raudo.'
    }
}
finally {
    if (Test-Path -LiteralPath $assetGeneratorExe) {
        Remove-Item -LiteralPath $assetGeneratorExe -Force
    }
}

$sourcePaths = @(
    Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' -File -Recurse |
        Sort-Object FullName |
        Select-Object -ExpandProperty FullName
)
if ($sourcePaths.Count -eq 0) {
    throw "No se encontraron fuentes en $sourceDirectory."
}

$msbuild = Get-Command 'msbuild.exe' -ErrorAction SilentlyContinue
if ($msbuild) {
    $solutionPath = Join-Path $projectRoot 'Raudo.sln'
    & $msbuild.Source `
        $solutionPath `
        /nologo `
        /m `
        /t:Rebuild `
        /p:Configuration=Release `
        '/p:Platform=Any CPU' `
        /p:ContinuousIntegrationBuild=true

    $msbuildOutput = Join-Path $sourceDirectory 'bin\Release\Raudo.exe'
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msbuildOutput)) {
        throw "La compilación con MSBuild falló con código $LASTEXITCODE."
    }
    Copy-Item -LiteralPath $msbuildOutput -Destination $outputPath -Force
}
else {
    $compilerArguments = @(
        '/nologo',
        '/target:winexe',
        '/optimize+',
        '/warn:4',
        '/warnaserror+',
        '/platform:anycpu',
        '/reference:System.dll',
        '/reference:System.Drawing.dll',
        '/reference:System.IO.Compression.dll',
        '/reference:System.IO.Compression.FileSystem.dll',
        '/reference:System.Runtime.dll',
        '/reference:System.Runtime.WindowsRuntime.dll',
        '/reference:System.Windows.Forms.dll',
        '/reference:System.Web.Extensions.dll',
        "/reference:$windowsContractsPath\Windows.WinMD",
        "/reference:$windowsContractsPath\Windows.Foundation.FoundationContract.winmd",
        "/reference:$windowsContractsPath\Windows.Foundation.UniversalApiContract.winmd",
        "/win32icon:$iconPath",
        "/win32manifest:$manifestPath",
        "/out:$outputPath"
    ) + $sourcePaths

    & $compiler $compilerArguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
        throw "La compilación falló con código $LASTEXITCODE."
    }
}

$testSource = Join-Path $testsDirectory 'TestRunner.cs'
if (Test-Path -LiteralPath $testSource) {
    & $compiler `
        /nologo `
        /target:exe `
        /optimize+ `
        /warn:4 `
        /warnaserror+ `
        /reference:System.dll `
        /reference:System.Drawing.dll `
        /reference:System.IO.Compression.dll `
        /reference:System.IO.Compression.FileSystem.dll `
        /reference:System.Runtime.dll `
        /reference:System.Runtime.WindowsRuntime.dll `
        /reference:System.Windows.Forms.dll `
        /reference:System.Web.Extensions.dll `
        "/reference:$windowsContractsPath\Windows.WinMD" `
        "/reference:$windowsContractsPath\Windows.Foundation.FoundationContract.winmd" `
        "/reference:$windowsContractsPath\Windows.Foundation.UniversalApiContract.winmd" `
        "/reference:$outputPath" `
        "/out:$testOutputPath" `
        $testSource

    if ($LASTEXITCODE -ne 0) {
        throw "No se pudieron compilar las pruebas: $LASTEXITCODE."
    }
}

if ($Test) {
    & $testOutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Las pruebas fallaron con código $LASTEXITCODE."
    }
}

if ($IntegrationTest) {
    & $testOutputPath --integration
    if ($LASTEXITCODE -ne 0) {
        throw "Las pruebas de integración fallaron con código $LASTEXITCODE."
    }
}

$file = Get-Item -LiteralPath $outputPath
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath
Write-Output "Compilado: $($file.FullName) ($([math]::Round($file.Length / 1KB, 1)) KB)"
Write-Output "SHA256: $($hash.Hash)"
