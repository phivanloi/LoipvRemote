[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$MsiPath,

    [Parameter(Mandatory)]
    [string]$InstallDirectory,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:CI -ne 'true') {
    throw 'Installer smoke tests are restricted to CI runners.'
}

$msi = (Resolve-Path -LiteralPath $MsiPath).Path
$installDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$applicationPath = Join-Path $installDirectory 'LoipvRemote.exe'
$installLog = Join-Path $LogDirectory 'loipvremote-installer-install.log'
$uninstallLog = Join-Path $LogDirectory 'loipvremote-installer-uninstall.log'

if (Test-Path -LiteralPath $installDirectory) {
    throw "Installer smoke test requires a clean path: $installDirectory"
}

function Invoke-MsiExec {
    param(
        [Parameter(Mandatory)]
        [string]$Arguments
    )

    $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $Arguments -Wait -PassThru
    if ($process.ExitCode -notin 0, 3010) {
        throw "msiexec failed with exit code $($process.ExitCode): $Arguments"
    }
}

try {
    Invoke-MsiExec "/i `"$msi`" INSTALLFOLDER=`"$installDirectory`" /qn /norestart /l*vx `"$installLog`""
    if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
        throw "Installer did not create $applicationPath"
    }

    Invoke-MsiExec "/x `"$msi`" /qn /norestart /l*vx `"$uninstallLog`""
    if (Test-Path -LiteralPath $applicationPath) {
        throw "Uninstall did not remove $applicationPath"
    }
}
finally {
    if (Test-Path -LiteralPath $applicationPath) {
        Start-Process -FilePath 'msiexec.exe' -ArgumentList "/x `"$msi`" /qn /norestart" -Wait | Out-Null
    }
}
