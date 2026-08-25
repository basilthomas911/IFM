# ScyllaDB query-projection migration

## Scope

This migration replaces every application-storage `ALLOW FILTERING` read, including the formerly deferred futures ITI
signal queries. The new tables are query-shaped projections; the existing tables remain the canonical source during
deployment, repair, and rollback. `ScyllaCqlPolicyTests` now rejects every application-storage CQL constant containing
`ALLOW FILTERING`.

## New projection groups

| Context | Projection purpose |
| --- | --- |
| Fund | Order-ID lookup, LWT-backed logical transaction identities, transaction timeline, status/day balance, and amount-oriented reads, with per-fund/month cutover state. |
| Securities | Futures and futures-option contract reads partitioned by symbol, with global and per-symbol state. |
| Reference | Economic calendars partitioned by country/month, scheduled jobs keyed by exact name, and scope-specific readiness. |
| MarketData | Tick-time and EOD month projections, a VX contract index, and futures ITI day/month/trend-mode projections, with scoped V3 state and backfill/cutover state. |

### Futures ITI query shapes

| Table | Partition | Purpose |
| --- | --- | --- |
| `futures_iti_signal_by_contract_day` | `(contractId, valueDate)` | Bounded day/mode reads, latest direction/reversal/extreme events, and sequence-after-direction-change reads. |
| `futures_iti_signal_by_contract_month` | `(contractId, yearMonth)` | Symbol/contract date ranges with bounded month fan-out. |
| `futures_iti_signal_by_trend_mode_month` | `(contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth)` | Latest trend/mode discovery without filtering or an unbounded lifetime partition. |

All three tables duplicate the canonical ITI payload so reads do not require joins. A normal insert writes the canonical
row, the existing date index, all three projections, and the ITI month inventory under one scoped mutation fence. A
delete resolves the exact canonical rows first and deletes their complete projection primary keys; it never removes a
whole day or month partition when only one time period was requested.

The former predicted-delta aggregate storage APIs were removed. Their CQL referenced `PredictedDelta`, `FuturesRSI`,
and `FuturesMDI`, none of which exists in the canonical `futures_iti_signal` schema or insert contract, and repository
search found no application caller. Assigning those names to `targetDelta`, `intrinsicPrice`, or another field would
have silently invented financial semantics.

## Deployment sequence

1. Back up the affected keyspaces and record canonical row counts before changing application instances.
2. Apply all additive schema definitions. Do not drop or rename a canonical table.
3. Deploy the dual-write/fallback-capable application version. Until a projection is reconciled and marked complete,
   reads continue to use the canonical path.
4. Run the idempotent backfills in this order:

   - `ReferenceDbContext.BackfillQueryProjectionsV2Async`
   - `SecuritiesDbContext.BackfillSymbolProjectionsAsync`
   - `FundDbContext.BackfillFundOrderByOrderIdProjectionAsync`
   - `FundDbContext.BackfillFundTransactionProjectionsAsync`
   - `MarketDataDbContext.BackfillQueryProjectionsV2Async`

6. Require each backfill result to report a successful reconciliation. Reference and Securities also expose explicit
   reconciliation methods that can be run independently after the backfill.
7. Re-run reconciliation after a representative period of live dual writes. Compare canonical and projection counts,
   keys, and fingerprints where supplied; a count-only match is not sufficient.
8. Only then treat the completion markers as the read cutover. The code changes those markers only after a successful
   rebuild and verification.

Backfills are replayable. They rebuild ordinary target partitions, but uniqueness-reservation projections deliberately
do not delete rows from an online snapshot: such a row can be a live writer between its acknowledged LWT and canonical
write. Fund order projection-only rows are also valid permanent history. Retry the whole failed projection group
instead of attempting a manual partial repair.

## Failure and concurrency behavior

- Framework.Storage uses `LOCAL_QUORUM` for ordinary Scylla reads/writes and `LOCAL_SERIAL` for LWT serial phases, so
  marker, state, and data decisions are not based on a single potentially stale replica.
