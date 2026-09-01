# Market Outlook Realtime Indicators and TDI Implementation Plan v1.0

> Historical implementation record. Its Market Outlook revision/persistence mechanics were
> superseded by the versionless hot-cache implementation. Its signal calculations and UI field
> requirements remain applicable.

| Item | Value |
| --- | --- |
| Plan ID | `MOR` |
| Status | Implemented and qualified; MOR-00 through MOR-11 complete |
| Date | 2026-08-31 |
| Scope | Realtime Daily EMA/Bollinger previews, MDI, RSI hot-cache delivery, four-mode ITI trend updates, TDI recovery and UI |
| Historical baseline | `Market-Outlook-Historical-Warmup-Implementation-Plan-v1.0.md` |
| OR-composite baseline | `Documents/system/Live-Market-Data-Health-and-OR-Composite-Implementation-Plan-v1.0.md` |

## 1. Objective

Market Outlook shall start from a verified completed-session historical baseline and then remain responsive throughout the active ES session.

The implementation has two distinct stages:

1. **Historical baseline:** ensure the configured Daily ES history exists and replay completed observations to produce warm committed EMA and Bollinger checkpoints.
2. **Realtime preview:** for every accepted normalized ES last-trade event, treat the latest trade price as the current session's provisional Daily close and recalculate the Daily EMA, Bollinger and MDI display values without advancing the committed Daily observation count.

RSI, ITI and TDI remain independently triggered components. Missing or warming components never block valid siblings.

## 2. Binding calculation and presentation decisions

1. Only accepted normalized ES `New` trade events drive the provisional close. Quote-only, duplicate, stale, cancelled, corrected-without-a-new-normalized-trade, invalid-epoch and rejected-route records do not.
2. Every accepted trade is evaluated. There is no minimum price-change threshold; a same-price accepted trade may produce numerically unchanged values.
3. EMA50 and EMA200 previews always use the previous completed Daily checkpoint plus the current provisional close. Each tick replaces the prior preview and never counts as another day.
4. Bollinger previews use the prior 19 completed Daily closes plus the current provisional close for the 20-observation window, while retaining the approved EMA20 center calculation.
5. Provisional calculations are hot/latest-value state. They are not appended to the Daily event-sourced accumulator and do not create one durable Daily record per tick.
6. The completed session close advances durable EMA and Bollinger state exactly once. Its result must equal the final preview for the same close within configured numeric tolerance.
7. UI publication may coalesce bursts by time to the newest preview, but may not suppress an update because the price change is small. Backend calculation and accepted-input metrics remain per accepted trade.
8. MDI is `clamp(Bollinger Position20 * 100, 0, 100)`. MDI trend is DownTrending below 30, RangeBound from 30 through less than 60, and UpTrending at or above 60. The displayed limits are 30 and 60.
9. RSI is the latest warm, valid `FifteenSeconds`/period-13 value from the RSI hot cache. Pre-warm `-1` sentinels are never valid display values.
10. The accepted ITI trend family is exactly `TrendDirectionChanged`, `TrendExtremeChanged`, `TrendReversalChanged`, and `Trending`, from the configured Daily ITI stream.
11. Every accepted ITI trend signal can replace `LatestItiTrendSignal`. The three change modes also retain their specialized milestone slots. Trend Delta and current trend fields use `LatestItiTrendSignal`.
12. `PredictedIntervalChanged`, `HoldTradeChanged`, and `InTradeChanged` remain outside the Market Outlook trend family and are ignored without exceptions.
13. TDI is an optional Market Outlook component. Its absence or warming state does not make the snapshot incomplete.
14. The TDI row contains Direction, Strength, Market State, Cross, and Price/Signal Divergence.
15. TDI uses the approved `TDI-13-2-7-34-34-1.6185-SMA-v1` calculation and updates after each valid 15-second RSI window once 34 valid RSI samples exist.
16. TDI and MDI are separate concepts. TDI must never populate an MDI-labelled field.
17. All new serialized fields use additive MessagePack keys and additive storage evolution. Existing persisted snapshots remain readable.

