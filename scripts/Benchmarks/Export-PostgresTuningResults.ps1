[CmdletBinding()]
param([string]$EvidenceRoot='.artifacts/postgres-tuning')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root=Join-Path $repo $EvidenceRoot
$reports=Get-ChildItem $root -Filter '*report-full-compressed.json' -Recurse | Sort-Object LastWriteTime
$profiles=@{}
foreach($file in $reports){
    $report=Get-Content $file.FullName -Raw|ConvertFrom-Json
    if(@($report.Benchmarks|Where-Object{$null -eq $_.Statistics -or $_.Statistics.N -lt 6}).Count -gt 0){continue}
    $name=$file.Directory.Parent.Name
    $profiles[$name]=@{Report=$report;Path=$file.Directory.Parent.FullName}
}
if(-not $profiles.ContainsKey('Baseline')){throw 'No valid baseline.'}
$baseline=$profiles.Baseline.Report.Benchmarks
$rows=foreach($name in ($profiles.Keys|Sort-Object)){
    foreach($b in $profiles[$name].Report.Benchmarks){
        $reference=$baseline|Where-Object Method -eq $b.Method
        [pscustomobject]@{Profile=$name;Method=$b.Method;MeanMs=$b.Statistics.Mean/1e6;ConfidenceMarginMs=$b.Statistics.ConfidenceInterval.Margin/1e6;N=$b.Statistics.N;ChangePercent=100*($b.Statistics.Mean/$reference.Statistics.Mean-1);BaselineConfidenceOverlap=($b.Statistics.ConfidenceInterval.Lower -le $reference.Statistics.ConfidenceInterval.Upper -and $b.Statistics.ConfidenceInterval.Upper -ge $reference.Statistics.ConfidenceInterval.Lower);Evidence=$profiles[$name].Path}
    }
}
$rows|Export-Csv (Join-Path $root 'summary.csv') -NoTypeInformation
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('# PostgreSQL 18.6 tuning results')
$lines.Add('')
$lines.Add('Measured October 9, 2026 on the shared development workstation. Development database settings and data were not changed. All database profiles used four CPUs, 4 GiB memory and durable commits. See README.md for the workload and runner.')
$lines.Add('')
$lines.Add('## Configuration and findings')
$lines.Add('')
$lines.Add('| Parameter | Baseline | Sustained candidate |')
$lines.Add('|---|---|---|')
$lines.Add('| shared_buffers | 256 MiB | 1 GiB |')
$lines.Add('| wal_compression | off | lz4 |')
$lines.Add('| max_wal_size | 2 GiB | 4 GiB |')
$lines.Add('| checkpoint_timeout (stress only) | 30 seconds | 30 seconds |')
$lines.Add('')
$stress=@{}
foreach($file in (Get-ChildItem $root -Filter '*-sustained.json' -Recurse | Sort-Object LastWriteTime)){$s=Get-Content $file.FullName -Raw|ConvertFrom-Json;$stress[$s.Profile]=$s}
if($stress.ContainsKey('SustainedBaseline') -and $stress.ContainsKey('SustainedCandidate')){
    $a=$stress.SustainedBaseline;$b=$stress.SustainedCandidate
    $aw=[double]$a.StatsAfter.wal.wal_bytes-[double]$a.StatsBefore.wal.wal_bytes
    $bw=[double]$b.StatsAfter.wal.wal_bytes-[double]$b.StatsBefore.wal.wal_bytes
    $lines.Add(('The combined sustained candidate changed batch p95 from {0:F2} to {1:F2} ms ({2:F0}% reduction), p99 from {3:F2} to {4:F2} ms ({5:F0}% reduction) and WAL volume from {6:F1} to {7:F1} MiB ({8:F0}% reduction). Throughput was {9:F0} versus {10:F0} events/s. The runs crossed {11} and {12} timed checkpoints. These combined results do not isolate the contribution of each setting.' -f $a.P95BatchMilliseconds,$b.P95BatchMilliseconds,(100*(1-$b.P95BatchMilliseconds/$a.P95BatchMilliseconds)),$a.P99BatchMilliseconds,$b.P99BatchMilliseconds,(100*(1-$b.P99BatchMilliseconds/$a.P99BatchMilliseconds)),($aw/1MB),($bw/1MB),(100*(1-$bw/$aw)),$a.EventsPerSecond,$b.EventsPerSecond,$a.TimedCheckpoints,$b.TimedCheckpoints))
}
$lines.Add('')
$lines.Add('Use the measurements below to compare batching cost per event, client placement and individual settings. No tuning profile was applied to the development database.')
$lines.Add('')
$lines.Add('## BenchmarkDotNet measurements')
$lines.Add('')
$lines.Add('Mean and confidence margin are milliseconds per invocation. Margins are BDN 99.9% confidence intervals of iteration means, not request p95/p99. Negative change means faster than the Windows baseline. Confidence overlap is descriptive; sequential profiles on a shared workstation do not establish causation.')
$lines.Add('')
$lines.Add('| Profile | Method | Mean ms | Margin ms | N | Change | Baseline CI overlaps |')
$lines.Add('|---|---|---:|---:|---:|---:|---|')
foreach($r in $rows){$lines.Add(('| {0} | {1} | {2:F3} | {3:F3} | {4} | {5:F1}% | {6} |' -f $r.Profile,$r.Method,$r.MeanMs,$r.ConfidenceMarginMs,$r.N,$r.ChangePercent,$r.BaselineConfidenceOverlap))}
$lines.Add('')
$lines.Add('## Sustained checkpoint diagnostics')
$lines.Add('')
$lines.Add('Separate three-minute request samples, eight writers, 32 events per commit, target 5,000 events/s and stress-only checkpoint_timeout=30s. These are not BDN results or a recommended checkpoint configuration.')
$lines.Add('')
$lines.Add('| Profile | Events/s | Batch p50 ms | Batch p95 ms | Batch p99 ms | Timed/requested checkpoints | WAL MiB | WAL bytes/event | Full-page images |')
$lines.Add('|---|---:|---:|---:|---:|---|---:|---:|---:|')
foreach($file in (Get-ChildItem $root -Filter '*-sustained.json' -Recurse)){
    $s=Get-Content $file.FullName -Raw|ConvertFrom-Json
    $wal=[double]$s.StatsAfter.wal.wal_bytes-[double]$s.StatsBefore.wal.wal_bytes
    $fpi=[long]$s.StatsAfter.wal.wal_fpi-[long]$s.StatsBefore.wal.wal_fpi
    $lines.Add(('| {0} | {1:F0} | {2:F3} | {3:F3} | {4:F3} | {5}/{6} | {7:F1} | {8:F0} | {9} |' -f $s.Profile,$s.EventsPerSecond,$s.P50BatchMilliseconds,$s.P95BatchMilliseconds,$s.P99BatchMilliseconds,$s.TimedCheckpoints,$s.RequestedCheckpoints,($wal/1MB),($wal/$s.Events),$fpi))
}
$lines.Add('')
$lines.Add('## Evidence locations')
$lines.Add('')
foreach($name in ($profiles.Keys|Sort-Object)){$lines.Add("- ${name}: ``$($profiles[$name].Path.Substring($repo.Length+1))``")}
$lines.Add('')
foreach($file in (Get-ChildItem $root -Filter '*-sustained.json' -Recurse)){$lines.Add("- Sustained evidence: ``$($file.FullName.Substring($repo.Length+1))``")}
foreach($file in (Get-ChildItem $root -Filter 'server.stdout.log' -Recurse|Where-Object{$_.Directory.Name -eq 'IoUring'})){$lines.Add("- IoUring startup evidence: ``$($file.FullName.Substring($repo.Length+1))``")}
$lines.Add('')
$lines.Add('Raw folders also contain settings, client/server logs, Docker resource and free-space samples, and sort EXPLAIN plans. All benchmark containers and volumes are labelled and removed by the runner; raw files remain.')
$lines.Add('')
$lines.Add('## Interpretation')
$lines.Add('')
$lines.Add('- Batching amortizes durable commit cost. Compare per-event cost without treating a larger batch as lower latency for a single financial event. No application batching policy was changed.')
$lines.Add('- The working set is approximately 500 MiB of payload plus indexes. Operating-system cache remains available; these are warm runs, not a proof of benefits on a larger cold dataset.')
$lines.Add('- Sort plans use parallel in-memory quicksort under the baseline. No temp spill and no JIT execution were observed. Increasing work_mem or disabling JIT is not supported as an optimization for this workload.')
$lines.Add('- LinuxClient also changes client OS, runtime build and CPU allocation (two client CPUs), so its difference cannot be attributed to networking alone. Database configuration remains the baseline.')
$lines.Add('- io_uring startup failed with Operation not permitted under the existing Docker/kernel security configuration. No security settings were weakened. See the IoUring server.stdout.log for the exact failure.')
$lines.Add('- WAL compression affects full-page images, not all event payload WAL. Larger max_wal_size is a checkpoint budget, not a direct per-commit latency switch. The combined sustained profile cannot isolate the contribution of its three changes.')
$lines.Add('')
$lines.Add('PostgreSQL parameter semantics: [resource consumption](https://www.postgresql.org/docs/18/runtime-config-resource.html), [WAL configuration](https://www.postgresql.org/docs/18/runtime-config-wal.html).')
$lines | Set-Content (Join-Path $repo 'TomasAI.IFM.Application.Storage.Postgres.Benchmarks/POSTGRES-TUNING-RESULTS.md')
Write-Output "Exported $($rows.Count) valid BDN measurements."


