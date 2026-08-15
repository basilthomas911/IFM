[CmdletBinding()]
param(
    [ValidateRange(1, 1440)]
    [int]$DurationMinutes = 60,
    [ValidateSet('Future', 'Options')]
    [string]$Scenario = 'Future',
    [switch]$IncludeMbo,
    [switch]$CaptureCsv,
    [switch]$AllowDirtyWorkingTree,
    [switch]$PreflightOnly
)

$today = Get-Date
if ($today.DayOfWeek -ne [DayOfWeek]::Monday -and -not $PreflightOnly) {
    throw 'This launcher is intentionally restricted to Monday.'
}
$arguments = @{
    Implementation = 'Cpp'
    Scenario = $Scenario
    DurationMinutes = $DurationMinutes
    StartAt = $today.Date.AddHours(15)
    IncludeMbo = $IncludeMbo
    CaptureCsv = $CaptureCsv
    AllowDirtyWorkingTree = $AllowDirtyWorkingTree
    PreflightOnly = $PreflightOnly
}
& (Join-Path $PSScriptRoot 'Run-MarketCloseSoak.ps1') @arguments
