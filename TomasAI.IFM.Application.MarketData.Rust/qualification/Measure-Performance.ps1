param(
    [Parameter(Mandatory=$true)][string]$BaselineExecutable,
    [Parameter(Mandatory=$true)][string]$CandidateExecutable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(5,100)][int]$Pairs=5
)

# Build both executables first. Keep the original executable immutable: recompiling
# the source after a change cannot recreate the original baseline measurement.
$ErrorActionPreference='Stop'
$before=(Resolve-Path -LiteralPath $BaselineExecutable).Path
$after=(Resolve-Path -LiteralPath $CandidateExecutable).Path
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Get-Process cargo,rustc,link,cl -ErrorAction SilentlyContinue){throw 'Wait for all compilers to finish before measuring.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$metadata=Join-Path $output 'executables.json'
if(Test-Path -LiteralPath $metadata){throw 'Use a fresh output directory to preserve prior timing evidence.'}
@{baseline=@{path=$before;sha256=(Get-FileHash -LiteralPath $before).Hash};candidate=@{path=$after;sha256=(Get-FileHash -LiteralPath $after).Hash};pairs=$Pairs;recordedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');note='Amortized warmed batch timings; lookup kernels are not whole API timings'} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $metadata -Encoding utf8
for($pair=1;$pair -le $Pairs;$pair++){
    $order=if($pair % 2 -eq 1){@('before','after')}else{@('after','before')}
    foreach($variant in $order){
        $path=Join-Path $output ('pair-{0:D2}-{1}.json' -f $pair,$variant)
        if(Test-Path -LiteralPath $path){throw "Refusing to overwrite $path"}
        $result=if($variant -eq 'before'){& $before}else{& $after --indexed}
        if($LASTEXITCODE -ne 0){throw "Performance probe failed: $variant"}
        $result | ConvertFrom-Json | Out-Null
        $result | Set-Content -LiteralPath $path -Encoding utf8
        Write-Host "Recorded $path"
    }
}
