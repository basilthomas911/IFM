# Forward dotnet arguments/output and preserve its exit code after restoring missing NuGet paths.
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$DotNetArguments)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Initialize-DotNetEnvironment.ps1')
$dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
$dotnetPath = if ($dotnet) { $dotnet.Source } else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)) 'dotnet/dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnetPath -PathType Leaf)) {
    throw 'The .NET SDK was not found on PATH or in the Windows Program Files folder.'
}
# ProcessStartInfo on Windows PowerShell uses a command line rather than ArgumentList.
# Escape embedded quotes and trailing backslashes before quoting each argument.
$quotedArguments = foreach ($argument in $DotNetArguments) {
    $escaped = [regex]::Replace($argument, '(\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\+)$', '$1$1')
    '"' + $escaped + '"'
}
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $dotnetPath
$start.Arguments = $quotedArguments -join ' '
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($start)
try {
    $errorOutput = $process.StandardError.ReadToEndAsync()
    while (-not $process.StandardOutput.EndOfStream) {
        Write-Output $process.StandardOutput.ReadLine()
    }
    $process.WaitForExit()
    $errorText = $errorOutput.GetAwaiter().GetResult()
    if ($errorText) { [Console]::Error.Write($errorText) }
    $exitCode = $process.ExitCode
} finally {
    $process.Dispose()
}
exit $exitCode
