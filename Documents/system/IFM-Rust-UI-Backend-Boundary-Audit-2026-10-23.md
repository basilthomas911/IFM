# Current Rust UI: backend boundary audit

Date: 2026-10-23. Application source baseline: `ee157c454`.

## Conclusion

The full source inventory found additional active financial transformations beyond `SelectedOrderPriceRisk.MaximumOptionLoss()`. The earlier narrow conclusion is superseded by this audit.

Keep the current Rust UI as the rendering and behavior baseline. Before implementing its standalone client, close the seven backend boundary groups below. Several calculations already exist on the backend: reuse those implementations and return their results rather than copying another engine. Request preparation, form validation, selection, receipt reconciliation, formatting and rendering remain portable Rust client work.

This is an analysis and implementation backlog. No application code, database, running process or feed was changed, and no live order was submitted.

## Inventory and method

| Primary source project | Tracked C# source files |
| --- | ---: |
| UI.Net.ViewModels | 81 |
| UI.Net.Models | 15 |
| UI.Net.Services | 52 |
| **Total** | **148** |

The [complete file inventory](IFM-Rust-UI-Backend-Boundary-Inventory-2026-10-23.csv) has one row for every primary source file: decision, finding IDs, reference evidence, calculation/clock locations and review notes. The broader [model inventory](IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.md) and [dependency inventory](IFM-Windows-Rust-UI-Dependency-Inventory-2026-10-23.csv) provide supporting consumers, clients and bridge sources.

Method: census all tracked files in the three projects; scan executable identifiers and arithmetic, aggregation, computation, validation and clock usage; review the financial/policy candidates and their Rust bridge entry paths; follow shared helpers that actually execute inside the UI. Inspect backend preview, realtime and ledger implementations to distinguish missing client operations from already implemented server calculations. Selected bridge and Rust rendering calculations are included below because moving only view-model files would miss them.

The CSV reference paths are conservative identifier dependency paths, including interfaces and service catalogs. They are not semantic method call graphs or runtime execution evidence. In particular, a catalog reference to the pricing interface does not establish that Rust executes its pricing method. Manually traced method/branch evidence below takes precedence. Reflection and future entry paths must still be checked at cutover. No runtime certification or claim that every method in a referenced class executes is implied.

Regenerate the file checklist after changing the reviewed rules:

```powershell
python scripts/UI/Rust/Inventory-UiBackendBoundaries.py
```

## Backend prerequisites

### B1. Prepare the financial order candidate on the backend

**Current Rust path:** `TradeComposerSession` constructs `BrokerManualTradeOrderViewModel`; its `submit-order` intent calls `SubmitAsync`, which calls `SubmitCoreAsync` and `CreateCandidateAsync`.

Sources:

- [BrokerManualTradeOrderViewModel.cs](../../TomasAI.IFM.UI.Net.ViewModels/Trade/BrokerManualTradeOrderViewModel.cs): `CreateCandidateAsync`.
- [SelectedOrderPriceRisk.cs](../../TomasAI.IFM.UI.Net.ViewModels/Trade/SelectedOrderPriceRisk.cs): `MaximumOptionLoss`.

Local work includes maximum option payoff loss, `Abs(limit * quantity * multiplier)` notional, `Max(notional, calculated loss)`, assigning required capital / maximum loss / stress loss, selecting effective deployment and horizon by local UTC, hardcoded 0.25/0.05 tick increments, ten-tick minimum-price bands and manual execution-profile metadata. Moving the payoff helper alone leaves those financial and policy decisions in the client.

**Backend target:** an order preparation operation in Domain.Trade, using resolved instrument metadata and existing backend preview/payoff computation; Portfolio retains financial admission/capacity authority. Resolve the deployment through the appropriate server Portfolio/reference services. Return a prepared candidate/reference with price bounds, tick, risk inputs, effective context, revision/expiry and evidence identity. Bind confirmation/submission to the same prepared selection and input revision; validate again at authoritative submission. Do not silently equate a payoff maximum loss with the existing capital/stress policy: explicitly preserve or revise that policy under tests.

