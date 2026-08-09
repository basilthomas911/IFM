# QLNet to Black76 Migration and Implementation Plan

## Document status

- Status: Implementation specification; the Black76-only direction is approved and implementation has not started.
- Scope owner: `TomasAI.IFM.Framework.OptionPricer`.
- Trading scope: futures and futures options only.
- Pricing model: Black76 only.
- Follow-on work: return to the system-wide optimization plan after this migration is implemented and verified.

## Decision and objective

The IFM trading system will use one option-pricing model: Black76 for options on futures. QLNet will be removed from IFM product projects and from the option-Greeks execution paths.

The replacement must provide:

- theoretical prices for European futures options;
- implied volatility derived from an observed futures-option market price;
- Delta, Gamma, Vega, Theta, and Rho;
- deterministic, thread-safe, allocation-free calculations suitable for market-data processing;
- explicit validation and failure reasons instead of exceptions being swallowed and converted to zero values;
- numerical regression tests, integration tests, and repeatable performance benchmarks.

This scope deliberately does not add equity, equity-option, spot-FX, bond-option, stochastic-volatility, local-volatility, or American-option pricing. Expansion to another asset class or exercise model is deferred until the futures strategy and monitoring workflows are operational, stable, and producing meaningful results.

## Important model boundary

Black76 prices European options on forwards or futures. A contract being a futures option does not, by itself, prove that it has European exercise. Some futures options are American-style.

The system therefore must not silently treat an American-style or unknown-style contract as an exact Black76 valuation. The initial production policy is:

1. `European` exercise style is accepted and priced with Black76.
2. `American` exercise style is rejected as unsupported by the configured model.
3. Missing or `Unknown` exercise style is rejected until reference data identifies it.
4. No automatic American-to-European approximation is permitted in orders, risk, P&L, or strategy selection.

This preserves the one-model decision without presenting a Black76 approximation as an exact American-option result. If American futures options are intentionally added later, that is a separate reviewed model-extension project.

## Current implementation and reason for replacement

`TomasAI.IFM.Domain.OptionPricer.Shared/OptionCalculator.cs` currently:

- constructs a QLNet Black-Scholes-Merton process;
- supplies the futures price as the process underlying;
- creates an `AmericanExercise`;
- uses a Cox-Ross-Rubinstein binomial engine with 801 steps;
- solves implied volatility and then calculates Greeks;
- mutates QLNet global evaluation-date settings;
- serializes every calculation through a process-wide static lock;
- creates a large QLNet object graph for each operation;
- catches every exception and returns a failed result whose numeric fields are all zero.

This has both correctness and operational costs. The model is not the selected futures-option model, global settings prevent safe parallel execution, the allocation graph creates garbage-collection pressure, and a zero-filled failure can be confused with a valid zero Greek.

`TomasAI.IFM.Framework.OptionPricer/Black76/OptionModel.cs` already provides managed scalar and batch pricing, Greeks, and Newton-based implied-volatility inversion. It is static and has no mutable global state. The migration will harden and extend this implementation rather than introduce another pricing library.

## Existing production call paths

The QLNet-backed `OptionCalculator` currently has five direct production consumers:

| Consumer | Use |
| --- | --- |
| `TomasAI.IFM.Domain.MarketData.Feed/FuturesOptionTickData/Event/FuturesOptionTickBidAsk.cs` | Calculates implied volatility and Greeks from streaming futures-option bid/ask data. |
| `TomasAI.IFM.Framework.MarketData.InteractiveBrokers/IBMarketDataApi.cs` | Supplies snapshot futures-option Greeks. |
| `TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers/IBMarketDataApi.cs` | Legacy/service snapshot implementation. |
| `TomasAI.IFM.Domain.OptionPricer/SpreadDistribution/Job/Services/IronCondorSpreadDistributionJobService.cs` | Calculates per-leg implied volatility and Delta before Black76 spread calculations. |
| `TomasAI.IFM.UI.Net.ViewModels/Trade/IronCondor/IronCondorTradeOrderViewModel.cs` | Recalculates displayed trade-leg Greeks. |

