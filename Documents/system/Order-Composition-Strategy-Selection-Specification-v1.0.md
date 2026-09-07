# Order Composition Strategy Selection Specification v1.0

> **Strategy catalog direction (2026-09-06):** The target catalog separates trading logic from structure and variant. TradeSelection will supply exact strategy/deployment/structure/variant versions; Composer builds one unit using the required supported builder. Desired coverage includes Long/Short futures, all four credit/debit verticals and Long/Short iron condors with independent Balanced/Bullish/Bearish bias. Existing three-profile algorithms below are a limited baseline, not complete support for these variants. Leg count and expiry constraints are per structure; Jade Lizards and double calendars require explicit builder/data/risk capabilities. TradeSelection implementation is on hold; no algorithms are changed by this document update. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
| --- | --- |
| Specification ID | `OC-SELECT-v1` |
| Date | 2026-09-05 |
| Status | Requested policy design; proposed financial profiles for review; not live-authorized or implemented by this document |
| Scope | ES monthly four-leg iron condor, weekly two-leg vertical, daily one-leg outright futures |
| Authorities | [TradeSelection design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-High-Level-Design-v0.1.md), [Portfolio/Fund design](Portfolio-Fund-High-Level-Design-v0.1.md) |
| Dependencies | [Stage 4 pricing](Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md), [Stage 4 implementation plan](Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md), [approved ownership mappings](Stage4-Durable-Pricing-Dependency-Decisions.md) |
| Construction/sizing authority | [Trade Strategy Builder design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md): Composer constructs one unit for the selected family; Portfolio Risk Manager determines final units and reserves risk |

## 1. Accepted direction and responsibility

On 2026-09-05 the owner requested that Order Composer inside the strategy workflow select expiry,
strikes, deltas and quantities through explicit policies for all three profiles. Subsequent owner
clarification separates **one-unit construction** from **Portfolio-owned final sizing**. Composer
selects actual contracts and per-unit leg ratios, not the number of strategy units to trade. The
production design is not limited to caller-supplied exact contracts. Supplied-contract fixtures
remain useful, but cannot complete production composer acceptance.

The existing workflow design separates these responsibilities:

| Stage/authority | Responsibility |
| --- | --- |
| RegimeDiscovery | Accepted market direction/phase and regime evidence for the triggering horizon |
| MarketCondition | Accepted, expiring market-only assessment for that horizon; no strategy-family or variant selection |
| TradeSelection | Select/validate the Fund-assigned versioned template and composition-policy reference |
| OrderComposition | Build one complete unit: exact contracts, expiry, strikes, leg ratios, candidate prices and per-unit economics |
| Portfolio / RiskManagement | Issue construction constraints; determine final strategy-unit quantity using current limits/capacity; independently validate and atomically reserve/approve risk |
| OrderExecution | Separately authorized execution; no submission is performed by composition |
| MarketData | Discovery, coherent prices/Greeks, subscription ownership and recovery; no trading-policy decisions |

Do not create a new signal, infer direction from an old ITI row, switch strategy family to rescue
a rejected candidate, or let an LLM select a trade. The Monthly Fund's existing directionally
biased condor design is preserved. Neutral condors require a template explicitly permitting
Neutral; they are not a silent substitute for a bullish/bearish instruction.

## 2. Invocation and versioned policy contract

Extend the existing `StartOrderCompositionPipelineCommand`/terminal-event pattern; do not invent
a second workflow engine. Resolve a proposed `IOrderCompositionPolicy` by frozen policy ID/version
with deterministic implementations for the three profiles. Selection is pure over immutable
inputs; the stage actor separately orchestrates bounded market-data acquisition and handoff.
For options, use the companion design's `OptionStrategyBuilder`, construction-policy resolver and
leg selector inside that stage. Daily futures uses a separate one-unit futures construction path.

Required input:

- Workflow ID, stage invocation ID, expected workflow revision, authorized scope, correlation,
  deadline, schema version and canonical invocation digest.
- Accepted unexpired TradeSelection result, template ID/version, direction/bias and horizon.
- Frozen `PortfolioFundStrategySnapshot`, Fund mandate, delegated `FundRiskEnvelope` and policy
  versions. Fund intent does not create capital or approve risk.
- Portfolio/Fund-reserved OrderId and TradeId from the existing composition authority; no local
  generation of substitute business IDs. These identities are not financial risk reservations.
