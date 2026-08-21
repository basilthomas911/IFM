[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '..\..'),
    [string] $DeploymentRoot = 'C:\TomasAI\IFMAppDir',
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipLiveDatabentoBuild,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'

$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$deployment = [IO.Path]::GetFullPath($DeploymentRoot)
$solution = Join-Path $repository 'TomasAI.IFM.sln'
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw "IFM solution was not found beneath RepositoryRoot: $solution"
}

$dotnet = Get-Command dotnet -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Source
$runningProcessNames = @(
    'IFMServerManager',
    'TomasAI.IFM.Application.Api.Server',
    'TomasAI.IFM.UI.Net',
    'TomasAI.IFM.Application.ServerManager.SchedulerHost',
    'TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose',
    'TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen',
    'TomasAI.IFM.Application.ScheduledTask.SetClosingPrice',
    'ScheduledTask.TrainFuturesItiPredictiveModel'
)
$runningProcesses = Get-Process -Name $runningProcessNames -ErrorAction SilentlyContinue
if ($runningProcesses) {
    $runningSummary = ($runningProcesses | ForEach-Object { "$($_.ProcessName) ($($_.Id))" }) -join ', '
    throw "Stop the published IFM applications before deployment. Running: $runningSummary"
}

$schedulerService = Get-Service -Name 'IFMSchedulerHost' -ErrorAction SilentlyContinue
if ($schedulerService -and $schedulerService.Status -ne 'Stopped') {
    throw "Stop the IFMSchedulerHost Windows service before deployment. Current state: $($schedulerService.Status)"
}

if (-not $SkipLiveDatabentoBuild) {
    $nativeBuild = Join-Path $repository 'native\DatabentoFeed.Native\build-native.ps1'
    if (-not (Test-Path -LiteralPath $nativeBuild -PathType Leaf)) {
        throw "Databento native build script was not found: $nativeBuild"
    }

    Write-Host 'Building and testing the live Databento adapter...'
    & $nativeBuild -Configuration $Configuration -EnableLive -RunTests
    if ($LASTEXITCODE -ne 0) {
        throw "Live Databento build failed with exit code $LASTEXITCODE."
    }
}

$applications = @(
    [pscustomobject]@{
        Name = 'API Server'
        Project = 'TomasAI.IFM.Application.Api.Server\TomasAI.IFM.Application.Api.Server.csproj'
        Destination = 'ApiServer'
    },
    [pscustomobject]@{
        Name = 'UI.Net'
        Project = 'TomasAI.IFM.UI.Net\TomasAI.IFM.UI.Net.csproj'
        Destination = 'UI.Net'
    },
    [pscustomobject]@{
        Name = 'Scheduler Host'
        Project = 'TomasAI.IFM.Application.ServerManager.SchedulerHost\TomasAI.IFM.Application.ServerManager.SchedulerHost.csproj'
        Destination = 'SchedulerHost'
    },
    [pscustomobject]@{
        Name = 'Futures Market Close'
        Project = 'TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose\TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose.csproj'
        Destination = 'Tasks\FuturesMarketClose'
    },
    [pscustomobject]@{
        Name = 'Futures Market Open'
        Project = 'TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen\TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen.csproj'
        Destination = 'Tasks\FuturesMarketOpen'
    },
    [pscustomobject]@{
        Name = 'Set Closing Price'
        Project = 'TomasAI.IFM.Application.ScheduledTask.SetClosingPrice\TomasAI.IFM.Application.ScheduledTask.SetClosingPrice.csproj'
        Destination = 'Tasks\SetClosingPrice'
    },
    [pscustomobject]@{
        Name = 'Train Futures ITI Predictive Model'
        Project = 'TomasAI.ScheduledTasks\SceduledTask.TrainFuturesItiPredictiveModel\ScheduledTask.TrainFuturesItiPredictiveModel.csproj'
        Destination = 'Tasks\TrainFuturesItiPredictiveModel'
    },
    [pscustomobject]@{
        # Keep Server Manager last so an accidental launch cannot race incomplete API/UI deployment.
        Name = 'Server Manager'
        Project = 'TomasAI.IFM.Application.ServerManager\TomasAI.IFM.Application.ServerManager.csproj'
        Destination = 'ServerManager'
    }
)

New-Item -ItemType Directory -Path $deployment -Force | Out-Null
$deploymentPrefix = $deployment.TrimEnd(
    [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$published = foreach ($application in $applications) {
    $project = [IO.Path]::GetFullPath((Join-Path $repository $application.Project))
    $destination = [IO.Path]::GetFullPath((Join-Path $deployment $application.Destination))
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Project for '$($application.Name)' was not found: $project"
    }

    if (-not $destination.StartsWith($deploymentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Publish destination escaped DeploymentRoot: $destination"
    }

    Write-Host "Publishing $($application.Name) to $destination..."
    $arguments = @('publish', $project, '-c', $Configuration, '-o', $destination)
    if ($NoRestore) {
        $arguments += '--no-restore'
    }

    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing '$($application.Name)' failed with exit code $LASTEXITCODE."
    }

    [pscustomobject]@{
        Application = $application.Name
        Destination = $destination
    }
}

Write-Host ''
Write-Host "IFM application publishing completed successfully ($Configuration)."
$published | Format-Table -AutoSize
Write-Host 'Scheduler Host installation/startup and schedule enablement remain separate operator actions.'
