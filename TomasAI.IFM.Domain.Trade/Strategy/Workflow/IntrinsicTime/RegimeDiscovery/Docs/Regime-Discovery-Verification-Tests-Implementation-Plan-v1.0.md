# Regime Discovery Verification Tests Implementation Plan v1.0

| Item | Value |
|---|---|
| Status | Complete; RDV-00 through RDV-10 and RD-20 through RD-25 qualified |
| Created | 2026-08-28 |
| Source specification | `Regime-Discovery-Verification-Tests-v1.0.md` |
| Target project | `TomasAI.IFM.Domain.Trade.VerificationTests` |
| Target folder | `Strategy/IntrinsicTime/RegimeDiscovery` |

## 1. Objective

Implement an independently runnable Regime Discovery business-verification suite that executes production calculation and workflow code, validates authoritative Daily/Weekly/Monthly results, and proves that only a completed Regime result selects and dispatches Market Condition.

The plan does not move existing tests out of their projects. Existing Unit, BDD, and Integrated suites remain regression coverage. Shared test infrastructure may be extracted only when doing so removes literal duplication without weakening test ownership or introducing a test-project dependency cycle.

## 2. Gate sequence

```text
RDV-00 Baseline and specification freeze
  -> RDV-01 Verification project and solution registration
  -> RDV-02 Runtime fixture, isolation, and Market Condition probe
  -> RDV-03 Scenario catalog and deterministic data builders
  -> RDV-04 Golden-vector calculation verification
  -> RDV-05 Daily/Weekly/Monthly workflow verification
  -> RDV-06 Extended structure and directional scenarios
  -> RDV-07 Volatility, Fusion, and optional-data scenarios
  -> RDV-08 Failure, timeout, and projection atomicity
  -> RDV-09 Persistence, exactly-once continuation, and final qualification
  -> RDV-10 Decision V2 input-maximization and combination qualification
```

No later gate is complete until its own targeted tests and all earlier verification tests are green.

## 3. Gates

### RDV-00 — Baseline and specification freeze

Steps:

1. Preserve and inventory the current uncommitted Command-handler and documentation work.
2. Record the solution build and current Trade Unit, BDD, and Integrated suite baselines.
3. Run the existing focused runtime success test and record its duration/result.
4. Freeze the expected vectors, especially the distinction between `TrendingUp` (`BreakoutDistanceAtr = 0.4`) and `BullishBreakout` (`0.6`).
5. Confirm that `StrategyWorkflowStage.MarketCondition` in the committed state update is the authoritative next-pipeline enum.

Exit gate:

- baselines are recorded;
- unrelated user changes are preserved;
- the verification specification has no unresolved business-result ambiguity.

### RDV-01 — Verification project and solution registration

Steps:

1. Create `TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj` targeting `net10.0`.
2. Configure xUnit, FluentAssertions, Microsoft.NET.Test.Sdk, coverlet, and the ASP.NET test-host packages using repository-aligned versions.
3. Add only the production and infrastructure project references required for the runtime path. Do not reference `Domain.Trade.UnitTests`, `Domain.Trade.BDDTests`, or `Domain.Trade.IntegratedTests` merely to reuse internal test code.
4. Add the project to `TomasAI.IFM.sln` under the Trade solution grouping.
5. Create `Strategy/IntrinsicTime/RegimeDiscovery` and establish the approved namespace.
6. Add a category/assembly smoke test proving discovery under `Category=Verification`.

Exit gate:

- the new project restores, builds, is discovered by `dotnet test`, and can be filtered independently;
- no production behavior has changed.

### RDV-02 — Runtime fixture, isolation, and Market Condition probe

Steps:

1. Create the non-parallel `RegimeDiscoveryVerificationCollection`.
2. Create a verification fixture based on the production integration host, real actor supervisor, NATS, PostgreSQL, ConfigurationDb, ScyllaDB, and market-signal cache.
3. Add bounded wait helpers that include last-observed state in timeout failures.
4. Implement `MarketConditionPipelineCommandProbe` to capture complete commands, not only counts.
5. Ensure the probe responds successfully but publishes no terminal event and starts no later stage.
6. Implement per-scenario cache cleanup, unique identities, and workflow-ID-based database lookup.
7. Prove fixture startup and shutdown do not hang and do not leak actors/producers between tests.

Exit gate:

- one infrastructure qualification test starts the host, receives a probe command, and shuts down cleanly;
- tests cannot execute in parallel against fixed actor addresses.

### RDV-03 — Scenario catalog and deterministic data builders

Steps:

1. Add the immutable `RegimeDiscoveryScenario` contract and typed expected-result records.
2. Implement the authoritative `TrendingUp`, `BullishBreakout`, and mirrored `TrendingDown` vectors.
3. Implement `RangeBound`, `BearishBreakout`, `Compressing`, `ExpandingUp`, and `Transitioning` vectors.
4. Implement volatility/restriction and optional/required-data variants without duplicating complete metric dictionaries.
5. Build every signal set from `RegimeDiscoverySnapshotRequestFactory` requirements.
6. Publish a real default parameter set per target horizon and retain its identity/version for assertions.
7. Add builder self-tests proving all required requirements are populated unless the scenario intentionally removes or corrupts one.

Exit gate:

- scenario data is deterministic, reviewable, complete, and cannot silently omit a newly required metric.

### RDV-04 — Golden-vector calculation verification

Steps:

1. Execute production Trend, Volatility, Market Structure, and Fusion models with fixed timestamps and freshness.
2. Verify the current Trending Up scores for every horizon: `0.799250`, `0.353125`, `0.966667`, `0.857846`, and `0.706383`.
3. Verify exact Bullish Breakout scores: structure `0.300000`, Decision `0.624512`, and conviction `0.514247`.
4. Verify deterministic evidence/reason ordering and expected reason codes.
5. Verify sequential and parallel calculations serialize byte-identically.
6. Verify the same uniform market vector produces identical scores for Daily, Weekly, and Monthly while using TDI on every correct configured evidence timeframe.

Exit gate:

- every authoritative exact number in the verification specification is executable and green.

### RDV-05 — Daily/Weekly/Monthly workflow verification

Steps:

1. Publish one real `FuturesItiSignalGeneratedEvent` for each supported target horizon.
2. Assert revision 1 is committed as Started/RegimeDiscovery before calculation continuation.
3. Execute the real Regime Function and deserialize the projected result.
4. Apply the complete Trending Up assertions for each horizon.
5. Cross-check result identities, parameter version, trigger identity, result envelope, payload, and hashes.
6. Assert revision 2 selects `StrategyWorkflowStage.MarketCondition`.
7. Assert the Market Condition probe receives exactly one command with revision 2 and the immutable completed Regime view.
8. Assert no later pipeline command is dispatched.

Exit gate:

- Daily, Weekly, and Monthly each prove correct calculations plus exact workflow continuation.

### RDV-06 — Extended structure and directional scenarios

Steps:

1. Add runtime verification for Trending Down and Range Bound.
2. Add Bullish and Bearish Breakout, proving breakout precedence.
3. Add Compressing, Expanding Up, and Transitioning classifications.
4. Assert exact classifications, direction, breakout state, stable reason codes, and formula scores where the specification fixes them.
5. Assert Transitioning completes with a `Transition` restriction and still dispatches Market Condition exactly once.

Exit gate:

- every Market Structure V1 classification and both directional signs have at least one executable positive scenario.

### RDV-07 — Volatility, Fusion, and optional-data scenarios

Steps:

1. Add Extreme Volatility and Severe Backwardation scenarios.
2. Add Volatility Expanding and Contracting scenarios.
3. Add Direction Conflict and Low Confidence Fusion scenarios.
4. Add missing realized-volatility and missing optional-timeframe scenarios.
5. Assert restrictions and stable reason codes are preserved in the Regime result and immutable workflow envelope.
6. Assert restricted or degraded completed results still advance to Market Condition exactly once.

Exit gate:

- Volatility level/change/term-structure and all V1 Fusion restrictions are verified without confusing restriction with Function failure.

### RDV-08 — Failure, timeout, and projection atomicity

Steps:

1. Add required missing, stale, not-warm, invalid, unsupported-schema, and calculation-version-mismatch cases.
2. Add fixed maximum-execution timeout with a controlled blocking snapshot/calculation seam.
3. Add a throwing Function projector verification.
4. Add an incomplete-specialist verification.
5. Assert each case returns or translates the correct typed failure and workflow terminal classification.
6. Assert no successful Regime projection, completed Function state, completed workflow envelope, or Market Condition command exists.
7. Assert a late worker cannot project/save completion after timeout.

Exit gate:

- every specified failure is demonstrably fail-closed at both persistence and downstream-dispatch boundaries.

### RDV-09 — Persistence, exactly-once continuation, and final qualification

Steps:

1. Cross-check PostgreSQL Function state, Scylla Regime projection, workflow event state, workflow read model, and Query API for successful scenarios.
2. Prove duplicate/current completion cannot dispatch Market Condition twice.
3. Prove concurrent Daily/Weekly/Monthly workflows remain isolated.
4. Prove the suite is stable under repeated focused execution and full-suite load.
5. Run `dotnet format`/repository formatting checks where applicable and `git diff --check`.
6. Run the solution build plus Trade Unit, BDD, Integrated, and Verification suites.
7. Record exact pass counts, durations, infrastructure prerequisites, and any intentionally deferred scenario.
8. Update this plan to Complete only when there are no skipped verification placeholders.

Exit gate:

- all definition-of-done conditions in the verification specification are green;
- verification can be run independently with a documented command;
- no failure path can select a later pipeline.

### RDV-10 — Decision V2 input-maximization and combination qualification

Steps:

1. Verify exact trigger-event authority for target-horizon price, ITI direction,
   band, reversal, provenance, and front VX.
2. Verify TDI cache publication and optional 75/25 ITI/TDI weighting with a
   full-ITI fallback when TDI is absent.
3. Verify spot VIX, front VX, and Daily term structure remain distinct.
4. Verify direct price/high/low/ATR breakout derivation and confidence reduction
   when the supplied breakout signal disagrees.
5. Verify schema-V1-shaped result deserialization into the wire-compatible
   schema-V2 nested `RegimeDiscoveryDecision`.
6. Execute the 12-case minimum reasonable pairwise Decision matrix and assert
   mirrored language, restrictions, confidence, and conviction bounds.
7. Add BDD scenarios for aligned direction, transition/expansion, extreme
   volatility, and specialist direction conflict.
8. Strengthen live integration to deserialize schema V2, validate Decision
   specialist fields, and require real TDI supporting evidence.

Exit gate:

- all RD-20 through RD-25 inputs have executable positive and fallback tests;
- no Decision field is inferred from a downstream trade hint;
- the combination matrix is broad enough to cover common market language but
  intentionally avoids a low-value full Cartesian product.

## 4. Planned test commands

Focused project:

```powershell
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj --filter "Category=Verification"
```

Regime Discovery only:

```powershell
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj --filter "FullyQualifiedName~Strategy.IntrinsicTime.RegimeDiscovery"
```

Final regression qualification:

```powershell
dotnet build TomasAI.IFM.sln --no-restore -m:1
dotnet test TomasAI.IFM.Domain.Trade.UnitTests/TomasAI.IFM.Domain.Trade.UnitTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.BDDTests/TomasAI.IFM.Domain.Trade.BDDTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests/TomasAI.IFM.Domain.Trade.IntegratedTests.csproj --no-build
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests/TomasAI.IFM.Domain.Trade.VerificationTests.csproj --no-build
```