## 3. Target runtime flow

```text
Development UI startup
  -> historical coverage check/replay when required
  -> committed Daily EMA/BB checkpoints through prior completed session

accepted Databento ES last trade
  -> canonical hot-cache admission
  -> realtime Daily preview calculator
       committed baseline + current provisional close
       -> EMA50 / EMA200
       -> BB20 standard deviation / upper / EMA20 center / lower
       -> MDI value / MDI trend / 30 and 60 limits
  -> atomic latest Market Outlook preview
  -> bounded latest-value NATS/UI delivery

completed 15-second bar
  -> Wilder RSI13
  -> warm valid RSI hot cache
  -> Market Outlook RSI component
  -> 34-valid-RSI TDI window
  -> TDI Direction / Strength / State / Cross / Divergence

accepted Daily ITI trend signal
  -> one of DirectionChanged / ExtremeChanged / ReversalChanged / Trending
  -> LatestItiTrendSignal and applicable milestone slot
  -> Trend Delta/current trend UI refresh

Daily session close
  -> commit one Daily observation
  -> advance durable EMA/BB checkpoints once
  -> clear/roll provisional state
```

## 4. Implementation gates

### MOR-00 - Baseline, documentation and superseding decisions

Deliverables:

- record current live triggers, cache ownership, persistence boundaries, UI mappings and observed failure modes;
- update the historical-warmup document's completed-Daily-only presentation decision to distinguish committed history from approved realtime previews;
- document the per-trade calculation versus latest-value UI-publication boundary;
- document MDI/TDI separation, four-mode ITI scope and TDI optionality; and
- freeze additive serialization/storage compatibility requirements.

Exit tests:

- architecture/documentation tests resolve every referenced contract and gate;
- current behavior is captured by failing-first characterization tests; and
- no production code changes enter before the baseline evidence is recorded.

### MOR-01 - Realtime preview contracts and provenance

Deliverables:

- define an immutable provisional Daily observation carrying contract, continuation identity, value date, last price, source sequence, source event time, stream epoch and calculation time;
- define typed live preview values for EMA, Bollinger and MDI with explicit `Provisional` calculation method;
- define additive Market Outlook fields for `LatestItiTrendSignal`, TDI presentation values and preview provenance;
- define per-component source watermarks that order previews by accepted source sequence/event time; and
- define hot-state query and notification contracts without treating previews as completed Daily history.

Exit tests:

- unit: serialization keys, round trips, defaults, provenance and ordering;
- BDD: older/duplicate preview cannot replace a newer preview; and
- compatibility: pre-MOR persisted snapshots deserialize successfully.

### MOR-02 - Per-trade Daily EMA and Bollinger preview engine

Deliverables:

- seed the preview engine from the committed prior-session EMA/BB checkpoints;
- recalculate EMA10/20/50/200 and BB10/20 for every accepted ES trade while exposing the approved six UI values;
- replace the current provisional close rather than appending another Daily observation;
- handle first tick, same-price tick, smallest supported price increment, contract roll, restart and session transition; and
- commit exactly one completed Daily observation at the session boundary.

Exit tests:

- unit: deterministic EMA50/EMA200 and BB20 calculations against an independent reference calculator;
- unit: 10,000 ticks leave the committed observation count unchanged;
- BDD: historical baseline -> first tick -> multiple ticks -> final close -> next session;
- verification: final preview and committed-close results agree; and
- performance: sustained and burst tick fixtures remain bounded with no mailbox backlog growth.

### MOR-03 - Live MDI and Market Outlook preview orchestration

Deliverables:

- calculate MDI from the same provisional BB20 result and close lineage;
- publish MDI value, trend and fixed 30/60 limits with EMA/BB preview provenance;
- remove legacy EOD MDI and TDI-as-MDI mappings from the Market Outlook presentation path;
- atomically overlay the latest preview on the durable Market Outlook snapshot; and
- coalesce only UI publication time during bursts while always retaining the newest accepted preview.

