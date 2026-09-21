# ES Trade Blotter, Contract Reference, and Option Pricing Specification v3

## 1. Status and scope

Status: approved design specification; implementation is not implied. This document supersedes both earlier ES Trade Blotter documents. V1 trades only futures and futures options, including qualified European and American futures options.

Stage 4 extension: the [IV Rank/Percentile requirements](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md) specify shared option-volatility analytics, comparable-series metadata, strategy consumers and durable/as-known historical evidence. This is planned scope, not implemented capability, and does not change MarketCondition's current underlying-only responsibility.

| Underlying | Exercise | Model | V1 use |
| --- | --- | --- | --- |
| Futures | European | Black-76 | Active |
| Futures | American | American futures lattice | Active |
| Equity | European | Black-Scholes-Merton | Framework/tests only |
| Equity | American | American equity lattice | Framework/tests only |

V1 excludes equity feeds, editors, trading, positions and accounting; exotic models; silent approximations; and changing the two-Trade limit. Equity pricers do not enable equity trading.

## 2. Governing rules

1. Databento supplies provider facts; reviewed IFM data supplies pricing conventions.
2. IFM owns `ContractId`; provider identity is preserved separately.
3. Exercise style is authoritative metadata and is never inferred.
4. `Unknown` blocks pricing/trading when required.
5. Pricing results retain model, versions, inputs, timestamps and convention.
6. Chain Greeks are latest-state; persisted trades/fills retain durable evidence.
7. UI displays domain capabilities and does not recreate policy.
8. Existing serialization keys are immutable; evolution is append-only/versioned.
9. Historical evidence is never rebuilt from current data.

## 3. Architecture

```text
Databento definition -> raw store -> selector -> IFM ID conversion
  -> reviewed editor -> versioned reference contract
Quote -> coherent latest snapshot -> OptionCalculator -> chain Greeks -> blotter
Option trade -> synchronized context -> OptionCalculator -> enriched ScyllaDB trade
Staged strategy -> Composer -> Risk Manager -> broker -> Portfolio/accounting
```

## 4. Contract identity

Every imported instrument retains IFM ID, dataset, publisher ID, instrument ID, raw symbol, definition timestamp/digest and raw payload/reference. Canonical provider key is `(Dataset, PublisherId, InstrumentId)`.

Conversion occurs before save and is deterministic, idempotent, ordinal, culture-independent, collision-checked, versioned, display-independent and covers standard/weekly/end-of-month expiries. Collisions fail without overwrite. Consumers never parse IFM IDs to recover facts.

Provider identity is immutable. Reviewed corrections create effective versions. Selecting another provider instrument is add/replacement. Historical versions remain available.

## 5. Expanded `FuturesContractV3ReadModel`

This is the current `FuturesContractReadModel`. MessagePack keys `0..10` remain unchanged; additions append keys.

| Addition | Rule |
| --- | --- |
| SchemaVersion | Explicit integer |
| Dataset, PublisherId, InstrumentId, RawSymbol | Provider identity |
| DefinitionTimestampUtc, DefinitionDigest | Evidence |
| ExpirationUtc, LastTradingUtc | Exact UTC instants |
| ExchangeTimeZoneId | Validated zone |
| SettlementStyle | Typed convention |
| PriceScale, TickSize | Positive values |
| CalendarVersion | Qualified calendar |
| EffectiveFromUtc/EffectiveUntilUtc | Version validity |
| EvidenceId | Review/source evidence |

Legacy multiplier remains compatible during numeric migration. `OnTheRun` and `Rollover` remain IFM-controlled. Validation requires `FUT`, valid provider identity, coherent times and positive tick/multiplier.

## 6. Expanded `FuturesOptionContractReadModel`

Keys `0..10` remain unchanged; additions append or use a versioned successor with tested translation.