## 5. Scope controls

This plan does not:

- implement Market Condition or any later strategy calculation;
- use a fake Regime Function, fake calculation model, or fake completed result in positive runtime verification;
- replace exact golden-vector unit verification with broad confidence assertions;
- treat a restricted completed regime as a failed Regime calculation;
- introduce replay, resume, or durable publication behavior;
- redesign ConfigurationDb; or
- move or delete existing regression tests solely because verification coverage now exists.

## 6. Implementation record

RDV-00 through RDV-09 were completed on 2026-08-28. RDV-10 and RD-20 through
RD-25 were completed on 2026-08-29.

| Gate | Status | Qualification evidence |
|---|---|---|
| RDV-00 | Complete | Existing uncommitted workflow work was inventoried and preserved; the specification fixes the Trending Up and Bullish Breakout vectors and names `MarketCondition` as the next-stage selector. |
| RDV-01 | Complete | `TomasAI.IFM.Domain.Trade.VerificationTests` targets `net10.0`, is registered in `TomasAI.IFM.sln`, builds independently, and exposes the `Verification` category. |
| RDV-02 | Complete | A non-parallel runtime fixture uses the production host and infrastructure; a passive typed Market Condition command probe captures commands without advancing the workflow. |
| RDV-03 | Complete | Immutable scenarios and requirement-driven deterministic builders cover all supported horizons and detect missing required observations. |
| RDV-04 | Complete | Production Trend, Volatility, Market Structure, and Fusion calculations pass exact Trending Up and Bullish Breakout golden values, fixed freshness, deterministic ordering, and parallel determinism checks. |
| RDV-05 | Complete | Concurrent Daily, Weekly, and Monthly signals execute the real workflow and Function path, retain input revision 1, commit revision 2 at Market Condition, and dispatch exactly one immutable next-stage command. |
| RDV-06 | Complete | Trending Down, Range Bound, bullish/bearish breakout, Compressing, Expanding Up, and Transitioning scenarios are verified. |
| RDV-07 | Complete | Extreme volatility, backwardation, contraction, directional conflict, low confidence, and optional-evidence degradation/restriction behavior are verified. |
| RDV-08 | Complete | Required-data availability failures, fixed timeout with late-completion fencing, and Function-projector failure all terminate fail-closed with no successful projection or downstream command. |
| RDV-09 | Complete | PostgreSQL Function state, Scylla projection, workflow state/read model, Query API, duplicate-trigger idempotency, and concurrent-horizon isolation are cross-checked; all regression commands below are green. |
| RDV-10 | Complete | Exact trigger authority, TDI acquisition/weighting, VIX/VX separation, direct breakout derivation, Decision schema compatibility, 12 pairwise combinations, four BDD scenarios, and live schema-V2/TDI integration assertions are executable and green. |

### Final qualification results

| Qualification | Result |
|---|---|
| Serialized full solution build | Passed; 0 warnings, 0 errors; 1 minute 30.92 seconds (2026-08-29 RD-25 rerun) |
| Verification project format check | Passed; no changes required |
| Trade Unit | Passed 323/323; 0 skipped |
| Market Data Analytics Unit | Passed 946/946; 0 skipped |
| Trade BDD | Passed 18/18; 0 skipped |
| Trade Integrated | Passed 46/48; 2 pre-existing explicit skips |
| Trade Verification | Passed 67/67; 0 skipped; 59 seconds |

The solution build uses `-m:1` because two DataBento projects invoke the same native build output. A normal parallel build can race on `databento_feed_native.lastbuildstate`; this is a build-output lock, not an RDV failure.

Full-solution qualification also required the integration-test `ForwardingEventProducer` to forward the current `IActorProducer.RequestFunctionAsync` contract. This was a test-harness compatibility update and did not change production behavior.
