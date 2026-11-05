[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$PortableExecutablePath,
    [string]$WorkRoot = [IO.Path]::GetTempPath(),
    [switch]$AllowCurrentUserRegistryChanges
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# These integration checks launch the app and change its HKCU startup entry.
# Use a fresh CI account or a disposable Windows user, never an active installation.
if ($env:GITHUB_ACTIONS -ne 'true' -and -not $AllowCurrentUserRegistryChanges) {
    throw 'Run in CI or a disposable Windows account with -AllowCurrentUserRegistryChanges.'
}

$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$portableSource = (Resolve-Path -LiteralPath $PortableExecutablePath).Path
$startupKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$startupValueName = 'CodexUsageMeter'
$appRegistryPath = 'Software\CodexUsageMeter'
$uninstallKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9B7B432F-36A6-4E80-AB97-6D020F2F0CAB}_is1'
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'CodexUsageMeter'

function Get-StartupCommand {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupKeyPath)
    try { if ($key) { return $key.GetValue($startupValueName) } }
    finally { if ($key) { $key.Dispose() } }
    return $null
}

function Set-StartupCommand([string]$Executable) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($startupKeyPath)
    try { $key.SetValue($startupValueName, ('"{0}"' -f $Executable)) }
    finally { $key.Dispose() }
}

foreach ($registryPath in @($appRegistryPath, $uninstallKeyPath)) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryPath)
    if ($key) { $key.Dispose(); throw 'Installer checks require a profile without an existing Codex Usage Meter installation.' }
}
foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
    $machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
    try {
        $key = $machine.OpenSubKey($uninstallKeyPath)
        if ($key) { $key.Dispose(); throw 'Installer checks require a machine without an existing system-wide Codex Usage Meter installation.' }
    }
    finally { $machine.Dispose() }
}
if ($null -ne (Get-StartupCommand) -or (Test-Path -LiteralPath $settingsDirectory) -or
    @(Get-Process CodexUsageMeter -ErrorAction SilentlyContinue).Count) {
    throw 'Installer checks require a clean profile without existing app settings, startup registration, or a running copy.'
}

