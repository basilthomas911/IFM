[CmdletBinding()]
param(
    [string] $ServiceName = 'IFMSchedulerHost',
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $TaskRunRoot
)

$ErrorActionPreference = 'Stop'
$checks = [Collections.Generic.List[object]]::new()
function Add-Check([string] $Name, [bool] $Passed, [string] $Detail) {
    $checks.Add([pscustomobject]@{ Check = $Name; Passed = $Passed; Detail = $Detail })
}

$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$serviceStatus = if ($service) { [string]$service.Status } else { 'Missing' }
Add-Check 'Service installed' ($null -ne $service) $serviceStatus
$serviceConfig = if ($service) { (& sc.exe qc $ServiceName) -join "`n" } else { '' }
Add-Check 'Delayed automatic start' ($serviceConfig -match 'DELAYED_AUTO_START') 'Windows service start mode'
$failureConfig = if ($service) { (& sc.exe qfailure $ServiceName) -join "`n" } else { '' }
Add-Check 'Recovery actions' ($failureConfig -match 'RESTART') 'At least one restart action is configured'
$configPath = Join-Path $publishRoot 'appsettings.json'
$config = if (Test-Path -LiteralPath $configPath) { Get-Content $configPath -Raw | ConvertFrom-Json } else { $null }
Add-Check 'Configuration readable' ($null -ne $config) $configPath
Add-Check 'Production operator group configured' ($null -ne $config -and $config.SchedulerHost.AllowedOperatorGroups.Count -gt 0) 'Named-pipe ACL source'
Add-Check 'Task-run root exists' (Test-Path -LiteralPath $TaskRunRoot -PathType Container) ([IO.Path]::GetFullPath($TaskRunRoot))
Add-Check 'No seeded schedule enabled' ($null -ne $config -and @($config.SchedulerHost.InitialSchedules | Where-Object Enabled).Count -eq 0) 'Configuration seed gate'

$checks | Format-Table -AutoSize
if ($checks.Where({ -not $_.Passed }).Count -gt 0) { throw 'Scheduler Host acceptance checks failed.' }
