# Current Rust UI backend processing implementation plan

Date: 2026-10-23. Status: B1-B7 implemented; automated verification recorded below; complete live-screen acceptance remains open.

## Objective and scope

Move the financial calculations and business-policy transformations identified in the [backend boundary audit](IFM-Rust-UI-Backend-Boundary-Audit-2026-10-23.md) into the backend, wire the current hybrid Rust UI to consume those results, and verify that application before starting the standalone Rust cutover.

The baseline is the existing Rust UI and its active managed view models, models, services and bridge sessions. The [150-file working-tree checklist](IFM-Rust-UI-Backend-Boundary-Inventory-2026-10-23.csv) is the source-level closure register. Track actual methods and branches: an active class or service catalog does not make all its legacy methods active.

Rendering, editable drafts, selection, input-shape checks, formatting, visual normalization, API orchestration, subscriptions and pending-operation journals remain client responsibilities. Financial request construction and canonical hashes can remain client-side. Unused WinForms-only pricing paths are excluded. This plan does not implement standalone Rust, full backtesting, or change the forward-loss amplification formula, financial admission policy or recovery deadlines.

## Actor and API design

Reuse existing actors. No new actor or process is required by this plan. Add focused domain calculation/preparation helpers and query handlers behind the current actors. The operations below are implemented; the final section records verification and remaining gates.

| Finding | Existing owner | Planned operation/result |
| --- | --- | --- |
| B1 order candidate preparation | `TradeOrderQueryActor`; existing Portfolio services provide effective fund/deployment context | Add `PrepareTradeOrder` query, reusing backend payoff computation and resolved contract/policy data |
| B2 confirmation totals | Same order preparation handler | Include signed cash amount, per-leg/total fee estimate, currency and confirmation totals |
| B3 fund metrics | `GeneralLedgerQueryActor` and financial query store | Add `GetFundPerformanceMetrics` query with inclusive accounting-date range and consistent ledger revision |
| B4 realtime financial summaries | Existing backend realtime cache/registry; `FuturesOptionTradeQueryActor` and realtime notifications | Complete server-owned summary/spread fields; client becomes a numeric snapshot mirror |
| B5 saved history valuation | `FuturesOptionPositionQueryActor`, with established execution evidence from Trade | Add/enrich valued history projection and execution-backed opening snapshot |
| B6 market/context summaries | `MarketDataQueryActor`, Market Outlook snapshot query/realtime actors; existing Portfolio capacity query | Add named IV/change/MDI/capacity summary values to appropriate responses |
| B7 dates, eligibility and range queries | `MarketDataQueryActor` / market-session authority and `FuturesBarDataQueryActor` | Reuse `GetValueDate` / `GetMarketSession`; return server eligibility and implement server-partitioned UTC-range history |

Order preparation is read-only: no reservations, order placement or ledger postings before confirmation. Existing Portfolio composition/admission and Trade command/function actors remain authoritative at submission. Do not repurpose Portfolio admission as a preview operation. Backend queries may use injected read services; they must not recursively wait on their own actor mailbox.

## Contract rules

- Preserve existing consumers through additive/versioned contracts as appropriate to the repository's MessagePack conventions. Cover explicit numeric keys, null/default behavior and old/new payloads with fixtures.
- Financial values use exact decimals with explicit currency, multiplier, signed quantity and points-versus-currency units. Missing inputs produce unavailable/error results, never fabricated zero-valued financial success.
- Include input revision, source observations/as-of, authoritative ValueDate, relevant policy/fee versions and availability in prepared results. Realtime results retain trade/composition/monitor generation and position/plan revisions.
- Bind confirmation and submission to the prepared order identity and exact selected contracts, quantities, limit, account and environment. Validate scope, freshness and revisions again at submission; do not trust client-supplied derived risk values.
- Server business time controls preparation expiry and effective policies. UI elapsed-time measurement and display-zone conversion remain local. Server timestamps used for financial hashes and request expiry must follow one documented contract.
- Preserve operation IDs and uncertain-outcome reconciliation across timeout/reconnect. Do not introduce a new operation ID or submit again blindly after an ambiguous acknowledgement.