`GetFuturesOptionSpreadData` consumes the snapshot API results indirectly. All these flows must use the same framework API and conventions after migration.

The spread-distribution job currently mixes QLNet per-leg calculations with Black76 spread valuation. The migration removes that model inconsistency.

## Target architecture

The target separates pure quantitative functions from feed and application policy:

```text
reference contract metadata
        |
        v
validate exercise style, option right, dates, quote, and rate
        |
        v
FuturesOptionGreeksCalculator.CalculateFromMarketPrice
        |
        +--> validate Black76 no-arbitrage price bounds
        +--> solve implied volatility with safeguarded Newton/bisection
        +--> OptionModel.PriceWithGreeks
        |
        v
typed success/failure result
        |
        +--> market-data tick computation
        +--> snapshot API
        +--> spread-distribution job
        +--> UI view-model projection
```

The framework calculation layer remains synchronous because it is CPU-only, bounded work and does no I/O. Callers must not wrap individual calculations in `Task.Run`. Concurrency belongs at the market-data pipeline or batch-partition level, while each calculation remains a small pure function.

## Public API design

### Strong types

Replace string and signed-integer option-type conventions at the public boundary with explicit enums:

```csharp
public enum FuturesOptionRight : byte
{
    Put = 0,
    Call = 1
}

public enum OptionExerciseStyle : byte
{
    Unknown = 0,
    European = 1,
    American = 2
}
```

The internal scalar hot path may continue to use a branch-friendly representation if benchmarks justify it, but the public contract must not interpret an unrecognized string as a put.

### Failure contract

Use an enum that callers and telemetry can classify without parsing text:

```csharp
public enum Black76FailureReason : byte
{
    None = 0,
    NonFiniteInput,
    InvalidForwardPrice,
    InvalidStrikePrice,
    InvalidMarketPrice,
    InvalidRiskFreeRate,
    InvalidDateRange,
    Expired,
    UnknownOptionRight,
    UnknownExerciseStyle,
    UnsupportedExerciseStyle,
    PriceBelowLowerBound,
    PriceAboveUpperBound,
    ImpliedVolatilityNotBracketed,
    ImpliedVolatilityDidNotConverge,
    NonFiniteResult
}
```

Expected bad market data returns a typed failure. Programmer-contract errors in low-level APIs may still throw `ArgumentException` or `ArgumentOutOfRangeException`. No blanket `catch` is allowed inside the mathematical implementation.

### Request and result

Add immutable value types similar to:

```csharp
public readonly record struct FuturesOptionGreeksRequest(
    DateOnly ValueDate,
    DateOnly MaturityDate,
    double FuturesPrice,
    double StrikePrice,
    double MarketPrice,
    double RiskFreeRate,
    FuturesOptionRight Right,
    OptionExerciseStyle ExerciseStyle);

public readonly record struct FuturesOptionGreeksResult(
    bool Success,
    Black76FailureReason FailureReason,
    double TimeToExpiry,
    double ImpliedVolatility,
    double TheoreticalPrice,
    double Delta,
    double Gamma,
    double Vega,
    double Theta,
    double Rho,
    int SolverIterations);
```

The primary entry point is a static, pure method:

```csharp
public static FuturesOptionGreeksResult CalculateFromMarketPrice(
    in FuturesOptionGreeksRequest request,
    in Black76SolverSettings settings = default);
```

If optional `in` defaults are awkward for the chosen C# compiler, expose overloads instead. The implementation requirement is an immutable, allocation-free input/result contract, not this exact syntax.

### Solver settings

Use validated defaults held in an immutable settings value:

