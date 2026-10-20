# Iron Condor ForwardPrice Function Implementation Plan

Date: 2026-10-20  
Status: Implemented; entry-credit projected-PnL v2 supersedes the prior signed-total-value rule  
Scope: Current Iron Condor option-leg updates through backend forward pricing, trade-plan risk evaluation, persistence and display

Implement the [migration design](Iron-Condor-ForwardPrice-Function-Migration-Design.md), following [Actor Implementation Conventions](Actor-Implementation-Conventions.md) and [Actor Event Modeling Conventions](Actor-Event-Modeling-Conventions.md). This plan records implementation work; it does not claim completed tests, deployment or live trading qualification.

## 1. Fixed requirements

1. Preserve the existing delta/time forward adjustment as the owner's gamma-like heuristic. Add no Gamma term, stress model, loss probability or alternative formula.
2. The Function returns the actual **Iron Condor ForwardPrice per strategy unit**, plus typed identity/currency/input evidence. It does not return ForwardLoss.
3. Pass that exact price to the existing trade-plan forward-loss calculation point:

   ```text
   ForwardPnl = (EntryCreditPoints - ForwardPrice) * ContractMultiplier * StrategyQuantity
   ForwardLoss = min(0, ForwardPnl)
   ForwardLossRatio = ForwardLoss < 0 ? ForwardLoss / (-MaxLossLimit) : 0
   ExitRecommended = ForwardLoss <= -MaxLossLimit
   ```

4. Currency/multiplier come from the actual option contracts. MaxLossLimit is the positive magnitude of this trade's configured maximum currency loss. Do not substitute a daily-loss budget.
5. Preserve negative losses with ForwardLoss = min(0, ForwardPnl); projected profits have zero loss. Preserve the raw amplified closing-cost price without a sign flip, Abs conversion or payoff cap. No current-DailyPnl or actual-loss prerequisite delays the projected-loss exit recommendation.
6. Move all **needed** OptionPricer-owned shared contracts and reusable forward support to `Domain.Trade.Shared` with Trade namespaces. Keep actor/runtime implementations in `Domain.Trade`. The old `IronCondorSpreadDistributionJobService` is the excluded legacy reference, not a runtime dependency of the new Function.
7. Current positions remain observable during failed forward calculations. Missing data never becomes a successful zero-risk result.

## 2. Stage summary

| Stage | Deliverable | Required gate |
| --- | --- | --- |
| 1 | Dependency inventory and numerical reference fixtures | Prove the recovered formula can supply the requested signed price and early-exit contract without altering the heuristic |
| 2 | Trade.Shared ownership and typed wire contracts | No active-path OptionPricer namespace dependency; no circular project references; compatible wire layouts |
| 3 | Pure forward-price calculation | Original numerical formula/fallback/tie behavior; short/long signed combination; no loss-probability work |
| 4 | ForwardPrice Function actor | Convention-compliant mapped execution, typed terminal reply and completed-only lifecycle |
| 5 | Position → ForwardPrice → TradePlan wiring | Exact result identity and unmodified price reach the plan; explicit failure path |
| 6 | Signed loss, ratio and exit rule | Single scaling, equality triggers exit, no actual-loss/DailyPnl gate |
| 7 | Storage and UI compatibility | Versioned signed metrics survive persistence/query/display without reinterpreting history |
| 8 | Full qualification and legacy consumer cleanup | Real actor/transport/storage integration, relevant regressions/builds and performance evidence |

Each stage depends on the previous stage's gate. Stop only for an identified blocker requiring a business/formula decision, missing required external input, or an incompatible contract that cannot be migrated safely. Do not silently change the formula to make a test pass.

## 3. Stage 1 — Inventory dependencies and freeze reference behavior