## Implementation sequence

### Phase 0 — Lock behavior and contracts

1. Reconcile B1-B7 against active Rust actions and the working-tree register. Record each moved method, existing backend implementation, operation, response fields and current UI consumer.
2. Capture representative current inputs/outputs: debit/credit strategies, quantities/multipliers, confirmation, realtime/saved positions, fund metrics and market/session context.
3. Separate intended existing policy from defects: the current capital/stress proxy, emulator fee assumption and hardcoded tick bands must not silently become new financial rules.
4. Define fund metrics explicitly. The current realized-posting Sharpe with `sqrt(252)` is not automatically a daily-return Sharpe; define treatment of external cash flows and drawdown. Preserve existing comparisons as characterization evidence, not proof of a correct metric definition. Resolve definitions before marking B3 accepted.

**Gate:** source-to-operation registry and explicit numerical/unit expectations are reviewable; no unresolved assumption is silently encoded as authoritative behavior.

### Phase 1 — Establish server business context (B7)

1. Reuse the existing API ValueDate and MarketSession operations, including server as-of and session revision. Add explicit entry/monitor eligibility where required, including configured Development emulator behavior.
2. Replace operational UI session calculations in `TradeMonitoringPolicy`, `PositionEntryWindow` and their current Rust consumers with server context. Keep local button presentation but do not derive financial eligibility from workstation time.
3. Remove local wall-clock evaluation of deployment/risk context. Initialize operational date defaults from the API; retain dates explicitly selected by the user for reporting.
4. Add a bounded UTC-range bar query under the existing bar query actor; backend owns exchange-session partition enumeration. Wire app/market-chart history to it.

**Gate:** workstation date/time differences, rollover, weekends, DST and pending EOD cannot change the authoritative trading date or independently enable a forbidden action.

### Phase 2 — Prepare orders and confirmation on the server (B1+B2)

1. Add the order preparation contract/API client, actor handler and injected preparation service. Reuse `TradeOrderPreviewCompute` for supported payoff calculations.
2. Resolve exact selected instrument definitions, multiplier, tick/bounds, effective deployment and account/environment fee context server-side. Distinguish estimated fees from eventual executed fees.
3. Return the prepared candidate/risk inputs and confirmation values. Preserve existing financial admission ownership and explicitly test any approved capital/stress policy.
4. Update `BrokerManualTradeOrderViewModel` and `TradeComposerSession` to request preparation before confirmation and submit the confirmed prepared selection. Input changes invalidate the prepared result.
5. Remove active-path `MaximumOptionLoss`, notional/capital/stress derivation, USD 0.65 commission calculations, deployment selection and hardcoded financial tick/price-band calculations from client execution. Do not delete a shared helper needed by an excluded WinForms path without separately handling that dependency.

**Gate:** prepare-confirm-submit works through the actual API/client boundary; a changed selection, stale result or unavailable policy cannot submit a mismatched candidate. Cancellation causes no financial side effect.

### Phase 3 — Consume complete realtime and saved-history projections (B4+B5)

1. Ensure backend realtime snapshots contain complete aggregate Greeks, position value, per-spread net value/P&L/OTM estimates and authoritative plan values with quality metadata.
2. Replace the current UI's computational `TradeRealtimeHotCache` usage with a client mirror: retain received numbers, identities and revisions; manage visual freshness without recomputing financial totals. Keep backend cache calculations in place.
3. Return valued position-history rows, including opening snapshots built from execution fills, weighted prices, quantities and authoritative dates. Replace UI synthesis and leg-price aggregation with API results.
4. Preserve last valid values and visible stale/pending/unavailable status; prevent mixing different composition quantities or generations. Maintain six-hour database chart history and live merging.