| Addition | Rule |
| --- | --- |
| SchemaVersion | Explicit integer |
| Dataset, PublisherId, InstrumentId, RawSymbol | Provider identity |
| DefinitionTimestampUtc, DefinitionDigest | Evidence |
| UnderlyingContractId | Exact IFM future ID |
| UnderlyingAssetType | Futures in V1; framework permits Equity |
| OptionRight | Typed Call/Put |
| StrikePrice | Positive decimal |
| ExerciseStyle | European/American/Unknown |
| SettlementStyle | DeliveryOfFuture/Cash/Unknown |
| PremiumStyle | PremiumPaid/FuturesStyleVariation/Unknown |
| ExpirationUtc, LastTradingUtc | Exact instants |
| ExerciseCutoffUtc | Optional cutoff |
| ExerciseResultContractId | Optional resulting future |
| ExchangeTimeZoneId | Validated zone |
| MultiplierValue, PriceScale, TickSize | Positive values |
| PremiumTickRule | Typed/versioned rule |
| DayCount, CalendarVersion | Pricing conventions |
| MappingVersion, EvidenceId | Reviewed evidence |
| EffectiveFromUtc/EffectiveUntilUtc | Mapping validity |

`ContractMonth` remains for compatibility only. Priceability requires complete identity, underlying, right, strike, style, settlement, exact times, multiplier/ticks, calendar and reviewed mapping.

## 7. Storage and Databento selector

Raw definitions and reviewed `OptionPricingConvention` remain separate immutable records. Queries map provider key to IFM ID, IFM ID/version to exact convention, root/expiry/right/strike to option, underlying/expiry to chain and effective date to historical version. Never renumber MessagePack keys; missing enums become `Unknown`; maintain golden fixtures. Migration is additive schema, backfill, count/digest reconciliation, cutover, retirement.

Both editors get a shared selector for `Futures` and `Futures Options`; equity choices are hidden. Filters cover dataset, root, exchange, expiry, underlying, right, strikes and lifecycle. Results are paged/virtualized.

1. Query stored definitions; bound live refresh.
2. Select and validate one instrument.
3. Convert to IFM ID.
4. Resolve exact IFM underlying for an option.
5. Populate provider fields read-only.
6. Populate reviewed fields and flag unknowns.
7. Preview mapping/version and save via correlated command/event flow.

Missing underlying blocks save and offers navigation to import it. Add publishes a mapping; Change versions data for the same provider identity; another instrument uses replacement.

## 8. Unified option pricing

`OptionCalculator` becomes the model-neutral facade. `Black76.OptionCalculator` remains focused or acts as a compatibility adapter.

```text
Futures + European -> Black76
Futures + American -> AmericanFuturesLattice
Equity  + European -> BlackScholesMerton
Equity  + American -> AmericanEquityLattice
otherwise          -> typed Unsupported/Incomplete failure
```

The facade validates context, routes, checks model bounds, solves model-consistent IV, calculates price/Greeks and attaches provenance. Requests carry contract/context identity, asset/style/settlement/premium/right, price and basis, spot/futures price, strike, exact times, rate/day count and model-specific carry/dividend data.

Full-risk results carry theoretical price, IV, Delta, Gamma, Vega, Theta, Rho, time to expiry, typed status/failure, model/engine version, convention version, context digest and calculation time. Dedicated price/Delta and IV-only results expose only their calculated outputs and corresponding provenance, never zero-filled uncalculated Greeks. Failure returns no usable numeric payload; zero is not a failure sentinel. See the [calculation-tier and Stage 1 addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md).

Black-76 prices European futures options. Black-Scholes-Merton prices European equity options with explicit dividend/carry inputs. American equity/futures adapters may share a qualified CRR binomial or trinomial kernel but validate distinct economics.

American requirements: early exercise at permitted nodes; bounded configurable steps; convergence verification; flat/reusable buffers; no mutable global date; deterministic scalar/batch APIs; documented Greek bumps; stable exercise-boundary behavior; safeguarded IV; and explicit expiry/zero-volatility behavior.

Each model needs independent price fixtures, IV round trips, Greek checks, monotonicity/boundaries, convergence where applicable, invalid-input tests, scalar/batch equivalence and managed/native parity where relevant. QLNet is not restored.

## 9. Greek calculation and persistence

### Option-chain quotes

