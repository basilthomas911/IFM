# Regime Discovery Verification Tests v1.0

| Item | Value |
|---|---|
| Status | Implemented and qualified; RDV-00 through RDV-09 complete |
| Created | 2026-08-28 |
| Scope | Deterministic Regime Discovery calculations and their Strategy Workflow continuation boundary |
| Test project | `TomasAI.IFM.Domain.Trade.VerificationTests` |
| Test folder | `Strategy/IntrinsicTime/RegimeDiscovery` |

Companion documents:

- `Regime-Discovery-Specification-v1.0.md`
- `Regime-Discovery-Implementation-v1.0.md`
- `Regime-Discovery-Atomic-Workflow-Implementation-Plan-v1.0.md`
- `Regime-Discovery-Verification-Tests-Implementation-Plan-v1.0.md`

## 1. Purpose

This document defines the executable business verification for Regime Discovery. The verification suite proves both that the production calculation produces the intended economic classifications and that a valid completed result advances the Intrinsic Time Strategy Workflow to the correct next pipeline exactly once.

The suite is intentionally stronger than a transport smoke test. A non-empty result payload is insufficient. A successful verification must deserialize the real `RegimeDiscoveryResult` and validate Trend, Volatility, Market Structure, Fusion, evidence, restrictions, persistence, and workflow continuation.

Verification tests complement rather than replace the existing test layers:

- Unit tests continue to qualify individual formulas, boundaries, serialization, and actor helpers.
- BDD tests continue to describe business state transitions.
- Integrated tests continue to protect broad runtime topology and infrastructure behavior.
- Verification tests provide named, reviewable market scenarios with authoritative expected business outcomes through the production execution path.

## 2. Required production path

Successful runtime scenarios must execute this path:

```text
FuturesItiSignalGeneratedEvent
  -> IntrinsicTimeStrategyWorkflowRealtimeActor
  -> ExecuteIntrinsicTimeStrategyWorkflowCommand
  -> WorkflowStrategyStateUpdatedEvent
       Status = Started
       CurrentStage = RegimeDiscovery
       WorkflowRevision = 1
  -> ExecuteRegimeDiscoveryPipelineCommand
  -> RegimeDiscoveryFunctionActor
  -> production snapshot provider
  -> production Trend + Volatility + Market Structure + Fusion models
  -> RegimeDiscoveryPipelineCompletedEvent returned directly to caller
  -> synchronous RegimeDiscovery Function projection
  -> completed-only Function state append
  -> CompleteRegimeDiscoveryCommand
  -> WorkflowStrategyStateUpdatedEvent
       Status = Started
       CurrentStage = MarketCondition
       WorkflowRevision = 2
  -> StartMarketConditionPipelineCommand exactly once
```

The verification host must use the production Workflow Command actor, Workflow Realtime actor, Regime Discovery Function actor, snapshot provider, calculation models, PostgreSQL event storage, ConfigurationDb parameter resolution, NATS request/reply, and ScyllaDB projection.

Only the not-yet-implemented Market Condition pipeline may be replaced. Its replacement is a passive command probe that records the received command and returns an accepted response. It must not publish a completion or advance another stage.

## 3. Meaning of the next-pipeline enum

Regime Discovery does not publish a separate next-actor instruction event. The authoritative next-pipeline selector is:

```csharp
WorkflowStrategyStateUpdatedEvent.State.CurrentStage
```

After Regime Discovery completes, its required value is:

```csharp
StrategyWorkflowStage.MarketCondition
```

The Workflow Realtime actor uses that enum to select and dispatch `StartMarketConditionPipelineCommand`. Verification must therefore prove both the committed enum and the resulting exact command dispatch. Observing only one of these is incomplete.

## 4. Verification-test ownership and structure

The project must use the following structure:

