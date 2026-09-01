# Futures Value Date and Trading State Correction Implementation Plan v1.0

**Status:** Implemented; feed-health portions amended by MDF v1.0
**Date:** 2026-08-31  
**Authoritative policy:** [Futures Value Date and Live Trading Hours Policy](Futures-Value-Date-and-Live-Trading-Hours-Policy.md)

## 1. Objective

Correct the server, actors and desktop UI so they share one Eastern-time futures-session decision and transition coherently without an application restart.

The implementation must distinguish these states:

| State | Market/value date | Open positions | Close positions | Live services |
|---|---|---:|---:|---|
| `LiveTrading` | Market open; active value date | Allowed | Allowed | Available and health-monitored |
| `OffTrading` | Market open; active value date | Prohibited | Allowed | Available as required for monitoring/exits |
| `Closed` | No active value date | Prohibited | Prohibited | Stopped; read-only application remains available |

The authoritative Eastern-time boundaries are:

- market/value-date open: 18:00 Sunday through Thursday;
- market/value-date close: 17:00 Monday through Friday;
- live trading and position entry: 03:00 inclusive through 16:00 exclusive Monday through Friday;
- off trading: the market-open intervals 18:00-03:00 and 16:00-17:00;
- daily close: 17:00-18:00 Monday through Thursday;
- weekend close: Friday 17:00 through Sunday 18:00.

At Monday 18:00 Eastern, for example, the state is `OffTrading` and the value date is Tuesday.

## 2. Current defects

1. `FuturesTradingValueDate.TryGet` rolls at 18:00 but does not exclude 17:00-18:00 Monday through Thursday or Friday after 17:00.
2. `MarketSessionReadModel.IsLiveSessionOpen` conflates an active value-date session with the narrower live-trading/position-entry window.
3. `IFMAppViewModel` reads `GetMarketSession` only during startup, so an open UI keeps the previous value date after 18:00.
4. `FuturesContractRolloverStartupService` stops its transition loop after the first successful feed start and therefore does not supervise later session/value-date boundaries.
5. The 03:00-16:00 position-entry window is also used as a feed-monitoring/automatic-startup concept. That is incorrect: production automatic feed startup belongs to the 18:00 market-open boundary, and an active feed required throughout the value-date session must remain monitored during `OffTrading`.
6. `TradeOrderEditorViewModel` currently allows close actions without a market-open gate, including closed hours.
7. Value-date-dependent UI models, analytics services and feed epochs are initialized from a startup snapshot and lack an atomic rollover/rebind workflow.
8. Existing tests encode the incomplete semantics, including Friday evening as active and Sunday evening as “live.”

## 3. Design decisions

### 3.1 One authoritative decision

Add a pure shared resolver, provisionally `FuturesMarketSessionPolicy.Resolve(DateTimeOffset)`, using `FuturesTradingValueDate.MarketTimeZone`. It returns one internally consistent decision containing:

- `FuturesMarketState State` (`Closed`, `OffTrading`, `LiveTrading`);
- `OperationalValueDate`;
- nullable `ActiveValueDate`;
- market-local time;
- current operational session start/end UTC;
- next state-transition UTC.
- decision `Revision` and `AsOfUtc` when published by the runtime authority.

`ActiveValueDate` is present exactly when `State != Closed`. Position permissions and convenience booleans are derived from `State`; they are not independently writable fields.

The API server hosts a singleton, lifecycle-owned `IFuturesMarketSessionAuthority`. It initializes synchronously during API startup, before dependent market-data startup and before the server is reported ready to the UI. It owns the current immutable, monotonically versioned session snapshot and advances it at authoritative boundaries. `GetMarketSession` returns this snapshot rather than recalculating an unrelated decision for every transport or consumer.

On process restart, the authority reconstructs the deterministic current decision from the injected Eastern-time clock and this policy. No database persistence is required for the calendar-only decision. Starting during an open market session establishes that session's active value date immediately; starting during `Closed` exposes the most recently completed operational value date and no active value date.