- Policy-defined target period (month, week or exchange session), entry window and expiry rules.
- Versioned contract definitions/calendar, qualified discovery or outright market snapshot,
  and options pricing context where applicable.
- Portfolio-issued construction constraints and approved costs/exit/stress inputs for per-unit
  economics. Actual current capacity/margin and final sizing are revalidated by Risk Management.

Policy must specify permitted product families/exercise style, DTE units/bounds, entry cutoffs,
strike/delta/width constraints, directional exposure bounds, liquidity limits, limit-price rules,
leg ratios, cost model and result lifetime. Final unit caps belong to Portfolio/Risk policy.
Missing required configuration is `Failed:
CompositionPolicyIncomplete`, not a default dollar budget, contract count or invented market datum.

Freeze policy versions for the invocation. Obtain fresh market data without extending the upstream
signal's validity. Output validity is bounded by upstream validity, invocation deadline, policy,
contract entry cutoff, pricing-context validity and selected-quote expiry. A result retrieved after
expiry remains historical evidence, not a renewed candidate.

## 3. Proposed initial profiles for owner review

These are **design proposals**, not owner-approved trading settings or claims of best P&L. Numbers
below are deterministic offline-fixture starting points only. No production configuration is
created by this document; live activation requires a reviewed complete profile and qualification.

| Setting | Monthly ES iron condor | Weekly ES vertical | Daily ES futures |
| --- | --- | --- | --- |
| Expiry/contract | European E-mini ES EOM option in the workflow's target month | European E-mini ES option at the final eligible expiry of the workflow's target exchange week | One canonical ES outright eligible through the planned session exit |
| Entry timing proposal | 7–45 calendar days to expiry, inside the Fund's entry window | At least 24 hours and at most 7 calendar days to expiry, inside the Fund's entry window | During the configured entry session/window only |
| Direction | Bullish/bearish bias from accepted selection; Neutral only if explicitly permitted | Bullish: call debit spread; bearish: put debit spread; Neutral: NoTrade | Bullish: long; bearish: short; Neutral: NoTrade |
| Delta targets proposal | Bullish short put absolute 0.25 / short call 0.15; bearish 0.15 / 0.25; permitted neutral 0.20 / 0.20 | Long leg absolute 0.60; short leg absolute 0.35 | No option delta selection |
| Strike/width proposal | OTM shorts; each protective wing targets 10 index points, allowed 5–25; short-delta tolerance 0.05 | Distinct strikes; width allowed 5–50 points; each delta-target tolerance 0.10 | No strikes or wings |
| Construction basis | One unit, four legs with ContractsPerUnit = 1; final units unset | One unit, two legs with ContractsPerUnit = 1; final units unset | One unit of one selected futures instrument; final units unset |

Dollar risk budgets, credit/debit hurdles, net-delta bounds, liquidity participation, margin,
stop/stress distances and live sizing caps must come from approved versioned profiles/Portfolio
authority. They are deliberately not guessed here. Numeric tests may supply explicitly synthetic
values. Portfolio Risk Manager selects final unit count under approved caps. One-unit economics
are not a proposed maximum order quantity; final leg quantities are separately typed.