- Fund transaction and Reference writes publish durable operation markers and make only their affected readiness scopes
  unreadable before mutating canonical and projection rows. Reference calendar scopes are length-prefixed country/month
  keys; scheduled-job scopes use length-prefixed exact names. Disjoint normal writes therefore retain independent
  readiness. Their global journal coordinates whole-projection backfill without forcing unrelated normal reads onto
  fallback. MarketData instead uses V3 tick `(contractId,valueDate)`, EOD `yearMonth`, and VX bucket scopes.
  ITI uses contract/day, contract/month, and contract/trend/mode/month data scopes. Thirty-two stable FNV-sharded guard
  scopes fence MarketData discovery: backfill claims every guard before inventory,
  ordinary writes protect their data scope and guard, and reads validate both. A tick performs four nominal requests:
  a two-statement logged batch durably registers its guard operation, a bounded logged batch atomically changes the
  canonical rows, query rows, and data-scope generation (at most 49 statements), a conditional LWT releases the guard,
  and the durable marker is deleted. Registration precedes the data batch, so a tick already in flight when backfill
  starts cannot be lost to scalar last-write-wins timestamps. This extra request is required because a logged batch that
  spans partitions guarantees eventual all-or-none application, not cross-partition read isolation. Scope stamps require
  an empty active-operation set both before and after the projection read. A known post-commit conflict leaves the exact
  operation active and marks it failed for automatic repair; no tick touches a projection-wide hot row. A failure after
  registration was acknowledged but before any data request is safe to mark failed. In contrast, a registration-batch
  timeout or data-batch timeout remains an unclassified journal row and possible active guard because batchlog replay may
  apply it later; it can be reclaimed only with an explicit stale-operation cutoff after writers are drained. EOD/VX
  writes and backfill follow the same rule once a mutation or target-rebuild request may have been submitted. Backfill
  also retains original journals for any unacknowledged global or scoped `Begin`; acknowledged pre-target failures are
  marked failed without racing a speculative `End`. The current-EOD `<= target` lookup stamps all 32 EOD guards in
  addition to discovered month scopes, so a writer paused before publishing a newly created month to the inventory
  forces canonical fallback. Bulk tick batches sharing one guard run sequentially, while up to eight distinct guards
  run concurrently. Backfill releases data scopes and global state first while its durable global marker still blocks
  readers, then conditionally releases guards before deleting that marker.
- Fund transaction writers publish their fund/month operation marker before resolving an ID. A
  `fund_transaction_identity_v4` LWT then reserves one immutable ID for the complete logical transaction key. Because
  that complete key is the partition key, only true retries contend in Paxos; unrelated transactions for the same fund
  remain concurrent. Reservations intentionally survive canonical deletion so a delete racing an already-reserved
  retry cannot later create a second ID.
- Fund order IDs use `fund_order_by_order_id_v3` as a permanent historical identity registry. A delete removes only the
  canonical order and never releases the reservation, so another fund can never reuse that order ID. Online backfill
  streams canonical rows, inserts missing reservations with `IF NOT EXISTS`, point-checks the exact owner, and then
  stream-counts the registry; its memory use is constant with table size. Projection-only rows are valid history and do
  not fail reconciliation. Missing canonical mappings, conflicting fund owners, and tokenless legacy rows do fail it.
- Reference scheduled-job names remain reusable. Same-owner writes rotate the V3 reservation token; a canonical
  delete/rename is acknowledged before its exact owner-and-token reservation is released. A backfill candidate is
  revalidated against the exact canonical job and compensated only with its own token when the canonical name no longer
  matches. Unexpected or tokenless scheduled-name reservations keep reconciliation incomplete.
- `fund_order_write_ownership_v3` serializes Fund order mutations by order ID. Reference scheduled-job mutations claim
  `scheduled_job_write_ownership_v3` scopes for the job ID and every affected exact name before reading canonical or
  reservation state, then hold them through reservation, canonical mutation, and conditional release. Claims and
  releases are LWTs keyed by an operation UUID. Contenders fail fast; there is no in-process lock, semaphore, or wait.
  An ambiguous post-submission failure retains exact ownership for operator recovery. Known pre-canonical failures
  release it only after any new reusable-name reservation has been exactly compensated.