The minimum chain-selection calculation is model price and Delta, alongside actual market bid/ask. Use qualified cached IV from a separate controlled IV-only refresh; valid bid/ask midpoint is the default quote-based IV mark, not an executable-price guarantee. Require qualified context, generation and bounded input/IV age/skew. Coalesce rapid quote updates and use batch/reusable buffers; do not calculate full Greeks or solve whole-chain IV per raw tick. Changing Delta targets or widths selects from the latest qualified snapshot rather than automatically repricing the whole chain. Missing/stale required inputs block eligibility until refreshed. Full per-quote Greek history is not durable.

### Selected spreads and current position risk

Order Composer's spread engine consumes the shared immutable real-time chain snapshot for leg selection, spread pricing and aggregate Delta. Selected-candidate validation and Risk Manager obtain fresh IV/full Greeks as required for the selected legs. Held positions require current quote-based full Greeks even when no new option trade occurs; historical trade-time Greeks are not current risk. Portfolio aggregates shared qualified results rather than independently duplicating pricing. See the [calculation-tier addendum](Option-Pricing-Calculation-Tiers-and-Stage-1-Addendum.md) for scheduling, provenance, cache boundaries and staged implementation requirements.

### Persisted Databento option trades

Every retained option trade persisted to ScyllaDB receives a complete `PriceBasis=Trade` calculation attempt. Persist trade and underlying prices/sequences/times, IV and all five Greeks when successful, rate/carry inputs, convention/model versions, digest, calculation time and status/failure. Persist source trade even if pricing fails; derived fields are nullable or status-guarded.

### Broker fills

Broker fills are distinct from Databento trades. Every fill retains independent execution-time pricing/risk evidence. Portfolio/accounting use broker truth.

### IV Rank/Percentile history (Stage 4)

Use shared comparable-series IV analytics, not full Greeks, to calculate Rank/Percentile. Persist both source-IV observations and derived, versioned metric snapshots plus exact workflow references. TradeSelection, Composer/Risk, monitoring and Portfolio/blotter use the measures according to explicit rules and freshness/coverage status. Historical access distinguishes what was known at the decision time from restated research using later corrections. See the [Stage 4 requirements](Option-Volatility-IV-Rank-Percentile-Stage-4-Requirements.md) for consumers, proposed storage/access patterns, roll/tenor policies, retention and test gates.

## 10. Trade Orders integration

Retain Portfolio/Fund/Fund Order/Trade selection, history and lifecycle. Rename `pnlTradeControl` to `pnlTradeBlotter`; replace only its workspace and remove right-side controls after tab equivalents work.

```text
TradeOrderEditorForm
|-- retained Portfolio/Fund/Order/Trade controls
`-- pnlTradeBlotter
    `-- EsTradeBlotterControl
        |-- Market Selection
        |-- Leg Staging
        `-- Orders and Fills