```text
TomasAI.IFM.Domain.Trade.VerificationTests/
  TomasAI.IFM.Domain.Trade.VerificationTests.csproj
  Strategy/
    IntrinsicTime/
      RegimeDiscovery/
        RegimeDiscoveryVerificationCollection.cs
        RegimeDiscoveryVerificationFixture.cs
        RegimeDiscoveryScenario.cs
        RegimeDiscoveryScenarioCatalog.cs
        RegimeDiscoveryScenarioDataBuilder.cs
        RegimeDiscoveryVerificationAssertions.cs
        RegimeDiscoveryGoldenVectorVerificationTests.cs
        RegimeDiscoveryWorkflowVerificationTests.cs
        RegimeDiscoveryRestrictionVerificationTests.cs
        RegimeDiscoveryFailureVerificationTests.cs
        MarketConditionPipelineCommandProbe.cs
```

The namespace is:

```csharp
TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery
```

The suite must be marked `Trait("Category", "Verification")`. Runtime verification must be placed in one non-parallel xUnit collection because actors use fixed addresses and the market-signal cache is process-wide.

## 5. Scenario data rules

Each scenario owns:

- a stable scenario name;
- a unique futures contract identity;
- a target horizon;
- a complete map of metric values or explicit overrides from a named baseline;
- expected specialist classifications and exact deterministic scores where applicable;
- expected Fusion direction, score, restrictions, confidence band, and quality;
- expected reason codes;
- whether a completed projection must exist; and
- whether the workflow may dispatch Market Condition.

Scenario builders must populate the exact requirements returned by `RegimeDiscoverySnapshotRequestFactory`. Tests must not create a smaller hand-selected signal set that accidentally bypasses a configured required timeframe.

Every runtime scenario must publish a versioned Regime Discovery parameter set through the real ConfigurationDb API. The result must retain the selected parameter-set ID and version.

Contract IDs and workflow IDs must be unique per scenario. Static cache state must be cleared before and after each runtime scenario. Database assertions must query by workflow identity rather than assuming an empty database.

## 6. Authoritative positive Trending Up vector

The following baseline represents an established bullish trend with normal volatility and trending bullish market structure.

### 6.1 Trend evidence

Apply the values to every observation timeframe configured for the target horizon:

| Metric | Value |
|---|---:|
| CurrentPrice | 105 |
| Ema20 | 103 |
| Ema50 | 101 |
| Ema200 | 99 |
| Ema20Slope | 0.08 |
| Ema50Slope | 0.06 |
| Ema200Slope | 0.04 |
| Rsi14 | 65 |
| Rsi14Slope | 2 |
| Adx14 | 30 |
| PlusDi14 | 30 |
| MinusDi14 | 15 |
| MacdHistogram | 0.5 |
| Atr14 | 2 |

### 6.2 Volatility and structure evidence

Apply these values to the target horizon:

| Metric | Value |
|---|---:|
| VixLevel | 18 |
| AtrBaselineRatio | 1.0 |
| VxFrontSecondRatio | 0.95 |
| PriorVolatilityComposite | 0.35 |
| RealizedVolatilityPercentile | 0.40 |
| BollingerWidthRatio | 1.0 |
| BollingerPosition | 0.5 |
| Ema20Interaction | 1.0 |
| AtrNormalizedRange | 1.0 |
| RollingHigh20 | 104 |
| RollingLow20 | 96 |
| BreakoutDistanceAtr | 0.4 |
| ItiDirection | 1.0 |
| ItiBandLevel | 1.2 |
| ItiReversalLevel | 0.1 |

`BreakoutDistanceAtr` is deliberately `0.4`. A value of `0.5` or greater is a breakout, and breakout classification has precedence over Trending.

### 6.3 Exact expected result

With default V1 parameters and equal values on all contributing observation timeframes:

| Result | Expected value |
|---|---|
| Trend complete | `true` |
| Trend direction | `Up` |
| Trend strength | `Strong` |
| Trend phase | `Established` |
| Trend score | `0.796750` |
| Volatility complete | `true` |
| Volatility level | `Normal` |
| Volatility change | `Stable` |
| VX term structure | `Contango` |
| Volatility score | `0.353125` |
| No new trade | `false` |
| Market Structure complete | `true` |
| Market Structure classification | `Trending` |
| Market Structure direction | `Up` |
| Breakout | `None` |
| Market Structure score | `0.966667` |
| Fusion complete | `true` |
| Fusion direction | `Up` |
| Fusion directional score | `0.856221` |
| Risk-adjusted conviction | `0.705044` |
| Restrictions | Empty |
| Confidence band | `VeryHigh` |
| Overall quality | `High` |