The ambiguous `IsLiveSessionOpen` contract is replaced by precise state/derived properties. All server, HTTP, NATS and UI consumers are updated together; no legacy compatibility behavior is required. Consumers, including the UI, may use their clocks only to decide when to re-query; the returned server snapshot remains authoritative.

### 3.2 Separate market state, permissions and feed intent

- `LiveTrading` controls permission to open positions.
- `LiveTrading` and `OffTrading` permit closing/reducing positions.
- `Closed` permits neither opening nor closing.
- When production automatic feed startup is enabled, the required feed starts at every market open: 18:00 Eastern Sunday through Thursday. Sunday 18:00 starts the Monday value-date feed; Monday 18:00 starts Tuesday's, and so on through Thursday 18:00 for Friday.
- An API server started or restarted mid-session starts the required production feed immediately after the authoritative value date and contracts are resolved; it does not wait for 03:00 or the next 18:00 boundary.
- The feed remains active throughout both `OffTrading` and `LiveTrading`. During `OffTrading`, health is `OffHoursActive` or non-critical `OffHoursDegraded`; during `LiveTrading`, health is green/yellow/red. The 03:00 and 16:00 transitions alter health mode and position permissions but never constitute feed lifecycle boundaries.
- At 03:00, every enabled running route starts a new green live-health epoch. If no accepted update arrives, it becomes yellow after five minutes and red after fifteen minutes.
- At 17:00, active feeds and value-date-specific live services stop before the market becomes `Closed`. During `Closed`, the supervisor waits for the next 18:00 market open.
- Development/manual operation may disable automatic startup by configuration. If automatic startup is enabled in any environment, it follows the market-open/market-close lifecycle above, never the 03:00 position-entry window.

### 3.3 Coherent rollover

One serialized rollover coordinator owns each transition. For a value-date change it must:

1. fence concurrent and stale session responses;
2. stop or detach old-value-date feed/analytics/listener owners;
3. resolve the new current-contract snapshot;
4. establish the new operational and active value date;
5. recreate/rebind value-date-capturing UI models, including Strategy Operations;
6. reload bars, Market Outlook snapshots and value-date history;
7. start the new value-date consumers and historical warm-up;
8. publish one final coherent UI state and transition status.

The displayed date must never advance alone while dependent services remain on the previous date. Failure retains the last coherent state, reports degraded transition status without expected first-chance exceptions, and retries with bounded delay.

### 3.4 Boundary-driven monitoring

The server remains authoritative. The UI queries the typed `GetMarketSession` result at startup and schedules its next query from `NextTransitionUtc`, with a bounded periodic reconciliation interval to recover from clock drift, missed wakeups or transient query failure. `TimeProvider` is used throughout so boundary behavior is deterministic in tests.

## 4. Implementation gates

### VDS-01 — Policy baseline and affected-path inventory

**Work**

- Treat the approved policy document as binding.
- Record every use of `FuturesTradingValueDate`, `MarketDataFeedMonitoringWindow`, `PositionEntryWindow`, `IsLiveMarketSessionOpen`, captured `_valueDate`, feed epoch value date and session start/end.
- Classify each use as market-state, entry-permission, exit-permission, feed-lifecycle, health-monitoring or read-only.

**Exit criteria**

- The inventory has no unclassified production consumer.
- Existing contradictory tests and documentation are listed before modification.

### VDS-02 — Shared state contract

**Work**

- Add `FuturesMarketState` with stable serialized values.
- Replace ambiguous session fields with the authoritative state decision and derived permission properties.
- Add `Revision`, `AsOfUtc` and `NextTransitionUtc` to the typed session result.
- Preserve `OperationalValueDate` for read-only operation during `Closed`; make `ActiveValueDate` nullable and state-consistent.

**Tests**

- Contract/default/MessagePack round-trip tests.
- Invalid or contradictory decision construction tests.

**Exit criteria**

- A session result cannot claim `Closed` with an active value date or `LiveTrading` without one.

### VDS-03 — Authoritative Eastern-time resolver

**Work**

- Implement exact inclusive/exclusive 03:00, 16:00, 17:00 and 18:00 boundaries.
- Correct weekday maintenance and weekend closure behavior.
- Calculate operational value date, active value date, session bounds and next transition from one resolver.
- Route `FuturesTradingValueDate`, position-entry decisions and market-session queries through it.