- Trace `IronCondorSpreadDistributionJobService`, `ProbabilityValueCollection.SetForwardPrice`, `OptionSpreadPricer`, and `IronCondorMonitoringDistributionCompute` through their actual consumers and project references.
- Record each required symbol, source namespace, target Trade.Shared location, existing callers, published wire/storage use, and migration/compatibility treatment.
- Include `CreditSpreadPricerArgs`, `OptionStyle`, `OptionSpreadResult`, `ProbabilityValueCollection`, `ProbabilityValue`, `SpreadDistribution` and their extension dependencies in the inventory; move only what the forward-only computation actually needs.
- Capture reference fixtures from the existing implementation using real contract/trade inputs or recorded representative snapshots. Identify immutable fixture provenance and legacy model version.
- Pin short/long Iron Condor price signs, component combination, OTM risk-side ties, spread-mean fallback, actual calendar-day differences, and the old pricing-horizon cap. Distinguish component formula parity from current-model sign adaptation.
- Verify that a negative generated ForwardPrice is a projected-loss observation under the requested convention. Compare cases with projected loss crossing the trade limit while actual loss is below it.

**Gate:** executable reference tests establish the numerical contract. If the existing formula only supplies a nominal signed position premium and cannot produce the requested risk behavior, record the exact failing fixture and resolve that mismatch before wiring recommendations. Do not force a negative sign based only on strategy type.

## 4. Stage 2 — Establish Trade.Shared ownership and contracts

- Add Trade-owned ForwardPrice command, complete/fail events, immutable input capture and result records under the mirrored Iron Condor ForwardPrice hierarchy.
- Move required pure shared arithmetic and needed OptionPricer types/fields into `Domain.Trade.Shared`; update consumers and namespaces. Prefer lean domain-named forward inputs/results over retaining unused distribution containers.
- Keep actor-specific compute orchestration and framework-calculator calls in `Domain.Trade`. Shared pure arithmetic must not depend on `Framework.OptionPricer` or `Domain.OptionPricer.Shared`.
- Preserve the existing dependency direction: OptionPricer.Shared may depend on Trade.Shared; Trade.Shared must not depend back on OptionPricer.Shared. Inventory any framework adapter that still exposes old OptionPricer types.
- Preserve permanent MessagePack keys, enum meanings, serialization constructors and persisted identity compatibility. Use narrowly scoped adapters for still-needed historical/legacy contracts; do not introduce adapters into the active Function path.
- Append a typed result/status to the trade-plan request and a versioned forward-risk payload to relevant plan snapshots. Do not silently reinterpret the old nonnegative ForwardLoss fields.
- Event factories explicitly set `CommandId = command.CommandId`. Use business payload names such as `IronCondorForwardPrice`; each request identity corresponds to one exact input fingerprint.

**Gate:** wire round trips, legacy-payload compatibility, project builds, and dependency architecture checks pass. New contracts/helper/Function code does not import OptionPricer-owned domain namespaces.

## 5. Stage 3 — Implement pure forward computation

- Implement the original delta/time/risk-side adjustment with its exact documented fallback and tie behavior.
- Price only the required spreads using captured current contract/trade inputs and compatible existing backend numerical pricing. Do not reload legacy trade/position models simply to call the old service.
- Combine the two spreads into one signed Iron Condor price per complete strategy unit, independent of quote ordering. Preserve current-model contract identities and atomic leg ratios.
- Return points without multiplying by contract multiplier or quantity. Return those scaling inputs as validated evidence for the plan.
- Exclude MAD, loss probability, simulation arrays, job tables, distribution view models, extra gamma penalties and independent adverse scenarios.
- Validate incomplete/mismatched contracts, unsupported ratios, expired or invalid date denominators, missing prices/deltas, numeric overflow and nonfinite output.

**Gate:** original component parity and current-model signed-price unit tests pass for short/long condors and varied strikes, expiries and multipliers. Captured Gamma changes cannot introduce a new adjustment when actual formula inputs are unchanged.

## 6. Stage 4 — Implement the Function actor