**Gate:** realtime and saved screens match independent backend numerical expectations across partial fills, quantity changes, stale/missing inputs and reconnect. No client fallback computes currency values or Greeks.

### Phase 4 — Move fund performance aggregation (B3)

1. Implement a scoped, date-bounded metrics query in the existing ledger query actor/store, using the definitions agreed in Phase 0 and a consistent read/revision.
2. Return a typed report with currency, period, revision and availability/insufficient-history reasons.
3. Wire `FundMetricsViewModel` / `PortfolioSession` to the report. Remove client transaction-page accumulation, local financial filtering and metrics calculation.

**Gate:** ledger integration cases cover deposits, withdrawals, commissions, realized gains/losses, empty and insufficient history, accounting boundaries and concurrent writes. The UI does not download an entire ledger to display its report.

### Phase 5 — Complete market and capacity summaries (B6)

1. Return nearest-strike IV summary, underlying daily change/percentage, DTE and instrument tick metadata through evaluated-chain/preview context.
2. Return MDI value/classification in backend Market Outlook projections; preserve existing supported indicator semantics and warmup/quality status.
3. Return the displayed reserved-capital summary through the existing capacity query/initialization context.
4. Remove these transformations from `TradeComposerSession.MarketMetrics`, `BrokerTradePreviewPresentation` and affected market display constructors. Keep formatting and colors local.

**Gate:** financial summary values match backend contracts; missing inputs remain visibly unavailable and no display calculation becomes an alternative trading policy.

### Phase 6 — Close the inventory and qualify the current Rust UI

1. Build affected backend/shared/API-client and current managed UI/bridge projects; run existing relevant regressions and focused new tests.
2. Run real transport integration through the current services/view models. Use isolated/emulator accounts and test storage for submissions/postings.
3. Verify current Rust screens with FlaUI/AccessKit where exposed and source-level bridge assertions for values: composer preview/confirmation, established live/saved monitor, Portfolio metrics and market context.
4. Exercise backend errors, timeout/cancellation, reconnect, expired preparation and late replies after a view closes. Confirm receipt reconciliation and reliable UI errors continue to work.
5. Repeat the source inventory, including transitive shared helpers and bridge sessions. For each B1-B7 method record its server owner, revised UI caller, tests and removal of local financial fallback. Mark excluded WinForms branches separately.

**Final gate:** all seven groups pass backend and API/current-client integration checks, desktop evidence is recorded, and no identified active financial calculation or business-policy transformation remains in the current Rust UI. Unexecuted live/desktop checks remain open gates. Only then begin the standalone Rust state/client migration.

## Verification matrix

| Boundary | Required tests |
| --- | --- |
| Order preparation | Credit/debit, futures/vertical/condor, long/short, quantity and multiplier scaling, missing/unbounded inputs, overflow, changed selection, ticks/bounds, fee/account/environment, expired preparation |
| Submission | Independent server revalidation, confirmation cancel, exact prepared identity, accepted/rejected results, ambiguous acknowledgement, receipt reconciliation, no duplicate financial effects |
| Realtime/history | Independent numerical expectations, source units/signs, partial/multiple fills, missing opening row, last valid/stale values, quantity/generation change, ordered snapshots and reconnect |
| Fund metrics | Explicit cash-flow/return conventions, independent expected results, zero denominators, insufficient history, fees, bounded range and consistent revision |
| Business context | Server date differs from workstation, rollover/EOD, weekend, DST, emulator policy, bounded chart range across ValueDates |
| Compatibility | MessagePack old/new payloads, exact decimals, canonical hash/operation identity, unchanged journal recovery and error delivery |

Do not rely only on comparing the old UI output with the new backend output: shared formula mistakes would pass. Include independent expected values and domain invariants.

## Execution boundaries