The exact scores are independent of wall-clock freshness for this vector. Exact confidence decimals are not stable in a runtime test because the production snapshot provider computes freshness from capture time. Runtime tests assert the confidence band, quality, and a safe minimum confidence. A model-level golden verification uses a fixed freshness factor and asserts exact confidence decimals.

## 7. Horizon verification

The Trending Up vector must be verified for all supported workflow horizons:

| Target horizon | Contributing Trend timeframes | Expected business result |
|---|---|---|
| Daily | 5m, 15m, 1h, 4h | Exact Trending Up result in section 6.3 |
| Weekly | 15m, 1h, 4h, Daily | Exact Trending Up result in section 6.3 |
| Monthly | 1h, 4h, Daily | Exact Trending Up result in section 6.3 |

Each horizon verification must additionally prove:

- `RegimeDiscoveryResult.TargetHorizon` equals the ITI signal timeframe;
- supporting evidence contains only the configured horizon timeframes and target-horizon specialist evidence;
- required evidence is present and available;
- the published parameter set selected for that horizon is retained in the result; and
- the workflow advances to Market Condition exactly once.

## 8. Existing bullish breakout vector

The current runtime integration seed uses the section 6 vector except:

```text
BreakoutDistanceAtr = 0.6
```

Its authoritative expected result is:

| Result | Expected value |
|---|---|
| Trend | `Up / Strong / Established` |
| Trend score | `0.796750` |
| Volatility | `Normal / Stable / Contango` |
| Volatility score | `0.353125` |
| Market Structure | `BreakingOut / Up` |
| Market Structure score | `0.300000` |
| Fusion direction | `Up` |
| Fusion directional score | `0.622888` |
| Risk-adjusted conviction | `0.512909` |
| Restrictions | Empty |

This scenario must remain as an explicit `BullishBreakout` verification. It must not be described or asserted as Market Structure Trending.

## 9. Extended successful scenario matrix

| Scenario | Principal input change | Required outcome |
|---|---|---|
| TrendingDown | Mirror directional Trend and structure metrics below zero | Trend `Down/Strong/Established`; Structure `Trending/Down`; Fusion `Down`; no restriction |
| RangeBound | Flat EMA alignment/slopes, RSI 50, balanced DI, zero MACD and ITI direction, neutral structure organization | Trend `Neutral/None/RangeBound`; Structure `Ranging/Neutral`; Fusion `Neutral` |
| BullishBreakout | `BreakoutDistanceAtr = 0.6` | Structure `BreakingOut/Up`; breakout precedence proven |
| BearishBreakout | `BreakoutDistanceAtr = -0.6` with bearish organization | Structure `BreakingOut/Down` |
| Compressing | Width ratio at or below 0.75 and ATR ratio at or below 0.85; no breakout | Structure `Compressing` |
| ExpandingUp | Width ratio at or above 1.25 or ATR ratio at or above 1.25; positive organization; no breakout | Structure `Expanding/Up` |
| Transitioning | Mid-range width/ATR, absolute organization between ranging and trending thresholds, no breakout | Structure `Transitioning`; Fusion contains `Transition` |
| OptionalEvidenceMissing | Omit realized-volatility evidence or an optional Trend timeframe | Calculation completes; `OptionalDataMissing`; confidence/quality is degraded as defined by the formulas |

All completed scenarios must persist a completed Regime read model and may advance to Market Condition. A restriction is an immutable input to the next pipeline, not a Regime Discovery failure.

## 10. Volatility and Fusion restriction matrix

| Scenario | Principal input change | Required outcome |
|---|---|---|
| ExtremeVolatility | VIX at or above 30 and/or composite score at or above 0.75 | `NoNewTrade = true`; Fusion contains `NoNewTrade` |
| SevereBackwardation | VX front/second ratio at or above 1.05 | warning/restriction evidence appropriate to V1 rules; no silent normalization |
| VolatilityExpanding | Composite exceeds prior composite by at least 0.10 | Change `Expanding` and expansion reason |
| VolatilityContracting | Prior composite exceeds current composite by at least 0.10 | Change `Contracting` and contraction reason |
| DirectionConflict | Directional Trend and Market Structure point in opposite directions | Fusion contains `DirectionConflict` |
| LowConfidence | Deliberately conflicting but complete evidence | Fusion contains `LowConfidence`; quality is `Low` or `Degraded` as formula dictates |