- Create `IronCondorForwardPriceFunctionActor` under `Futures/Option/Position/IronCondor/ForwardPrice/Function/Actor`.
- Follow the existing Function base directly, with explicit frozen parse, validation, receive, execution-policy and terminal-event maps.
- Add separate XML-documented Calculate, Complete, Fail and execution-policy extensions. The calculation handler delegates pure arithmetic and terminal-event construction through the mapped conventions.
- Implement compatible completed-only Function state/repository and register context, actor routes and the request capability in API and integration hosts.
- Use Core NATS typed `FunctionResult<CalculatedEvent, FailedEvent>` request/reply. Do not publish the completed Function event to the realtime event stream or create a JetStream replay consumer.
- Retain the framework's successful-event persistence semantics explicitly; do not claim Core NATS makes the Function database-free or insert an undocumented in-memory repository.
- Use bounded existing execution policies and cancellation/generation fencing. No indefinite retries or job-in-progress polling.

**Gate:** parser/handler parity, typed success/failure, command identity, timeout/cancellation, duplicate identity/input consistency, completion persistence and matching replay tests pass.

## 7. Stage 5 — Wire current position updates to the plan

- Update `IronCondorPositionChanged.ExecuteAsync` to publish the position immediately, capture one coherent input set, request ForwardPrice, and attach the returned result to `UpdateIronCondorTradePlanCommand`.
- Keep source position event, trade/session identity, calculation fingerprint, model version, multiplier/currency and request/result identities correlated throughout the call chain.
- Pass the exact completed ForwardPrice unchanged to the existing plan calculation point. Do not reprice or apply another adjustment in an intermediate adapter.
- Represent an unavailable/failed Function result explicitly in plan inputs/status; preserve current position/cache updates and withhold a fresh risk recommendation.
- Do not add provider sequence-order rejection to incoming option-cache updates or reset the market feed for an ordinary calculation failure.

**Gate:** an option-leg update traverses actual position, ForwardPrice and TradePlan actors over isolated NATS. Captured/result/plan identities match and a failed Function cannot produce a normal zero-risk plan.

## 8. Stage 6 — Replace the plan's forward-risk calculation

- Replace the forward-daily-PnL branch in `IronCondorTradePlanSnapshotCalculator.CalculateForwardRisk` with the returned price and the fixed formulas in section 1.
- Resolve the exact configured trade MaxLossLimit in contract currency; normalize a negative stored maximum-loss limit once, leaving ForwardLoss itself unchanged.
- Remove the current-DailyPnl gate from this exit rule. At `ForwardLossRatio >= 1`, set the plan's exit recommendation/action through existing monitoring semantics.
- Update `UpdateIronCondorTradePlan.ApplyMonitoringRecommendation`, completeness checks, reason codes and content hashes to consume the new signed/versioned payload.
- Audit `IronCondorTradePlanAlgorithm`, background input capture and fallback mappings so an older scenario/positive-loss path cannot overwrite or silently replace this result.
- Retain actual current marks, current DailyPnl and commissions as independent observations; they are not added into the new ForwardLoss formula.
- Preserve current plan generation/cache/persistence and existing notification suppression policy. Do not add a new price-based publication trigger as part of this work.

**Gate:** tests prove exact one-time scaling and exit behavior below/at/above the configured limit, including actual loss below its limit and current DailyPnl equal to zero or positive. This scope produces an exit recommendation; it does not add a new direct broker-order path.

## 9. Stage 7 — Persist and display versioned signed results

- Add compatible schema/projection/query mappings for the typed forward price, signed ForwardLoss, ratio, MaxLossLimit, currency, scaling evidence, status and calculation version.
- Persist exact calculation inputs/result with the full plan evidence. Keep existing historical rows and other strategies' original field semantics intact.
- Display ForwardPrice in points, ForwardLoss in signed contract currency and the ratio as a percentage. The UI only formats backend results.
- Keep retained last-valid values distinguishable from a current success when input/calculation status becomes unavailable.
- Do not reinterpret historical positive-loss values or old DailyPnl-derived ratios under the new model. Existing null/nonnullable legacy fields must not be interpreted as fresh zero-risk evidence.

