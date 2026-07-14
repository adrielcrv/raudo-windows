[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packageVersion = '10.0.19041.2'
$packageHash = '379B663EA3EBAA7DF78B2B2666190BBBABE5E11B221428C503C49C09310C7F78'
$packageName = "microsoft.windows.sdk.contracts.$packageVersion.nupkg"
$packageUri = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.contracts/$packageVersion/$packageName"
$dependencyRoot = Join-Path $ArtifactsPath "dependencies\Microsoft.Windows.SDK.Contracts\$packageVersion"
$packagePath = Join-Path $dependencyRoot $packageName
$referencePath = Join-Path $dependencyRoot 'ref'

New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $packagePath)) {
    Invoke-WebRequest -Uri $packageUri -OutFile $packagePath
}

$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash
if (-not [string]::Equals($actualHash, $packageHash, [StringComparison]::OrdinalIgnoreCase)) {
    Remove-Item -LiteralPath $packagePath -Force
    throw "La suma SHA-256 de Microsoft.Windows.SDK.Contracts no coincide."
}

$requiredEntries = @(
    [pscustomobject]@{
        ArchivePath = 'ref/netstandard2.0/Windows.WinMD'
        FileName = 'Windows.WinMD'
        Hash = '249D4EBDCB399002F7B6DCB50384AD0DF3AB6A7CF7087161EDA4E43052128E6D'
    },
    [pscustomobject]@{
        ArchivePath = 'ref/netstandard2.0/Windows.Foundation.FoundationContract.winmd'
        FileName = 'Windows.Foundation.FoundationContract.winmd'
        Hash = '7F415384A27E313CBD240A24E0F8DCC82F72C8B3FEBC5B65B8E2F781C01E8BA1'
    },
    [pscustomobject]@{
        ArchivePath = 'ref/netstandard2.0/Windows.Foundation.UniversalApiContract.winmd'
        FileName = 'Windows.Foundation.UniversalApiContract.winmd'
        Hash = '863DAE0871727CC424C07E737146B629ABC516CA4D8C14914E0D81E9C637B587'
    }
)

$missingReference = $false
foreach ($entryInfo in $requiredEntries) {
    $destination = Join-Path $referencePath $entryInfo.FileName
    if (-not (Test-Path -LiteralPath $destination)) {
        $missingReference = $true
        break
    }

    $referenceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
    if (-not [string]::Equals(
            $referenceHash,
            $entryInfo.Hash,
            [StringComparison]::OrdinalIgnoreCase)) {
        $missingReference = $true
        break
    }
}

if ($missingReference) {
    New-Item -ItemType Directory -Path $referencePath -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $packagePath).Path)
    try {
        foreach ($entryInfo in $requiredEntries) {
            $entry = $archive.GetEntry($entryInfo.ArchivePath)
            if ($null -eq $entry) {
                throw "No se encontró $($entryInfo.ArchivePath) en el paquete de contratos de Windows."
            }

            $destination = Join-Path $referencePath $entryInfo.FileName
            $input = $entry.Open()
            $output = [IO.File]::Open(
                $destination,
                [IO.FileMode]::Create,
                [IO.FileAccess]::Write,
                [IO.FileShare]::None)
            try {
                $input.CopyTo($output)
            }
            finally {
                $output.Dispose()
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

foreach ($entryInfo in $requiredEntries) {
    $destination = Join-Path $referencePath $entryInfo.FileName
    $referenceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash
    if (-not [string]::Equals(
            $referenceHash,
            $entryInfo.Hash,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "La suma SHA-256 de $($entryInfo.FileName) no coincide."
    }
}

Write-Output (Resolve-Path -LiteralPath $referencePath).Path