**Tests**

- Unit tests immediately before, exactly at and immediately after every boundary.
- Monday-through-Thursday, Friday, Saturday and Sunday cases.
- Eastern daylight/standard-time cases around both DST transitions.

**Exit criteria**

- Monday 18:00 resolves `OffTrading` with Tuesday value date.
- Monday 17:00 resolves `Closed` with no active value date.
- Monday 03:00 resolves `LiveTrading`; Monday 16:00 resolves `OffTrading`.

### VDS-04 — Market-session query transport

**Work**

- Update query actor, REST mapping/client, NATS client and UI service for the new typed result.
- Return the immutable snapshot owned by the API-server session authority; do not recalculate independent transport-specific decisions.
- Ensure each state advancement samples its injected time once and increments the monotonic revision exactly once.
- Remove assumptions that `ActiveValueDate.HasValue == IsLiveSessionOpen`.

**Tests**

- Actor unit tests.
- HTTP and NATS serialization/typed-response integration tests.
- Cancellation and failure-result tests.

**Exit criteria**

- HTTP and NATS return the same authoritative decision and revision for the same server state.

### VDS-05 — Long-running backend rollover supervision

**Work**

- Initialize `IFuturesMarketSessionAuthority` before dependent market-data startup and expose readiness only after its first coherent snapshot exists.
- Convert `FuturesContractRolloverStartupService` into a lifetime supervisor rather than a first-start loop.
- When production automatic startup is enabled, prepare the new value-date contract state and start its required feed at every 18:00 market open, Sunday through Thursday.
- If the API server starts during an open session, resolve the current value date/contracts and start the required feed immediately; if it starts during `Closed`, wait for the next 18:00 transition.
- Keep the feed running without restart across the 03:00 and 16:00 permission transitions.
- Stop live epochs at 17:00 and reject closed-market starts.
- Retain an explicit development/manual override while making its environment scope and state visible.
- Make repeated or missed transitions idempotent and recoverable.

**Tests**

- Manual-time tests across 16:00, 17:00, 18:00 and 03:00.
- Cold-start/restart tests at 01:00, 04:30, 16:30, 17:30 and during the weekend.
- Production-automatic versus development-manual configuration tests.
- Delayed wakeup/missed-boundary recovery.
- Duplicate start/stop and partial provider-failure tests.

**Exit criteria**

- The supervisor processes multiple consecutive value dates without process restart or duplicate epochs, and automatic startup never waits for 03:00 while the market is already open.

### VDS-06 — Feed lifecycle and health semantics

**Work**

- Separate position-entry window from feed-health monitoring.
- Monitor every enabled Databento feed during both `LiveTrading` and `OffTrading`.
- Keep green/yellow/red freshness semantics active only during `LiveTrading`.
- During `OffTrading`, expose `OffHoursActive` through fifteen minutes and `OffHoursDegraded` afterward without a critical modal or automatic lifecycle action.
- Reset enabled running routes to a new green health baseline at 03:00 without restarting the feed epoch.
- Start production-automatic feeds at 18:00, keep them active across 03:00 and 16:00, and stop them at 17:00.
- Stop feeds in `Closed`; allow development/manual feed start during a market-open session.
- Replace misleading `OutsidePositionEntryWindow` health behavior where it suppresses enabled-feed monitoring.

**Tests**

- Active feed at 15:59 remains monitored after 16:00.
- Feed automatically starts at 18:00 when production automatic startup is enabled.
- Feed remains the same owned epoch across 03:00 and 16:00 permission changes.
- Feed is stopped at 17:00.
- API startup during `OffTrading` or `LiveTrading` starts the configured production feed immediately; startup during `Closed` does not.
- Development/manual market-open start is accepted; closed-market start is rejected without an expected exception path.
- Live green/yellow/red thresholds remain unchanged; off-hours use the single fifteen-minute degraded threshold.

**Exit criteria**

- No enabled Databento route is marked “monitoring paused” solely because entry hours ended, and no off-hours route is reported as live yellow/red.