Exit tests:

- unit: below-lower, lower, 30, 60, upper, above-upper and zero-width cases;
- BDD: any accepted price increment recalculates the preview without a magnitude threshold;
- integration: accepted trade -> preview -> NATS -> UI model; and
- verification: no provisional path writes a completed Daily row or increments Daily state.

### MOR-04 - RSI13/15-second hot-cache correctness and continuity

Deliverables:

- mark pre-warm RSI output unavailable/invalid for presentation rather than valid `-1`;
- prevent pre-warm values from replacing the last warm RSI hot-cache value;
- restore the Wilder accumulator checkpoint and valid window after actor/application restart;
- register the RSI completion lifecycle and publish the exact hot-cache `FifteenSeconds`/13 value to Market Outlook; and
- expose warm sample progress for diagnostics without making RSI absence block other components.

Exit tests:

- unit: seed, warm transition, no-loss/no-gain, restart, duplicate and stale observations;
- BDD: pre-warm never displays `-1`; the first warm RSI and each subsequent 15-second RSI update the snapshot;
- integration: actor restart retains warm RSI and source ordering; and
- verification: hot cache, Scylla row and Market Outlook RSI share identity and value.

### MOR-05 - TDI recovery, continuity and optional component delivery

Deliverables:

- generate TDI only from 34 ordered warm valid RSI13 observations;
- restore the previous persisted TDI divergence on restart so the first post-restart cross is classified correctly;
- preserve the standard 2/7/34/34/1.6185 SMA calculation contract;
- publish Direction, Strength, Market State, Cross and Divergence independently to Market Outlook; and
- represent absence as `Warming`/`N/A`, not `missing TDI` or a composite failure.

Exit tests:

- unit: lines, bands, state thresholds, bullish/bearish crosses, direction and strength boundaries;
- BDD: 33 valid RSI values remain warming; the 34th produces TDI; restart preserves crossover continuity;
- integration: RSI hot window -> TDI projector -> Scylla -> Market Outlook -> NATS; and
- verification: deterministic fixture matches an independent TDI calculator.

### MOR-06 - Four-mode ITI trend family and latest-trend state

Deliverables:

- admit exactly DirectionChanged, ExtremeChanged, ReversalChanged and Trending for the configured Daily ITI stream;
- add `LatestItiTrendSignal` with one family watermark ordered by source sequence/time;
- retain specialized direction/extreme/reversal milestone slots;
- source Trend, Trend Delta and current ITI values from the latest family signal; and
- ignore the three non-trend modes without throwing or suppressing valid siblings.

Exit tests:

- unit/BDD: all four modes independently update Latest and Trend Delta;
- unit: each change mode updates its milestone slot while Trending leaves milestone history intact;
- stale/out-of-order matrix covers every mode pair;
- verification: the three excluded modes are non-error no-ops; and
- integration: each accepted ITI mode produces a new Market Outlook revision and UI notification.

### MOR-07 - Completeness, status and OR-composite alignment

Deliverables:

- remove TDI from mandatory `MissingInputs` and `IsComplete` conditions;
- stop requiring all three ITI milestones when a valid latest ITI trend signal exists;
- distinguish `warming`, `optional unavailable`, `stale` and genuinely required missing input status;
- preserve independent component progression and last-valid sibling values; and
- update availability-combination verification for the additive preview/latest-ITI/TDI components.

Exit tests:

- BDD: TDI warming never marks Market Outlook incomplete;
- BDD: any one of the four ITI trend modes supplies current trend/Trend Delta;
- exhaustive pairwise and boundary combinations verify OR admission; and
- no expected unavailable/warming/stale path throws a first-chance exception.

### MOR-08 - Market Outlook view model and TDI row

Deliverables:

