# Futures contract rollover startup

## Purpose

Market-data workflows must not start without persisted, currently traded futures
contracts. Market-data initialization therefore treats
`futures_contract_rollover` as the master assignment table and validates the
corresponding `futures_contract` rows. Core API, actor, and UI workflows remain
available when that optional initialization is deferred or unavailable.

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

Startup uses the same incomplete-or-due rule. A valid persisted ES or VX
assignment is reused until its rollover date, so an unnecessary historical
provider request cannot prevent an otherwise healthy live feed from restarting.
Before taking that reuse path, the API verifies that the referenced contract row
still exists, matches the root symbol and contract ID, and remains marked as
currently traded. A stale rollover reference is reconciled through the provider
and atomically repaired even when its rollover date has not yet arrived.
Missing and due assignments still block market-data initialization until DataBento
resolves and persists a valid current contract. An operator workflow may explicitly request
`forceProviderRefresh: true` when provider identity must be revalidated early.

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

`FuturesContractRolloverStartupService` runs the following market-data admission
work in the background:

1. creates `futures_contract_rollover` if it does not exist;
2. ensures the ES and VX bootstrap rows exist;
3. reconciles both rows through `IMarketDataApi`;
4. verifies every required row has a contract ID and rollover date; and
5. verifies each ID resolves to a persisted, matching, currently traded
   `futures_contract` row;
6. atomically replaces the ES/VX entries in the runtime DataBento contract
   registry; and
7. starts the value-date market-data epoch from that registry snapshot.

An empty table, unresolved DataBento symbol, missing provider configuration, or a
rollover/contract mismatch prevents market-data admission, but does not prevent
the core application from starting. During a market-open value-date session the
service reports the first failure and retries every minute. During `Closed`, it
stops any active epoch and waits for the next 18:00 Eastern market opening.

The API readiness response includes `market_data_runtime`, but market data is an
optional capability rather than a prerequisite for the rest of the application.
During `Closed`, the entry is healthy when feeds are inactive. During any
market-open value-date session, including off-trading hours, a missing runtime or
stale current-contract route is degraded, not unhealthy, so readiness remains
HTTP 200 and Server Manager can keep the API and UI available. Its data includes
the Eastern market time, whether feeds are expected, configured-contract state,
and source quote/trade counters so operators can distinguish an expected
closed-market state from an open-session feed incident.

The API-owned `IFuturesMarketSessionAuthority` initializes before rollover/feed
supervision and supplies one versioned operational/active value-date snapshot.
The rollover and Databento runtime then initialize as an optional background
service. Starting the API during an open session starts the current value-date
epoch immediately. At each 17:00 Eastern close it stops that epoch; at each
18:00 Sunday-through-Thursday opening it resolves the new value date and starts
the replacement epoch. The 03:00 and 16:00 position-permission boundaries do not
restart or stop the feed. Initialization failure is reported once and retried
every minute without terminating the API or UI.

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
persistence, valid startup assignment reuse, due refresh, return values, and cleanup of its
ES/VX fixture rows after each test.
