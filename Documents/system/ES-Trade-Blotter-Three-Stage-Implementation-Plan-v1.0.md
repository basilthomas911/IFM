# ES Trade Blotter: Implementation Plan v1.0 (Stages 1-4)

## 1. Status, scope, and authority

Status: Stages 1-3 are complete. S4.2-S4.7 implementation and offline qualification are complete; S4.1 production activation is pending approval. See the [Stage 4 implementation and verification record](ES-Trade-Blotter-Stage-4-Implementation-and-Verification-v1.0.md), [original Stage 1 verification record](Option-Pricing-Stage-1-Implementation-and-Verification.md), [S1A verification record](Option-Pricing-Stage-1A-Verification.md), [calculation-tier addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md) and [Stage 4 requirements](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md).

Source: [Detailed specification v3](ES-Trade-Blotter-Contract-Reference-and-Option-Pricing-Specification-v3.md). This plan replaces its seven-package execution sequence with the staged sequence below; its functional requirements remain applicable. The original three stages retain their scope; Stage 4 is an additive requirement. The filename is retained for existing links. No application code, runtime configuration, database, or broker state is changed by publishing this plan.

| Stage | Deliverable | Required exit gate |
| --- | --- | --- |
| 1 | Additional option pricers and unified OptionCalculator | Numerical, BDD, unit, integration, regression, performance verification |
| 2 | Expanded existing IFM contracts, persistence/messages, Databento add/change selection, market-data pricing integration | BDD, unit, serialization/migration, service/storage/UI integration, restart verification |
| 3 | Three-tab blotter and IBKR emulator integration through ITradeBroker | Full trading happy paths and edge cases, BDD/unit/integration/UI/Portfolio/accounting verification |
| 4 | Comparable option-IV series, IV Rank/Percentile, durable history and strategy consumers | Numerical/BDD/unit/storage/pipeline/UI tests, as-known replay, restart and performance verification |

Execution is sequential. A stage is complete only after its tests pass and evidence is recorded; skipped or unavailable required tests keep its gate open. Later stages rerun earlier-stage regression tests affected by their changes. Future equity trading is excluded: all four pricing combinations are delivered, but only futures/futures options are traded in V1. Emulator qualification is the Stage 3 target; real IBKR Paper/Live and market-hours load qualification remain distinct deployment gates.

## 2. Verified integration anchors

- `TomasAI.IFM.Framework.OptionPricer/Black76/OptionCalculator.cs`: current European futures calculator; preserve its proven behavior through a compatibility adapter as necessary.
- `TomasAI.IFM.Framework.MarketData/Contracts/Pricing/OptionPricingContracts.cs`: existing versioned reference and pricing-context contracts.
- `TomasAI.IFM.Application.MarketData/Pricing`: current Black-76 context qualification, chain enrichment, and composition pricing; update all applicable call sites, not only the calculator class.
- `TomasAI.IFM.Domain.MarketData.Shared/ViewModels/FuturesContractReadModel.cs`: existing `FuturesContractV3ReadModel`.
- `TomasAI.IFM.Domain.MarketData.Shared/ViewModels/FuturesOptionContractReadModel.cs`: existing option reference model.
- `TomasAI.IFM.Framework.MarketData.DataBento/Runtime/ExactInstrumentDefinition.cs`: exact provider evidence and summary mapping.
- `TomasAI.IFM.UI.Net.ViewModels/MarketData` and `TomasAI.IFM.UI.Net.Views/MarketData`: reference editor view models and controls.
- `TomasAI.IFM.Application.TradeBroker/Contracts/ITradeBroker.cs` and `InteractiveBrokersEmulatorTradeBroker.cs`: existing application boundary and emulator implementation.

ITradeBroker currently exposes Environment, AccountAlias, Generation, PlaceAsync, ModifyLimitAsync, CancelAsync, ReconcileOrderAsync, GetAccountSnapshotAsync, ResynchronizeAccountAsync, PublishMarketQuoteAsync, ObserveOrdersAsync, and ObserveAccountAsync. It has no general quantity-replacement method or advertised algorithm-capability method. Stage 3 must resolve those gaps explicitly before enabling the corresponding controls. A dispatch receipt is not a fill or terminal confirmation.

## 3. Stage 1: Additional pricers and OptionCalculator

### 3.1 Implementation sequence