Implementation will change backend contracts and current UI consumers together in verified increments. Starting/stopping the user's running servers, applying production financial writes or placing live orders is not part of this planning task. Controlled runtime verification must identify the exact binaries/environment and record any required user restart or trade loading. The execution record below distinguishes automated evidence from live acceptance.

## Implementation and verification results

### Agreed formula basis

The user confirmed: **preserve current fund formulas and identify their basis in the report**. The backend returns `CalculationBasis` and the current Portfolio report exposes it in the message and Rust metric tooltips:

- `LegacyLedgerPostings/v1`: realized ledger postings, with period opening balances and deposits as capital.
- Sharpe uses sample variance across individual realized postings and annualizes by ?252; this is **not a daily-return Sharpe**.
- Drawdown includes deposits, withdrawals, commissions and realized P&L; there is no cash-flow adjustment.
- No metric-definition change was made during this migration.

### Source closure register

| Group | Backend implementation | Current UI consumer / closure |
| --- | --- | --- |
| B1+B2 | `PrepareTradeOrder` on existing `TradeOrderQueryActor`; `TradeOrderPreparationCompute`, shared backend `TradeOrderPayoffCompute` and `ManualOrderEntryPolicy` | `BrokerManualTradeOrderViewModel` requests preparation before confirmation; displays server cash/fees; submits the prepared candidate through existing Portfolio admission/receipt APIs. Exact returned component, legs, limit and scope are checked. |
| B3 | `GetFundPerformanceMetrics` on existing `GeneralLedgerQueryActor`; `FinancialQueryStore` uses scoped accounting-date SQL and one committed read revision | `FundMetricsViewModel` requests one report; `PortfolioSession` exposes its basis. No UI ledger download/aggregation. |
| B4 | Existing server realtime cache remains the financial computation owner | `TradeRealtimeClientMirror` copies server snapshots/revisions; manages visual freshness without recalculating aggregates. Active established Iron Condor path no longer applies prices/positions/plans to a local financial cache. |
| B5 | `StrategyPositionHistoryProjection`; position queries, latest-plan responses and server realtime publication enrich history; Trade query supplies weighted-fill opening evidence | `IronCondorViewModel` copies backend history valuation and opening snapshots. Missing evidence is unavailable, rather than synthesized from guessed opening fills. |
| B6 | `EvaluatedOptionChainSummary`, `MarketOutlookComposer`, capacity query summary and backend preview tick policy | Composer/market display constructors copy IV, price change, DTE, MDI/classification, reserved loss capital and resolved tick; formatting/colors remain local. |
| B7 | Existing ValueDate/MarketSession authority; server-selected risk evaluation time; `FuturesBarWindowPartitions` under existing bar query | Current models use API eligibility. Bridge refreshes session before compose/submit/live actions. Range service sends one UTC range and server partitions it. Portfolio default report dates come from API ValueDate. |

The regenerated CSV includes **150 rows** (81 view-model, 17 model, 52 service files), including the new mirror. Its original finding/review columns remain historical audit descriptions; `ImplementationStatus` and `ClosureEvidence` identify their disposition. Legacy methods that remain in shared classes for WinForms are not active Rust financial fallbacks.

### Policies preserved and explicit limits

- Candidate risk preserves the old **max(abs(cash notional), bounded payoff loss)** capital/stress proxy. This is not a new broker margin model.
- Confirmation fees are estimates. The emulator preserves USD 0.65 per leg contract, configurable on the server. Other accounts require a configured preparation fee policy; no guessed real-broker fee fallback.
- Futures tick comes from resolved reference data. ES option combinations preserve the old 0.05 manual entry profile on the server, separately from per-leg premium ticks. Other products need an explicit combination profile. Preview and preparation use the same policy.
- Backend preparation resolves instrument multipliers/strikes/rights/expiry and fund/deployment context. Existing Portfolio admission remains responsible for submission authorization and expiry validation. This change does not introduce a signed, tamper-proof preparation certificate or redesign the existing admission model.
- Existing history P&L source/unit conventions were preserved; this is not a correction of historical persisted values.
- Full TimeProvider/backtesting conversion, standalone Rust, recovery logic and live trading are outside this change.

