# Option Pricing Calculation Tiers and Stage 1 Addendum

Date: 2026-09-19. Status: S1A APIs implemented and verification gate passed; see the [S1A verification record](Option-Pricing-Stage-1A-Verification.md). Downstream Stage 2-4 integration remains planned.

Subsequent Stage 4 scope: [IV Rank/Percentile analytics and persistence](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md) consumes qualified IV-only observations without requiring another full-Greek calculation path. It is separate from S1A and does not change the original stage responsibilities below.

Authority: supplements sections 8-9 of the [detailed specification](ES-Trade-Blotter-Contract-Reference-and-Option-Pricing-Specification-v3.md) and the [three-stage plan](ES-Trade-Blotter-Three-Stage-Implementation-Plan-v1.0.md). Where earlier wording implies full Greeks for every chain refresh, this addendum takes precedence. The original Stage 1 verification remains valid for its tested APIs; it does not qualify the new APIs below.

## 1. Agreed calculation policy

Leg selection must use the fastest qualified price-and-Delta path. Changing default or user-updated Delta targets or spread widths selects from the latest qualified chain snapshot; it does not itself trigger a whole-chain IV solve or full-Greek calculation. If eligible data is missing or stale, request bounded refresh and report selection unavailable until qualified data is available.

Market price means current bid/ask, with midpoint explicitly identified when used as the IV mark. A model theoretical price is a separate field and must not replace executable market prices. Buy/sell direction, leg ratios and order routing determine the applicable spread-price calculation; displayed indicative prices are not a fill guarantee.

Delta requires a volatility input, but calculating Delta does not require solving IV again on every pass. A controlled IV-refresh path supplies qualified, versioned cached IV. Subsequent price/Delta refreshes may use that IV with updated underlying price/time, explicitly identifying the reused IV observation. Such a refresh is not a fresh option-quote IV fit.

| Consumer / purpose | Calculation required | Refresh and ownership |
| --- | --- | --- |
| Chain selection and visible rows | Bid/ask and model price/Delta; qualified IV input | Coalesced latest state, not every raw tick |
| IV refresh | IV-only solve against a coherent option/underlying snapshot | Controlled cadence/material-change policy; initial IV required before eligibility |
| Order Composer spread selection | Eligible legs, executable/indicative spread prices, ratios, quantities and aggregate Delta | Shared immutable chain snapshot; no duplicate market feed |
| Selected candidate and Risk Manager validation | Fresh IV and full Greeks where required by validation/risk policy | Only candidate legs, with bounded age/skew and current authorization |
| Held-position monitoring | Current quote-based full Greeks | Periodic/event-driven refresh even without a new option trade |
| Portfolio risk | Aggregate qualified current position Greeks | Reuse contract calculations; apply signed quantity/multiplier exactly once |
| Retained Databento option market trades | Trade-basis IV and all five Greeks, or explicit failure | Every retained trade; never coalesce away required trade calculations |
| Broker fills | Independent execution-time pricing/risk evidence | Preserve execution identity; do not confuse fills with provider trade ticks |
| Scenario/volatility strategies | IV and requested sensitivities appropriate to the scenario | Distinct scenario inputs/provenance; not a substitute for live observed values |

Historical trade-time Greeks are not current position risk. A held contract must be refreshed from qualified quotes, underlying price, time and volatility inputs. If those are unavailable, display stale/unavailable risk rather than treating historical Greeks as current.

Full-Greek fields in staging/position views may be supplied on demand by the full-risk tier. Their presence in the UI specification does not require full-chain recalculation on every selection refresh.

## 2. Baseline API gaps addressed by S1A

Inspection of Framework.OptionPricer/Pricing/OptionCalculator.cs and OptionPricingContracts.cs at planning time established the following baseline gaps (historical, before S1A):

- TheoreticalPrice returns price only, not Delta.
- Price returns the complete OptionValues payload. For American CRR, it evaluates one core tree and six additional trees for Vega, Rho and Theta.
- The private American Evaluate routine already returns price, Delta and Gamma from the core tree. The European branch of this private routine currently returns zero placeholders for Delta/Gamma; it cannot simply be exposed as a valid fast API.
- ImpliedVolatility performs a bracketed solve but then calls Price, forcing full Greeks even when the caller only needs IV.
- PriceBatch and ImpliedVolatilityBatch inherit these full-output behaviors.

S1A addresses these gaps through additive API/output separation, not another option model or a replacement of the verified full-Greek functionality.

## 3. Required Stage 1 follow-up: S1A

The scalar APIs below and their PriceAndDeltaBatch/SolveImpliedVolatilityBatch equivalents are now implemented. The requirements below are retained as the acceptance contract; measured evidence is in the [S1A verification record](Option-Pricing-Stage-1A-Verification.md).

