# Databento ES Options Included-History Loader Design

**Version:** 1.0  
**Status:** Proposed implementation design  
**Scope:** Initial zero-additional-cost ES futures-option backfill and monthly maintenance  
**Parent design:** `Historical_Market_Data_Backtesting_Archive_Specification_v1.0.md`

## 1. Decision

Implement a dedicated administrative archive loader that acquires the Databento history already
included by the configured account. The loader shall:

- acquire `GLBX.MDP3` ES futures-option data using parent symbol `ES.OPT`;
- acquire `definition`, `status`, `statistics`, `bbo-1s`, and `trades`;
- preserve provider-delivered `DBN.zst` files as the canonical source;
- never load the complete quote archive into ScyllaDB or PostgreSQL;
- reject every request whose immediately preceding Databento estimate is greater than `$0.00`;
- acquire the oldest eligible interval first because the included-history boundary moves forward;
- reconcile existing manifests and acquire only missing or invalid coverage;
- resume provider jobs and file downloads without duplicate submissions; and
- maintain the archive monthly after the initial backfill.

The loader is not part of API startup and shall never delay, stop, or reset the realtime Databento
feed.

## 2. Verified account boundary

Authenticated Databento metadata checks on 2026-09-04 established:

| Item | Verified result |
|---|---|
| Dataset | `GLBX.MDP3` |
| Full accessible dataset range | 2010-06-06 through the current published endpoint |
| L1 included tier | Approximately 12 months |
| L2/L3 included tier | Approximately one month |
| ES option `bbo-1s`, 2025-09-01 | Positive estimated cost |
| ES option `bbo-1s`, 2025-09-02 onward | Zero estimated cost for the probed range |
| Exact zero-cost probe | `[2025-09-02T00:00Z, 2026-09-02T00:00Z)` |
| `bbo-1s` volume for that probe | 646,799,390,400 uncompressed billable bytes |
| `trades` volume for that probe | 73,002,432 uncompressed billable bytes |

The measured BBO input averages approximately 53.9 GB of uncompressed billable data per month.
August 2026 measured 46.2 GB. These are input-size estimates, not expected compressed file sizes.

The dates above are evidence, not permanent configuration. The implementation shall discover the
current boundary using Databento metadata and exact request-cost estimates every time it plans work.
It shall not infer entitlement from a hard-coded number of days or calendar months.

## 3. Data scope

### 3.1 Canonical ES option data

| Schema | Purpose |
|---|---|
| `definition` | Point-in-time expiration, strike, right, instrument and underlying resolution |
| `status` | Trading-state and halt interpretation |
| `statistics` | Exchange settlement and related reference observations |
| `bbo-1s` | Broad-chain bid/ask snapshots for executable-price and spread modelling |
| `trades` | Reported option trades for liquidity and trade-price analysis |

`bbo-1s` plus `trades` is the V1 execution dataset. `mbp-1`, `mbp-10`, and `mbo` are excluded from
the options backfill. They may be added only by a later version with a demonstrated strategy need.

### 3.2 Symbol scope

The initial canonical request uses Databento parent symbology with `ES.OPT`. This preserves the
complete chain and avoids choosing strikes with knowledge of later price movement. Definitions are
archived with every period so historical instrument IDs never depend on current securities data.

If capacity estimates later make full-chain acquisition unacceptable, a new archive version may use
a deterministic universe policy based only on information available at each historical instant. The
initial loader shall not silently filter strikes or expirations.

### 3.3 Futures dependency

Option backtests also require the corresponding ES futures archive for underlying prices, contract
rolls, Intrinsic Time reconstruction, and option-underlying association. That acquisition remains the
parent design's ES futures `definition`, `status`, `statistics`, and `mbp-1` workflow. The option
loader records the required futures archive identity in its manifest but does not duplicate it.

## 4. Coverage policy

The bootstrap target is the maximum contiguous interval that satisfies all of the following:

1. It does not exceed the requested 12-month lookback.
2. Its end is the end of the latest complete CME trading session available from Databento.
3. Every exact schema request has an estimated cost of exactly zero.
4. Required local staging and archive capacity is available.

The range may begin or end with a partial calendar month. The manifest records exact nanosecond UTC
bounds and expected CME sessions. Monthly packages between the two edges remain normal immutable
archive units; partial edge packages are explicitly marked `PartialCoverage` and are never described
as complete calendar months.

After bootstrap, the monthly task archives the immediately preceding complete month while it remains
inside the included L1 window.

### 4.1 Incremental reconciliation

The loader shall calculate required work from verified manifests rather than from the requested
lookback alone:

| Existing archive state | Required action |
|---|---|
| No valid ES options archive | Acquire the maximum current zero-cost interval, up to 12 months |
| Eleven valid initial months and one missing month | Acquire only the missing month |
| Valid historical archive plus one newly completed month | Append only the new month |
| One missing or corrupt daily object | Repair only that object when it is still zero-cost |
| Missing object outside the current included window | Restore from RAID/AWS; do not purchase automatically |
| Entire local archive lost | Reacquire the current zero-cost window and restore older months from backup |

Successfully archived months are permanent and are not deleted when they become older than the
Databento included window. Consequently, monthly incremental acquisition grows the local research
history beyond 12 months without buying older provider history.

Deleting the archive has an irreversible time consequence: Databento can provide only the then-current
included window at zero additional cost. It cannot recreate older locally accumulated months for free.

## 5. Fail-closed cost policy

The command mode `IncludedOnly` has these non-overridable rules:

- `MaximumCostUsd` is exactly `0.00`.
- A positive estimate for any request prevents its submission.
- Estimates are persisted with the exact canonical request hash.
- An estimate must be refreshed immediately before submission; the default maximum age is five
  minutes.
- A refreshed estimate must match the request dataset, symbols, schema, symbology and time range.
- Paid-history override identifiers are rejected in `IncludedOnly` mode.
- The provider job's reported final cost is recorded and must be zero for successful completion.
- A non-zero reported final cost raises a critical billing-policy violation and stops remaining work.

Cost is checked per independently submitted request rather than only for the combined plan. This
prevents a paid oldest day from being hidden inside an otherwise discounted 12-month request.

The byte budget is separate from the cost budget. Free data may still exceed staging, archive, I/O,
or backtest capacity limits.

## 6. Loader workflow

```text
Discover provider range and latest complete CME session
    -> construct candidate 12-month coverage
    -> split into schema/month/day request units
    -> estimate every exact request
    -> trim paid leading intervals
    -> persist immutable zero-cost acquisition plan
    -> acquire oldest unit first
    -> submit or resume Databento batch job
    -> download DBN.zst to attempt-scoped staging
    -> verify size, SHA-256 and complete DBN decode
    -> validate definitions, sessions, timestamps and instrument references
    -> publish atomically into the local immutable archive
    -> finalize manifest and manifest checksum
    -> optionally upload and verify using the parent archive's AWS phase
```

Before each submission, the loader repeats the exact cost estimate. Planning success never grants
permission to submit later without revalidation.

Restart recovery is file-granular. Already verified and published files are skipped. A complete staged
file is validated and reused; an incomplete file is byte-range resumed only when its provider object
identity, ETag/length, and range support still match. Otherwise only that incomplete file is restarted.
The scheduled-task design is authoritative for checkpoint states and crash-window reconciliation.

Databento batch jobs shall request DBN encoding, Zstandard compression, instrument-ID output, and
daily file splitting. Files are downloaded as supplied and are not recompressed or converted before
canonical publication.

## 7. Runtime and persistence ownership

Implement the operator/scheduler entry point as the dedicated one-shot
`TomasAI.IFM.Application.ScheduledTask.HistoricalArchive` console process defined by
`Databento-Historical-Archive-Scheduled-Task-Design-v1.0.md`. It may be launched by Windows Task
Scheduler, a Linux `systemd` timer, the optional Windows IFM Scheduler Host, or an operator command
line. A separate Historical Archive capability host owns the provider work, while PostgreSQL owns the
durable lease, state machine, provider job IDs, checkpoints, and run journal.

Representative commands (the scheduled-task design is authoritative for the full CLI contract):

```text
TomasAI.IFM.Application.ScheduledTask.HistoricalArchive.exe plan --product es-options --lookback-months 12 --included-only
TomasAI.IFM.Application.ScheduledTask.HistoricalArchive.exe bootstrap-included --product es-options --lookback-months 12 --included-only
TomasAI.IFM.Application.ScheduledTask.HistoricalArchive.exe reconcile --product es-options --lookback-months 12 --included-only
TomasAI.IFM.Application.ScheduledTask.HistoricalArchive.exe status --product es-options
```

An executing command is permitted only after the persisted plan passes cost and capacity gates. There
is no command-line switch that converts an `IncludedOnly` run into paid acquisition.

## 8. Archive organization

Canonical objects follow the parent design:

```text
historical/databento/GLBX.MDP3/year=YYYY/month=MM/
  definitions/asset=es-options/
  status/asset=es-options/
  statistics/asset=es-options/
  es-options/schema=bbo-1s/date=YYYY-MM-DD/part-NNN.dbn.zst
  es-options/schema=trades/date=YYYY-MM-DD/part-NNN.dbn.zst
  manifest.json
  manifest.sha256
```

Bulk quotes remain in this archive. Optional Parquet/DuckDB datasets are derived, versioned caches
for research and backtesting. PostgreSQL stores control metadata; ScyllaDB retains only the short
operational window and selected compact projections.