- minimum volatility: `1e-6`;
- maximum volatility: `4.0` (400 percent), matching the existing QLNet search ceiling;
- initial volatility: `0.20`;
- absolute price tolerance: `1e-10`;
- relative price tolerance: `1e-10`;
- volatility interval tolerance: `1e-10`;
- maximum iterations: 100;
- Vega floor: `1e-12`.

These are numerical solver settings, not trading risk limits. A later configuration change must be tested against recorded contracts before deployment.

## Financial conventions

All consumers must share the following conventions:

- underlying input is the current futures price `F`, not a spot price;
- strike is `K` in the same price units as `F`;
- rate is an annual continuously compounded decimal rate;
- volatility is annualized and expressed as a decimal;
- time to expiry is `max(0, maturityDate.DayNumber - valueDate.DayNumber) / 365.0`;
- the date convention is Actual/365 Fixed;
- the calculation is for a European option on a futures contract;
- prices and Greeks are per option unit before contract multiplier and quantity;
- the market price used for inversion is not multiplied by contract multiplier.

Greek units must be documented and preserved:

- Delta is price change per one futures-price unit and includes the Black76 discount factor.
- Gamma is Delta change per one futures-price unit.
- Vega is price change for a `1.00` absolute volatility change; divide by 100 for a one-percentage-point report.
- Theta is price change for one year of calendar time passing while other inputs, including the futures price, are held fixed; divide by 365 for daily Theta.
- Rho is price change for a `1.00` absolute rate change with the futures price held fixed; divide by 100 for a one-percentage-point report.

No caller may independently rescale a Greek without naming the resulting unit.

## Quote-selection policy

Implied volatility cannot be more reliable than its observed option price. Before invoking the solver:

1. Reject NaN, infinity, zero, or negative bid/ask values.
2. Reject a crossed market where bid is greater than ask.
3. Use `(bid + ask) / 2` only when both sides are valid.
4. If the workflow deliberately permits a one-sided or last-trade fallback, represent the selected source explicitly in the market-data result; do not silently substitute it.
5. Reject a selected market price outside the Black76 no-arbitrage bounds, allowing only a small documented floating-point tolerance.
6. Do not publish new zero Greeks after a failed calculation. Preserve the last valid snapshot when the containing workflow supports staleness, mark it stale, and emit failure telemetry.

The first implementation should continue using a valid midpoint where the existing callers have bid and ask, while making invalid-quote behavior explicit.

## Numerical implementation

### Price and Greeks hardening

Harden `OptionModel.Price` and `PriceWithGreeks` so both enforce the same rules:

- every input must be finite;
- futures price and strike must be greater than zero;
- time to expiry must not be negative at the low-level pricing API;
- volatility must not be negative;
- option right must be a defined value;
- every returned field must be finite;
- expiry behavior must be covered by tests at, below, and immediately above zero time.

Keep the existing closed-form Black76 implementation, static state-free design, `record struct` result, and caller-owned span batch APIs.

### No-arbitrage bounds

Let `df = exp(-rT)`. For a European futures option:

- call lower bound: `df * max(F - K, 0)`;
- call upper bound: `df * F`;
- put lower bound: `df * max(K - F, 0)`;
- put upper bound: `df * K`.

Check the observed option price against these bounds before solving. A price at the lower bound maps to the configured minimum volatility within tolerance. A price at or above the upper limit is not a finite-volatility solution and must fail explicitly.

### Safeguarded implied-volatility solver

Replace the current unbounded Newton-only behavior with a bracketed hybrid solver:

1. Validate all inputs and solver settings.
2. Calculate the no-arbitrage bounds.
3. Evaluate price error at the minimum and maximum volatility.
4. Fail with `ImpliedVolatilityNotBracketed` if the root is not inside the configured interval.
5. Clamp the initial guess into the bracket.
6. At each iteration, calculate price and Vega once.
7. Update the lower or upper bracket using the monotonic relationship between price and volatility.
8. Use the Newton step only when Vega exceeds its floor, the candidate is finite, and the candidate stays strictly inside the bracket.
9. Otherwise use the bracket midpoint.
10. Stop when absolute/relative price error or volatility-interval width reaches tolerance.
11. Return `ImpliedVolatilityDidNotConverge` when the iteration limit is reached.
12. Calculate all Greeks once, using the converged volatility.