### Verification evidence

Evidence logs are under `.artifacts/` in this workspace (ignored test artifacts):

| Verification | Result / evidence |
| --- | --- |
| Current UI ? typed NATS client ? running API order actor ? PostgreSQL references | Happy/cancel and missing-tick cases passed. Intentionally incorrect client multiplier 999 produced backend multiplier 50, $500 cash and $0.65 estimated fee. Cancellation submitted no order. `backend-current-ui-integration.log` |
| Current fund metrics UI ? typed NATS client ? running ledger actor | Passed, including report basis and invalid range response. Same integration log. |
| Metrics store, scoped accounting range / committed revision / access denial | Passed against isolated PostgreSQL. Same integration log: **4 tests passed overall**. |
| Backend Trade preparation/preview/realtime/history tests | **32 passed**, `backend-trade-tests-final.log`. |
| Fund metric numerical and MessagePack tests | **4 passed**, `backend-metrics-tests-final.log`. Independent expected Sharpe ?14 and drawdown $210 / 17.5% included. |
| Evaluated-chain summary | **2 passed**, `backend-market-summary-tests.log`. |
| Market Outlook MDI/boundaries | **8 passed**, `backend-outlook-tests.log`. |
| Server UTC-window partition selection | **2 passed**, `backend-range-tests.log`. |
| Functional managed UI suite | **434 passed, 1 skipped**, `backend-ui-functional-final.log`; includes confirmation acknowledgements/rejection, timeout/reconciliation, generation handling and history cases. |
| Rust compile and native example build | Passed: `backend-rust-check.log`, `backend-rust-desktop-build.log`. |
| Native history FlaUI plus managed realtime contract checks | **25 passed**: `backend-desktop-focused.log`; screenshots `backend-boundary-desktop/history-initial.png` and `history-updated.png`. Fixture-based native history, not full live-screen acceptance. |
| Architecture source suite | **39 passed, 0 failed** after resolving the 11 baseline failures. Full presentation suite: **480 passed, 1 skipped, 0 failed** (`architecture-resolution-ui-tests.log`). Original baseline reproduction retained in `backend-ui-head-architecture.log`. See resolution details below. |

### Open acceptance gates / blockers

1. **Complete live-screen acceptance is not claimed.** The two existing live TradeWorkspace bridge checks require a configured API, populated trade/fund state and runtime services. The unconfigured run failed; fixture-only realtime/history checks passed separately. No user API/UI was started or stopped and no live order was placed. Composer confirmation, Portfolio metrics and live/saved monitor should be inspected in the configured current Rust UI before standalone cutover.
2. API and current managed UI/bridge must be deployed together for the additive fields/new queries. No local financial fallback is provided when the new backend operations are missing.

These are qualification limits, not authorization to begin the standalone cutover. Keep the standalone migration acceptance gate open until live checks pass.

### Architecture failure resolution (2026-10-23)

