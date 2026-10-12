[CmdletBinding()]
param([string]$PostgresContainer = "ifm_db", [switch]$NoBuild)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$deploymentRoot = Join-Path $repositoryRoot ".artifacts/scheduled-tasks/development"
$settingsPath = Join-Path $deploymentRoot "scheduler.settings.json"
New-Item -ItemType Directory -Force -Path $deploymentRoot | Out-Null
$settings = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "TomasAI.IFM.Application.ServerManager.SchedulerHost/appsettings.json") | ConvertFrom-Json
# Use the same PostgreSQL database as the API. Scheduler and Quartz own schemas,
# not a separate database or independently provisioned database identity.
$apiSettingsPath = Join-Path $repositoryRoot "TomasAI.IFM.Application.Api.Server/appsettings.Development.json"
$apiSettings = Get-Content -Raw -LiteralPath $apiSettingsPath | ConvertFrom-Json
$storageProperty = $apiSettings.PSObject.Properties["Storage"]
$sharedConnection = if ($null -ne $storageProperty -and $null -ne $storageProperty.Value.PSObject.Properties["PostgresConnection"]) {
    $storageProperty.Value.PostgresConnection
} else {
    $apiSettings.ConnectionStrings.EventSourceActorDbConnection
}
if ([string]::IsNullOrWhiteSpace($sharedConnection)) { throw "The API shared PostgreSQL connection is required." }
$settings.ConnectionStrings.SchedulerDbConnection = $sharedConnection
$settings | Add-Member -Force NoteProperty Storage (@{ PostgresConnection = $sharedConnection })
$settings.SchedulerHost | Add-Member -Force NoteProperty ActorManaged $true
$settings.SchedulerHost | Add-Member -Force NoteProperty HostId "development"
$settings.SchedulerHost.SeedInitialSchedules = $false
$settings.SchedulerHost.TaskRunRoot = Join-Path $deploymentRoot "TaskRuns"
$settings.SchedulerHost.DeploymentRoot = $deploymentRoot
$settings.SchedulerHost.DependencyEndpoints.API = "http://localhost:22543/health/launch-ready"
foreach ($seed in $settings.SchedulerHost.InitialSchedules) { $seed.Enabled = $false }
$settings | Add-Member -Force NoteProperty Nats (@{ Producer = @{ Url = "nats://localhost:4222" }; JetStreamEventListener = @{ Url = "nats://localhost:4222" } })
$telemetry = @{ Logs = @{ Enabled = $true; OtlpEndpoint = "http://localhost:4318/v1/logs"; OtlpProtocol = "http/protobuf" }; Metrics = @{ Enabled = $true; OtlpEndpoint = "http://localhost:4318/v1/metrics"; OtlpProtocol = "http/protobuf" } }
$settings | Add-Member -Force NoteProperty Telemetry $telemetry
$settings | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
# Deployment settings remain restricted to this Windows identity; database credentials are resolved from POSTGRES_DEV_KEY at runtime.
& icacls $settingsPath /inheritance:r /grant:r "$([System.Security.Principal.WindowsIdentity]::GetCurrent().Name):(F)" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Unable to restrict scheduler settings permissions." }
if (-not $NoBuild) {
    $projects = @{
        "Host" = "TomasAI.IFM.Application.ServerManager.SchedulerHost"
        "Administration" = "TomasAI.IFM.Application.ScheduledTask.Administration"
        "Tasks/FuturesMarketClose" = "TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose"
        "Tasks/FuturesMarketOpen" = "TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen"
        "Tasks/SetClosingPrice" = "TomasAI.IFM.Application.ScheduledTask.SetClosingPrice"
    }
    foreach ($destination in $projects.Keys) {
        $project = $projects[$destination]
        & dotnet publish (Join-Path $repositoryRoot "$project/$project.csproj") -m:1 -c Debug --no-self-contained -o (Join-Path $deploymentRoot $destination) --nologo
        if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
    }
}
foreach ($task in $settings.SchedulerHost.TaskCatalog) {
        $taskSettingsPath = Join-Path (Join-Path $deploymentRoot $task.WorkingDirectory) "appsettings.json"
        if (-not (Test-Path -LiteralPath $taskSettingsPath)) { throw "Publish the task artifacts before applying deployment settings: $taskSettingsPath" }
        $taskSettings = Get-Content -Raw -LiteralPath $taskSettingsPath | ConvertFrom-Json
        foreach ($sink in $taskSettings.Serilog.WriteTo) {
            if ($sink.Name -eq "File") { $sink.Args.path = Join-Path $settings.SchedulerHost.TaskRunRoot "$($task.TaskKey)_.log" }
        }
        $taskSettings | Add-Member -Force NoteProperty Telemetry $telemetry
        $taskSettings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $taskSettingsPath -Encoding UTF8
}
Write-Output "Scheduler artifacts prepared at $deploymentRoot. Definitions are created disabled through the administration command API."