- add five typed presentation values: TDI Direction, Strength, State, Cross and Divergence;
- add a dedicated five-column TDI table directly below the existing RSI row and above ITI limits;
- format and color bullish/up/positive, bearish/down/negative, reversal/extreme and neutral states consistently;
- display `Warming`/`N/A` until a valid TDI exists; and
- ensure MDI Trend is calculated from MDI, never TDI.

Exit tests:

- presentation unit tests cover every enum, divergence sign, null/warming state and formatting;
- UI binding tests prove a TDI-only component update refreshes the five fields;
- accessibility tests verify names/descriptions and non-color-only meaning; and
- visual verification confirms row order and five equal-width columns.

### MOR-09 - Market Outlook sizing, font and layout qualification

Deliverables:

- reduce labels and value fonts by exactly one point inside `MarketOutlookView` only;
- include the TDI table in dynamic preferred-height and parent-panel calculations;
- retain readable status, borders, contrast, margins and alignment;
- prevent clipping or overlap at supported DPI/scaling and minimum main-window width; and
- leave Market Data, Economic Calendar, Status Console and unrelated screens unchanged.

Exit tests:

- WinForms system tests at 100%, 125% and 150% scaling;
- layout tests at the current 527-pixel panel width and supported wider sizes;
- screenshot/interactive acceptance verifies text, borders, row order and no clipping; and
- regression test proves the font change is scoped to `MarketOutlookView`.

### MOR-10 - End-to-end integration and failure qualification

Deliverables:

- exercise PostgreSQL event state, Scylla projections, Redis hot cache, NATS commands/events/notifications and the WinForms consumer together;
- inject delayed, duplicate, out-of-order, missing, restart and partial-failure conditions at every component boundary;
- verify calculation-per-trade metrics separately from coalesced UI-publication metrics;
- verify live input health remains green/yellow/red based on accepted Databento input rather than indicator availability; and
- retain UI/menu availability regardless of market session or component status.

Exit tests:

- integration: historical warm-up -> live trades -> EMA/BB/MDI previews -> final Daily commit;
- integration: 15-second RSI -> TDI and independent Market Outlook updates;
- integration: all four ITI trend modes over NATS with stale-response fencing;
- restart/soak: repeated API/UI restarts converge without losing warm state or duplicating Daily observations; and
- failure injection: Redis, Scylla, PostgreSQL, NATS and UI-consumer failures recover without corrupting the durable baseline.

### MOR-11 - Full regression, verification and closeout

Required suites:

- Market Data Analytics unit and BDD suites;
- Application MarketData and feed unit suites;
- Redis/NATS/PostgreSQL/Scylla integration suites;
- deterministic EMA/BB/MDI/RSI/TDI/ITI reference verification;
- Market Outlook availability, ordering and restart matrices;
- WinForms presentation, layout, accessibility and interactive UI acceptance;
- API Server and UI builds with zero errors; and
- controlled Development live-feed smoke using the active ES contract.

Exit criteria:

1. Historical coverage is checked and loaded/replayed only when required.
2. Every accepted ES last trade recalculates EMA, Bollinger and MDI without advancing committed Daily state.
3. The final Daily close advances committed state exactly once and agrees with the final preview.
4. Market Outlook displays the latest warm RSI13/15-second hot-cache value and never displays `-1`.
5. TDI appears in its own five-value row after 34 valid RSI values and is optional while warming.
6. All four approved ITI trend modes update Latest ITI and Trend Delta; the other modes are safe no-ops.
7. MDI and TDI are never conflated in contracts, status or UI labels.
8. Small price changes are never filtered by magnitude; UI burst handling always retains the newest result.
9. Market Outlook resizes without clipping at supported scale factors and uses fonts exactly one point smaller only within that view.
10. No expected warm-up, unavailable, stale, duplicate or rejected-input path throws an intentional exception.
11. All listed BDD, unit, integration, verification and UI tests pass and final evidence is recorded before the plan is marked complete.

## 5. Execution order