Existing [TradeOrderPreviewCompute](../../TomasAI.IFM.Domain.Trade/Order/Query/Model/TradeOrderPreviewCompute.cs) already computes option payoff limits. Extending this flow or adding a separate prepare operation is an implementation choice; a second local payoff implementation is unnecessary.

### B2. Return order confirmation amounts and fees from the backend

**Same active submission path:** `SubmitCoreAsync` computes cash amount, USD 0.65 per leg contract, total commission and cash plus commission for confirmation. The preview screen currently says fee schedule unavailable while confirmation assumes an emulator fee.

**Backend target:** include cash amount, per-leg and total estimated fees and final confirmation totals in B1 preparation, resolved for the actual broker/account/environment and fee schedule. The UI displays the estimate and its provenance; actual execution fees remain execution/ledger evidence. Preserve signed debit/credit conventions. An emulator assumption must not become a general broker fee schedule.

### B3. Calculate fund performance metrics on the backend

**Current Rust path:** [PortfolioSession](../../TomasAI.IFM.UI.Rust.Bridge.Net/PortfolioSession.cs) owns `FundMetricsViewModel` and displays its report.

[FundMetricsViewModel.Calculate](../../TomasAI.IFM.UI.Net.ViewModels/Portfolio/FundMetricsViewModel.cs) currently pages all fund transactions, filters the requested accounting-date range locally, then computes win/loss rates, average gains/losses, win/loss ratio, realized P&L, P&L percent, commissions, Sharpe and drawdown. No matching fund-metrics operation was found in the inspected Portfolio domain/shared API/client sources.

**Backend target:** a bounded date-range Portfolio financial metrics query, calculated against canonical ledger data with a consistent revision and defined accounting/return conventions. Return report, range, revision and availability information. The current Sharpe applies `sqrt(252)` to realized-posting observations, not explicitly daily returns; the current drawdown mixes cash movements and realized outcomes. Confirm metric definitions during implementation rather than promoting those labels to mathematical guarantees. Avoid downloading an entire ledger to render one report.

### B4. Stop recalculating financial realtime summaries inside the UI cache

**Current Rust path:** `IronCondorViewModel` owns [TradeRealtimeHotCache](../../TomasAI.IFM.Domain.Trade.Shared/Trade/Realtime/TradeRealtimeHotCache.cs). `ApplyRealtimeSnapshot` calls `AcceptSnapshot`; that method calls `Qualify`. Consequently, receiving a server snapshot does not mean that the UI only copies numeric fields.

[TradeRealtimeSnapshotCalculations](../../TomasAI.IFM.Domain.Trade.Shared/Trade/Realtime/TradeRealtimeSnapshotCalculations.cs) contains:

- `Qualify`: quantity-adjusted delta/gamma/vega/theta/rho and currency position value. This executes on accepted remote snapshots as well as local cache updates.
- `CalculateSpreads`: net spread, spread currency value and P&L, combined net spread, and `1 - Abs(short-leg delta)` OTM estimate. These execute on local `Publish` paths, including fallback position/plan/quote updates and expiry processing; not on every `AcceptSnapshot` call.

**Backend target:** keep financial calculations in the existing backend realtime registry/cache and publish complete summary/spread values. Use a Rust client mirror that retains immutable server values, identity/revision fencing and visual freshness/availability metadata without recalculating money or Greeks. Do not delete shared backend calculations simply because the same class currently executes in the UI. Preserve last valid numeric values and their stale/pending indicators. Current forward-price amplification already remains backend-owned.

### B5. Return valued saved-position/history rows from the backend

**Current Rust path:** `TradeWorkspaceSession` loads established trades and invokes `LoadSavedPositionDataAsync` for saved history selections.

In [IronCondorViewModel](../../TomasAI.IFM.UI.Net.ViewModels/Trade/IronCondor/IronCondorViewModel.cs):

- `LoadSavedPositionDataAsync` synthesizes a missing opening snapshot from original fills and computes weighted fill prices.
- `PublishPositionHistory` calculates per-unit signed combined price from current leg prices/quantities.
- `CreateOpeningPosition` copies opening values but derives its date from the establishment timestamp; history also has timestamp-derived date fallbacks.