1. Capture Black-76 regression fixtures and current consumer behavior; define common typed requests/results and model identifiers. Use framework-owned inputs so this stage is testable before reference schemas change.
2. Define units: rates/IV as annual decimals; per-option, unscaled Greeks; Delta per underlying-price unit, Gamma per squared unit, Vega/Rho per unit decimal volatility/rate, Theta per year. UI conversion to per-day or per-percentage-point is explicit. Apply quantity/multiplier exactly once outside numerical kernels.
3. Implement model-neutral OptionCalculator routing for European futures (Black-76), European equities (Black-Scholes-Merton), American futures, and American equities. Unknown or incompatible metadata returns a typed failure with no numeric result.
4. Implement American pricing with a shared bounded lattice kernel and separate equity/futures economic adapters. Freeze a deterministic engine/step/bump policy and version it. Use reusable buffers; prohibit mutable global evaluation dates.
5. Supply theoretical-price, IV-plus-Greeks, scalar, and bounded batch APIs. IV must use the same model as the resulting Greeks and a bracketed safeguarded solver with iteration and convergence limits.
6. Define dividend/carry and settlement capability explicitly. Include cash-dividend schedules and continuous yield as distinct equity inputs; no silent replacement of discrete dividends by a yield. Premium-paid and futures-style settlement require qualified model treatment; a mere exercise-style switch is insufficient. Unsupported conventions fail explicitly until implemented and tested.
7. Test numerical parameters across increasing resolutions before selecting production defaults. Commit versioned tolerances, step limits, bump sizes, and fixture provenance with the engine; constants are verification outputs, not invented trading settings.
8. Preserve existing Black-76 callers and native selection behavior. New models may use managed kernels initially; never dispatch an American/equity request to the existing Black-76-only native ABI. New equity models remain inaccessible to V1 order workflows.

### 3.2 Happy-path BDD scenarios

- Given a valid call/put for each of the four combinations, when priced through OptionCalculator, then the correct model returns finite price, IV, and all Greeks with engine provenance.
- Given a model-generated price with identifiable IV, when inverted, then recovered volatility reprices within the qualified tolerance.
- Given identical scalar and batch inputs, then results/status agree and input ordering is preserved.
- Given existing European ES inputs, then legacy and routed Black-76 results agree within the existing contract.

### 3.3 Edge-case BDD and unit matrix

Cover expired/at-expiry/near-expiry, zero volatility, deep ITM/OTM, very small time value, negative rates, invalid or nonfinite inputs, unknown style/asset, invalid dividend schedules, missing carry, unsupported settlement, prices outside model bounds, IV non-uniqueness near intrinsic, non-convergence, cancellation and batch capacity. Non-identifiable IV returns a distinct failure rather than arbitrary volatility.

Check European parity and derivative identities only under their valid assumptions; check American early-exercise behavior, convergence and lower/upper bounds using model-specific conventions. Test Greek bump sensitivity near exercise boundaries and document unstable/undefined sensitivities. Validate positive-underlying limitations explicitly rather than silently applying lognormal formulas to nonpositive futures.

### 3.4 Integration and verification gate S1

Integrate the facade with existing pricing consumers using frozen context fixtures and the existing Black-76 adapter. Verify thread safety, serialization where applicable, failure propagation, cancellation and regression behavior. Compare against independent documented numerical references; testing the engine against itself is insufficient. Benchmark representative 1/2/4-leg and full-chain batches in Release, capturing latency, allocations and GC. Record exact commands, commit/worktree identity, fixture versions, numerical error maxima, and benchmark environment. Gate S1 passes only with all four combinations tested, unchanged Black-76 behavior, no false-success results, and a committed numerical acceptance policy.

### 3.5 Required follow-up S1A: calculation tiers

Status: completed and verified; 238 passing test executions and 32 comparative benchmarks. See the [S1A verification record](Option-Pricing-Stage-1A-Verification.md). The following requirements are retained as the acceptance contract.

Add dedicated scalar/batch price-and-Delta and IV-only APIs, preserving existing full-Greek APIs. Avoid American Vega/Rho/Theta bump passes for selection and avoid the current full-Greek postpass in IV-only refresh. Correctly calculate European Delta rather than exposing the private evaluator's zero placeholders. Implement and pass the numerical, BDD, integration, regression and comparative benchmark gates in the [S1A addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md) before Stage 2 depends on these APIs. Original Stage 1 test evidence does not cover this new scope.

## 4. Stage 2: Contracts, persistence, and Databento selection

