# Futures contract rollover startup

## Purpose

The application must not start actor and trading workflows without persisted,
currently traded futures contracts. Startup therefore treats
`futures_contract_rollover` as the master assignment table and validates the
corresponding `futures_contract` rows before later hosted services start.

## Master table

`futures_contract_rollover` is keyed by `symbol` and contains:

- `symbol text` (primary key)
- `contractId text` (nullable during bootstrap)
- `nextRolloverDate date` (nullable during bootstrap)
- `updatedOn timestamp`, `updatedBy text`
- `createdOn timestamp`, `createdBy text`

Startup creates the table additively and inserts missing bootstrap rows for
`ES` and `VX`. The inserts use `IF NOT EXISTS`, so they never erase operator or
rollover-workflow changes.

## Reconciliation rule

`IMarketDataApi.UpdateCurrentlyTradedFuturesContractAsync(symbol, valueDate)`
queries DataBento only when:

- `contractId` is empty;
- `nextRolloverDate` is empty; or
- `valueDate >= nextRolloverDate`.

The startup reconciliation passes `forceProviderRefresh: true`, so every host
start also revalidates the current ES and VX assignments against DataBento.
This repairs a stale provider identity or a changed nearest eligible contract
before the market-data epoch is admitted. Ordinary callers retain the
incomplete-or-due behavior above.

The argument is a futures root symbol, not a contract ID, because an incomplete
bootstrap row has no contract ID yet. The resolver queries `<symbol>.FUT`, keeps
futures whose maturity is at least `valueDate + 1 day`, and selects the nearest
eligible maturity. Until a separate exchange-calendar rollover policy is
implemented, the DataBento maturity/expiration date is persisted as
`nextRolloverDate`.

The currently traded contract, its symbol projection, removal of the previous
current assignment, and the rollover-row update are submitted as one logged
Scylla mutation. The API returns `true` only when the next-rollover date is
first populated or changes. It returns `false` when the row is not due or a due
resolution leaves the date unchanged.

ES uses the configured default DataBento dataset (normally `GLBX.MDP3`). VX
uses `XCBF.PITCH`. `DatabentoMarketDataRuntimeOptions.FuturesContractDatasets`
can override the dataset per root symbol.

## Startup admission

`FuturesContractRolloverStartupService` runs before the existing trading hosted
services. It:

1. creates `futures_contract_rollover` if it does not exist;
2. ensures the ES and VX bootstrap rows exist;
3. reconciles both rows through `IMarketDataApi`;
4. verifies every required row has a contract ID and rollover date; and
5. verifies each ID resolves to a persisted, matching, currently traded
   `futures_contract` row;
6. atomically replaces the ES/VX entries in the runtime DataBento contract
   registry; and
7. starts the value-date market-data epoch from that registry snapshot.

Any failure propagates from `StartAsync` and prevents application startup. An
empty table, unresolved DataBento symbol, missing provider configuration, or a
rollover/contract mismatch is therefore a startup error rather than a degraded
trading state.

## Runtime contract registry and datasets

`DatabentoContractRegistrationRegistry` is the runtime source of truth for new
market-data epochs. Rollover reconciliation replaces registrations by futures
root symbol, which removes a stale ES or VX assignment while preserving
explicit registrations for unrelated roots and options. Each epoch snapshots
the registry when it is created, so an in-flight epoch cannot observe a partial
rollover mutation.

The same atomic registry state retains the full startup-validated current
futures contract for each reconciled root. Domain clients use
`IMarketDataApi.TryGetCurrentlyTradedFuturesContract(symbol, out contract)` to
read this state without querying Scylla or DataBento. Realtime signal handlers
therefore resolve ES/VX identity from the rollover source of truth without
placing storage work on the per-tick path.

A registration carries its DataBento dataset. One logical `IMarketDataApi`
epoch partitions provider queries, ticker feeds, and tick aggregation by
dataset, while sharing the domain contract catalog, hot-price store, live
router, and public API. This is required because ES normally uses `GLBX.MDP3`
and VX uses `XCBF.PITCH`. Contract stream ownership and hot-price access remain
keyed by the domain contract ID and are routed to the correct dataset partition
internally.

`AppSettings:Databento:Contracts` remains an explicit bootstrap/override input
for unrelated contracts. Validated rollover assignments replace any configured
ES/VX futures entries before the epoch starts; therefore appsettings is no
longer authoritative for those current contracts.

## Test coverage

The market-data unit suite verifies nearest-maturity selection, ES and VX
dataset routing, and the typed no-contract failure. The Scylla integration suite
verifies bootstrap insertion, DataBento-resolution substitution, atomic
persistence, startup provider revalidation, return values, and cleanup of its
ES/VX fixture rows after each test.
