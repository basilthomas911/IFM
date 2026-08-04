# Fund command GC soak

`FundCommandGcSoakTests` is a manually enabled end-to-end diagnostic. It hosts
the application with `TomasAI.IFM.Application.Actor.IntegrationTests`, which is
the current integration-test runtime source of truth. It does not use
`TomasAI.IFM.Application.Api.Server`.

The test creates and projects one real Fund, verifies a Fund query, warms the
runtime, and then repeatedly submits a 4 KB `CreateFundCommand` for that same
Fund. The expected "already exists" response keeps actor and stream cardinality
fixed while exercising HTTP, NATS command ingress, striped dispatch, the actor
mailbox, direct command deserialization, state loading, command logging, and the
reply path. Periodic Fund queries verify the query actors during the soak.

The final JSON report contains:

- total and per-command allocated bytes;
- Gen0, Gen1, and Gen2 collection counts;
- cumulative GC pause duration and percentage of elapsed time;
- managed heap before and after an explicit final compacting collection;
- retained heap and working-set growth;
- command throughput, query count, and exceptions.

## Run the optimized owned-memory path

Use Release configuration for comparison runs:

```powershell
$env:IFM_RUN_FUND_GC_SOAK = '1'
$env:IFM_FUND_GC_SOAK_SECONDS = '600'
$env:IFM_FUND_GC_REPORT_PATH = 'C:\temp\fund-gc-owned.json'
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests\TomasAI.IFM.Domain.Fund.IntegrationTests.csproj `
  --configuration Release `
  --filter FullyQualifiedName~FundCommandGcSoakTests `
  --logger "console;verbosity=normal"
```

## Run the legacy `byte[]` comparison

Run this in a fresh test process with otherwise identical settings:

```powershell
$env:IFM_RUN_FUND_GC_SOAK = '1'
$env:IFM_FUND_GC_SOAK_SECONDS = '600'
$env:IFM_FUND_GC_USE_LEGACY_COMMAND_PAYLOADS = '1'
$env:IFM_FUND_GC_REPORT_PATH = 'C:\temp\fund-gc-legacy.json'
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests\TomasAI.IFM.Domain.Fund.IntegrationTests.csproj `
  --configuration Release `
  --filter FullyQualifiedName~FundCommandGcSoakTests `
  --logger "console;verbosity=normal"
```

Clear `IFM_FUND_GC_USE_LEGACY_COMMAND_PAYLOADS` before running the optimized
case. Run each case at least three times and compare medians. The important
normalized fields are `AllocatedBytesPerCommand`, collections per command, and
`GcPausePercent`; throughput and retained heap must not regress.

## Configuration

| Environment variable | Default | Purpose |
|---|---:|---|
| `IFM_RUN_FUND_GC_SOAK` | unset | Must be `1` to execute the manual soak. |
| `IFM_FUND_GC_SOAK_SECONDS` | `600` | Measured duration. |
| `IFM_FUND_GC_WARMUP_COMMANDS` | `25` | Commands before the baseline full GC. |
| `IFM_FUND_GC_MAX_COMMANDS` | `0` | Optional command cap; zero is unlimited. |
| `IFM_FUND_GC_QUERY_EVERY` | `100` | Issue a real Fund query every N commands; zero disables periodic queries. |
| `IFM_FUND_GC_PAYLOAD_CHARACTERS` | `4096` | Deterministic, poorly compressible Fund description size. |
| `IFM_FUND_GC_PROGRESS_SECONDS` | `30` | Console progress interval. |
| `IFM_FUND_GC_MAX_RETAINED_MB` | `128` | Maximum permitted post-GC managed-heap growth. |
| `IFM_FUND_GC_USE_LEGACY_COMMAND_PAYLOADS` | unset | Set to `1` only for the A/B legacy comparison. |
| `IFM_FUND_GC_REPORT_PATH` | unset | Optional JSON output path. |

The manual test cleans up its Fund and event stream. It fails on unexpected
command/query results, exceptions, zero completed commands, or retained heap
growth above the configured limit.

## Initial verification sample

The implementation was sanity-checked in two fresh Release/server-GC processes
with 500 commands, five queries, a 25-command warmup, and a 4,286-byte serialized
command. This short sample validates that the A/B switch and counters work; use
the ten-minute, three-run procedure above for a decision-quality result.

| Path | Allocated/command | GC pause | Gen0 | Exceptions |
|---|---:|---:|---:|---:|
| Legacy `byte[]` | 235,964 B | 8.282 ms | 1 | 0 |
| Owned pooled payload | 228,442 B | 7.567 ms | 1 | 0 |

The owned path avoided 7,522 allocated bytes per end-to-end command in this
sample (3.2% of total application allocation) and reduced measured GC pause by
8.6%. Total application allocation includes HTTP, JSON, database, logging,
query, and actor-state work, so this percentage is intentionally broader than
the isolated serializer benchmark.