**Backend target:** enrich the strategy position/history query with execution-backed opening snapshots, valued history rows, explicit currency/points, quantities, expiry, days-to-expiry and authoritative trading ValueDate. Preserve fill and position sequence provenance. The client selects, sorts and formats these rows. Do not recompute old P&L from incomplete UI history or fabricate missing valuations as zero.

The older `_optionTrade` branch has other calculations (daily target, legacy spread/P&L handling). The current established-monitor branch returns before enabling those legacy listeners/target updates. Those methods are not automatically additional standalone requirements.

### B6. Return financial market/context summaries instead of transforming them in the client

[TradeComposerSession.MarketMetrics](../../TomasAI.IFM.UI.Rust.Bridge.Net/TradeComposerSession.cs) is active in composer snapshot rendering. It selects the nearest valid IV strike, averages IV at that strike, calculates daily underlying price/percentage change and days to expiry. This is financial summary preparation even though it is outside the view-model projects. Its tick caption is also hardcoded.

[BrokerTradePreviewPresentation](../../TomasAI.IFM.UI.Net.ViewModels/Trade/BrokerTradePreviewPresentation.cs) sums fund loss-charge capacity `Held + Working` for its reserved-capital display.

The market-outlook constructors in [FuturesEodDataUIViewModel](../../TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesEodDataUIViewModel.cs) and [FuturesTradeSignalUIViewModel](../../TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesTradeSignalUIViewModel.cs) also derive a clamped MDI percentage from Bollinger `Position20`; the latter classifies it as UpTrending/DownTrending/RangeBound using local 60/30 thresholds. Return that indicator value/classification from the server summary. Color choice and decimal/percentage formatting may stay local. These helpers do not independently calculate Bollinger bands, EMA or RSI.

**Backend target:** add these named, unit-defined values to the evaluated-chain/preview/context results, reusing existing source data. Keep number/percentage formatting, quote-age text and chart geometry in Rust. This follows the requested rule that data transformation belongs on the backend; these small operations are not inherently impossible to port to Rust.

### B7. Make business dates, session eligibility and history partitioning server-owned

Sources and active uses:

- [TradeMonitoringPolicy.CanStart](../../TomasAI.IFM.UI.Net.Models/TradeMonitoringPolicy.cs), called by `IronCondorViewModel.CanEnableLiveFeed`, derives session availability from local time and `FuturesTradingValueDate`.
- [PositionEntryWindow](../../TomasAI.IFM.UI.Net.Models/PositionEntryWindow.cs), used by `TradeOrderEditorViewModel`, derives entry eligibility through the shared market-session policy.
- [MarketDataFeedQueryService.GetFuturesBarWindowAsync](../../TomasAI.IFM.UI.Net.Services/MarketDataFeed/MarketDataFeedQueryService.cs), used by the app/market-chart sessions, enumerates database partition dates and an 18:00 rollover locally.
- `BrokerTradeInitializationQuery`, `PortfolioAdministrationViewModel` and candidate preparation pass local `DateTime.UtcNow` into business-effective queries or selection. `PortfolioSession` also initializes report date filters from the local wall clock.

**Backend target:** return ValueDate, business as-of instant and market/session eligibility through server context; expose timestamp-range market-history queries that select partitions on the server. Use these values for preparation and operational enablement, including backtesting. UI timers, elapsed-time diagnostics and optional user-entered report dates remain client concerns. A local `TimeProvider.System` replacement would not by itself establish server clock authority. This is boundary work, not a new pricing algorithm.

## Code that can stay client-side and be ported to Rust

### C0. Presentation, models and API adapters

The remaining file rows cover DTOs, observable state, editors, lifecycle/disposal, query orchestration, error mapping, subscriptions, stable selection, pagination and formatting. No additional financial engine was identified by the source review in these files. Their backend API counterparts and wire types still need direct Rust clients; that port is not itself moving computation to the server.

### C1. User choices and visual hints

Leg-role normalization, buy/sell selection, closing-type selection, UI button rules, enum/text/color mappings, identity format checks and lookup display ordering can be Rust functions. Submitted contracts/actions remain server-validated. `PortfolioFundOrderTradeEditorModel.GetContractIds` parses a human reference into contract IDs; prefer API-resolved exact IDs, but do not confuse this convenience parser with a valuation engine or require it to be a separate backend service.