| Original failure | Resolution |
| --- | --- |
| API startup registration, actor startup, market-data health ownership (3 tests) | Source assertions follow the executable facade into the current Core registration, actor-map and HTTP pipeline files. Both local and remote runtime modes must retain startup dispatch; readiness tags and endpoints remain checked. |
| Lifecycle console-only error assertion | Restrict the assertion to listener setup. Explicitly require terminal dataset/option failure handling to retain visible errors and deduplication. Existing behavioral tests verify the actual messages. |
| Backend DTO allowlist | Document `OptionChainDisplaySnapshot` as the existing quote-display retention boundary, with no selection authority or financial calculation. |
| Status console title assertion | Check the current untabbed, title-free black/white list in the right pane; do not restore an obsolete title strip. |
| Empty catches over baseline | Remove four swallowed status-console failures. Awaiting callers now receive listener/write failures and can report them. Added two behavioral regression tests. The debt ceiling was not increased. |
| Sync-over-async in backup logs | Use nonblocking `WaitAsync(0)` for refresh/read semaphores, retaining overlap suppression. |
| Workstation time and distributed zone conversions | Backup logs display Eastern time; time-only cron defaults use a fixed hidden date. Schedule bounds/logs use `ScheduleTime` with the explicit configured zone. Five tests cover zone round trips and invalid/ambiguous DST bounds. |
| Blanket Task.Run prohibition | Replace the obsolete blanket assertion with a single explicit exception for existing CPU-only workflow property-tree preparation. Check that it is the only use, is cancellation-aware, bounded by node/depth/item limits, fenced by generation and cancelled/disposed with the view. No new background work was added. |
| WinForms composition assertions | Follow the current shared `UiClientServiceRegistration` instead of expecting NATS/clock registrations duplicated in the WinForms executable. |

The full solution build additionally exposed three stale test call sites from the earlier `IMarketDataApi` cutover (Treasury refresh and calendar import). Their fixtures were updated to use the current boundary without reverting production code to provider-specific dependencies. The calendar fixture also needed its obsolete Scylla schema setup replaced with current PostgreSQL schema creation and event-transaction wiring. Verification:

- Full `dotnet build TomasAI.IFM.sln --no-restore`: **succeeded, 0 warnings, 0 errors**, `architecture-resolution-build.log`.
- Full presentation tests: **480 passed, 1 skipped, 0 failed**, including **39/39 architecture tests**, `architecture-resolution-ui-tests.log`.
- Treasury hosted-service fixtures: **2 passed**, `architecture-resolution-treasury-tests.log`.
- Calendar import integration: **1 passed**, `architecture-resolution-calendar-integration.log`; fixture now uses the PostgreSQL schema catalog and shared accepted-import/event-log transaction.
- No live acceptance test was started during this cleanup.

## Pre-standalone backend qualification without live trading

Run date: **2026-10-23**. This qualification exercises the existing backend and current managed Rust client boundary before standalone UI implementation. It does not qualify standalone Rust or rendered-screen parity.

### Isolation and permitted operations

- The existing Development API (PID 79168) and MarketData Host (PID 117556) were not restarted or fault-injected. Running-backend checks used queries only: market session/ValueDate, fund metrics, established trade, realtime availability, valued position history, chart history, and order preparation. No live feed was enabled and no order or financial posting was submitted to Development.
- Mutating integration cases use the domain actor fixture's separately owned PostgreSQL, Scylla, Redis and NATS containers, synthetic market data, and emulator execution. They do not use the Development financial database or a real broker.
- Builds use isolated artifact directories. The current UI implementation and deployment remain available; no standalone project was created in this verification step.

### Running API evidence through current UI services

Both read-only tests in `LiveBackendProcessingBoundaryTests` passed against execution trade `101.701.1901.1501`:

- API ValueDate was **2026-10-26**, with a valid **Closed** session, no active trading date, and next session start **2026-10-25 22:00 UTC**. The client used the API date despite the workstation still being on Friday 23 October. No current Monday realtime snapshot was available; this is recorded as unavailable, not healthy live-market proof.
- The server returned a bounded six-hour chart with **237 stored samples** in the initial successful capture. The final check additionally verifies that every sample lies inside the returned range and that samples are ordered. Stored sample count can change as the server window moves.
- Seven saved position-history rows spanning six value dates from 16 through 23 October carried backend history valuations. The query uses a separate 30-day server-as-of range because these are stable daily lifecycle rows, not minute chart samples.
- Fund metrics included the approved `LegacyLedgerPostings/v1` calculation basis. Existing formula semantics are preserved; the report is not represented as daily-return or cash-flow-adjusted performance.
- Read-only preparation of the saved Iron Condor opening-price candidate returned cash **-$572.50**, estimated fees **$2.60**, total **-$569.90**, and maximum loss **$1,927.50**. These are preparation values for a saved-price candidate, not a current executable market quote or an order submission.

