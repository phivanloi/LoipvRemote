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

$installerProject = Join-Path $PSScriptRoot 'Installer.wixproj'
if (-not (Select-String -LiteralPath $installerProject -Pattern 'SelfContained=true' -Quiet)) {
    throw 'Installer must publish a self-contained application payload.'
}

$msi = (Resolve-Path -LiteralPath $MsiPath).Path
$installDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$applicationPath = Join-Path $installDirectory 'LoipvRemote.exe'
$installLog = Join-Path $LogDirectory 'loipvremote-installer-install.log'
$uninstallLog = Join-Path $LogDirectory 'loipvremote-installer-uninstall.log'
$applicationProcess = $null

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
        if (Select-String -LiteralPath $installLog -Pattern 'Windows Installer reconfigured the product' -Quiet) {
            throw 'Installer smoke test requires a clean Windows Installer product state; the same LoipvRemote MSI is already installed.'
        }
        throw "Installer did not create $applicationPath"
    }

    # Verify the installed payload creates a top-level UI window. A responsive
    # process without a window is not a valid desktop-app startup.
    $applicationProcess = Start-Process -FilePath $applicationPath -WorkingDirectory $installDirectory -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 5
    $applicationProcess.Refresh()
    if ($applicationProcess.HasExited) {
        throw "Installed application exited during startup (code $($applicationProcess.ExitCode))."
    }
    if (-not $applicationProcess.Responding) {
        throw 'Installed application is not responding after startup.'
    }
    if ($applicationProcess.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Installed application did not create a main window after startup.'
    }
    Stop-Process -Id $applicationProcess.Id -Force -ErrorAction SilentlyContinue
    $applicationProcess = $null

    Invoke-MsiExec "/x `"$msi`" /qn /norestart /l*vx `"$uninstallLog`""
    if (Test-Path -LiteralPath $applicationPath) {
        throw "Uninstall did not remove $applicationPath"
    }
}
finally {
    if ($null -ne $applicationProcess -and -not $applicationProcess.HasExited) {
        Stop-Process -Id $applicationProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $applicationPath) {
        Start-Process -FilePath 'msiexec.exe' -ArgumentList "/x `"$msi`" /qn /norestart" -Wait | Out-Null
    }
}