Monthly **EOM** is a proposed product-family choice, not a reinterpretation of every ES monthly
option as European. CME offers both European and American ES option families. The current
European Black-76 pricer cannot silently price an American contract. [CME weekly/EOM FAQ](https://www.cmegroup.com/trading/equity-index/weekly-eom-options-faq.html).

Weekly debit-only is a proposed first policy, not removal of Stage 4's structural support for
credit verticals. A later reviewed credit policy may map bullish to put credit and bearish to call
credit. It must use a different explicit profile/version, not dynamically switch when debit
economics fail. Backtesting/walk-forward can later compare policies; this design predicts no winner.

## 4. Common deterministic selection procedure

1. Validate invocation, upstream validity, Fund/template/policy compatibility and authorized
   identities. Disabled/unapproved profiles cannot be used for live composition.
2. Resolve the permitted expiry/contract universe from complete versioned definitions. Apply
   profile entry windows, exact expiry, supported pricing metadata and the pricing specification's
   `<90 trading days` options limit. No symbol approximation or unrelated underlying substitution.
3. Acquire a bounded renewable discovery lease for options, or a direct lease for the exact daily
   future. Missing pricing prerequisites fail before a new option session allocates resources.
4. Capture an immutable qualified universe with per-contract eligibility. Do not wait for every
   illiquid option in the chain; all legs of the selected candidate must qualify. Record exclusions.
5. Enumerate valid shapes using the profile-specific algorithm below. Price each with a coherent
   underlying/rate context; apply structural, delta, spread, liquidity and cost constraints.
6. Validate one-unit economics against the supplied construction constraints. Do not compute
   final quantity, spend capacity or reserve risk; those belong to Portfolio Risk Manager.
7. Rank eligible candidates with the profile's lexicographic keys and deterministic ordinal
   contract-ID tie-breaks. Do not claim an expected-return optimum. Collection arrival order,
   culture, dictionary enumeration and random seeds must not alter the result.
8. Recompute selected-leg Greeks in one final current context, revalidate selected-quote freshness,
   generation, policy expiry, per-unit economics and ratios. Changed/invalid context fails this invocation;
   no stale fallback or self-retry into a later signal.
9. Acquire/commit the exact selected-contract ownership set atomically before releasing discovery.
   Complete only with qualified realized subscriptions and a valid result. Pass the candidate to
   the workflow, which alone decides whether to invoke RiskManagement.

Respect Stage 4 limits (8 discovery sessions/dataset, 512 options/chain, 2,048 unique options/dataset)
and bounded acquisition deadlines. Proposed candidate bound: retain at most 8 nearest-delta shorts
per side and 4 nearest-width protective choices per short for a condor (at most 1,024 combinations
per expiry); verticals retain at most 16 candidates for each role (at most 256 pairs). Filter hard
constraints before deterministic pruning. Sort by delta/width deviation then canonical ID. This is
a bounded search policy, not exhaustive global optimization. No unbounded cross-chain Cartesian
product; overload returns `Failed: CapacityExceeded`. Include the pruning-policy version in evidence.

## 5. Monthly ES iron-condor algorithm

Resolve the workflow target month explicitly; do not roll a late/invalid signal into another month.
The proposed EOM profile requires the exact EOM expiry for that month, within its entry/DTE bounds.
No eligible listed expiry is `NoTrade: NoEligibleExpiry`; missing/incomplete definitions are Failed.
Use the option definition's underlying future even if it differs from today's front-month future.

Select four distinct options with identical expiry, underlying, multiplier, currency and exercise
style. In increasing strike order:

```text
buy put K1 < sell put K2 < sell call K3 < buy call K4
signed per-unit ratios: +1, -1, -1, +1; UnitCount = 1
Portfolio Risk Manager later determines ApprovedStrategyUnits
```

Require `K2 < F < K3` for the proposed OTM-short profile. Select short put/call candidates nearest
their direction-specific **absolute** delta targets, within tolerance. For each short, choose listed
protective strikes farther OTM with widths inside the allowed band, nearest the target width. Do
not fabricate a missing strike or widen beyond the cap to force a candidate. Validate the aggregate
signed delta against the profile's required bullish/bearish/neutral bounds **after including long
wings**; short-delta targets alone do not guarantee the desired net exposure.

Conservative entry estimate in index points:

```text
C = bid(short put) + bid(short call) - ask(long put) - ask(long call)
Wp = K2 - K1; Wc = K4 - K3
gross risk per spread = (max(Wp, Wc) - C) * multiplier + cost reserve per spread
maximum expiry profit before costs = C * multiplier
```

Require positive credit below the narrower wing width, positive gross risk, valid side sizes,
minimum configured credit/width ratio and required net credit after costs. Independently evaluate
the complete payoff at all strike breakpoints and both tails; reject invariant violations rather
than trusting supplied widths. Defined-risk bounds describe the complete option package at
expiry, not protection against legging, assignment or later unhedged futures exposure.

Rank: smallest summed short-delta error, then smallest summed wing-width error, then largest
credit/gross-risk ratio, then smallest summed relative bid/ask spread, then ordered contract IDs.
Hard risk/liquidity constraints always precede ranking; do not trade extra units merely to improve
a ranking score. Record bullish/bearish profile and actual net Greeks in the result.

## 6. Weekly ES vertical algorithm

Define target week by its exchange trading dates, not the machine's local week number. Select the
last permitted European expiry **within that target week** from authoritative contract definitions,
subject to entry/DTE bounds. Holidays may change the relevant date; do not assume Friday, and do
not move to next week when today's opportunity has expired. Ties require the profile's product-
family priority then canonical ID. A complete empty eligible universe is NoTrade.

- Bullish call debit: buy lower-strike call, sell higher-strike call.
- Bearish put debit: buy higher-strike put, sell lower-strike put.
- Both legs share exact expiry, underlying, option type and multiplier; ratios are `+1/-1`.

Enumerate long/short pairs nearest the configured absolute delta targets, preserving those strike
orders and width bounds. Validate aggregate delta sign, all quotes and both solver results.

```text
D = ask(long option) - bid(short option)
W = abs(long strike - short strike)
gross risk per spread = D * multiplier + cost reserve per spread
maximum expiry profit before costs = (W - D) * multiplier
```

Require `0 < D < W`, configured debit/width and reward/risk bounds, and positive reward after the
specified costs. Rank by summed delta-target error, then largest after-cost reward/risk, then
smallest summed relative quote spread, then ordered contract IDs. Recalculate the exact payoff.
No debit candidate means NoTrade if complete healthy data were evaluated; never silently change
to naked options, a ratio spread, different expiry, credit vertical or future.

## 7. Daily ES futures algorithm

Resolve one exact ES future from the approved, versioned active-contract/roll schedule for the
workflow session. It must remain eligible through the planned exit and outside the profile's
expiry/roll exclusion window. No arbitrary volume-based contract switch or automatic roll of a
held position. Missing/ambiguous mapping is Failed. End of day does not terminate position feeds.

Buy at a conservative ask-based estimate for Bullish; sell at a bid-based estimate for Bearish.
Construct one unit of the selected instrument; do not set the final contract count. Portfolio Risk
Manager sizes it later. Neutral selection yields NoTrade, not a coin-flip direction. Use tick size/multiplier from
definitions, not a business-logic hardcode. Do not create dummy option Greeks or fetch Treasury.

An approved exit policy must provide a finite adverse exit threshold and a positive stress-distance
input, with provenance. Proposed deterministic implementation supports a versioned fixed-point
distance profile first; an ATR/ITI-boundary version would require its own explicit data/formula.
No numeric stop distance is invented here. Long exit must be below entry; short exit above entry.

```text
planned loss per unit = abs(entry - adverse exit threshold) * multiplier
stress loss per unit  = configured stress distance * multiplier
gross risk per unit   = max(planned loss per unit, stress loss per unit) + unit cost reserve
gross notional per unit = abs(entry) * multiplier
```

These are sizing controls, **not a guaranteed maximum loss**. Gaps/slippage can exceed an exit
threshold; initial margin is not maximum loss. The candidate records planned exit/session cutoff,
but the composer neither submits stop orders nor closes a position. Missing required stop/stress
inputs fail construction even if quotes are current; missing margin authority fails final risk evaluation.

## 8. Quantity, costs, prices and risk handoff

Composer returns exactly one **normalization unit**, with final strategy quantity absent and
`SizingStatus = RequiresPortfolioSizing`. It may reject a unit against authenticated construction
constraints, but it never divides available capital by unit risk, determines final units or
reserves capacity. The builder design is authoritative for this boundary.

Portfolio Risk Manager determines the final whole strategy-unit count from the most restrictive
Portfolio, family and Fund limits, current positions/reservations, margin, notional/exposure,
liquidity and contract caps. It independently validates the exact shape/economics and atomically
reserves approved risk. For nonlinear fees/margin, recompute at actual size; do not blindly multiply
unit costs. Missing required truth is Failed; complete truth permitting no unit is a risk rejection,
not permission for Composer to create a zero-quantity order or retry with a different strategy.

Cost reserve explicitly covers profile-defined commissions, fees and adverse entry/exit slippage
without double counting spreads already included in side-based estimates. Candidate package limit
price uses permitted exchange/package tick rules: debit rounded up, credit rounded down, then
economics/limits recalculated. Side quotes are conservative estimates, not guaranteed atomic
fills. No market-order or leg-by-leg execution assumption; execution support is a separate gate.

Risk Management may approve more than one unit: that is initial sizing, not increasing a quantity
previously approved by Composer. The sized result binds the unit candidate hash, approved units,
final leg quantities, price/economics constraints, reservation and approval expiry. Later changes
require valid reapproval. Concurrent workflows cannot spend the same capacity. Risk reservation
and feed ownership are separate; the unit candidate itself cannot be submitted for execution.

## 9. Results, failures and ownership safety

Terminal outcomes:

- `Completed + Candidate`: exact contracts/sides/ContractsPerUnit, UnitCount = 1, final units absent,
  entry estimate/limit, option Greeks, unit payoff/risk/costs, policy/input versions, selection reasons,
  context digest, generation/revision, lease references, result hash and validity; not risk approval.
- `Completed + NoTrade`: healthy complete evaluation found no permissible candidate (expiry,
  direction, delta/width/economics or supplied one-unit constraints); no success-shaped empty order.
- `Failed`: missing/stale/corrupt data, pricing failure, recovery, missing policy/authority,
  timeout/cancellation, capacity overload or invariant failure, with structured error information.

Distinguish a healthy contract failing spread/delta rules from unavailable required quotes. If
missing/stale eligible instruments prevent a qualified candidate, return Failed with the data
cause; do not disguise operational outage as NoTrade. A usable candidate does not require every
unselected illiquid contract to qualify. Record excluded-contract reason counts.

Idempotency binds stage invocation to workflow revision, immutable input/policy digest and deadline.
Duplicate delivery returns the same logical outcome; conflicting reuse fails. Exactly one terminal
event wins completion/failure/timeout races. No stage self-retry, automatic re-entry or historical
tick replay after recovery. Market-data bounded qualification waiting is not a new business retry.

Discovery/composer temporary leases belong to the requesting workflow and expire/renew under Stage
4 policy. Atomically acquire all selected legs before discovery release, retain renewal while the
handoff is pending, and query the idempotent outcome if commit acknowledgement is lost. Durable
strategy need is asserted by the IntrinsicTime workflow; working order by TradeOrder; position by
TradePosition. Successor ownership commits before predecessor ownership is relinquished.

Partial fill retains remaining-order and resulting-position claims. Candidate rejection only
releases candidate/workflow claims that actually ended. UI disconnect, cancel or terminal strategy
cannot remove independent position ownership. On failure/reset, new composition fails closed and
existing durable subscriptions remain. Persist compact result evidence, not a durable tick queue.

## 10. Implementation and acceptance packages

| Package | Implementation targets and required tests |
| --- | --- |
| C1: contracts/configuration | Extend existing Trade.Shared stage contracts with typed input/result/policy references; explicit serialization versions, authorization, incomplete-profile failures and frozen-version tests |
| C2: deterministic policies | One-unit option builder and separate futures builder, condition-policy resolver, expiry/leg selectors and evaluator/ranker; bullish/bearish/explicit neutral, deterministic permutations, complete NoTrade cases and bounded search; no final sizing calculator in Composer |
| C3: financial verification | Independent unit payoff oracle at strikes/tails for unequal-wing condors and both verticals; costs/rounding, delta signs, multiplier/ratio application and unit constraints; synthetic figures only |
| C4: production stage actor | Existing command/processing/completed/failed routing; Portfolio/Fund identity integration, deadlines, duplicate/conflicting invocation, exactly-one terminal, late result/expiry rejection; no submission capability |
| C5: Stage 4 integration | Real coordinator/route wiring, coherent final prices/Greeks, atomic four-/two-leg handoff, independent futures path, reset during selection/commit, lost acknowledgement, renewal/cancellation and retained position feeds |
| C6: risk/workflow handoff | Portfolio-owned initial sizing including multiple units, zero-capacity rejection, nonlinear economics, independent recalculation, stale capacity/broker failure, concurrent reservation safety, sized-result hash and unsized execution rejection; synthetic evidence is not live certification |
| C7: qualification | Windows/Linux managed tests plus required native parity; monthly/weekly/daily UI observations; cold restart and process replacement; provider/calendar fixtures; existing Stage 4 load and live gates remain open |

Proposed types are implementation targets, not claims of existing classes. Keep selection logic in
Trade, market-data qualification in Application.MarketData, and Treasury/model implementation at
their existing boundaries. Update Stage 4 runners and record exact new evidence when implemented.

## 11. Review/activation checklist

Owner direction is recorded: the composer selects contracts for all three profiles. Remaining
financial profile review covers EOM versus another monthly family, weekly debit-first direction
mapping, numerical delta/width/entry parameters, initial quantity caps and approved exit/cost/risk
inputs. Existing Portfolio authority must provide actual budgets; this document does not allocate
funds. No backtested profitability or live acceptance is implied.

The Stage 4 application flag remains false; the existing guard rejects enablement. This design
update does not pass `S4G-04`/`S4G-08`, sign off Stage 3, or authorize live subscriptions/orders.