This retains Newton's speed near the solution while guaranteeing bounded progress for low-Vega, deep in/out-of-the-money, and near-expiry cases.

### Normal distribution accuracy

The current normal-CDF approximation documents maximum absolute error near `1.5e-7`. Before accepting it for production Greeks:

- compare price and Greeks across a broad input grid against an independently verified high-accuracy reference;
- pay particular attention to tail probabilities and deep out-of-the-money options;
- define error tolerances by output and economic materiality;
- replace the approximation with a more accurate managed implementation if it misses the acceptance tolerances.

Micro-optimization must not be accepted at the cost of material pricing error.

## Exercise-style reference data

`FuturesOptionContractReadModel` currently contains option right but not exercise style. Add exercise-style metadata to the reference model and its upstream storage/API mappings before enforcing the model at calculation sites.

Requirements:

- persist `European`, `American`, or `Unknown` per contract or contract family;
- source the value from authoritative exchange/reference data;
- preserve backward-compatible serialization rules when adding the field;
- default old/missing records to `Unknown`, never to `European`;
- validate the field during contract onboarding;
- block calculation and trading workflows for unsupported or unknown styles;
- include the style in diagnostic output where a contract is rejected.

The existing code that hardcodes `OptionStyle.American` in the iron-condor spread job must be removed or mapped from reference data. `OptionSpreadPricer` currently ignores that field, so leaving the hardcoded value would create misleading domain state.

## Migration phases

### Phase 0 - Baseline and characterization

Before deleting QLNet:

1. Capture current QLNet outputs for representative calls and puts across strikes, expiries, prices, and rates.
2. Record that the values are characterization data, not Black76 correctness baselines, because the models and exercise assumptions differ.
3. Run the existing four-leg allocation benchmark and retain its artifacts.
4. Record current throughput, latency distribution, allocated bytes, Gen0/Gen1/Gen2 collections, and contention for single-thread and concurrent runs.
5. Collect representative valid and invalid quote examples from integration fixtures without recording secrets or live account data.

### Phase 1 - Build the production Black76 API

1. Add strong option-right, exercise-style, solver-settings, failure-reason, request, and result value types.
2. Add `FuturesOptionGreeksCalculator.CalculateFromMarketPrice`.
3. Implement shared finite-input validation and no-arbitrage bounds.
4. Replace Newton-only inversion with the safeguarded hybrid solver.
5. Standardize Actual/365 Fixed date conversion.
6. Define Greek units in XML documentation.
7. Keep the code static, deterministic, lock-free, and free of per-call heap allocation.
8. Add dedicated automated unit tests before changing consumers.

### Phase 2 - Add exercise metadata and map external data

1. Extend futures-option contract reference data with exercise style.
2. Update serialization, persistence, NATS/API contracts, validators, fixtures, and contract factories.
3. Map Interactive Brokers or other upstream values to the internal enum where authoritative data is available.
4. Add an explicit contract-family reference mapping where the feed does not supply exercise style.
5. Reject `Unknown` and `American` in the Black76 calculation boundary.

### Phase 3 - Migrate all consumers

Migrate one vertical flow at a time, using the framework result directly or mapping it to the existing `TickOptionComputation` at the boundary:

1. streaming market-data event calculation;
2. primary Interactive Brokers snapshot implementation;
3. legacy/service Interactive Brokers implementation;
4. iron-condor spread-distribution job;
5. WinForms trade-order view model;
6. indirect futures-option spread-data query verification.

For each consumer:

- replace `new OptionCalculator(...)` with the static Black76 API;
- map option-right strings exactly once at the boundary;
- pass exercise style from reference data;
- handle each failure without fabricating successful zero values;
- emit bounded, aggregated diagnostics rather than logging every bad tick;
- add or update focused tests before moving to the next consumer.

Projects that call the framework API need a direct reference to `TomasAI.IFM.Framework.OptionPricer`; do not rely on a transitive reference.

### Phase 4 - Remove QLNet and obsolete types

After every call path has migrated:

1. Delete `Domain.OptionPricer.Shared/OptionCalculator.cs`.
2. Delete `Domain.OptionPricer.Shared/OptionGreeks.cs` if a final usage search confirms it is unused.
3. Remove the QLNet package reference from `TomasAI.IFM.Domain.OptionPricer.Shared`.
4. Remove the QLNet package reference from `TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers`.
5. Remove the QLNet package reference from `TomasAI.IFM.Shared`.
6. Remove unused `using QLNet;` directives from Messaging, Storage, Shared actor/status-console, UI view-model, and UI view code.
7. Run a repository-wide search for `QLNet`, `OptionCalculator`, and `OptionGreeks`.
8. Confirm QLNet is absent from project assets and published product output.

Do not remove the broad `Domain.OptionPricer.Shared` project reference from consumers merely because `OptionCalculator` is gone; many projects use unrelated shared contracts. Review dependency cleanup independently.

### Phase 5 - Integration, performance, and paper-trading verification

1. Run Framework OptionPricer unit tests.
2. Run Domain OptionPricer unit, BDD, integration, and benchmark projects.
3. Run Market Data Feed unit and integration tests.
4. Run affected Interactive Brokers adapter tests where available.
5. Build the WinForms application and exercise the iron-condor view model.
6. Run the full solution build and the relevant application integration suite.
7. Run sustained replay with realistic option-chain quote rates and invalid-quote bursts.
8. Paper trade for at least one to three representative sessions before relying on the results for production decisions.

## Test specification

### Deterministic numerical tests

Add a dedicated automated test project for `TomasAI.IFM.Framework.OptionPricer` and cover:

- independently verified Black76 call and put prices;
- put-call parity: `C - P = exp(-rT) * (F - K)`;
- call/put Delta relationship;
- known values at-the-money and across moneyness;
- zero and negative rates where supported;
- short, medium, and long expiries;
- minimum and high-but-supported volatility;
- intrinsic value at expiry;
- scalar versus batch equivalence;
- all batch length mismatch validations.

### Implied-volatility tests

- price-volatility-price round trips over a parameter grid;
- calls and puts across deep ITM, ATM, and deep OTM cases;
- convergence from poor but valid initial guesses;
- Newton fallback to bisection when Vega is small;
- prices at and just above the lower bound;
- prices at, below, and above the upper bound;
- maximum-iteration failure;
- invalid solver settings;
- finite output for every successful result;
- correct typed failure for every rejected input.

### Greek verification

Compare analytic Greeks to central finite differences with step sizes selected for each variable:

- Delta and Gamma against futures-price perturbations;
- Vega against volatility perturbations;
- Theta against time-to-expiry perturbations with the documented calendar-time sign;
- Rho against rate perturbations while holding the futures price fixed.

Use combined absolute and relative tolerances. Do not use QLNet American-engine outputs as the expected values.

### Integration tests

- a valid streaming bid/ask produces a successful `TickOptionComputation`;
- invalid, zero, non-finite, and crossed quotes do not publish successful zero Greeks;
- unknown and American exercise styles are rejected;
- a valid snapshot API request maps all fields and units correctly;
- the iron-condor job uses Black76 for all four legs;
- the UI receives success/failure state without blocking its single UI thread;
- serialization round trips preserve exercise style;
- legacy serialized contracts become `Unknown` and are handled safely.

## Benchmark specification

Use BenchmarkDotNet in Release mode with server-GC settings matching the intended service deployment. Report environment, runtime, CPU, GC mode, warmup, iteration count, and input distribution.