### 8.1 Storage placement and capacity

Canonical DBN.zst objects belong on SATA RAID1, with the independently verified off-site copy defined
by the parent archive specification. NVMe is reserved for attempt-scoped staging, the currently active
backtest working set, and reproducible derived data.

Capacity shall be planned without assuming a compression ratio:

- Reserve at least 1 TB of RAID1 capacity for the initial 12-month ES options archive version. This
  safely covers the measured 646.9 GB uncompressed input plus metadata, validation and growth even if
  compression is less effective than expected.
- Record actual DBN.zst sizes after the one-week pilot and replace this conservative projection with
  a trailing compressed-bytes-per-session forecast.
- Maintain at least 250 GB free on the staging NVMe. The loader processes and publishes one bounded
  month at a time, so NVMe never needs to hold the full 12-month archive.
- Allocate 500 GB to 1 TB of additional NVMe only if the user wants a broad multi-month Parquet or
  DuckDB working set resident locally. That derived cache is optional and evictable.
- Do not decompress complete months to persistent NVMe files. Validation and replay shall stream
  decompression from DBN.zst.

At the measured average input rate, permanent history grows by about 0.65 TB uncompressed-equivalent
per year before provider compression. Actual SATA growth alerts and forecasts use compressed bytes,
not this input estimate.

## 9. Required implementation changes

The existing historical acquisition code is a suitable foundation but is not yet capable of this
options archive. Implementation requires:

1. Add provider-neutral schemas for `BboOneSecond`, `MbpOne`, and `Status` without changing existing
   enum numeric values.
2. Add `Parent` historical symbology and map it to Databento parent symbology in both C++ and Rust.
3. Add the new schemas to the native ABI capability contract and both native implementations.
4. Make batch file split duration explicit and select daily splitting for canonical archives.
5. Keep the current futures bar/trade normalization path intact; do not force BBO records through
   `HistoricalProviderRecord120`.
6. Introduce an opaque canonical-file acquisition boundary for archive download and validation.
7. Add a versioned quote replay record carrying event/receive timestamps, instrument/publisher IDs,
   bid/ask prices and sizes, sequence, and source-file ordinal.
8. Add the archive coordinator, manifest writer, DBN validator, local publisher, PostgreSQL catalogue,
   and administrative CLI described by the parent specification.
9. Move archive/staging roots to explicit absolute configuration; do not use the API binary folder.
10. Ensure archive jobs run independently of the realtime API and Databento lifecycle owner.

## 10. Concurrency and resource policy

- Only one mutating acquisition owns a given product/schema/time-range lease.
- Initial bootstrap runs oldest-first with bounded provider submissions and download concurrency.
- Downloads, hashing and derived-data creation pause or throttle during active trading when resource
  thresholds are exceeded.
- The realtime feed and Market Outlook pipeline have priority over archive work.
- Cancellation checkpoints the current provider job and file state; it does not delete valid staged
  files.

## 11. Acceptance gates

Implementation is acceptable when:

1. A dry run discovers the current included boundary and produces a deterministic plan.
2. A request estimated at `$0.00` can be submitted, resumed, downloaded, decoded and archived.
3. A request estimated at any positive amount is rejected before provider submission.
4. The oldest eligible interval is processed first.
5. A valid 11-month archive plus one absent month submits work only for the absent month.
6. An empty archive plans the current maximum zero-cost interval up to 12 months.
7. A corrupt daily object is repaired without redownloading valid neighboring objects.
8. Duplicate execution reuses completed jobs/files and produces no duplicate archive.
9. One known trading week contains resolvable definitions plus BBO and trade records.
10. A replay can reconstruct the bid/ask available at a chosen ITI signal timestamp without using a
   later record.
11. Repeated replay of the same manifest produces the same ordered-record hash.
12. API/UI startup and shutdown are unaffected while the archive process is stopped, running, or
   interrupted.
13. A complete 12-month included-history run reports provider cost `$0.00` and records actual local
    compressed size.

## 12. Delivery sequence

1. Provider contract and native ABI additions (`bbo-1s`, `status`, parent symbology, daily split).
2. Zero-cost range planner with exact per-request estimate enforcement.
3. Opaque DBN.zst batch acquisition and restartable staging.
4. DBN archive validator and deterministic manifest.
5. PostgreSQL catalogue, leases and reconciliation.
6. Local atomic publication and capacity monitoring.
7. Quote replay reader and one-week deterministic backtest fixture.
8. One-week live-account pilot in `IncludedOnly` mode.
9. Twelve-month bootstrap, oldest interval first.
10. Monthly scheduled maintenance and optional AWS publication/verification.

No implementation phase may submit a paid request as part of this design.
