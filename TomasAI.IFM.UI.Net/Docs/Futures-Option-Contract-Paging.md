# Futures Option Contract virtual list

The Market Data Manager's Futures Option Contract editor uses a single-column, double-buffered WinForms `ListView` in virtual mode. It retains the Dark Trading Theme: Microsoft Sans Serif 10 pt, black background, white text and blue selected rows.

## Query contract

The existing `GetFuturesOptionContracts(symbol)` query remains unchanged. Other callers can continue retrieving a complete array.

The new `GetFuturesOptionContractsPage` query is supported by the FuturesOptionContract query actor, HTTP client, NATS client and UI query service:

- Request: `Symbol`, `PageSize` (default 200, allowed 1–1000), optional `ContinuationToken`.
- Response: `Items` (full contract read models), `ContinuationToken`; a null token means the end.
- HTTP: `GET /api/marketdata/futures/option/contracts/page` with escaped query parameters.
- Storage: `futures_option_contract_by_symbol_v2`, partitioned by symbol, ordered by contract month descending, option type, strike price and contract ID ascending.

The storage provider disables automatic fetching and materializes one driver page. Setting a page size on the old collection query alone would not achieve this. No new Scylla table is required.

Continuation tokens are signed and bound to the symbol, requested page size and projection generation. They are valid only for the issuing server process lifetime. Invalid/tampered tokens, changed request scope and stale projection generations are rejected. A server restart requires a fresh first page; horizontally distributed query hosts would need a shared signing key or routing affinity before sharing these tokens.

Each read checks projection readiness before fetching and checks its generation again afterward. An unready projection returns an explicit error; the paged query never scans the base table or repairs a projection. Existing non-paged reconciliation or the Securities symbol-projection backfill must complete before that symbol can be browsed. Projection writers retain their existing generation and completion fencing.

## Editor behavior

- Initial load retrieves the first 200 contracts for the selected symbol.
- Scrolling near the end requests the next page asynchronously. Painting reads cached rows and immediately returns a loading placeholder when necessary; it never performs synchronous database work.
- Already visited rows remain cached until refresh, symbol change or editor close. This is progressive loading, not a bounded sliding cache or a scrollbar over a pre-counted complete dataset.
- Simultaneous scroll requests share one operation. Symbol changes and refresh invalidate old responses; close cancels outstanding operations.
- A failed continuation request attempts one fresh first-page request. Repeated failures stop automatic restarting, retain cached rows and offer double-click retry on the loading row. The list's context menu also provides Refresh.
- Change/Remove require a real selected contract; the loading row cannot be edited or removed. Selection is preserved by contract ID when the cached sequence changes.
- Add/Change/Remove retain their existing correlated command/event workflow. Successful writes restart paging. Saving a contract outside the first page reads additional pages as needed to restore its selection.
- The Symbol combo remains available in view mode for browsing another symbol.

## Verification

- Domain unit tests cover validation before storage, actor dispatch, cancellation forwarding, HTTP escaping and MessagePack query/response round trips.
- Editor unit tests cover first-page-only loading, next-page append/end, duplicate requests, stale in-flight responses, rapid symbol changes, expired cursors, saved-contract restoration and existing mutation correlation/failure handling.
- The Scylla integration test creates and removes its own unique temporary keyspace. It verifies 405 ES contracts across 200/200/5 pages against the legacy full query, token replay/scope checks, cancellation, mutation invalidation, projection readiness and an empty completed catalog. It does not use the fixture that truncates development projection-state tables.
- A real WinForms rendering test verifies initial virtual row count, asynchronous scrolling while the UI remains enabled, selection retention, theme colors and completion without calling the legacy query. Set `IFM_OPTION_PAGING_RENDER_DIR` to save its PNG.

Rebuild and restart both the API server and UI to activate the new endpoint and editor together.

Verification on 2026-09-06: **23 tests passed** (12 editor unit tests, 9 Futures Option actor/query unit tests, 1 Scylla integration test and 1 real WinForms scrolling/rendering test). Views, HTTP/NATS clients, API server and UI application builds completed with zero warnings and errors. Server/UI verification used separate output directories because the running applications lock their normal binaries. A read-only check confirmed the existing ES symbol projection is complete; no application contract data was changed.
