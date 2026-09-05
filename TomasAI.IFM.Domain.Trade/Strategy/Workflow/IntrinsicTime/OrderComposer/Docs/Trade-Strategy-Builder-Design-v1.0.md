# Trade Strategy Builder Design v1.0

> **Catalog identity clarification (2026-09-05):** TradeSelection supplies the exact TradeStrategyFamilyId/DefinitionVersion. Family-Strategy SystemKey is classification and may be shared by several products/timeframes; it must not select the construction policy or contract universe by itself. TradeStrategySymbolId resolves product Symbol/Currency/Exchange, not a tradable expiry contract. The live chain and exact futures contract remain builder inputs. See [catalog implementation](../../../../../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Symbol-Catalog-Implementation.md). Catalog creation does not enable a strategy or alter the three-family implementation boundary.

| Item | Value |
| --- | --- |
| Date | 2026-09-05 |
| Status | Requested design; one-unit construction boundary; not runtime implementation or live approval |
| Purpose | Build every leg of one complete strategy unit from the accepted TradeSelection result (including regime and MarketCondition), a construction policy and qualified live market data |
| Initial scope | Three families: monthly ES iron condor, weekly ES vertical spread and daily single-leg ES futures |
| Related specifications | [Trade Selection result](../../../../../../Documents/system/TradeSelection-High-Level-Design-v0.1.md#12-tradeselection-result), [Composer selection](../../../../../../Documents/system/Order-Composition-Strategy-Selection-Specification-v1.0.md), [pricing](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md), [Stage 4 plan](../../../../../../Documents/system/Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md) |

## 1. Core decision: construct one unit, size later

`TradeStrategyOrderBuilder` is the deterministic entry point **inside OrderComposition**, not a new actor,
workflow stage, market-data service or risk manager. It returns a complete immutable **one-unit
strategy order candidate**. It selects the actual expiry, contracts, strikes, sides and leg ratios;
it does not decide how many strategy units the Fund should trade.

The accepted selected strategy routes to a specialized `OptionStrategyBuilder` for condors/verticals
or `FuturesStrategyBuilder` for outright futures. Neither path repeats Trade Selection. The fixed
workflow remains RegimeDiscovery, MarketCondition, TradeSelection, OrderComposition, RiskManagement
and then separately authorized OrderExecution.

For the initial equal-ratio profiles:

- One iron-condor unit contains four distinct option contracts: buy one protective put, sell one
  short put, sell one short call, buy one protective call.
- One vertical unit contains two distinct options: buy one and sell one, with the profile's
  required option type and strike order.
- One futures unit contains one exact futures contract, with Buy/Long or Sell/Short direction and
  `ContractsPerUnit = 1`; this is not a final one-contract position-size cap.
- `UnitCount = 1` describes the construction basis, not an approved position size. Final strategy
  quantity is absent until Portfolio Risk Manager calculates it.

If Risk Manager subsequently approves three condor units, each leg has three contracts: twelve
leg-contracts in total. It does not change the candidate's strikes or ratios. Changing the shape
requires a new construction result, not an unrecorded risk adjustment.

This design supersedes earlier draft instructions for Composer to calculate Fund-sized quantities
and Risk Manager merely to reduce them. Portfolio Risk Manager owns **initial final sizing,
independent validation and atomic risk reservation**. This does not mean that the existing
Portfolio configuration implementation already implements live sizing or reservations.

## 2. Component responsibilities

| Component | Input and responsibility | Output |
| --- | --- | --- |
| MarketCondition | Existing accepted, unexpired market classification; no reclassification by the builder | Direction, condition, phase, strength, confidence, volatility/liquidity quality and evidence |
| TradeSelection | Existing workflow stage choosing the Fund-permitted family/template | Accepted selected strategy: family/template, direction, horizon, construction-policy reference and frozen regime/MarketCondition context |
| StrategyConstructionPolicy | Map accepted condition fields to explicit versioned construction rules; constrain them by Fund mandate and Portfolio-issued limits | One immutable `ResolvedConstructionRules` |
| OptionLegSelector | Search actual listed contracts using resolved expiry, delta, width, shape and liquidity constraints | Bounded complete leg-set candidates; never a partial successful strategy |
| OptionStrategyBuilder | Coordinate pure resolution/selection, evaluate one-unit economics, validate/rank and create the result | `Built`, `NoTrade`, or `Failed` |
| FuturesStrategyBuilder | Resolve one canonical eligible futures contract, direction, quote and unit economics; no option dependencies | One-unit single-leg futures candidate or typed NoTrade/Failed |
| TradeStrategyOrderBuilder | Validate the accepted selected strategy and dispatch to the exact permitted family builder | Common typed unit-result envelope; no strategy reselection or family fallback |
| OrderComposition actor | Acquire data/leases, supply immutable inputs, fence deadlines/generations and persist the terminal result | One-unit candidate accepted by the Strategy Workflow |
| Portfolio Risk Manager | Revalidate candidate and current financial capacity; calculate final units and reserve risk | Separately typed sized/risk-approved order, or rejection/failure |

Reuse the existing Black-76 calculator through the pricing specification's corrected context/time
boundary. The leg selector consumes/calculates qualified Greeks; it does not implement a second
pricing model. Greeks are model inputs to selection, not proof of future profit. CME identifies
price, time and volatility sensitivities as separate option-risk factors. [CME option Greeks](https://www.cmegroup.com/education/courses/option-greeks/options-the-greeks-options-premium-and-the-greeks).

## 3. Input contract

Proposed `TradeStrategyBuildRequest` contains these immutable, validated inputs:

- **Identity:** WorkflowId, invocation/command ID, expected workflow revision, authorized
  Portfolio/Fund scope, previously allocated OrderId/TradeId, schema version and input digest.
- **Decision:** `SelectedTradeStrategy`, a validated Selected-only view of the canonical accepted
  TradeSelection result, retaining family/reference, template/version, direction, horizon, policy
  reference, result ID/hash and validity, and the exact accepted RegimeDiscovery/MarketCondition
  `DecisionContext`. MarketCondition `OutputHints` remain advisory. Do not fetch newer upstream
  decisions, change their hashes or create a second mutable strategy authority.
- **Policy:** Frozen StrategyConstructionPolicy ID/version and Fund mandate/profile references.
- **Constraints:** An authenticated, versioned `ConstructionConstraintSnapshot` issued by the
  Portfolio/Risk authority for this Fund and strategy family, with validity and reason codes.
- **Market:** A typed qualified live-market snapshot. Options require a bounded immutable option
  universe, contract definitions/calendar, coherent underlying quote, pricing context and per-contract
  quotes/Greeks. Futures requires its exact contract definition/calendar and qualified live futures
  quote, not an option universe. Both carry generation/revision and freshness evidence.
- **Evaluation:** Explicit valuation instant, deadline and cost-model reference. Pure helpers
  receive these values; they do not read a clock, fetch data, or mutate shared state internally.

OrderComposition acquires option-chain or direct-futures leases through MarketData and passes
immutable snapshots to the pure builder. A live feed is required at the orchestration boundary,
not a network connection opened by a selector. Stale/missing required data fails closed.

The constraint snapshot supplies construction-relevant permission and hard feasibility limits,
such as maximum risk for one unit, and any explicitly supported premium/exposure restrictions.
The builder tests one unit against these supplied bounds; it does not derive a trading budget,
divide remaining capital by unit risk, calculate final units or reserve capacity. Financial
restrictions can tighten but never enlarge policy/mandate permissions. Zero blocks the relevant
capacity; missing required limits fail instead of meaning unlimited.

Remaining capacity is provisional until final Risk Management. The authority must declare which
limits are static policy versus observed capacity and timestamp/version the latter. Do not pass a
UI-supplied limit as authenticated Portfolio authority. Optional Greek/margin restrictions require
real supported contracts/calculators; an enabled but unsupported constraint fails explicitly.

## 4. Construction policy

Use a versioned table of condition-to-profile rules, not arbitrary scripts or inference at runtime.
Each rule specifies a stable rule ID, priority, predicates and an immutable parameter-profile ID.
Predicates may use the existing MarketCondition fields: horizon, condition type, direction, phase,
volatility behavior, liquidity quality and explicit strength/confidence intervals. Interval endpoint
semantics are serialized. Reject overlapping matches at equal priority; no matching rule is NoTrade.
A matched rule referencing missing/invalid parameters is Failed. Do not fall through to a looser
rule after a risk rejection.

Resolved parameters contain:

- Exact permitted family/variant, exercise style, target week/month, expiry/DTE rule and entry window.
- Short/long delta targets as applicable, absolute-versus-signed convention and tolerances.
- Target/minimum/maximum wing or spread widths, in explicitly named index-point units.
- Leg roles, required side/type, relative strike order and integer ratios.
- Required net directional exposure, premium/payoff bounds, liquidity and freshness thresholds.
- Cost/ranking versions, search bounds and result lifetime; no final quantity or capital allocation.

These option-specific fields apply only to the option variants. The daily futures profile instead
contains canonical contract/roll rules, session/entry window, direction, price/tick rules and
approved exit/stress inputs for unit economics. Never populate dummy option fields for a future.

For monthly condors, a bullish versus bearish rule can select different put/call delta targets and
wing preferences. For weekly verticals, direction selects a permitted bullish/bearish construction
variant. Volatility/strength may select another **predefined** profile only if that mapping is
explicitly configured. There is no automatic rule that higher volatility means wider wings or
more contracts. An expected-move/ATR rule would require separately defined, qualified inputs;
do not derive an undocumented price distance from a confidence score.

The prior Composer document's EOM/debit-first and numerical examples remain **proposed offline
fixtures**, not approved live profiles. This design approves no new trading thresholds.

## 5. Build algorithm

The common entry point first validates family/template, direction and horizon against the selected
result and frozen assignment. Unknown/inconsistent family data fails; there is no fallback from
options to futures. It then dispatches to one of the following specialized paths.

### 5.1 Monthly condor and weekly vertical

1. Validate authorized identities, accepted upstream results, policy/constraint versions and
   validity. Reject incompatible family/horizon or unsupported product/pricing metadata.
2. Resolve exactly one construction-rule profile. Intersect policy/mandate permissions with the
   supplied hard constraints; an empty permissible set is NoTrade, not relaxed rules.
3. Resolve eligible actual expiries/contracts for the target period. Use authoritative expiry,
   underlying and calendar metadata; never guess Friday, roll into another week/month or create
   a strike that is not listed.
4. Select complete candidate leg sets. Condor: select short put/call near target deltas, then
   protective strikes within wing-width bounds. Vertical: select directional long/short pairs
   satisfying delta targets and strike/width rules. All legs share the required exact expiry,
   underlying, exercise style, currency and multiplier.
5. Evaluate each candidate for **one unit**: side-based net debit/credit, valid package tick
   rounding, costs, expiry payoff/max loss and reward, per-leg Greeks and signed aggregate Greeks.
   Keep index-point amounts, currency amounts and Greek units explicit; apply ratios/multiplier
   once. Do not treat leg midpoints as executable fills or margin as maximum loss.
6. Reject hard-rule violations, then rank deterministically using the selected policy's ordered
   delta/width/economics/liquidity keys, ending with ordinal canonical contract IDs. Return one
   preferred candidate. Do not optimize the number of contracts or claimed future P&L.
7. Independently validate the winner's complete payoff/shape, common pricing context, full leg set
   and one-unit constraints. Attach immutable evidence, identity, expiry and result hash.
8. The actor revalidates freshness/generation at handoff and atomically acquires all selected-leg
   leases before releasing discovery. Only then may the workflow accept the built candidate.

Delta targets and exact widths may be incompatible on the listed strike grid. Apply explicit hard
bounds and target tolerances; never approximate a missing contract or silently violate a limit.
Use the Composer specification's bounded search (maximum 1,024 condor combinations or 256 vertical
pairs per selected expiry) within Stage 4's universe limits. This is bounded policy selection,
not an exhaustive optimizer. Pruning and tie-break behavior are versioned and reproducible.

The builder either supplies **all four/all two legs or none**. A quiet unselected option need not
block a usable strategy, but all selected quotes/Greeks must qualify in one coherent context.

### 5.2 Daily single-leg futures

1. Resolve the exact ES futures contract through the approved contract/roll schedule for the
   selected session and planned exit; do not roll an existing position or infer a contract from a
   display name. Unknown/ambiguous mapping fails.
2. Apply selected Long/Short direction and the explicit policy entry/exit windows. Resolve multiplier
   and tick rules from definitions. Obtain one qualified current-generation two-sided futures quote.
3. Build one Buy or Sell leg with `ContractsPerUnit = 1`. Compute the side-based entry estimate,
   tick-valid candidate price, unit costs, gross notional, planned-loss-at-exit and stress-loss
   evidence using the already specified daily profile; no stop/stress distance is invented.
4. Apply supplied one-unit construction constraints, validate freshness/identity and return the
   complete unit or NoTrade/Failed. Futures risk is not a guaranteed finite maximum loss; margin
   does not stand in for maximum loss. Stop submission and position closure remain outside composition.
5. The actor retains its direct futures lease through the same selected-owner handoff and validity
   checks. Treasury/option-pricer unavailability must not fail an otherwise qualified futures unit.

This path uses no option-chain discovery, option-leg selector, IV, option Greeks or Treasury rate.
Full per-unit economics and exit/stress requirements remain in the linked Composer specification;
Portfolio Risk Manager chooses actual contract quantity later.

## 6. Output and failure contract

Proposed `OneUnitStrategyOrder` is a discriminated result with an option variant
(`OneUnitOptionStrategyOrder`) and a futures variant (`OneUnitFuturesStrategyOrder`). Common fields:

| Field group | Required contents |
| --- | --- |
| Attribution | Candidate ID/hash, allocated OrderId/TradeId, workflow/Fund/Portfolio/family/template identities and versions |
| Construction | Policy/rule/constraint versions, direction, exact contract expiry/maturity and underlying where applicable, `UnitCount = 1`, `SizingStatus = RequiresPortfolioSizing` |
| Legs | Stable role/leg ID, canonical contract, side and positive `ContractsPerUnit` (1 in all initial profiles); 4/2/1 legs as required by the selected family |
| Economics | Typed family-specific unit economics, price units/currency/multiplier and cost-model version; no final sizing |
| Validity | Market snapshot hash and option pricing-context hash where applicable, worker generation/revision, evaluated time and earliest input/result deadline |
| Explanation | Matched rule, selection/tie-break evidence, deviations from targets, bounded rejection summaries |

Option variants add option type/strike/Greeks per leg and explicit Debit/Credit premium,
payoff/max-loss and aggregate-Greek evidence. The futures variant adds outright entry/exit prices,
unit notional and planned/stress loss; it has no option premium, option expiry/strike, IV, option
Greeks or Treasury context. Its contract maturity and eligibility remain part of contract metadata.
Do not use zero-filled option properties or a hard-coded four-element array in the common type.

There is **no** approved quantity, risk reservation, broker order ID or execution authorization.
Serialize absent sizing explicitly; never overload zero to mean one, unlimited or approved.
Transport/result references must distinguish this type from a `SizedRiskApprovedOrder`. Execution
admission must reject the one-unit candidate, even though it contains complete legs and prices.

- `Built`: complete one-unit candidate; actor maps to `Completed + Candidate` after handoff.
- `NoTrade`: complete reliable evaluation finds no matching permissible policy, expiry or complete
  shape, or no shape meets supplied one-unit constraints. No partial order is returned.
- `Failed`: missing/stale inputs, invalid/ambiguous configuration, unsupported pricing/constraint,
  solver failure, recovery, overload, timeout or invariant failure; structured code/message/context.

If unavailable required data prevents a valid construction, report Failed rather than disguise an
outage as NoTrade. The pure builder performs no retries. The actor has one bounded qualification
deadline and one logical terminal result; conflicting duplicate input fails, identical duplicate
input returns its prior outcome without extending validity. A reset invalidates stale readiness,
not authoritative position ownership. No automatic recomposition after a risk rejection in v1.

## 7. Portfolio sizing handoff

The Strategy Workflow passes the immutable unit candidate to Portfolio Risk Manager. Risk Manager
independently recalculates its economics and checks current Portfolio, family and Fund capacity,
existing positions, working-order reservations, account/margin truth and quote validity. It chooses
integer strategy units under the applicable limits and atomically reserves the approved risk.

```text
FinalLegQuantity = ApprovedStrategyUnits * ContractsPerUnit
```

One is a normalization basis, **not a Composer-imposed upper bound**. Risk Manager may approve
more than one unit when permissible, or reject when no unit is allowed. Recalculate nonlinear
costs/margin and liquidity at the actual size; scaling one-unit cost figures blindly is prohibited.
The sized result binds original candidate hash, approved units, final leg quantities, revalidated
prices/economics, risk-decision/reservation identity and approval expiry. A changed structure or
out-of-tolerance price requires a new valid candidate/approval, never an in-place mutation.

MarketData leases identify contracts/owners, not unit count. Increasing approved units does not
create duplicate physical feeds. Preserve discovery-to-strategy/order/position handoff semantics,
including independent order/position claims after partial fills and no release on UI disconnect.

## 8. Implementation placement and verification

Proposed interfaces (design targets, not existing runtime APIs):

```csharp
TradeStrategyBuildResult Build(TradeStrategyBuildRequest request);
OptionStrategyBuildResult BuildOptions(OptionStrategyBuildRequest request);
FuturesStrategyBuildResult BuildFutures(FuturesStrategyBuildRequest request);
ConstructionRulesResult Resolve(ConstructionPolicyRequest request);
OptionLegSelectionResult Select(OptionLegSelectionRequest request);
```

Place immutable contracts in `Domain.Trade.Shared` under the existing OrderComposition pipeline
area; pure builder/policy/selector/economics components in `Domain.Trade`. Keep pricing in the
existing framework, feed acquisition/recovery in Application.MarketData, and financial constraint
issuance/sizing in the Portfolio/Risk authority. No new dependency from MarketData back to Trade.
The existing start/terminal stage contracts need a versioned typed-result integration; their
presence does not establish that a production composition actor/builder is implemented.
Place implementations under the existing `Strategy/Workflow/IntrinsicTime/OrderComposer` area;
this `Docs` folder is the canonical location of the three-family builder design.

| Package | Required evidence |
| --- | --- |
| B1: unit contracts | Complete 4-/2-/1-leg shape; UnitCount fixed at one; side/ratio/ID validation; immutable selected-strategy context; typed option/futures serialization; unsized result rejected by execution admission |
| B2: policy resolution | All supported condition/direction branches; threshold endpoints; rule ambiguity; missing profile; disabled family; no-match NoTrade; frozen versions and no hidden defaults |
| B3: selection/economics | Listed strikes only; exact expiry/underlying; delta/width conflicts; asymmetric wings; both vertical directions; independent payoff oracle; cost/tick rounding; Greek sign/unit checks; stable ranking under shuffled inputs |
| B4: fail-closed integration | Missing/stale quotes/Greeks/constraints; no partial success; bounded search; reset/deadline races; coherent context; duplicate/conflicting requests; atomic lease handoff and retained positions |
| B5: sizing boundary | Builder never computes final units or reserves risk; Risk Manager approves multiple units from one candidate, rejects zero capacity, revalidates nonlinear economics, and prevents concurrent double reservation |
| B6: futures path and family dispatch | Long/short single-leg unit; canonical contract/roll and session eligibility; tick/unit notional/exit/stress checks; stale futures failure; futures succeeds without Treasury/options; malformed family/template/direction/horizon cannot dispatch or switch builders |

Implement/tests remain separate work. Numerical policies, source/calendar qualification and Stage
4 rollout gates remain open. This design changes no running application, subscriptions or orders.
