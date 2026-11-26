[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('discover','hold','status','stop-native','release')][string]$Command,
    [string]$Lease)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ($Command -eq 'discover') {
    $rows=@(& (Join-Path $PSScriptRoot '../Development/Get-IFMDevelopmentProcess.ps1'))
    ConvertTo-Json -InputObject $rows -Depth 12 -Compress
    return
}
if ($Command -ne 'status' -and $Lease -notmatch '^[a-f0-9]{32}$') { throw 'An exact capture lease is required.' }
$pipe=[IO.Pipes.NamedPipeClientStream]::new('.', 'IFM.ServerManager.Development.v1', [IO.Pipes.PipeDirection]::InOut,[IO.Pipes.PipeOptions]::Asynchronous)
try {
    $pipe.Connect(3000)
    $writer=[IO.StreamWriter]::new($pipe,[Text.UTF8Encoding]::new($false),1024,$true)
    $reader=[IO.StreamReader]::new($pipe,[Text.UTF8Encoding]::new($false),$false,1024,$true)
    try {
        $writer.AutoFlush=$true
        $message="capture-$Command"
        if ($Command -ne 'status') { $message+=' '+$Lease }
        $writer.WriteLine($message)
        $read=$reader.ReadLineAsync()
        if (-not $read.Wait(305000)) { throw 'Capture manager command exceeded its bounded wait; no forced cleanup attempted.' }
        $line=$read.GetAwaiter().GetResult()
        if ($null -eq $line -or $line.Length -gt 1048576) { throw 'Capture manager omitted a bounded receipt.' }
        $value=$line|ConvertFrom-Json
        if ($value.State -eq 'Blocked') { throw 'Capture manager refused the operation; ownership remains held.' }
        Write-Output $line
    } finally { $writer.Dispose();$reader.Dispose() }
} finally { $pipe.Dispose() }
