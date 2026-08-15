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
   `futures_contract` row.

Any failure propagates from `StartAsync` and prevents application startup. An
empty table, unresolved DataBento symbol, missing provider configuration, or a
rollover/contract mismatch is therefore a startup error rather than a degraded
trading state.

## Test coverage

The market-data unit suite verifies nearest-maturity selection, ES and VX
dataset routing, and the typed no-contract failure. The Scylla integration suite
verifies bootstrap insertion, DataBento-resolution substitution, atomic
persistence, startup validation, return values, and idempotent second startup.
