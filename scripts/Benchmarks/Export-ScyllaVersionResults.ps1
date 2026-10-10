[CmdletBinding()]
param([string]$EvidenceRoot='.artifacts/scylla-version-benchmarks')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path;$root=Join-Path $repo $EvidenceRoot
$profiles=@{}
foreach($file in (Get-ChildItem $root -Filter '*report-full-compressed.json' -Recurse|Sort-Object LastWriteTime)){
    $r=Get-Content $file.FullName -Raw|ConvertFrom-Json
    if(@($r.Benchmarks|Where-Object{$null -eq $_.Statistics -or $_.Statistics.N -lt 6}).Count -gt 0){continue}
    $profiles[$file.Directory.Parent.Name]=@{Report=$r;Path=$file.Directory.Parent.FullName}
}
$rows=foreach($name in ($profiles.Keys|Sort-Object)){
    $mode=$name.Substring($name.LastIndexOf('-')+1);$version=$name.Substring(0,$name.LastIndexOf('-'))
    foreach($b in $profiles[$name].Report.Benchmarks){[pscustomobject]@{Profile=$name;Version=$version;Mode=$mode;Method=$b.Method;MeanMs=$b.Statistics.Mean/1e6;MarginMs=$b.Statistics.ConfidenceInterval.Margin/1e6;N=$b.Statistics.N;Lower=$b.Statistics.ConfidenceInterval.Lower/1e6;Upper=$b.Statistics.ConfidenceInterval.Upper/1e6;Evidence=$profiles[$name].Path}}
}
$rows|Export-Csv (Join-Path $root 'summary.csv') -NoTypeInformation
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('# ScyllaDB 6.2.2 versus 2026.3.3')
$lines.Add('')
$lines.Add('Measured October 9, 2026 with BenchmarkDotNet 0.15.8, .NET 10 and the repository''s ScyllaDBCSharpDriver 3.22.0.4. Current deployment version was confirmed using scylla --version. [2026.3.3 release notes](https://forum.scylladb.com/t/release-scylladb-2026-3-3/5531) identify the latest stable patch released October 7.')
$lines.Add('')
$lines.Add('## Findings')
$lines.Add('')
foreach($sync in @('periodic','batch')){
    $pairs=@(foreach($old in ($rows|Where-Object{$_.Version -eq '6.2.2' -and $_.Mode -eq $sync})){
        $latest=$rows|Where-Object{$_.Version -eq '2026.3.3' -and $_.Mode -eq $sync -and $_.Method -eq $old.Method}
        if($null -ne $latest){[pscustomobject]@{Change=100*($latest.MeanMs/$old.MeanMs-1);Overlap=$old.Lower -le $latest.Upper -and $old.Upper -ge $latest.Lower}}
    })
    $lines.Add(('- {0}: latest had lower means in {1}/{2} cases. Confidence intervals overlapped in {3}/{2} cases; overlapping intervals limit claims of a decisive version advantage.' -f $sync,@($pairs|Where-Object{$_.Change -lt 0}).Count,$pairs.Count,@($pairs|Where-Object{$_.Overlap}).Count))
}
$stressOld=Get-ChildItem $root -Filter '6.2.2-sustained-sustained.json' -Recurse|Sort-Object LastWriteTime|Select-Object -Last 1
$stressNew=Get-ChildItem $root -Filter '2026.3.3-sustained-sustained.json' -Recurse|Sort-Object LastWriteTime|Select-Object -Last 1
if($null -ne $stressOld -and $null -ne $stressNew){
    $a=Get-Content $stressOld.FullName -Raw|ConvertFrom-Json;$b=Get-Content $stressNew.FullName -Raw|ConvertFrom-Json
    $lines.Add(('- Sustained batch p95: {0:F3} to {1:F3} ms; p99: {2:F3} to {3:F3} ms. One three-minute diagnostic per version; this does not establish repeatability or production capacity.' -f $a.P95BatchMilliseconds,$b.P95BatchMilliseconds,$a.P99BatchMilliseconds,$b.P99BatchMilliseconds))
}
$lines.Add(('- Valid measured BDN cases: {0}. Cleanup verification for the completed comparison is retained in .artifacts/scylla-version-benchmarks/cleanup-verification.json.' -f $rows.Count))
$lines.Add('')
$lines.Add('## Controls')
$lines.Add('')
$lines.Add('Separate disposable upstream containers; four CPUs/shards, 4 GiB container memory, 3 GiB Scylla memory, 256 MiB reserve, epoll reactor, developer mode and overprovisioning. RF=1, LOCAL_QUORUM, durable_writes=true, tablets=false, STCS and classic LZ4/64 KiB compression. Representative dated trade-plan history plus current snapshots; 500,000 seeded one-kilobyte payloads. Seed and readback are outside timing. Write keys cycle over a bounded range; this is an append/upsert test, not unbounded history growth. The synthetic payload compresses across rows. See README.md for full methodology.')
$lines.Add('')
$lines.Add('The initial 6.2.2 baseline used its existing default STCS/classic LZ4 table settings. The subsequent runs explicitly pin those same settings because 2026.3.3 has changed compression and compaction defaults. These controls represent the existing table layout rather than a comparison of every new default.')
$lines.Add('The periodic baselines seeded with individual inserts. Follow-up setup uses unlogged batches of 32 rows and four concurrent seed writers for both durable profiles (32 for periodic sustained profiles). This setup is outside measurement; table content is unchanged. The initial latest-version durable seed with 32 concurrent writers timed out, so it is excluded and retained as a load-test finding.')
$lines.Add('')
$lines.Add('Periodic commit-log sync uses 10 seconds, matching the current development node. Batch commit-log sync uses a 1 ms grouping window and waits for synchronization. These policies are reported separately. CQL batches are unlogged, single-partition batches. Do not compare periodic acknowledgements directly with PostgreSQL synchronous_commit=on.')
$lines.Add('')
$lines.Add('## BenchmarkDotNet measurements')
$lines.Add('')
$lines.Add('Milliseconds per invocation; margin is the BDN 99.9% confidence half-width for iteration means, not individual request tail latency. Three warmups/eight measured iterations, at least six accepted values. Negative change means latest is faster. Sequential runs on a shared workstation and overlapping confidence intervals limit claims about causation.')
$lines.Add('')
$lines.Add('| Sync policy | Method | 6.2.2 mean +/- margin ms | 2026.3.3 mean +/- margin ms | Change | N old/new | CI overlaps |')
$lines.Add('|---|---|---:|---:|---:|---|---|')
foreach($mode in @('periodic','batch')){
    foreach($a in ($rows|Where-Object{$_.Version -eq '6.2.2' -and $_.Mode -eq $mode})){
        $b=$rows|Where-Object{$_.Version -eq '2026.3.3' -and $_.Mode -eq $mode -and $_.Method -eq $a.Method}
        if($null -eq $b){continue}
        $overlap=$a.Lower -le $b.Upper -and $a.Upper -ge $b.Lower
        $lines.Add(('| {0} | {1} | {2:F3} +/- {3:F3} | {4:F3} +/- {5:F3} | {6:F1}% | {7}/{8} | {9} |' -f $mode,$a.Method,$a.MeanMs,$a.MarginMs,$b.MeanMs,$b.MarginMs,(100*($b.MeanMs/$a.MeanMs-1)),$a.N,$b.N,$overlap))
    }
}
$lines.Add('')
$lines.Add('## Three-minute sustained diagnostics')
$lines.Add('')
$lines.Add('Eight writers, 32 logical events per unlogged single-partition batch, offered target 5,000 events/s with pacing. Two explicit memtable flushes are requested around 60/120 seconds. Periodic commit-log sync is used. These are individual request samples, separate from BDN, and are not maximum-throughput, replicated quorum or crash-recovery tests.')
$lines.Add('')
$lines.Add('| Version | Actual events/s | Batch p50 ms | Batch p95 ms | Batch p99 ms | Successful explicit flushes | Flush duration ms |')
$lines.Add('|---|---:|---:|---:|---:|---:|---|')
foreach($file in (Get-ChildItem $root -Filter '*-sustained.json' -Recurse|Sort-Object Name)){
    $s=Get-Content $file.FullName -Raw|ConvertFrom-Json
    $lines.Add(('| {0} | {1:F0} | {2:F3} | {3:F3} | {4:F3} | {5} | {6} |' -f $s.Profile,$s.EventsPerSecond,$s.P50BatchMilliseconds,$s.P95BatchMilliseconds,$s.P99BatchMilliseconds,$s.SuccessfulFlushes,(($s.FlushMilliseconds|ForEach-Object{'{0:F1}' -f $_}) -join ', ')))
}
$lines.Add('')
$lines.Add('## Environment findings and limits')
$lines.Add('')
$lines.Add('- Docker/WSL AIO slots were exhausted: 65,530 in use against a ceiling of 65,536. The runtime ceiling was temporarily raised to 1,048,576 for the tests. The runner offers an opt-in switch to raise/restore it; no persistent sysctl configuration is edited.')
$lines.Add('- Docker''s shared filesystem is approximately 98% full. The latest node initially rejected all writes at its default critical-disk threshold. Only the isolated latest-version containers use a 99.5% threshold; a stricter external 3 GiB free-space guard remains active. Free capacity before upgrading with normal safeguards. [ScyllaDB disk guard documentation](https://docs.scylladb.com/manual/stable/troubleshooting/error-messages/critical-disk-utilization.html).')
$lines.Add('- The current development databases and their settings/data were not changed. Existing backup/manager-agent overhead is excluded from both benchmark nodes. No application snapshot calculation, serialization or actor workflow is included.')
$lines.Add('- Both versions use the same Windows client. Docker developer-mode measurements on this shared host are not production capacity or SLA guarantees.')
$lines.Add('')
$lines.Add('## Evidence')
$lines.Add('')
foreach($name in ($profiles.Keys|Sort-Object)){$lines.Add("- ${name}: ``$($profiles[$name].Path.Substring($repo.Length+1))``")}
foreach($file in (Get-ChildItem $root -Filter '*-sustained.json' -Recurse)){$lines.Add("- Sustained: ``$($file.FullName.Substring($repo.Length+1))``")}
$lines.Add('')
$lines.Add('Folders retain settings, validated server identity, full BDN JSON/CSV, stdout/stderr, server logs and Docker resource/free-space samples. Sustained folders also retain before/after Prometheus metrics. Failed setup attempts have no valid measured statistics and are excluded.')
$lines|Set-Content (Join-Path $repo 'TomasAI.IFM.Application.Storage.Scylla.Benchmarks/SCYLLA-VERSION-RESULTS.md') -Encoding UTF8
Write-Output "Exported $($rows.Count) valid BDN cases."