Completion update, 2026-09-19: Stage 2 implementation and scoped fixture verification are complete. This includes decimal references, both provider selectors, immutable reference/convention publication and history, European/American futures pricing, coalesced selection snapshots and durably acknowledged Trade-basis Greek evidence. See the [final Stage 2 verification record](Option-Pricing-Stage-2-Implementation-and-Verification.md) for exact tests, scoped startup/UI evidence, skips and measured performance. The approved 250 ms / 5 s / 1 s settings remain test defaults; production activation is gated. Only disposable stores were migrated; no live provider qualification or deployed application restart is claimed.

### 4.1 Implementation sequence

1. Expand the existing FuturesContractV3ReadModel and FuturesOptionContractReadModel in place. Preserve serialized keys and existing field types. In particular, do not change the existing double StrikePrice wire field to decimal in place: append a canonical decimal strike and translate/validate legacy data explicitly. Add a numeric multiplier alongside the legacy representation, with conflict validation.
2. Add provider identity/evidence, exact UTC times, exchange/calendar conventions, underlying identity, exercise/settlement/premium style, ticks/scales, and effective mapping/version data specified by v3. Use Unknown/null for absent legacy facts; never invent qualification.
3. Establish one reviewed source of truth for conventions. Expanded editor models carry the selected version; saves publish matching reference and convention data. Reject partial publication or mismatched versions instead of allowing independent copies to drift.
4. Update commands, events, completion/failure events, HTTP/NATS APIs, queries/pages, mappers, validators, generated serialization fixtures, storage schemas and caches. Preserve old payload readability. No destructive truncation as migration strategy.
5. Add provider-to-IFM mapping and deterministic ID conversion using existing IFM conventions. Preserve raw provider identity. Handle decimal strikes, expiry calendars, weekly/EOM families, culture, duplicates and collisions. Do not alter an established ID to force an import to succeed.
6. Extend both Add and Change editor flows with asynchronous, cancellable, paged Databento selection. Preview imported values/IFM ID; show source/as-of and missing facts. Freeze provider identity after selection; allow reviewed convention updates with evidence. Selecting another instrument creates a replacement/new record, preserving historical references.
7. Require an exact existing underlying future for option save. Keep IFM rollover/on-the-run controls independent of provider terms. Permit incomplete reference drafts only with explicit unqualified status; pricing/trading stays blocked.
8. Replace European-only model checks in qualified chain/context paths with the Stage 1 router while preserving freshness, generation, calendar, rate and subscription ownership rules. This stage owns market-data integration so Stage 3 consumes complete snapshots.
9. Implement shared coherent chain snapshots, qualified IV caching and coalesced price/Delta refresh using S1A. Keep IV-only refresh separate from full-risk calculations for selected/held contracts; retain independent IV observation and calculation timestamps, generation fencing and bounded scheduling. Enrich every retained option market trade with a Trade-basis full-Greek attempt and persist all Greeks or explicit failure plus provenance. Never substitute cached midpoint Greeks for trade-price Greeks or historical trade Greeks for current position risk. Persist source trades even when derived pricing fails; avoid per-quote durable replay. Apply the tests and ownership boundaries in the calculation-tier addendum.
10. Run additive migrations and qualified backfill on disposable/restorable test stores, verify counts/digests, and test old/new reader behavior and startup. Keep incomplete imports/migrations inactive.

### 4.2 Happy-path BDD scenarios

- Given a Databento future, when selected during Add, then its IFM ID and fields are previewed, saved, queried and redisplayed identically.
- Given an existing future and qualified European/American option definition, when imported, then exact underlying/style/strike/times survive API, event and database round trips and route to the correct pricer.
- Given a saved contract, when reviewed metadata changes, then a new effective version is visible while historical pricing resolves the old version.
- Given retained option trades, when persisted, then trade-basis Greeks/provenance are retrievable; failed calculations preserve the source trade and failure status.

### 4.3 Edge-case BDD and unit matrix

Cover repeated import, concurrent duplicate saves, distinct-instrument ID collision, unknown exercise, missing/wrong underlying, decimal strikes, stale/deleted definitions, missing entitlement, timeout/cancellation, late search response after selection change, timezone/DST boundaries, option/future expiry mismatch, truncated legacy blobs, absent appended fields, conflicting decimal/legacy values, invalid multiplier/ticks, partial migration failure and restart. Test quotes with stale/skewed timestamps, crossed markets, missing rates, generation change, unavailable model inputs and invalid trade price.