Benchmarks must include:

- price only;
- price plus Greeks with a supplied volatility;
- market price to implied volatility plus Greeks;
- four-leg iron-condor calculation;
- scalar loop and span-based batch calculation;
- one, two, four, and processor-count concurrent workers;
- easy ATM roots and difficult low-Vega/near-expiry roots;
- valid calculations separately from rejected invalid inputs.

Report:

- operations per second;
- mean, median, p95, and p99 latency where the harness supports them;
- bytes allocated per operation;
- Gen0, Gen1, and Gen2 collection rates;
- scaling efficiency by worker count;
- solver iteration count distribution;
- failure reason counts for validation workloads.

The minimum architectural acceptance criteria are:

- no process-wide lock in calculation code;
- no mutable global pricing state;
- zero managed allocation per steady-state scalar calculation;
- deterministic results for identical inputs;
- no loss of throughput as a result of hidden serialization;
- numerical results inside the approved error tolerances;
- sufficient measured capacity for the recorded/replayed market-data workload, with documented headroom.

Do not publish a maximum IDs/quotes-style capacity claim from a microbenchmark alone. Pair raw calculation throughput with end-to-end replay because decoding, contract lookup, event dispatch, persistence, and UI publication also consume CPU and memory bandwidth.

## Observability and operational behavior

Add low-cardinality counters rather than per-tick informational logs:

- total calculations;
- successful calculations;
- failures by `Black76FailureReason`;
- unsupported/unknown exercise-style count;
- solver iteration histogram;
- solver convergence duration histogram;
- stale-result reuse count, if that policy is implemented;
- input queue depth and dropped/coalesced update counts in the containing market-data pipeline.

Contract identifiers may appear in sampled diagnostic logs, but must not be metric labels. Rate-limit repeated failures for the same contract.

## Rollout and rollback

The model difference means QLNet and Black76 outputs are not expected to be identical. Rollout should therefore use an observational comparison period:

1. In a non-production or paper-trading environment, calculate both models from the same captured inputs.
2. Publish only the currently active result while recording bounded comparison statistics.
3. Explain material differences using exercise style, futures-versus-spot assumptions, solver behavior, and Greek units.
4. Enable Black76 as the sole result only after numerical and workflow acceptance criteria pass.
5. Retain the pre-removal commit as the rollback point; do not retain dormant QLNet production code after acceptance.

Rollback is a code deployment rollback, not a runtime model toggle kept indefinitely. This avoids maintaining two pricing systems and prevents accidental model drift.

## Documentation updates required during implementation

Update these records as the phases complete:

- this document: actual API names, decisions, benchmark results, and final status;
- `Docs/Option-Pricer-Implementation-Details.md`: final implementation inventory and conventions;
- Domain OptionPricer optimization documentation: removal of QLNet allocation and locking;
- Market Data Feed documentation: quote validation, failure behavior, and exercise-style flow;
- `Documents/system/System-Wide-Optimization-Plan.md`: replace SWO-08's QLNet isolation action with the completed Black76 migration results.

## Definition of done

The migration is complete only when all of the following are true:

- every production option calculation uses the framework Black76 implementation;
- exercise style is explicit and unsupported/unknown styles fail safely;
- all five direct consumers and the indirect spread query are migrated and tested;
- implied volatility uses the safeguarded bracketed solver;
- successful results are finite and failures are typed;
- no calculation failure is represented as a successful zero-filled result;
- QLNet source imports, package references, restored assets, and published binaries are absent;
- numerical, integration, build, and replay tests pass;
- benchmarks confirm lock-free, steady-state allocation-free operation and adequate throughput headroom;
- paper-trading verification finds no unexplained pricing, Greek, strategy, or monitoring discrepancies;
- implementation and system-wide optimization documentation contains the measured final results.

After these conditions are met, work returns to the remaining items in the system-wide optimization plan.