Gates execute in numeric order. MOR-00 through MOR-03 establish the provisional calculation boundary before UI exposure. MOR-04 and MOR-05 correct RSI/TDI source continuity. MOR-06 and MOR-07 correct ITI and composite semantics. MOR-08 and MOR-09 change the UI only after the typed data is reliable. MOR-10 and MOR-11 qualify the complete runtime.

No gate is complete solely because code compiles. Every gate requires its listed BDD, unit, integration, verification and UI evidence.

## 6. Completion record

| Gate | Status | Qualification evidence |
| --- | --- | --- |
| `MOR-00` | Complete | The historical-warmup plan now separates committed completed-Daily state from non-durable live previews; this plan freezes trigger, provenance, optionality, MDI/TDI and compatibility decisions. |
| `MOR-01` | Complete | Additive EMA/BB preview provenance and `LatestItiTrendSignal` MessagePack keys round-trip. Scylla gains an additive full-snapshot blob while legacy snapshot columns remain readable fallback state. |
| `MOR-02` | Complete | Independent EMA50/EMA200 and BB20 reference assertions pass; final-preview/final-close equality passes; same-price trades recalculate; quote/correction inputs are rejected; 10,000 previews leave the committed 220-observation EMA and 20-close BB checkpoints unchanged. |
| `MOR-03` | Complete | The realtime actor overlays provisional EMA/BB on the latest snapshot, uses strict normalized `New`-trade admission, fences duplicate/stale/gapped epochs and recovers on a new epoch. MDI clamp and exact 30/60 boundary verification passes. |
| `MOR-04` | Complete | Pre-warm RSI is explicitly invalid, cannot enter the presentation hot cache, and warm valid FifteenSeconds/13 completions independently publish to Market Outlook. Restart/order scenarios pass. |
| `MOR-05` | Complete | TDI requires 34 ordered warm valid RSI inputs, seeds prior persisted TDI exactly once after restart, uses non-throwing warming behavior and publishes five optional values. |
| `MOR-06` | Complete | DirectionChanged, ExtremeChanged, ReversalChanged and Trending update the latest ITI family; specialized milestones remain separate; excluded modes are verified no-op paths. |
| `MOR-07` | Complete | TDI is optional, one valid latest ITI mode satisfies current-trend availability, independent siblings retain last-valid values, and the exhaustive availability/OR-composite tests pass without expected-path exceptions. |
| `MOR-08` | Complete | Presentation tests verify Bollinger-derived MDI, latest-ITI Trend/Delta and Direction/Strength/State/Cross/Divergence TDI values, including warming defaults and accessibility descriptions. |
| `MOR-09` | Complete | WinForms system tests render the 527-pixel view through `DrawToBitmap` at simulated 100%, 125% and 150% scaling; row order, contrast, accessibility, equal-percent columns, clipping bounds, dynamic height and view-local font scope pass. The test exposed and corrected an initial TDI docking-order defect. |
| `MOR-10` | Complete | The 49-test Analytics integration suite passes across NATS, PostgreSQL event state, Scylla projection and actor restart/order paths. Storage snapshot/working-state integration round trips pass. |
| `MOR-11` | Complete | All listed regression suites and zero-error API/UI builds pass. A Development `DatabentoLive` runtime smoke registered and started Market Outlook, RSI, TDI, EMA and BB actors; deterministic feed/actor integration supplies repeatable accepted-trade evidence outside an open live-feed window. |

Final qualification on 2026-08-31:

- Market Data Analytics unit: `1101/1101`.
- Market Data Analytics BDD: `474/474`.
- Market Data Analytics integration: `49/49`.
- Application MarketData unit: `89/89`.
- Market Data Feed unit: `500/500`.
- Databento managed/native unit: `132/132`.
- UI presentation: `269/269`.
- UI layout/rendering: `4/4` at 100%, 125% and 150% simulated scaling.
- Market Outlook Scylla storage integration: `2/2`.
- API Server and UI: zero build errors and zero warnings in the explicit build qualification.
- `git diff --check`: clean; line-ending notices are repository CRLF normalization notices, not whitespace defects.