### 4.4 Integration and verification gate S2

Exercise real application APIs/actors/message transport and disposable Scylla/reference stores, plus UI Add/Change automation with deterministic provider fixtures. Verify old payload fixtures, new round trips, migration restart/idempotence, atomic visibility of reviewed versions, persistence counts and duplicate prevention. Capture screenshots/UI evidence for selection, populated values and validation failures. Run Stage 1 regression with imported contracts and verify API/UI startup after schema change. Gate S2 requires all BDD/unit/integration/verification tests passing, both editors functioning, qualified American/European chain pricing, and retained trade Greek evidence. A live Databento probe is separate evidence and cannot be claimed from fixture tests.

## 5. Stage 3: Three-tab blotter and ITradeBroker emulator integration

### 5.1 Implementation sequence

1. Rename pnlTradeControl to pnlTradeBlotter and retain the surrounding Trade Orders form. Build Market Selection, Leg Staging, and Orders and Fills using existing tab conventions.
2. Add Strategy/Direction selectors on the tab-header row; place Broker Mode in the right-hand header area. Derive modes from adapter/account configuration. Lock committed, automated and historical selections according to domain capabilities.
3. Connect Stage 2 chain snapshots. Implement delta-nearest shorts and width-nearest long wings with deterministic tie-breaking, configured tolerances and manual review. Use the exact option underlying rather than today's front-month future.
4. Render Leg then Delta; calls before puts with ascending signed Delta within groups. Use SL-/LL+, red shorts and blue longs; verticals follow the same rules. Futures show N/A option Delta. Preserve broker Buy/Sell separately from position intent so closing a long is not mistaken for opening a short. Support all direction variants without silently reversing selected contracts.
5. Show 10-20 visible chain rows with bounded virtualization and stable selections as quotes change. Fit the whole form between menu/status at supported resolution/DPI. Freeze staged contract identities; only refreshed price evidence changes until explicit recomposition.
6. Move manual actions into tabs. Composer validates one-unit structure; Risk Manager approves/reserves final quantity. Closing reverses the actual held contracts/remaining exposure and must not auto-select fresh entry legs. Preserve opening/closing Trade policy and prohibit deletion after economic evidence.
7. Route UI commands through existing application/domain order workflows to ITradeBroker. Never call emulator internals or an IBKR SDK from controls. Use PlaceAsync, ModifyLimitAsync, CancelAsync and observation streams; reconcile via ReconcileOrderAsync/account snapshot APIs after uncertainty or reconnect.
8. Bind the existing InteractiveBrokersEmulatorTradeBroker with explicit Emulator account/environment and historical/fixture market input. PublishMarketQuoteAsync is invoked by the authorized market-data/test pipeline, not a UI fill shortcut.
9. Audit algorithm/order-shape capabilities and extend common broker contracts and emulator mapping where necessary. Support None and emulated Adaptive for qualified Limit/Market cases; unsupported combinations are visibly rejected. Emulator acceptance tests verify semantics and lifecycle, not fidelity to proprietary IBKR execution behavior. Do not presume Adaptive support for every combo/routing combination.
10. Keep limit amendment within ModifyLimitAsync semantics. General quantity replacement requires an explicit shared API/workflow extension, fill-aware validation and emulator tests before enabling that action. Do not imply a limit-only method changes quantity.
11. Render parent/leg orders, requested/cumulative/remaining quantities, average fill, execution IDs, costs, pending states and reconciliation status. Deduplicate observations and bind commands to revision/generation. Dispatch success never marks an order filled or cancelled.
12. Integrate Portfolio fill exposure/reservations and idempotent completion-based ledger posting. Zero-fill completion posts nothing; any positive-fill terminal completion must hand off the actual filled quantity. Retain fill-specific pricing evidence and protect economic history.
13. Supply explicit historical simulation with virtual time, source labels and isolated emulator accounts/storage. Automated/historical audit modes are read-only; simulation command authority is distinct. Remove old right-hand controls only after equivalent actions and regression tests pass.

### 5.2 Full happy-path BDD scenarios

Run Futures Outright, both vertical rights/directions and long/short Iron Condor scenarios. For each applicable qualified exercise style: select Fund/Order, add opening Trade, select instruments, verify Greeks/roles, stage, approve risk, submit through ITradeBroker, receive emulator acknowledgements/fills, verify Portfolio and completion ledger, add compatible closing Trade, close exact exposure, and verify final policy/accounting state. Exercise Market/Limit and supported None/Adaptive combinations. Repeat using recorded historical input with the market closed.