**Gate:** actual storage round trips, legacy MessagePack payloads/history, additive migration tests and UI binding/render checks pass without local financial calculations.

## 10. Stage 8 — Qualify and retire migrated dependencies

- Run all new compute/actor/contract tests plus relevant Trade, OptionPricer, API-client, storage and UI regressions. Record failures precisely; distinguish unrelated baseline failures from regressions introduced here.
- Run isolated real NATS actor-chain tests and actual database projection/query round trips. Use recorded real market inputs, then exercise development provider/frozen inputs where available without fabricating observations.
- Test missing/expired inputs, cancellation, timeout, source append failure, projection failure, mismatched result identity and recovery on a subsequent accepted update.
- Measure arithmetic, Core NATS round trip, Function successful-event append, plan generation and projection separately; report p50/p95/p99, allocation rate and backlog under multiple monitored trades.
- Verify the current position path remains available during calculation failure and that retries/draining do not block option-cache ingestion.
- Update all migrated consumers to Trade.Shared names. Prove the new active path has no `Domain.OptionPricer*` dependency and that no duplicate forward implementation drifts from the selected formula.
- Keep the excluded `IronCondorSpreadDistributionJobService` and still-required compatibility behavior until remaining callers/tests have been deliberately addressed. Do not move unrelated device-management/job infrastructure wholesale.
- Build the API and Rust bridge/UI targets; verify desktop displays with existing UI automation where applicable. Relaunch development apps only after build/test evidence is recorded and the existing running session is accounted for.

**Gate:** implementation evidence records versions, test results, environment/input provenance, performance measurements, compatibility treatment and any unresolved qualification limits. Successful component tests alone are not described as complete live-provider qualification.

## 11. Minimum numerical and integration acceptance cases

| Entry credit | Returned ForwardPrice | Multiplier | Quantity | ForwardPnl | ForwardLoss | Ratio at 500 limit | Exit |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 10 | 12.5 | 50 | 3 | -375 | -375 | 0.75 | No |
| 10 | 12.5 | 50 | 4 | -500 | -500 | 1.00 | Yes |
| 10 | 13 | 50 | 4 | -600 | -600 | 1.20 | Yes |
| 10 | 10 | 50 | 4 | 0 | 0 | 0.00 | No |
| 10 | 7.5 | 50 | 4 | 500 | 0 | 0.00 | No |
| 10 | 12.5 | 20 | 3 | -150 | -150 | 0.30 | No |

These are plan-scaling/threshold fixtures. Separate compute tests must generate the signed prices from genuine numerical inputs rather than relying only on mocked returned prices. Integration must establish both calculation provenance and anticipatory exit behavior; the anticipated time to execute is not guaranteed.

## 12. Completion criteria

- [x] Required OptionPricer-owned shared functionality has moved to Trade.Shared; the excluded legacy service is not called by the new path.
- [x] Original forward formula is preserved; Function returns ForwardPrice only, with exact input identity and pricing evidence.
- [x] Plan scales once, preserves negative loss and recommends exit at the configured projected-loss threshold independently of current actual loss.
- [x] No loss probability, ForwardDailyPnl dependency, explicit gamma extension or competing scenario forward source remains in the new path.
- [x] Old contracts/history and other strategy semantics remain interpretable; no circular project references or hidden OptionPricer wrappers exist.
- [ ] Full relevant verification, performance evidence, failure behavior and builds have recorded outcomes.

Related: [current monitoring design](Iron-Condor-Trade-Plan-Monitoring-Design.md), [previous monitoring implementation plan](Iron-Condor-Trade-Plan-Monitoring-Implementation-Plan.md). For this migration, the agreed signed-price rule and exclusion of loss probability in the new design supersede contrary older monitoring-plan requirements; unrelated monitoring behavior remains in scope under its existing conventions.

