# C++ and Rust market-close live comparison

This procedure captures two equivalent one-hour ES futures sessions during the liquid
market-close window. Run C++ on Monday and Rust on Tuesday from 3:00 PM through 4:00 PM
Eastern time. The launchers build and verify the selected native DLL before 3:00 PM, open
the feed for warm-up, activate counters and resource measurements at the scheduled start,
run exactly 60 minutes, and preserve a self-contained result directory. Warm-up records
are drained and reconciled but excluded from the comparison interval.

## Comparison contract

Keep these settings identical on both days:

- scenario: current ES future;
- duration: 60 minutes;
- schemas: quote and trade;
- MBO: disabled;
- per-tick CSV capture: disabled;
- Release configuration and live adapter;
- the same clean Git commit; and
- the same workstation, network, Databento account, and Windows power configuration.

MBO and CSV are supported launcher options, but enabling either changes the workload and
invalidates a direct comparison with a run where it is disabled. The default launchers use
the comparison contract above.

Monday and Tuesday will naturally have different market volumes. Do not select an
implementation from raw tick count alone. Give greatest weight to correctness, zero loss,
zero overruns/failures, CPU seconds per million records, managed allocation per million
records, memory stability, ring high-water percentage, channel pressure, and shutdown
reconciliation. The later sustained same-input replay benchmark remains the controlled
throughput comparison.

## Before Monday

Use a PowerShell session with the Databento credential set. The launcher checks that the
working tree is clean because both days must use exactly the same commit.

```powershell
$env:DATABENTO_API_KEY = '<key>'
./scripts/Databento/Run-Monday-CppMarketCloseSoak.ps1 -PreflightOnly
./scripts/Databento/Run-Tuesday-RustMarketCloseSoak.ps1 -PreflightOnly
```

Preflight builds the live adapter and test host, verifies that the staged DLL exactly
matches the selected native build by SHA-256, and writes a manifest. It does not connect to
the live feed. `-AllowDirtyWorkingTree` exists for development diagnostics, but should not
be used for the final comparison.

Confirm that Windows is using Eastern time before both runs:

```powershell
Get-TimeZone
```

The Windows identifier should normally be `Eastern Standard Time`; Windows applies the
active daylight-saving offset automatically.

## Monday: C++

Open PowerShell before 3:00 PM, set the credential, and start the wrapper. Starting it at
approximately 2:45 PM leaves time for the preflight build. It will wait after preflight and
warm the live feed before activating the measured session at 3:00 PM local time.

```powershell
$env:DATABENTO_API_KEY = '<key>'
./scripts/Databento/Run-Monday-CppMarketCloseSoak.ps1
```

## Tuesday: Rust

Use the same procedure and settings on Tuesday:

```powershell
$env:DATABENTO_API_KEY = '<key>'
./scripts/Databento/Run-Tuesday-RustMarketCloseSoak.ps1
```

The weekday-specific wrappers refuse to run the live test on the wrong day. The generic
launcher is available when an explicit date/time or shorter diagnostic run is needed:

```powershell
./scripts/Databento/Run-MarketCloseSoak.ps1 `
  -Implementation Rust `
  -DurationMinutes 5 `
  -StartAt '2026-08-18 15:00'
```

## Evidence produced

Each run writes to
`artifacts/DatabentoMarketCloseSoak/<date-time>-<implementation>-future/`:

- `run-manifest.json`: implementation, commit, dirty state, scheduled time, machine,
  runtime version, native file hashes, and workload switches;
- `preflight.log`: native and managed Release build output;
- `console.log`: detailed test progress and final summary;
- `soak.trx`: test-run result and captured xUnit output;
- `soak-result.json`: structured feed, process, memory, GC, and reconciliation metrics; and
- `run-completion.json`: exit code, completion time, and evidence paths.

The structured result includes produced/consumed/tick counts, quote/trade/MBO counts,
exceptions, ring capacity/high-water, native batches, channel-full and pool-miss counts,
drain allocations, CPU time, average CPU cores, working/private/managed memory, total
managed allocations, GC collections, and optional CSV information.

## Acceptance checks

A passing test already requires:

- at least one tick;
- no recorded exception, unknown instrument, or unknown record kind;
- final native state `Stopped` and terminal status `Ok`;
- an empty native ring and managed channel;
- native produced records equal native consumed records;
- managed tick count equal native consumed records; and
- CSV rows equal consumed ticks when capture is explicitly enabled.

For the final implementation decision, also confirm both manifests name the same Git
commit and workload switches, both completion files report exit code zero, and neither run
reports channel pressure, pool misses, drain-pass-limit hits, or a warning. Normalize CPU
and allocation measurements by `ticks` before comparing because Monday and Tuesday volume
will differ.

After Tuesday completes, generate the normalized comparison. With no arguments the script
selects the latest completed C++ and Rust future runs:

```powershell
./scripts/Databento/Compare-MarketCloseSoak.ps1
```

It rejects mismatched commit, scenario, duration, MBO, or CSV settings, prints the two
normalized summaries, and writes
`artifacts/DatabentoMarketCloseSoak/cpp-rust-market-close-comparison.json`.