Evidence: `.artifacts/pre-standalone-backend-readonly.log` and `.artifacts/pre-standalone-backend-readonly/{backend-processing-live-results.json,backend-session-history-results.json}`.

### Test harness corrections

The initial read-only attempt supplied an execution-trade ID to a setup-trade lookup and correctly received `FundOrderTrade was not found`. The harness now accepts an explicit `IFM_BOUNDARY_ESTABLISHED_TRADE_ID`, retaining the separate setup-to-execution lookup for callers that supply a setup identity. A second assertion incorrectly required stable daily position-history rows within the chart's six-hour sample window; history now has its own bounded server-time range. The chart's six-hour and nonempty assertions remain in place.

Enabling the normally opt-in `BrokerTradePreviewNatsTests` exposed another stale fixture: its actor had no underlying reference data, which the migrated handler correctly rejects. The fixture now supplies controlled option/underlying references and deliberately sends an incorrect client tick of 0.25. Twenty real NATS preview updates prove the backend resolves the ES combination tick to 0.05 and returns independently expected quantity-scaled payoff values. Empty drafts and unknown references return errors. All corrections were test-harness changes; no production backend code was changed to make them pass.

### Final results

**615 unique tests passed; zero remaining failures in the selected verification matrix.** The full presentation run initially skipped its one explicitly opt-in NATS case; that same case was subsequently enabled on a disposable nonstandard-port broker and passed. It is counted once below and is not an outstanding skipped acceptance check.

| Verification | Passed | Evidence under `.artifacts/` |
| --- | ---: | --- |
| Order preparation, payoff preview, realtime projections, saved valuation, six-hour ranges | 37 | `pre-standalone-backend-trade-tests.log` |
| Current UI preparation/cancel and metrics over isolated API/NATS, scoped financial queries, book preparation, financial process replay | 27 | `pre-standalone-backend-isolated-integration.log` |
| Fund metric numerical and wire contracts | 4 | `pre-standalone-backend-metrics.log` |
| Evaluated option-chain summary | 2 | `pre-standalone-backend-market-summary.log` |
| Daily indicator preview/baseline behavior | 5 | `pre-standalone-backend-outlook.log` |
| Backend MDI classification and boundaries | 8 | `pre-standalone-backend-mdi.log` |
| Server UTC-window partition selection | 2 | `pre-standalone-backend-server-range.log` |
| Manual futures/vertical/condor emulator behavior | 17 | `pre-standalone-backend-broker-emulator.log` |
| Full current managed presentation suite, including 39 architecture checks | 480 | `pre-standalone-backend-current-ui.log` |
| Explicit real-NATS preview actor/client round trips | 1 | `pre-standalone-backend-preview-nats.log` |
| Recovery runtime/coordinator and closed-market frozen emulator behavior | 24 | `pre-standalone-backend-recovery.log` |
| Durable emulator submission, duplicate replay, changed request rejection | 6 | `pre-standalone-backend-emulator-submission.log` |
| Running Development API through current Rust UI services: metrics, preparation, server date/session, chart and valued history | 2 | `pre-standalone-backend-readonly.log` |

Affected projects built successfully under warnings-as-errors. This is not a claim of a new whole-solution build. The running API/Host retained their original PIDs and creation times throughout. Fixture-owned containers and the additional preview-only NATS broker were stopped after verification. No Development order, financial posting, live-feed start, or service reset occurred.

### Qualification limits

This run can establish backend/current-client behavior and isolated financial execution without live trading. Actual provider quote freshness, market close/open transitions, and visible desktop workflows remain separate acceptance gates. The new standalone Rust transports, models, journals, and screens will still require their own protocol and workflow parity tests before replacing the current UI.