### VDS-07 — UI session monitor

**Work**

- Add a lifecycle-owned, single-flight session monitor to `IFMAppViewModel`.
- Query the API-server authority at startup, at each authoritative `NextTransitionUtc`, and on bounded reconciliation.
- Use generation/revision fencing so delayed responses cannot restore an older state/value date.
- Retry transient failures without disabling menus or throwing expected first-chance exceptions.
- Stop the monitor cleanly during shutdown/disposal.

**Tests**

- Manual-time UI tests spanning all four daily boundaries without restart.
- Stale-response, cancellation, shutdown and query-failure recovery tests.

**Exit criteria**

- An app opened before Monday 18:00 displays Tuesday immediately after the boundary and never reverts to Monday.

### VDS-08 — Atomic value-date UI/service rollover

**Work**

- Add a serialized rollover coordinator in the UI application layer.
- Rebind every value-date-capturing dependency identified by VDS-01.
- Recreate Strategy Operations for the new date and reload Market Outlook/bars/history.
- Restart required event consumers, analytics activations and historical warm-up only after the new contract/value-date state is coherent.
- Fence late old-value-date events from all refreshed views.

**Tests**

- Successful rollover ordering and single execution.
- Failure injected at each rollover phase, followed by retry/recovery.
- Old-date event and delayed-query fencing.
- No mixed-date UI snapshots.

**Exit criteria**

- The visible date, Operations model, bar queries, analytics commands and feed epoch all use one value date after transition.

### VDS-09 — Open/close permission enforcement

**Work**

- Replace local clock-only entry checks with the authoritative state policy/decision.
- Allow open and close during `LiveTrading`.
- Allow close/reduce but not open during `OffTrading`.
- Allow neither during `Closed`.
- Apply the rule to manual Trade Orders now and expose a reusable policy for later automated order execution.

**Tests**

- Unit and BDD permission matrix for every state and order action.
- Stale-state rejection at the submission boundary.
- UI button/command enablement and operator message tests.

**Exit criteria**

- The previous “always allows close” closed-market behavior is removed; off-trading exits remain available.

### VDS-10 — Operator status and observability

**Work**

- Display market state, operational/active value date and next transition without expanding market-hours restrictions to menus.
- Use explicit text: `Live Trading`, `Off Trading — exits only`, or `Market Closed`.
- Emit one structured transition log containing old/new state, old/new value date, reason and correlation/trace identity.
- Add rollover success/failure/retry and stale-response counters.

**Tests**

- UI state text/color and accessibility assertions.
- Status-console transition ordering and no-duplicate-message tests.

**Exit criteria**

- Operators can distinguish market open from position-entry permission at a glance.

### VDS-11 — BDD qualification

**Scenarios**

- Sunday 17:59:59 closed to Sunday 18:00 off trading with Monday value date.
- Monday 02:59:59 off trading to 03:00 live trading.
- Monday 15:59:59 live trading to 16:00 off trading.
- Monday 16:59:59 off trading to 17:00 closed.
- Monday 17:59:59 closed to 18:00 off trading with Tuesday value date.
- Friday 16:59:59 off trading to 17:00 weekend closed.
- Open/close permission outcomes in all three states.
- Running UI crosses rollover with no restart and no mixed value dates.

**Exit criteria**

- Every approved business statement is represented by an executable scenario.

### VDS-12 — Unit and minimum-combination verification

**Work**

- Execute the deterministic state matrix across day categories, boundary buckets and daylight/standard time.
- Cover all meaningful combinations without generating thousands of redundant cases.

**Minimum matrix**

- Sunday: before and at 18:00.
- Monday-Thursday: before/at 03:00, before/at 16:00, before/at 17:00 and before/at 18:00.
- Friday: the same live/off boundaries plus weekend close at 17:00.
- Saturday: closed.
- DST: one daylight and one standard-time value-date cycle, plus transition-week conversion checks.
- Permissions: three states by open/close actions (six combinations).

**Exit criteria**

- Every distinct state transition and permission result is covered; duplicated calendar dates add no artificial completeness claim.

### VDS-13 — Integration and concurrency qualification

**Work**