```

## 11. Header and modes

Use the dark-tab convention. On the same header row, right-align dropdowns for Strategy (`Iron Condor`, `Vertical Spread`, `Futures Outright`), Strategy Direction (`Short`, `Long` as applicable) and Broker Mode.

Broker Mode is adapter-driven: Emulator exposes `Emulator`; IBKR exposes `Paper` and authorized `Live`. Unavailable modes are absent. Explicit modes are `ManualEditable`, `ManualSubmitted`, `AutomatedReadOnly`, `HistoricalReadOnly`. Automated/historical modes show persisted evidence and cannot mutate or reconstruct it.

The full form stays between top menu and bottom status bar at supported minimum resolution.

## 12. Market Selection tab

Display underlying, price/change, expiration/DTE, multiplier, tick, exercise/settlement, quote age, pricing status/model, expected move and liquidity. Show about 10-20 virtualized strike rows.

Iron Condor selects shorts nearest configured Delta targets then longs nearest configured widths. It snaps only to eligible listed strikes, obeys tolerances, never fabricates/widens silently and reports deviations. Vertical Spread applies its two-leg policy. Futures Outright selects one future without option Greeks. Clicking stages and never submits.

Short selections use dark red; long selections dark blue. Text stays high contrast and color never replaces textual role/status.

## 13. Leg Staging tab

First columns are `Leg` (`SL-` short/sell, `LL+` long/buy) then `Delta`. Calls precede puts; each group sorts by signed Delta ascending. For a short condor, calls are long then short and puts short then long. Vertical spreads repeat this convention. Futures Outright shows role and `N/A` Delta.

Display contract, right, strike, expiry/style, bid/ask/mid, age, quantity/ratio, multiplier, IV/all Greeks, debit/credit, payoff/risk and validation evidence. Submit requires valid structure, coherent pricing, ticks, Fund Order policy, broker capability and current Risk authorization/reservation.

Commands are Clear Staged Legs, Recalculate/Validate and Submit Opening/Closing Order.

Broker Algorithm contains `None` and `Adaptive` only when supported. IBKR Adaptive is allowed for basic Limit and Market orders after capability validation. Emulator emulates the contract or reports unsupported explicitly.

## 14. Orders and Fills tab

Display broker-authoritative parent orders, expandable legs, fills, requested/cumulative/remaining quantity, type, limit/mid, status, average fill, timestamps, broker IDs, algorithm, costs and ledger state. Commands include cancel unfilled and valid cancel/replace. Acknowledgement is not terminal confirmation. Textual states accompany green success, amber pending/partial and red failure accents.

## 15. Fund Order, Trade and ledger policy

Maximum remains two Trades: primary opening then compatible non-primary closing. Closing is allowed after opening reaches `TradeToOpen`, with compatible family, base symbol, reference and direction.

A Trade is removable only while its Fund Order is open, state is `NewTrade` or proven zero-fill `OrderCancelled`, and no fill/open/MTM/EOD/closing/completion evidence prohibits removal. A Fund Order is deletable only when all Trades are removable. Economic evidence must be closed, not deleted.

Drafts do not post. On order-placement completion, cumulative fill zero posts no position; positive fill triggers accounting exactly once. UI displays but does not own accounting.

## 16. Portfolio interaction

Before submission, Portfolio shows provisional risk/reservation. After fills it shows broker-confirmed quantities, basis, P&L, Greeks, margin/risk and lifecycle. Partial fill creates exposure while remainder stays working; cancelling remainder cannot erase exposure. Automated Risk Manager/exit workflows use the blotter read-only.

## 17. Historical and closed-market testing

Historical mode supplies immutable time-qualified definitions, quotes/trades, rates/calendars/conventions, broker events and Portfolio/Risk outcomes under a virtual/as-of clock and generation. Live and historical sources cannot mix. UI labels source, as-of, session and permissions.

Emulator scenarios cover no fill, partial/full fill, cancel before/after partial fill, replace, rejection, disconnect/recovery, duplicates and out-of-order events. Historical data enables closed-market testing but is not live qualification.

## 18. Performance and GC

- No per-paint parsing, joins or pricing.
- Coalesce superseded quotes to a bounded UI cadence.
- Use batch pricing, spans/reusable arrays, immutable snapshots, virtualized rows and bounded caches.
- Avoid per-node lattice allocations.
- Do not persist every quote's Greeks.
- Never drop retained trades/fills merely to protect UI throughput.
- Overload yields typed status, not unbounded queues.

Evidence includes allocation rate, Gen 0/1/2 counts, pause time, pricing latency percentiles, coalescing counts, Scylla throughput and UI latency under representative market load.

## 19. Safety and observability

Live mode needs explicit environment/account authorization. Confirmation includes adapter/account/mode/algorithm. Logs exclude credentials. Imports, convention publication, pricing failures, orders, broker terminal events and ledger handoffs carry correlation/trace IDs. Unsupported metadata is a visible capability failure, never fabricated data.

## 20. Contract and message impact inventory

Implementation must update every representation carrying the expanded models or pricing results:

- add/change commands and parameters;
- added/changed/completed/failed events;
- query parameters/results and paged option queries;
- HTTP/NATS client contracts and API maps;
- MessagePack generated resolvers and golden fixtures;
- ReferenceDB, SecuritiesDB and MarketDataDB schemas/mappers;
- exact Databento definition projections and pricing convention store;
- option-chain snapshots and trade-tick messages;
- Order Composer, Risk Manager, broker order/fill and Portfolio projections;
- UI view models, controls, designers and system tests.

Any cache or stored blob containing an old positional model needs an explicit migration or version discriminator. Startup must diagnose incompatible payloads without repeatedly throwing deserialization exceptions.

## 21. Implementation packages

### P1: Contracts

Expand both models append-only; add typed enums, exact times and provider identity; update validators, constructors, fixtures, MessagePack golden payloads, commands, events, queries, APIs, storage and tests.

### P2: Reference and selector

Add provider/IFM mapping and indexes; extend definition projections while retaining raw JSON; implement ID conversion/collision tests and paged selectors; enforce identity/versioning.

### P3: Pricing

Add facade/common contracts, retain Black-76, implement Black-Scholes-Merton and American lattice adapters, IV/Greeks/provenance/batches and numerical qualification.

### P4: Market data

Route by reviewed metadata, calculate coalesced chain Greeks, enrich persisted option trades and preserve fill evidence.

### P5: Blotter

Rename host; implement tabs/selectors/modes/history, virtualization, delta/width selection, roles/order/colors.

### P6: Risk and execution

Integrate Composer/Risk, adapter-driven modes/algorithms, Emulator/IBKR submission, cancel/replace and order/fill projections.

### P7: Portfolio and qualification

Preserve policies, integrate Portfolio/automation, reconcile schema, run emulator/load/GC/paper qualification and publish operations guidance.

P1 precedes persisted consumers. P2/P3 may proceed after common contracts stabilize. P4 depends on P2/P3; P5 may use fixtures; P6 depends on P4/P5; P7 closes activation.

## 22. Acceptance criteria

### Contracts and reference data

1. Old serialized fixtures deserialize without key reinterpretation.
2. New contracts round-trip all required fields.
3. Reimport returns the same IFM ID; collisions fail.
4. Historical convention versions remain readable.
5. Unknown style or missing underlying cannot become priceable.
6. Provider-owned identity is read-only after selection.

### Pricing and market data

1. All four routes are independently verified.
2. European/American futures route only to their exact engines.
3. Equity engines are testable but inaccessible to V1 trading.
4. IV/Greeks meet documented tolerances and lattice convergence gates.
5. Failures never return success-shaped zero Greeks.
6. Results retain model/version/convention/time/digest.
7. Chains calculate qualified latest Greeks without every superseded quote.
8. Persisted trades contain Greeks or explicit failure; fills retain separate evidence.
9. Stale, crossed, incoherent or out-of-bounds inputs cannot qualify.

### UI, policy and execution

1. Futures/options are selectable from Databento definitions and IFM ID is previewed.
2. Options require an exact existing underlying future.
3. Form fits between menu/status and shows roughly 10-20 responsive chain rows.
4. Strategy, Direction, Broker Mode and Algorithm obey workflow/adapter capability.
5. Calls precede puts; groups sort signed Delta ascending; role/colors match.
6. Automated/historical modes cannot mutate.
7. First Trade opens and second compatible Trade closes.
8. Fill/MTM/EOD evidence prevents destructive removal.
9. Requested/cumulative/remaining quantities reconcile.
10. Zero-fill completion posts none; positive fill hands off once.
11. Partial fills remain in working-order and Portfolio exposure.

### Performance and recovery

1. No unbounded quote, pricing, UI or persistence queue exists.
2. UI remains responsive under qualified load.
3. GC/allocation evidence meets activation thresholds.
4. Restart reconstructs reference/order/fill/position without durable raw-quote replay to UI.
5. Closed-market historical mode is visibly distinct from live mode.

## 23. Activation gates

Activation requires schema reconciliation, reviewed conventions for every enabled product family, numerical-model sign-off, deterministic-ID sign-off, emulator scenarios, IBKR Paper qualification, live-load GC evidence, Portfolio/accounting reconciliation and explicit product-family enablement.

Equity/equity-option trading remains disabled until a separate specification supplies its reference, market-data, order, risk, Portfolio and accounting workflows.

## 24. Required outcome

V1 selects Databento futures/options, converts them to stable IFM IDs, reviews and versions conventions, prices with the exact European/American engine, displays a low-allocation three-tab blotter, authorizes through Portfolio Risk Manager, executes through the active adapter and reconciles fills through Portfolio/accounting. The framework is ready for future European/American equity pricing without enabling equity trading.
