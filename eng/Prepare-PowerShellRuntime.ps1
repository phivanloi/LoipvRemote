[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

$version = '7.6.7'
$assets = @{
    x64 = @{
        FileName = "PowerShell-$version-win-x64.zip"
        Sha256 = 'd2f952ebdbf24386cb8923553aa58e41a846db35a6155ea04c58142a4b16dbb7'
    }
    arm64 = @{
        FileName = "PowerShell-$version-win-arm64.zip"
        Sha256 = '119bd9221254ef3637523cb1990797618f5fd68848b57de40f12b561d743149e'
    }
}

$asset = $assets[$Architecture]
$destinationPath = [IO.Path]::GetFullPath($Destination)
if ([IO.Path]::GetFileName($destinationPath) -ne 'PowerShell' -or
    [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($destinationPath)) -ne 'Runtime') {
    throw "PowerShell runtime destination must end with Runtime\PowerShell: $destinationPath"
}
$versionMarker = Join-Path $destinationPath '.loipvremote-powershell-version'
$expectedMarker = "$version-$Architecture"
if ((Test-Path -LiteralPath (Join-Path $destinationPath 'pwsh.exe')) -and
    (Test-Path -LiteralPath $versionMarker) -and
    ((Get-Content -Raw -LiteralPath $versionMarker).Trim() -eq $expectedMarker)) {
    Write-Host "PowerShell $expectedMarker is already prepared at $destinationPath."
    return
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cacheDirectory = Join-Path $repositoryRoot ".artifacts\powershell"
New-Item -ItemType Directory -Path $cacheDirectory -Force | Out-Null
$archivePath = Join-Path $cacheDirectory $asset.FileName
$downloadUrl = "https://github.com/PowerShell/PowerShell/releases/download/v$version/$($asset.FileName)"

$archiveIsValid = (Test-Path -LiteralPath $archivePath) -and
    ((Get-Sha256Hex -Path $archivePath) -eq $asset.Sha256)
if (-not $archiveIsValid) {
    $temporaryArchive = "$archivePath.download"
    if (Test-Path -LiteralPath $temporaryArchive) {
        Remove-Item -LiteralPath $temporaryArchive -Force
    }

    Write-Host "Downloading official PowerShell $version $Architecture runtime."
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUrl -OutFile $temporaryArchive
    $actualHash = Get-Sha256Hex -Path $temporaryArchive
    if ($actualHash -ne $asset.Sha256) {
        Remove-Item -LiteralPath $temporaryArchive -Force
        throw "PowerShell runtime SHA256 mismatch. Expected $($asset.Sha256), got $actualHash."
    }
    Move-Item -LiteralPath $temporaryArchive -Destination $archivePath -Force
}

$destinationParent = Split-Path -Parent $destinationPath
New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
$stagingPath = Join-Path $destinationParent ("PowerShell.staging." + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $stagingPath -Force
    $stagedExecutable = Join-Path $stagingPath 'pwsh.exe'
    if (-not (Test-Path -LiteralPath $stagedExecutable)) {
        throw "The PowerShell archive did not contain pwsh.exe."
    }

    Set-Content -LiteralPath (Join-Path $stagingPath '.loipvremote-powershell-version') `
        -Value $expectedMarker `
        -NoNewline

    if (Test-Path -LiteralPath $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Recurse -Force
    }
    Move-Item -LiteralPath $stagingPath -Destination $destinationPath
}
finally {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

Write-Host "Prepared PowerShell $expectedMarker at $destinationPath."
