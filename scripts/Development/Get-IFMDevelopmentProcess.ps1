[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sessionPath = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) `
    "TomasAI\IFM\Development\server-manager-session.json"
$session = if (Test-Path -LiteralPath $sessionPath) {
    try {
        Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
    }
    catch {
        $null
    }
}
else {
    $null
}

$repositoryPrefix = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\') + '\'
$entryRoles = @{
    "IFMServerManager.dll" = "manager"
    "TomasAI.IFM.Application.Api.Server.dll" = "api"
    "TomasAI.IFM.UI.Net.dll" = "ui"
    "TomasAI.IFM.Application.ServerManager.SchedulerHost.dll" = "scheduler"
}

function Test-SameStartTime {
    param([datetime] $Actual, [datetimeoffset] $Expected)
    return [Math]::Abs((([datetimeoffset] $Actual.ToUniversalTime()) - $Expected).TotalSeconds) -lt 1
}

function Get-Ownership {
    param(
        [Diagnostics.Process] $Process,
        [string] $Role,
        [string] $HostPath
    )

    if ($Role -eq "scheduler") {
        $schedulerSessionPath = Join-Path $RepositoryRoot ".artifacts/scheduled-tasks/development/scheduler-session.json"
        if (Test-Path -LiteralPath $schedulerSessionPath) {
            try {
                $schedulerSession = Get-Content -Raw -LiteralPath $schedulerSessionPath | ConvertFrom-Json
                if ($Process.Id -eq $schedulerSession.ProcessId -and (Test-SameStartTime $Process.StartTime ([datetimeoffset]$schedulerSession.StartedAtUtc)) -and [string]::Equals([IO.Path]::GetFullPath($HostPath), [IO.Path]::GetFullPath($schedulerSession.ExecutablePath), [StringComparison]::OrdinalIgnoreCase)) { return "Owned" }
            } catch { }
        }
        # Server Manager now owns SchedulerHost in the same development session as API/UI.
    }
    if ($null -eq $session) {
        return "Unowned"
    }

    if ($Role -eq "manager") {
        if ($Process.Id -eq $session.ManagerProcessId `
            -and (Test-SameStartTime $Process.StartTime ([datetimeoffset] $session.ManagerStartedAtUtc)) `
            -and [string]::Equals(
                [IO.Path]::GetFullPath($HostPath),
                [IO.Path]::GetFullPath([string] $session.ManagerExecutablePath),
                [StringComparison]::OrdinalIgnoreCase)) {
            return "Owned"
        }

        return "Unowned"
    }

    $record = @($session.Children) | Where-Object {
        $_.ProcessKey -eq $Role -and $_.ProcessId -eq $Process.Id
    } | Select-Object -First 1
    if ($null -eq $record) {
        return "Unowned"
    }

    if ((Test-SameStartTime $Process.StartTime ([datetimeoffset] $record.StartedAtUtc)) `
        -and [string]::Equals(
            [IO.Path]::GetFullPath($HostPath),
            [IO.Path]::GetFullPath([string] $record.ExecutablePath),
            [StringComparison]::OrdinalIgnoreCase)) {
        return "Owned"
    }

    return "Ambiguous"
}

$results = foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
    try {
        $role = $null
        $entryAssemblyPath = $null

        switch ($process.ProcessName) {
            "IFMServerManager" { $role = "manager" }
            "TomasAI.IFM.Application.Api.Server" { $role = "api" }
            "TomasAI.IFM.UI.Net" { $role = "ui" }
            "TomasAI.IFM.Application.ServerManager.SchedulerHost" { $role = "scheduler" }
            "dotnet" {
                $entryModule = $process.Modules | Where-Object {
                    $entryRoles.ContainsKey($_.ModuleName)
                } | Select-Object -First 1
                if ($null -ne $entryModule) {
                    $role = $entryRoles[$entryModule.ModuleName]
                    $entryAssemblyPath = $entryModule.FileName
                }
            }
        }

        if ($null -eq $role) {
            continue
        }

        $hostPath = $process.MainModule.FileName

        $identityPath = if ($process.ProcessName -eq "dotnet" -and $entryAssemblyPath) {
            $entryAssemblyPath
        }
        else {
            $hostPath
        }
        $locksRepositoryOutput = $identityPath.StartsWith(
            $repositoryPrefix,
            [StringComparison]::OrdinalIgnoreCase)

        [pscustomobject]@{
            Role = $role
            ProcessId = $process.Id
            StartedAtUtc = ([datetimeoffset] $process.StartTime.ToUniversalTime())
            HostPath = $hostPath
            EntryAssemblyPath = $entryAssemblyPath
            Ownership = Get-Ownership $process $role $hostPath
            SessionId = if ($null -ne $session) { $session.SessionId } else { $null }
            LocksRepositoryOutput = $locksRepositoryOutput
        }
    }
    catch [System.ComponentModel.Win32Exception] {
        continue
    }
    catch [System.InvalidOperationException] {
        continue
    }
    catch {
        continue
    }
}

$results | Sort-Object Role, StartedAtUtc