## 13. Implementation record - 2026-10-20

### Resolved price convention

The owner's final clarification supersedes the earlier short-position sign proposal. The Function returns the **raw sum of adjusted put and call forward prices**, without applying short-position signs or absolute values to the combined result. Thus 21 + 9 remains **+30**, produces no projected-loss exit, and is never converted to -30 simply because the trade is short. A zero returned forward price produces zero scaled ForwardLoss; the current marked spread being zero alone does not determine the adjusted theoretical forward price.

The original component delta/time adjustment and its nonpositive-mean fallback are preserved. The active path reuses current captured leg theoretical prices and deltas, rather than calling the legacy job's distribution/device infrastructure or constructing adverse scenarios. Negative results, if produced by that formula, retain their sign. No opening-premium subtraction or new gamma term is introduced.

### Implemented flow

1. `IronCondorPositionChanged` publishes the position and captures the current references and leg evidence.
2. The registered `IronCondorForwardPriceFunctionActor` calculates a typed price and commits only its successful completion using the standard Function repository.
3. `UpdateIronCondorTradePlanCommand` receives that exact completion or an explicit unavailable reason. Missing results never become a successful zero-risk recommendation.
4. The new versioned plan branch scales the returned price exactly once and uses the initialized **negative currency `TradeLimits.MaxLoss`** as the configured loss budget (normalized to positive for comparison). The legacy `TradeLimits.MaxLossLimit` is a price-point field and is not treated as a currency budget.
5. The existing full-plan MessagePack payload persists the versioned `IronCondorForwardRisk`, exact four-leg inputs, model version, input fingerprint, source identity, multiplier and complete-strategy quantity. No distribution table or new database schema is required.
6. Rust display preparation binds signed ForwardLoss, ratio and headroom from the versioned backend payload. Historical rows without that payload retain their original meaning. No financial arithmetic was added to the UI.

The public contract additions are append-only. Old plan requests default to their existing calculation branch; the active Iron Condor position path explicitly selects the new Function branch. The old component-scenario calculator and legacy spread job remain for existing compatibility consumers. The new calculation has no Domain.OptionPricer dependency. The retired distribution callback was removed from the active input reader. Terminal failures reuse the existing typed `TradePlanFailedEvent` contract; successful completions use the new price-specific event.

Current observations remain independent of this projection. The snapshot keeps current marks, PnL and Greeks; the forward threshold does not depend on current DailyPnl being negative. The recommendation is persisted through existing plan generation; no monitor consumer or broker exit workflow is added. The existing DailyPnl-based notification suppression policy is unchanged.

### Verification and deferred qualification

Backend API and Trade test-project builds passed with zero warnings/errors; Rust `cargo check` passed. Logs: `.artifacts/forward-price-api-build.log`, `.artifacts/forward-price-test-build.log` and `.artifacts/forward-price-rust-check.log`. The test project was compiled only; its tests were not executed. Compilation is performed with isolated backend outputs; running development apps are not restarted.

At the owner's request, numerical, real NATS, database round-trip, failure-injection, UI automation and performance qualification are deferred. Earlier legacy migration-gate tests document the rejected sign-inversion counterexample; they do not qualify the newly connected actor path. The verification checklists above remain open until those tests are executed. This implementation is not described as live-qualified.


### Forward-price failure fallback

When the ForwardPrice Function returns an error, plan generation continues with current position values. The snapshot stores `ForwardPrice = 0`, `CombinedForwardPrice = 0` and the append-only `ForwardPriceCalculationError` string (MessagePack key 102), prefixed with `Forward price calculation error:` and followed by the returned error code/detail and message. The same message is carried in the plan explanation/exit reason and persisted with the full plan payload.

The zero is an explicit failure fallback. Forward-risk evidence remains unavailable rather than being fabricated as a successful zero-risk calculation. Existing unavailable-risk status/action behavior is preserved. A subsequent successful calculation supplies the actual forward price and clears the error string. Numerical/end-to-end testing remains deferred as requested; a focused persistence regression case has been added for later execution.


