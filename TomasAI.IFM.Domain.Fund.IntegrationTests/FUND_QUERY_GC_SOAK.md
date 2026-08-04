# Fund query GC soak

`FundQueryGcSoakTests` is a manually enabled end-to-end diagnostic hosted by
`TomasAI.IFM.Application.Actor.IntegrationTests`, the current integration-test
runtime source of truth. It repeatedly requests the balance for one seeded Fund,
keeping database and actor cardinality fixed while exercising HTTP, NATS query
ingress, striped dispatch, the actor mailbox, query-context reply handling, and
typed response deserialization.

The report records throughput, total and per-query allocation, Gen0/1/2 counts,
GC pause time, working-set growth, and managed-heap growth after a final compacting
collection. The test fails on any query exception, an incorrect result, zero
completed queries, or retention above the configured limit.

## Run the owned path

```powershell
$env:IFM_RUN_FUND_QUERY_GC_SOAK = '1'
$env:IFM_FUND_QUERY_GC_SOAK_SECONDS = '600'
$env:IFM_FUND_QUERY_GC_REPORT_PATH = 'C:\temp\fund-query-gc-owned.json'
Remove-Item Env:IFM_FUND_QUERY_GC_USE_LEGACY_PAYLOADS -ErrorAction SilentlyContinue
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests\TomasAI.IFM.Domain.Fund.IntegrationTests.csproj `
  --configuration Release `
  --filter FullyQualifiedName~FundQueryGcSoakTests `
  --logger "console;verbosity=normal"
```

## Run the legacy ingress comparison

Run this in a fresh process with otherwise identical settings:

```powershell
$env:IFM_RUN_FUND_QUERY_GC_SOAK = '1'
$env:IFM_FUND_QUERY_GC_SOAK_SECONDS = '600'
$env:IFM_FUND_QUERY_GC_USE_LEGACY_PAYLOADS = '1'
$env:IFM_FUND_QUERY_GC_REPORT_PATH = 'C:\temp\fund-query-gc-legacy.json'
dotnet test TomasAI.IFM.Domain.Fund.IntegrationTests\TomasAI.IFM.Domain.Fund.IntegrationTests.csproj `
  --configuration Release `
  --filter FullyQualifiedName~FundQueryGcSoakTests `
  --logger "console;verbosity=normal"
```

Run each path at least three times and compare medians. The full application
workload includes HTTP, JSON, database, logging, and actor work, so use
`AllocatedBytesPerQuery`, collections/query, and `GcPausePercent` together with
the isolated BenchmarkDotNet results rather than drawing conclusions from one
short run.

## Configuration

| Environment variable | Default | Purpose |
|---|---:|---|
| `IFM_RUN_FUND_QUERY_GC_SOAK` | unset | Must be `1` to execute the manual soak. |
| `IFM_FUND_QUERY_GC_SOAK_SECONDS` | `600` | Measured duration. |
| `IFM_FUND_QUERY_GC_WARMUP_QUERIES` | `100` | Queries before the baseline full GC. |
| `IFM_FUND_QUERY_GC_MAX_QUERIES` | `0` | Optional query cap; zero is unlimited. |
| `IFM_FUND_QUERY_GC_PROGRESS_SECONDS` | `30` | Console progress interval. |
| `IFM_FUND_QUERY_GC_MAX_RETAINED_MB` | `128` | Maximum permitted post-GC heap growth. |
| `IFM_FUND_QUERY_GC_USE_LEGACY_PAYLOADS` | unset | Set to `1` only for the A/B comparison. |
| `IFM_FUND_QUERY_GC_REPORT_PATH` | unset | Optional JSON report path. |

The initial five-second validation completed 1,607 owned-path queries and 1,559
legacy-path queries with no exceptions and no retention-limit failures. That run
only validates the harness; the focused serializer benchmark is the reliable
evidence for the removed boundary allocations.