### 5.3 Edge-case BDD/unit matrix

Cover absent/stale Greeks, illiquid/missing strikes, invalid widths/ratios/quantities/ticks, risk rejection/expiry, strategy change with staged legs, no adapter/account, wrong mode/generation, double-click submit, duplicate/conflicting commands, delayed receipt, reject, no fill, partial fill, fill during cancel, cancel after partial, replace rejection, out-of-order observations, duplicate fills/fees, reconnect/restart, UI selection change mid-command and account reconciliation mismatch. No extra exposure, duplicate ledger posting, or removal of real fills is permitted.

Also test American early exercise/assignment implications: model availability alone does not prove assignment workflow support. Product activation must explicitly qualify the applicable broker/position lifecycle; unsupported assignment cases stay gated and are visible. This plan does not silently expand the two-Trade policy to support hedge trades.

### 5.4 Integration and verification gate S3

Use the real UI/application/domain pipeline, actual ITradeBroker emulator adapter, transport and test databases. Verify observation-driven state, risk reservations, order/leg quantities, Portfolio and ledger reconciliation, restart recovery and no direct UI-to-emulator bypass. Run complete BDD/unit/integration suites plus UI verification at supported DPI/resolution. Capture scenario traces, correlation IDs, storage/account reconciliations and screenshots. Run sustained quote/command load, reporting allocation, GC, UI/pricing latency and bounded queue behavior against an agreed baseline. Rerun Stage 1/2 regressions. Gate S3 passes only after full open-to-close emulator workflows and all edge cases are verified; live IBKR execution is not a substitute for or consequence of that gate.

## 6. Stage 4: Option volatility, IV Rank/Percentile and history

Implement the [Stage 4 requirements](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md) after Stages 1-3. Shared analytics own comparable IV observations and rolling Rank/Percentile. TradeSelection consumes strategy context; Composer/Risk/position workflows and Portfolio/blotter reuse qualified snapshots. MarketCondition remains underlying-only unless separately redesigned.

Persist immutable series definitions, source-IV history, derived metric snapshots and exact workflow references. Use bounded Scylla time-series queries and existing versioned configuration conventions, with idempotent publication, append-only corrections, explicit retention and as-known versus restated historical modes. Quote coalescing does not discard required trading evidence.

Gate S4 requires the full numerical, BDD, unit, storage/pipeline/UI integration, historical-replay, restart and performance verification described in the Stage 4 document. The original gates remain separate; S4.2-S4.7 implementation and offline qualification are complete, while S4.1 production activation remains pending approval. See the [Stage 4 implementation and verification record](ES-Trade-Blotter-Stage-4-Implementation-and-Verification-v1.0.md).

## 7. Test execution and evidence policy for every stage

Each stage has four required layers: executable Given/When/Then BDD, focused unit/numerical tests, cross-component integration tests, and system/manual verification where needed. BDD may drive the same production APIs as integration tests but must specify observable outcomes independently of implementation.

Use deterministic clocks, frozen definitions/rates/calendars, stable seeds, isolated identifiers and disposable stores. Record fixture provenance and never label synthetic results as live qualification. Run every IntegrationTests/IntegratedTests project sequentially per [Integration Test Execution](Integration-Test-Execution.md), using `scripts/Run-IntegrationTests.ps1` for the complete suite; do not parallelize shared infrastructure. Unit tests may run independently.

At each gate publish a stage verification record containing scope/revision, changed components, requirement-to-test mapping, test commands/environment, pass/fail/skip counts, logs/TRX locations, numerical comparisons or storage reconciliations, UI evidence where applicable, benchmarks, unresolved issues and explicit gate outcome. A build alone is not verification. An unavailable required dependency is an open test, not a pass. Resolve regressions before beginning the next implementation stage.

## 8. Completion and deployment distinction

The first three implementation gates establish model correctness, reference/market-data correctness and complete emulator trading behavior. Stage 4 adds qualified historical volatility context, persistence and strategy-consumer integration. Strategies requiring IV Rank/Percentile cannot activate until their Stage 4 requirements pass; this does not retroactively expand the original stage gates. Production activation additionally requires reviewed product conventions, American lifecycle support where enabled, provider qualification, IBKR Paper verification and representative market-hours profiling. Equity trading remains disabled throughout V1.