## Test execution results - 2026-10-20

The owner subsequently authorized test execution. The previously deferred numerical and error-fallback tests have now run.

| Suite | Passed | Failed | Skipped | Evidence |
| --- | ---: | ---: | ---: | --- |
| Full Domain.Trade unit suite, including new forward-price cases | 1567 | 0 | 0 | `.artifacts/forward-price-trade-suite.log` |
| Real Function actors/repositories and disposable PostgreSQL event log + trade-plan store | 3 | 0 | 0 | `.artifacts/forward-price-postgres-tests.log` |

Verified: raw positive 21 + 9 = 30 without a short-position sign inversion; formula-generated negative price -77.2; zero-price inputs; signed scaling using actual multipliers and quantities; exact maximum-loss threshold; exit recommendation despite positive current daily PnL; MessagePack input evidence; failure fallback with persisted zero price and explicit error. Database tests cover successful completion committed before reply, idempotent replay with one source event, persisted full-plan risk evidence, and failed Function calculation followed by a persisted error-bearing plan without a successful forward-risk payload.

One convention test initially failed because its explicit inventory omitted the new Function actor. The inventory now includes it and applies the existing frozen-policy-map checks. The first database attempts also revealed an uninitialized stream-ID cache substitute in the new harness; the harness now uses the real cache adapter with a cache-miss test double. These were corrected and the relevant suites rerun successfully. No production calculation change was required by these findings.

Scope: numerical inputs are synthetic controlled fixtures, not provider market data. Integration uses actual actor dispatch, repositories and PostgreSQL, with direct request dispatch and mocked auxiliary service capabilities. Production NATS routing, running UI, live provider operation, failure-injection/load qualification and latency/percentile measurements are not established by this run. Disposable infrastructure was cleaned up by the fixture; running development applications were not restarted.


## Entry-credit projected PnL v2 - implementation and test results

Approved change: retain the original uncapped delta/time amplification and direct put-plus-call price sum. The Function still returns raw projected closing cost. The trade plan now computes gross whole-trade projected PnL from the weighted opening-fill prices already stored on its position legs:

```text
EntryCreditPoints = -sum(OpeningPrice * SignedQuantity) / completeStrategyQuantity
ForwardPnl = (EntryCreditPoints - ForwardPrice) * actualMultiplier * completeStrategyQuantity
ForwardLoss = min(0, ForwardPnl)
ForwardLossRatio = -ForwardLoss / positiveMaxLossLimit
ExitRecommended = ForwardLoss <= -positiveMaxLossLimit
```

Entry credit and ForwardPnl are appended as nullable MessagePack keys 8 and 9 on IronCondorForwardRisk. Fresh results explicitly use `IronCondor/EntryCreditForwardPnl/v2`; old v1 records keep their original version/values and absent basis fields. The generic plan envelope receives projected PnL and a nonnegative loss magnitude for compatibility; the typed risk retains signed ForwardLoss. The actual contract currency and multiplier are unchanged. This calculation excludes fees, slippage and funding adjustments, matching the agreed minimal gross-PnL formula.

A failed Function still produces zero forward price and the persisted error message, with no successful risk/PnL result. Missing or nonpositive opening-credit basis also reports unavailable evidence; it is not assumed to be zero or converted with Abs. No extra database query is required to capture entry credit. Historical numerical/test records above refer to their then-current v1 rule and do not supersede this v2 contract.

### Same coherent valuation scenarios rerun

All rows use entry credit 35.642705870764 points, quantity 1, multiplier USD 50 and a USD 1000 whole-trade loss budget. Current daily PnL remains a separate prior-session observation; the new projected PnL uses the original entry reference.