Each restricted completed result must still prove result projection, workflow revision 2, `CurrentStage = MarketCondition`, and exactly one Market Condition command. Later pipeline logic is responsible for honoring the restrictions.

## 11. Failure and fail-closed matrix

| Scenario | Required terminal behavior |
|---|---|
| Required signal missing | Typed failed Function result; workflow Failed; no Regime completed projection; no Market Condition command |
| Required signal stale | Same fail-closed behavior with stale-data reason |
| Required signal not warm | Same fail-closed behavior with not-warm reason |
| Required signal invalid | Same fail-closed behavior with invalid-data reason |
| Unsupported schema | Same fail-closed behavior with schema reason |
| Calculation-version mismatch | Same fail-closed behavior with version reason |
| Calculation timeout | Workflow TimedOut; no completed projection; no next-pipeline command |
| Function projector exception | Function returns failed; completed Function state is not appended; workflow cannot advance |
| Malformed or incomplete specialist result | Fusion incomplete; typed failure; no completed projection or next stage |

Failure assertions must prove absence, not merely a terminal status. Specifically, no successful Regime read model, completed workflow result envelope, or Market Condition command may exist.

## 12. Completion, projection, and workflow assertions

Every successful runtime scenario must assert all of the following:

1. The first committed workflow snapshot is revision 1, Started, and at Regime Discovery.
2. The Regime Function uses the matching workflow ID, entity ID, trigger event ID, workflow input revision, deadline, and parameter set.
3. `RegimeDiscoveryReadModel.Status` is `Completed`.
4. `ResultPayload` deserializes into the expected `RegimeDiscoveryResult`.
5. `ResultPayloadSha256` matches the payload.
6. The Function result envelope and workflow Regime stage envelope contain the same result ID, type, schema, payload, and hash.
7. The second committed workflow snapshot is revision 2 and remains a running/started workflow because later pipelines remain.
8. Regime stage processing status is Completed.
9. Market Condition stage processing status is Processing.
10. `CurrentStage` is `StrategyWorkflowStage.MarketCondition`.
11. Exactly one `StartMarketConditionPipelineCommand` is received.
12. The Market Condition command contains revision 2 and an immutable workflow view carrying the completed Regime result and its parameter identity.
13. No Trade Selection, Order Composition, Risk Management, or Order Execution command is produced by this verification.

## 13. Determinism rules

Golden-vector tests must use fixed GUIDs, fixed timestamps, a fixed `FreshnessFactor`, default versioned parameters, and production calculation classes. They assert exact six-decimal scores, exact confidence, deterministic evidence ordering, deterministic reason ordering, and byte-equivalence between sequential and thread-pool-parallel execution.

Runtime tests use real current timestamps and the production snapshot provider. They assert exact classifications and scores, but confidence by band/range. They must never use arbitrary sleeps as the primary synchronization mechanism; they poll or await explicit persisted/probe conditions with bounded deadlines and report the last observed state on timeout.

## 14. Definition of done

The Regime Discovery verification suite is complete when:

- the new VerificationTests project is registered in the solution and builds independently;
- all files live under `Strategy/IntrinsicTime/RegimeDiscovery` with the approved namespace;
- Daily, Weekly, and Monthly Trending Up vectors prove the exact specialist and Fusion results;
- the existing bullish vector is correctly verified as BreakingOut rather than Trending;
- extended success, restriction, optional-data, required-data, timeout, and projection-failure cases are executable;
- successful results prove completed projection and exactly-once Market Condition selection;
- every failure proves no downstream pipeline command;
- verification tests are non-parallel where shared infrastructure requires it;
- the Verification category can be run independently from Unit, BDD, and Integrated suites; and
- the full solution, Trade Unit, Trade BDD, Trade Integrated, and Trade Verification suites pass without skipped verification placeholders.