1. Add PriceAndDelta(request, volatility) and its bounded batch equivalent. Return theoretical price, Delta and volatility used, with typed status, engine, request and numerical-policy evidence. Do not supply zero-filled Gamma/Vega/Theta/Rho fields to imply that they were calculated.
2. Use one core lattice/FD evaluation for American/cash-dividend price and Delta, avoiding Vega/Rho/Theta bump passes. Provide a correct analytic European Delta path; preserve qualified backend behavior and do not expose internal zero placeholders.
3. Add SolveImpliedVolatility(request, marketPrice) and its bounded batch equivalent. Return solved IV, repriced value/residual and numerical provenance without a full-Greek postpass. Preserve model-specific bounds, non-identifiability, iteration limits and typed failure behavior.
4. Share the IV solve implementation with the existing IV-plus-Greeks API to avoid divergent solver behavior. Preserve existing public APIs and their numerical contracts.
5. Preserve cancellation, batch bounds/order, thread safety, pooled buffers and per-option units. The fast result must reject undefined Delta boundaries explicitly; price-only boundary behavior remains available through TheoreticalPrice.

Theoretical operation reduction: American price/Delta should require one core tree instead of seven for full Greeks. IV-only removes the final full-Greek postpass, not the IV search itself. These are work-count expectations, not measured latency promises.

Optional subsequent optimizations: qualified warm-start brackets from prior IV, a safeguarded interpolation solver, lower verified step profiles and bounded cross-contract parallelism. They must not silently weaken numerical tolerances or become prerequisites for this API separation.

### S1A verification gate

- All four asset/exercise combinations, both rights, premium conventions and supported dividend modes: fast price/Delta agrees with the verified full result within documented tolerances.
- IV-only agrees with existing IV-plus-Greeks, reprices to tolerance and preserves failure semantics.
- Scalar/batch ordering, cancellation, concurrency, immutable evidence, serialization where applicable, invalid metadata, expiry/zero-volatility behavior, capacity, non-identifiable IV and numerical exhaustion.
- Regression tests must detect accidental use of European placeholder Delta and accidental full-Greek postpasses.
- Rerun the full option-pricer unit/BDD/integration suites and affected frozen consumer/backend regressions.
- Release benchmarks compare price/Delta, full Greeks, IV-only and IV-plus-Greeks for 1/2/4/64 contracts, including allocations/GC. Record measured speedups; do not claim sevenfold wall-clock improvement from work counts alone.

S1A passed with 238 passing test executions and 32 comparative benchmarks. Stage 2 can now depend on these framework APIs within their qualified limits. The original Stage 1 pass is retained as historical baseline evidence, not relabeled as a pass for these additions.

## 4. Stage 2 responsibilities

Implement shared backend chain snapshots, qualified IV caching, calculation scheduling and publication. Quote ingress updates latest state; it does not run full-chain calculations synchronously.

Coalesce superseded pending quote work per contract. An option quote generally invalidates that contract's IV mark; an underlying update can invalidate many contracts' price/Delta outputs. Use bounded scheduling with starvation protection and capacity reserved for both live risk and selection. Reject out-of-order results; never overwrite newer state with an older computation. Avoid repeated cancellation that prevents any result from completing.

Cache identities must include exact contract/underlying, model/convention versions, generation, price basis and relevant qualified inputs. Preserve the original IV observation's source timestamps/sequence separately from the later Delta calculation timestamp. Schedule freshness/material-change thresholds as explicit reviewed settings; this document invents no production intervals.

Do not share a trade-basis IV result as if it were quote-midpoint IV. Do not combine different generations, scenarios, stale inputs or incompatible numerical policies under a common cache entry. Identical eligible calculations can be shared across Composer, monitoring and Portfolio.

Retain every required source trade and its calculation attempt/failure evidence. Quote coalescing does not authorize dropping retained trades, broker fills or accounting evidence. Backpressure and failure status must be explicit.

Stage 2 tests must cover cache invalidation, stale-IV rejection, generation changes, coherent timestamps, late results, quote bursts, bounded queues, fairness and preserved per-trade evidence.

## 5. Stage 3 responsibilities

Order Composer's spread engine consumes the shared real-time snapshot; it does not establish a second independent option-chain subscription.

For Iron Condors, match eligible shorts to configured Delta targets, then select listed long strikes using configured wing widths. Verticals use the corresponding two-leg policy. Preserve deterministic tie-breaking, tolerances and missing-leg failure behavior from the specification.

Aggregate spread prices and signed exposure from actual leg ratios. Revalidate selected candidate legs and obtain the full-risk calculation required by Risk Manager before submission; candidate refresh must not silently substitute different contracts. Portfolio, position monitoring and blotter consume shared qualified outputs with visible freshness/status.

Strategy/target edits operate on the current qualified snapshot. Stage 3 tests must verify this does not trigger full-chain repricing, while stale/missing required data correctly blocks the relevant action.