- Run actor/NATS/HTTP session-query integration.
- Run backend supervisor across multiple simulated days.
- Run UI-to-server typed query and rollover integration.
- Inject concurrent session responses, feed events and shutdown during rollover.
- Verify feed and analytics actors reject old-value-date work after the generation advances.

**Exit criteria**

- No duplicate feed epoch, mixed-date actor state, leaked listener or stale UI reversal occurs under concurrency.

### VDS-14 — Interactive UI acceptance

**Journeys**

1. Start before 16:00; verify open/close enabled and feed health active.
2. Cross 16:00; verify `Off Trading — exits only`, open disabled, close enabled and active feed health continues.
3. Cross 17:00; verify `Market Closed`, open/close disabled and feed stopped while all menus remain usable.
4. Cross 18:00; verify next value date, `Off Trading — exits only`, production-automatic feed startup, coherent Operations/Market Outlook data and no restart.
5. Cross 03:00; verify `Live Trading` and open/close permissions while the existing feed epoch continues without restart.
6. Simulate query/provider failure and recovery without exceptions used as control flow.
7. Start the API during an open session; verify the authoritative value date is immediately available and the production-automatic feed starts without waiting for a boundary.
8. Start the API during `Closed`; verify read-only operational value date, no active value date and no feed until the next 18:00 open.

**Exit criteria**

- All journeys pass at 100%, 125% and 150% scaling with no disabled navigation or stale value date.

### VDS-15 — Documentation and final regression

**Work**

- Update the policy, market-session API documentation, futures rollover startup documentation, ITI timeframe specification and actor conventions with the precise state terminology.
- Remove obsolete wording equating active value date, live trading and feed monitoring.
- Run affected domain, API, feed, analytics, UI presentation and UI system suites.
- Record exact commands, counts and any unrelated baseline failures.

**Exit criteria**

- Builds have zero new warnings/errors.
- All VDS BDD, unit, integration, verification and UI tests pass.
- Documentation and implementation describe the same boundary and permission model.

## 5. Execution order and dependencies

```text
VDS-01
  -> VDS-02 -> VDS-03 -> VDS-04
                    |       |
                    v       v
                  VDS-05 -> VDS-06
                    |         |
                    +----+----+
                         v
                  VDS-07 -> VDS-08 -> VDS-09 -> VDS-10
                         |       |        |
                         +-------+--------+
                                 v
                         VDS-11 -> VDS-12 -> VDS-13 -> VDS-14 -> VDS-15
```

VDS-02 through VDS-06 establish the authoritative backend contract before the UI rollover coordinator is activated. Permission enforcement follows coherent state propagation so order controls never make decisions from a stale local date.

## 6. Required test layers

| Layer | Required coverage |
|---|---|
| BDD | Approved market-state transitions and open/close language |
| Unit | Boundary resolver, DST, next transition, permission matrix, health state |
| Actor/API | Query parsing, NATS/HTTP typed round trip, cancellation/failure |
| Backend integration | Multi-day rollover, feed epoch lifecycle, partial failure/recovery |
| UI presentation | Boundary monitor, stale fencing, atomic rebind, command enablement |
| Verification | Minimum representative day/time/state/action combinations |
| UI/system | Visible state/date, menus always available, scaling and interactive journeys |

## 7. Non-goals

- Exchange holidays and early closes are not added by this correction; they require a separately approved calendar policy.
- Broker-specific fill guarantees or liquidity assumptions are not introduced.
- New automated order-execution actors are not added.
- Historical datastore migration is not required.
- The correction does not restrict application navigation based on market state.
- Migrating every application/startup readiness check into the API server is deferred. This correction establishes the API-server session/value-date authority and feed lifecycle that the later migration will consume.

## 8. Definition of complete

The correction is complete only when the API server owns one versioned authoritative session/value-date snapshot, a UI and backend left running across multiple daily boundaries remain on that coherent value date, production-automatic feeds start at 18:00 and stop at 17:00 without lifecycle changes at 03:00 or 16:00, the approved open/close permissions are enforced, feed health remains active whenever a feed is enabled, and every VDS gate passes with recorded evidence.