- Completion is conditional on the same generation still being the sole, conflict-free owner. A superseded or
  overlapping writer/backfill cannot publish an obsolete projection as complete; it deliberately leaves canonical
  fallback enabled for replay.
- A partial write or interrupted backfill leaves the projection incomplete. Reads then use the canonical fallback
  until a successful replay repairs and reconciles it.
- Empty canonical partitions receive completion state too, preventing permanent full-table scans for valid negative
  lookups.
- Reads and writes are streamed/bounded, but some repair inventories retain O(N) identity records to discover stale or
  mis-bucketed partitions. Reference calendar reconciliation includes projected payload fields in those identities;
  size migration workers for that operator-only memory cost.

### Recovering abandoned operations

Backfill APIs accept an optional UTC stale-operation cutoff. Leaving it null never age-clears an operation. Supplying a
cutoff is an explicit operator assertion that every older writer has first been drained or terminated; timestamps are
not leases, and a paused writer could otherwise resume after its marker was removed. With writers quiesced, replay
subtracts and deletes only operation IDs at or before the supplied cutoff, then performs a full rebuild and exact
reconciliation before cutover. Never use the cutoff as an automatic timeout.

Fund and Reference expose this argument after `CancellationToken` as `staleOperationCutoffUtc`. They reject non-UTC or
future values. Recovery first invalidates only scopes recorded by qualifying projection journals, conditionally removes
matching projection ownership, and deletes those exact markers. It also scans the additive mutation-ownership tables
and conditionally releases only `fund_order_write_ownership_v3` or `scheduled_job_write_ownership_v3` rows whose
recorded operation UUID and timestamp qualify. Fund's permanent order-ID reservations are never released by recovery.
A partial recovery therefore stays fail-closed or remains on canonical fallback and is safe to replay.

## API boundary behavior

- `ReferenceDbContext.GetEconomicCalendarsAsync(startDate, endDate, countryCode)` preserves its inclusive public range:
  both endpoints are returned. The single-day overload passes the end of that day, including at `DateTime.MaxValue`.
- Fund starting/ending balances use financial chronology: choose the first/last eligible `ValueDate`, then preserve the
  old minimum/maximum `TransactionId` tie-break within that day. This intentionally corrects global-ID ordering for
  imported or backfilled rows whose explicit IDs are not chronological across value dates. `TransactionDate` is not
  introduced because these APIs historically used ledger IDs; opening/closing APIs own timestamp/status semantics.
- Fund identity backfill groups a complete month before reservation and selects the minimum canonical `TransactionId`
  for each logical key, independent of scan or page order. It uses `IF NOT EXISTS`, so it never overwrites an identity
  selected by a live post-deployment writer. Missing/conflicting identities and legacy canonical duplicates are
  reported; any such mismatch keeps the affected month incomplete and on canonical fallback. Duplicate canonical rows
  must be repaired explicitly before rerunning cutover.

## Operator CLI

`TomasAI.IFM.Application.Storage.ProjectionMigration` is the executable storage-operations entry point for these backfills. Each
command reads a credential-free connection string from its own environment variable. The Scylla userid/password JSON
continues to come from the Framework.Storage credential variable selected by `DOTNET_ENVIRONMENT`:

| `DOTNET_ENVIRONMENT` | Credential variable |
| --- | --- |
| `Development` | `SCYLLADB_DEV_KEY` |
| `Test` | `SCYLLADB_TEST_KEY` |
| `Staging` | `SCYLLADB_STAGING_KEY` |
| `Production` | `SCYLLADB_PROD_KEY` |

The credential value has the shape `{"userid":"<userid>","password":"<password>"}`. Do not put either field in a
connection string or command-line argument. Set the four base connections as required for the migration environment:

~~~powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:SCYLLADB_PROD_KEY = '{"userid":"<userid>","password":"<password>"}'