`StatusConsoleViewModel.BuildTradeStatus` formats server-supplied trend/execute-state values. It is not an independent trading decision algorithm.

The current Rust [trade P&L bar](../../TomasAI.IFM.UI.Rust/src/trading/trade_workspace/iron_condor.rs) divides the received P&L by the received matching maximum gain/loss to determine the drawn fill. This may remain visual normalization, together with number formatting, axis scaling and increase/decrease colors. It must not set financial risk limits or feed execution decisions. If all displayed financial percentages must also be server-returned, include the bar percentage in B4; moving drawing logic is unnecessary.

### C2. Financial request preparation and journals

`FinancialControlPreparation`, `FinancialPostingPreparation`, pending-request stores and posting/configuration operation trackers can be ported. They assemble operator input, expected revisions, IDs and hashes, provide early feedback, and reconcile backend receipts. Existing ledger services retain posting/configuration validation, journal construction and transactional persistence. No new backend equivalent is required solely because the UI used C# helpers.

Preserve decimal precision, canonical JSON/hash behavior (independent of MessagePack transport), journal compatibility, operation identities, ambiguous outcomes and API-owned business dates. Request hash construction is protocol compatibility work. Backend validation remains mandatory even where the client mirrors a check.

### C3. Existing backend preview

`BrokerTradePreviewQuery` already calls `TradeQueryService.QueryTradeOrderPreviewAsync` through the NATS API to `TradeOrderQueryActor`, `GetTradeOrderPreview` and `TradeOrderPreviewCompute`. Net bid/ask/mid, quantity-adjusted preview delta/vega, widths, spread values, order-price rounding and payoff estimates already run on the backend. Reuse this flow for B1/B2/B6 instead of implementing another Rust pricing calculator.

## L1. Explicitly excluded WinForms-only pricing path

`IronCondorTradeOrderViewModel.CalculateTradeValues` calls `OptionPricingService.CalculateGreeks`, which runs `OptionCalculator.ImpliedVolatility` locally. That model also calls the loss helper for its own candidate preparation. The current Rust composer constructs `BrokerManualTradeOrderViewModel` instead. Do not add a backend clone of the unused WinForms entry model for this migration. Catalog registration and a shared service interface are not evidence of method execution.

`MarketDataQueryService.GetRiskFreeRateAsync` also converts the returned one-month yield from percent to fraction for older Iron Condor paths. This is not a reason to introduce a new standalone local pricing dependency; the active backend pricing flow owns its required rate inputs and units.

Likewise, legacy methods inside the otherwise active `IronCondorViewModel` require branch-level analysis; an active class does not make every historical path active.

## Implementation sequence and verification gates

1. **B1+B2:** server order preparation, resolved fee/tick/policy metadata, canonical candidate identity; wire current composer to that result. Test debit/credit, quantity/multiplier scaling, unbounded/incomplete inputs, account/environment fees, expiry and changed selections. Verify existing risk/capital policy explicitly.
2. **B4+B5:** consume complete backend realtime and saved-history projections. Test quote/position/plan revisions, incomplete and stale snapshots, quantity changes, partial fills and opening-history fallback. Assert the client does not recalculate currency values or Greeks.
3. **B3:** server metrics query with bounded accounting dates and defined return/drawdown semantics. Test deposits, withdrawals, commissions, realized postings, no history and stable revision/paging behavior.
4. **B6+B7:** server summary/context/range operations; test ValueDate rollover, weekend/DST boundaries and a server-controlled date different from the workstation clock. Verify history is bounded and merged with live data without being cleared.
5. Port C0-C3 behavior, protocol adapters and journals to Rust; exclude L1. Use cross-language fixtures for decimal/hash/wire/journal compatibility and integrated backend tests for prepare-confirm-submit and receipt reconciliation.

The analysis is complete as a tracked-file census and reviewed boundary inventory. The backlog above is not implemented or runtime-verified by this audit. Repeat source and integrated checks as standalone features are cut over.