| Scenario | Current daily PnL USD | Forward closing cost points | Projected whole-trade PnL USD | Signed ForwardLoss USD | Exit recommendation |
| --- | ---: | ---: | ---: | ---: | --- |
| Large profit | +1020.66 | 10.44093965 | +1260.09 | 0.00 | No |
| Small profit | +57.51 | 29.78886600 | +292.69 | 0.00 | No |
| Flat | +0.00 | 30.93460909 | +235.40 | 0.00 | No |
| Small loss | -31.14 | 31.55457283 | +204.41 | 0.00 | No |
| Large loss | -671.06 | 99.34726587 | -3185.23 | -3185.23 | Yes |

The large-loss fixture's actual whole-trade PnL is approximately -426.38 USD, while its amplified projected PnL is -3185.23 USD. The recommendation therefore triggers before actual whole-trade PnL reaches -1000 USD; this is explicitly asserted. Amplification is not capped at the physical payoff bound.

### Verification

- Full Trade unit suite: **1582 passed, zero failed/skipped**. Log: `.artifacts/forward-price-entry-credit-trade-tests.log`.
- Focused forward-price/error/compatibility tests: **34 passed**. Includes old v1 payload decoding, entry-credit basis, alternate multiplier, quantity scaling, threshold equality, no current-daily-PnL gate and missing-entry handling. Log: `.artifacts/forward-price-entry-credit-focused-tests.log`.
- Five coherent valuation cases plus full-precision evidence export: **6 passed** after the explicit anticipatory-exit assertion. Log: `.artifacts/forward-price-entry-credit-scenario-tests.log`.
- Actual Function actors/repositories and isolated PostgreSQL persistence: **3 passed**, covering completed source-event persistence/replay, entry-credit/PnL/version round trips, exit at the exact budget and failed Function followed by a stored error-bearing plan. Log: `.artifacts/forward-price-entry-credit-postgres-tests.log`.
- API Server Core build passed with zero warnings/errors. Log: `.artifacts/forward-price-entry-credit-api-build.log`.

The focused/scenario tests overlap the full unit suite and are not additional unique full-suite counts. Fixtures use controlled computed prices or explicitly identified boundary inputs; no live-provider or production NATS/UI/load qualification is claimed. Running development apps were not restarted. Full-precision v2 outputs replace the scenario CSV/JSON files in `.artifacts/forward-price-coherent-valuations/` and include projected PnL, signed loss, ratio, headroom and version.


## Fixed amplification strength 0.20 (2026-10-20)

Supersedes the strength-2 numerical results above for newly calculated forward prices. Owner approved a hardcoded strength of 0.20; short-option delta selection, the remaining/opening lifetime square-root factor, raw component addition and entry-credit PnL scaling are retained. No runtime configuration or new dynamic factor is introduced.

```text
Adjustment = 0.20 * Abs(ShortOptionDelta) * Sqrt(RemainingDays / OpeningDays)
Risk-side price = BaseSpreadPrice * (1 + Adjustment)
Other-side price = BaseSpreadPrice * (1 - Adjustment)
```

For equal delta, the time adjustment is greatest at entry and decreases toward expiry. Actual dollar PnL and projected dollar PnL both scale by the actual multiplier and strategy quantity; the configured maximum-loss limit remains a whole-trade currency budget. A quantity-one payoff bound is not an implementation blocker. This heuristic does not guarantee that projected loss exceeds actual loss for every combination of component prices, deltas and marks versus theoretical prices.

New Function results explicitly use model version `IronCondor/DeltaTimeStrength020RawSum/v2`. The serialized record's historical default remains the strength-2 v1 version for older payloads; the risk/entry-credit equation version is unchanged. Earlier events are not recalculated.

Verification includes the coherent five-scenario valuations, a 7207 underlying-price / 20% volatility call-side adverse scenario near 700 USD actual whole-trade loss with projected loss exceeding 1000 USD, and a regression for the maximum time adjustment at entry. Scenario CSV/JSON evidence is regenerated by the valuation test. This produces recommendations only; it does not add automatic broker exit execution.