$workDirectory = [IO.Path]::GetFullPath($WorkRoot)
$testDirectory = [IO.Path]::GetFullPath((Join-Path $workDirectory ('CodexUsageMeter-installer-tests-' + [guid]::NewGuid().ToString('N'))))
if (-not $testDirectory.StartsWith($workDirectory.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Test directory must remain within the selected work directory.'
}
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$installDirectory = Join-Path $testDirectory 'Installed app'
$installedApp = Join-Path $installDirectory 'CodexUsageMeter.exe'
$portableDirectory = Join-Path $testDirectory 'Portable app'
$portableApp = Join-Path $portableDirectory 'CodexUsageMeter.exe'
$otherPortableApp = Join-Path $testDirectory 'Another portable copy\CodexUsageMeter.exe'
$checks = [Collections.Generic.List[string]]::new()

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Wait-Setup([string]$Executable, [string[]]$SetupArguments) {
    Write-Output "Starting $(Split-Path -Leaf $Executable)."
    $process = Start-Process -FilePath $Executable -ArgumentList $SetupArguments -WindowStyle Hidden -PassThru
    try {
        # Start-Process -Wait has no timeout and can wait for descendants indefinitely.
        if (-not $process.WaitForExit(120000)) { throw 'Setup process timed out.' }
        if ($process.ExitCode -ne 0) { throw "Setup exited with $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
}

function Wait-Uninstalled {
    # Inno's launcher delegates to a temporary child. Wait for the actual removal
    # rather than an unbounded process-tree wait, before reading registry state.
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ((Test-Path -LiteralPath $installedApp) -or (Test-Path -LiteralPath $uninstaller)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Uninstall did not remove the application and uninstaller within 30 seconds.' }
        Start-Sleep -Milliseconds 100
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallKeyPath)
    if ($key) { $key.Dispose(); throw 'Uninstall left its registration behind.' }
}

function Get-InstalledProcesses {
    return @(Get-Process CodexUsageMeter -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installedApp })
}

function Stop-TestProcesses {
    Get-Process CodexUsageMeter -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $installedApp -or $_.Path -eq $portableApp } |
        ForEach-Object {
            $testProcessId = $_.Id
            Stop-Process -Id $testProcessId -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $testProcessId -Timeout 10 -ErrorAction SilentlyContinue
        }
}

function Assert-Reopened([int]$PreviousId = 0) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $processes = @(Get-InstalledProcesses)
        if ($processes.Count -eq 1 -and $processes[0].Id -ne $PreviousId) { return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Automatic update did not reopen exactly one app from the installation directory.'
}

function Invoke-Installation([string]$RestartApp = '') {
    $setupArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/SP-', '/NORESTART', ('/DIR="{0}"' -f $installDirectory))
    if ($RestartApp) {
        $setupArguments += @('/CLOSEAPPLICATIONS', '/FORCECLOSEAPPLICATIONS', '/NORESTARTAPPLICATIONS', '/AUTOMATICUPDATE=1',
            ('/RESTARTAPP="{0}"' -f $RestartApp))
    }
    Wait-Setup $installer $setupArguments
    Assert-Condition (Test-Path -LiteralPath $installedApp -PathType Leaf) 'Installed executable not found.'
}

function Invoke-Uninstallation {
    Stop-TestProcesses
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    Wait-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    Wait-Uninstalled
    Assert-Condition (-not (Test-Path -LiteralPath $installedApp -PathType Leaf)) 'Uninstall left the application executable behind.'
}

try {
    Write-Output 'Checking silent installation with startup disabled.'
    Invoke-Installation
    Assert-Condition ($null -eq (Get-StartupCommand)) 'Installation unexpectedly enabled startup.'
    $checks.Add('Silent installation preserves disabled startup.')

    Write-Output 'Checking installed update and relaunch with startup enabled.'
    Set-StartupCommand $installedApp
    $beforeUpdate = Start-Process -FilePath $installedApp -WindowStyle Hidden -PassThru
    try {
        Start-Sleep -Seconds 2
        Invoke-Installation $installedApp
        Assert-Reopened $beforeUpdate.Id
    }
    finally { $beforeUpdate.Dispose() }
    Assert-Condition ((Get-StartupCommand) -ceq ('"{0}"' -f $installedApp)) 'Installed update lost startup registration.'
    $checks.Add('Installed update closes and reopens the app and preserves startup.')

    Write-Output 'Checking uninstall and startup cleanup.'
    Invoke-Uninstallation
    Assert-Condition ($null -eq (Get-StartupCommand)) ("Uninstall left startup entry: $(Get-StartupCommand); requested app: $installedApp" )
    $checks.Add('Uninstall removes its own startup entry.')

    Write-Output 'Checking portable-to-installed startup migration.'
    New-Item -ItemType Directory -Path $portableDirectory | Out-Null
    Copy-Item -LiteralPath $portableSource -Destination $portableApp
    Set-StartupCommand $portableApp
    Invoke-Installation $portableApp
    Assert-Reopened
    Assert-Condition ((Get-StartupCommand) -ceq ('"{0}"' -f $installedApp)) 'Portable update did not migrate the startup entry.'
    Assert-Condition (Test-Path -LiteralPath $portableApp -PathType Leaf) 'Portable update removed the original portable file.'
    $checks.Add('Portable-to-installed update migrates enabled startup and retains the portable copy.')

    Write-Output 'Checking preservation of another copy''s startup entry.'
    Set-StartupCommand $otherPortableApp
    $beforeUpdateId = @(Get-InstalledProcesses)[0].Id
    Invoke-Installation $installedApp
    Assert-Reopened $beforeUpdateId
    Assert-Condition ((Get-StartupCommand) -ceq ('"{0}"' -f $otherPortableApp)) 'Update changed another copy''s startup entry.'
    Invoke-Uninstallation
    Assert-Condition ((Get-StartupCommand) -ceq ('"{0}"' -f $otherPortableApp)) 'Uninstall removed another copy''s startup entry.'
    $checks.Add('Update and uninstall preserve another copy''s startup entry.')

    $checks | ForEach-Object { Write-Output "PASS: $_" }
}
finally {
    Stop-TestProcesses
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (Test-Path -LiteralPath $uninstaller -PathType Leaf) {
        try {
            Wait-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
            Wait-Uninstalled
        }
        catch { Write-Warning "Test uninstall cleanup failed: $_" }
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($startupKeyPath, $true)
    if ($key) {
        try {
            $command = $key.GetValue($startupValueName)
            if ($command -in @(('"{0}"' -f $installedApp), ('"{0}"' -f $portableApp), ('"{0}"' -f $otherPortableApp))) {
                $key.DeleteValue($startupValueName, $false)
            }
        }
        finally { $key.Dispose() }
    }
    # The preflight rejects existing app settings; only this test's data can be here.
    if (Test-Path -LiteralPath $settingsDirectory) {
        $resolvedSettings = (Resolve-Path -LiteralPath $settingsDirectory).Path
        if ($resolvedSettings -cne $settingsDirectory) { throw 'Settings cleanup target changed.' }
        Remove-Item -LiteralPath $resolvedSettings -Recurse -Force
    }
    $resolvedTest = (Resolve-Path -LiteralPath $testDirectory).Path
    if ($resolvedTest -cne $testDirectory -or
        -not $resolvedTest.StartsWith($workDirectory.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Test cleanup target changed.'
    }
    Remove-Item -LiteralPath $resolvedTest -Recurse -Force
}