$env:IFM_STORAGE_MIGRATION_REFERENCE_SCYLLA_CONNECTION = 'Contact Points=<hosts>;Port=9042;Default Keyspace=<reference-keyspace>'
$env:IFM_STORAGE_MIGRATION_SECURITIES_SCYLLA_CONNECTION = 'Contact Points=<hosts>;Port=9042;Default Keyspace=<securities-keyspace>'
$env:IFM_STORAGE_MIGRATION_FUND_SCYLLA_CONNECTION = 'Contact Points=<hosts>;Port=9042;Default Keyspace=<fund-keyspace>'
$env:IFM_STORAGE_MIGRATION_MARKET_DATA_SCYLLA_CONNECTION = 'Contact Points=<hosts>;Port=9042;Default Keyspace=<market-keyspace>'
~~~

If `ASPNETCORE_ENVIRONMENT` is also set in the operator shell, it must select the same environment as
`DOTNET_ENVIRONMENT`; Framework.Storage rejects conflicting environment selectors.

Run the commands from the repository root. `--apply-schema` creates only this migration's additive projection,
state, ownership, and operation-journal objects, including the three ITI projection tables, the bounded economic
calendar projections/catalogs, and the yield-curve ordered-date/year projections; it does not alter or truncate
canonical tables. It is safe to repeat because each selected definition uses
`CREATE ... IF NOT EXISTS`.

~~~powershell
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- reference --apply-schema --batch-size 256
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- securities --apply-schema --batch-size 256
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- fund --apply-schema --fund-id <fund-id> --start-date <yyyy-MM-dd> --end-date <yyyy-MM-dd> --batch-size 500
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- market --apply-schema --batch-size 256
~~~

Before running the `market` command, pause both economic-calendar and yield-curve import writers. The command truncates
only their derived query projections, streams the canonical `economic_calendar` and `yield_curve_rates` sources in
bounded batches, rebuilds the projections, and compares source/target row counts and order-independent fingerprints.
Do not resume either importer unless the FMP query reconciliation reports success. Runtime application reads contain
no canonical full-scan fallback, so deploying the new reader before this successful schema/backfill step would expose
incomplete lookup results for pre-existing data.

The Fund command rebuilds and verifies the global order-ID lookup before rebuilding the selected fund's complete
transaction months. It also reserves and verifies one immutable transaction identity for every canonical logical key;
the summary reports logical/reserved, missing/conflicting, and duplicate-canonical counts. Repeat it for every fund and
date span requiring migration. The CLI prints only non-secret row, fingerprint, readiness, and reconciliation summaries.
Exit code `0` means reconciled and ready, `3` means a detected
mismatch or incomplete cutover, `2` means invalid command/configuration input, `1` means an execution failure, and
`130` means cancellation.

For abandoned-operation recovery, first drain and prevent every affected writer from restarting. Then supply both
the explicit UTC cutoff and the confirmation flag; the CLI rejects either option by itself:

~~~powershell
dotnet run --project TomasAI.IFM.Application.Storage.ProjectionMigration -- securities --batch-size 256 --stale-operation-cutoff-utc 2026-08-03T14:30:00Z --confirm-writers-drained
~~~

Use the same guarded pair with `reference`, `fund`, or `market` when that context needs recovery. Never script a
moving cutoff such as the current time, and do not use the confirmation flag while writers can still resume.

## Rollback

Rolling back the application binary is safe because canonical tables are retained and older reads do not depend on the
new projections. Leave projection tables in place during rollback; dropping them while any new binary is still running
would turn a recoverable fallback into an application error.

Projection tables may be removed only after all application versions that reference them have been retired and the
rollback window has closed.

## Post-deployment checks

- Run the non-live CQL policy and positional-binding tests.
- Run Fund, Securities, Reference, and MarketData integration suites against the dedicated ScyllaDB test keyspace.
- Verify a projection miss returns canonical data while its state is incomplete.
- Verify a stale or mis-bucketed target row is removed by replay and detected by reconciliation.
- Verify Reference IDs are allocated through the PostgreSQL sequence service.
- Run the Framework.Storage ScyllaDB and PostgreSQL BenchmarkDotNet suites under the same host/database conditions used
  for the baseline comparison.
- For the production-shaped ITI benchmark, require identical logical maxima before measurement and compare the
  canonical filtered query with the bounded trend/mode/month projection at 4,096 and 32,768 canonical rows.
