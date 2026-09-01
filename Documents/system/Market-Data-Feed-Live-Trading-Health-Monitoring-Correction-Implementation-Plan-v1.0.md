# Market Data Feed Session-Aware Health Monitoring Correction Implementation Plan v1.0

**Status:** Complete
**Date:** 2026-08-31
**Supersedes:** The feed-health-monitoring portions of `Futures-Value-Date-and-Live-Trading-Hours-Policy.md` and gates VDS-01/VDS-06/VDS-09/VDS-10 that apply live-trading green/yellow/red semantics unchanged during `OffTrading`.

## 1. Objective

Apply market-state-aware Databento feed health: full green/yellow/red freshness evaluation during the authoritative weekday live-trading window of 03:00 inclusive through 16:00 exclusive Toronto/New York Eastern time, and a non-critical degraded indication after fifteen minutes without accepted data during `OffTrading`.

This correction does not change the futures value-date or feed-lifecycle boundaries:

- the active value-date session remains 18:00 through 17:00 Eastern;
- production automatic feeds may start at 18:00 and remain available through 17:00 for monitoring and exits;
- position entry remains permitted only during `LiveTrading`;
- exits remain permitted during `LiveTrading` and `OffTrading`;
- live-trading freshness is enforced during `LiveTrading`;
- off-trading freshness is observed with one fifteen-minute degraded threshold that never stops or restarts the feed;
- application navigation and stored-data functions remain independent of market state.

## 2. Required semantics

| Market state | Eastern interval | Feed may run | Freshness state | Operator behavior |
|---|---|---:|---|---:|
| `LiveTrading` | Weekdays 03:00-16:00 | Yes | Green/yellow/red | Yes |
| `OffTrading` | 18:00-03:00 and 16:00-17:00 | Yes | `OffHoursActive`/`OffHoursDegraded` | Status indication; no critical modal or automatic feed action |
| `Closed` | 17:00-18:00 and weekend close | No | `Inactive` | No |

During `LiveTrading`, the accepted-cache-update thresholds remain:

- green through five minutes without an accepted update;
- yellow after five minutes and through fifteen minutes;
- red after fifteen minutes.

During `OffTrading`, an enabled route is `OffHoursActive` through fifteen minutes without an accepted update and `OffHoursDegraded` after fifteen minutes. Degradation is visible in the UI, status console and API health data, but it never stops, restarts or disables the feed and never produces the live-trading critical modal.

At 03:00, every enabled route starts a new live-monitoring epoch with a green baseline, including a route that was `OffHoursDegraded` immediately beforehand. Off-hours degradation remains available in telemetry but does not carry its severity into live trading. If no accepted update arrives after the boundary, the route becomes yellow after five minutes and red after fifteen minutes. At 16:00, red/yellow becomes `OffHoursActive` or `OffHoursDegraded` according to the actual last-update age, and live-critical notification state is cleared.

Feed command lifecycle and feed freshness are separate concerns. Start/stop commands must still produce correlated completion or failure events. An off-hours automatic-start timeout is recorded as a lifecycle warning and degraded operational state, not described to the operator as a live-trading freshness failure. A manual command failure remains visible as the result of the requested operation.

## 3. Baseline defects corrected by this plan

1. `MarketDataFeedMonitoringWindow` currently follows the broad 18:00-17:00 value-date session.
2. `MarketSessionReadModel.IsLiveSessionOpen` currently means market/value-date open rather than weekday 03:00-16:00 live trading.
3. `IFMAppViewModel` starts its health-monitor loop whenever that broad field is true.
4. `MarketDataFeedHealthMonitor` cannot distinguish live-trading severity from non-critical off-hours degradation.
5. UI health text says "within 1 minute" although the implemented green threshold is five minutes.
6. API readiness evaluates ES/VX route freshness outside live-trading hours with the same route severity instead of the approved single off-hours degraded state.
7. The observed feed-start command was persisted without a correlated terminal event, causing the independent 60-second UI command timeout.
8. Existing authoritative documentation does not distinguish non-critical off-hours degradation from actionable live-trading yellow/red health.

## 4. Implementation gates

### MDF-01 — Policy and consumer inventory

**Work**

- Amend the authoritative policy to distinguish feed availability from freshness enforcement.
- Update the prior VDS plan and UI implementation documentation to define the separate off-hours fifteen-minute degraded behavior.
- Inventory every use of `IsLiveSessionOpen`, `MarketDataFeedMonitoringWindow`, `PositionEntryWindow`, route `HealthAt`, and feed-health UI state.
- Classify each use as value-date lifecycle, entry permission, exit permission, command lifecycle or freshness monitoring.

**Exit criteria**

- No production consumer uses a position-entry or freshness field to decide the 18:00/17:00 feed lifecycle.
- No freshness consumer uses only `ActiveValueDate.HasValue` or the broad market-open state.

### MDF-02 — Authoritative market-state contract

**Work**

- Add an explicit serialized `FuturesMarketState` decision: `Closed`, `OffTrading`, or `LiveTrading`.
- Replace ambiguous `IsLiveSessionOpen` usage with precise derived properties such as `IsMarketOpen` and `IsLiveTrading`.
- Keep operational/active value dates and 18:00/17:00 session boundaries unchanged.
- Make the API-owned session snapshot the only authority consumed by the UI and backend health checks.
- Include 03:00 and 16:00 in `NextTransitionUtc`, as well as 17:00 and 18:00.

**Tests**

- MessagePack/transport round-trip and default-validation tests.
- Exact state tests at 02:59:59, 03:00, 15:59:59, 16:00, 16:59:59, 17:00 and 18:00 Eastern.
- Weekday, weekend and Eastern DST transition tests.

**Exit criteria**

- A consumer cannot confuse `OffTrading` with `LiveTrading`.
- All four daily state boundaries are observable without restarting the API or UI.

### MDF-03 — Shared session-aware freshness mode

**Work**

- Replace the binary monitoring-window concept with an authoritative mode: `Inactive`, `OffTrading`, or `LiveTrading`.
- Keep feed lifecycle calculations in the value-date/session policy and prevent either policy from calling the other by implication.
- Expose deterministic current-start and next-transition calculations through injected `TimeProvider`-compatible APIs.

**Tests**

- Pairwise boundary tests for every weekday.
- Friday 16:00 live-to-off-hours reclassification, Friday 17:00 close, Sunday 18:00 off-hours monitoring start, and Monday 03:00 off-hours-to-live reclassification.
- Standard-time and daylight-time UTC conversions.

**Exit criteria**

- The resolver returns `LiveTrading` only from 03:00 inclusive to 16:00 exclusive, `OffTrading` only while the value-date session is open outside that window, and `Inactive` while closed.

### MDF-04 — Health-monitor state machine

**Work**

- Give `MarketDataFeedHealthMonitor` explicit feed-active and authoritative market-state inputs.
- Return `OffHoursActive` through fifteen minutes and `OffHoursDegraded` after fifteen minutes while a feed is active during `OffTrading`; return `Inactive` when no feed is owned.
- Reset every enabled route's live-health baseline at 03:00 while retaining its off-hours timestamp for telemetry; initialize a newly enabled route from its activation time.
- At 16:00, clear live-critical edge-notification state without clearing route timestamps or stopping the feed.
- Preserve per-route OR semantics: each enabled route records its own accepted update; one valid route update must not be rejected because another route is quiet.
- Preserve live-trading five-minute green and fifteen-minute red boundaries, and add the single off-hours fifteen-minute degraded boundary.

**Tests**

- Unit tests for inactive, off-hours active/degraded, live green/yellow/red and recovery transitions.
- A route degraded at 02:59:59 becomes live green at 03:00 and becomes yellow/red only if the new live epoch receives no accepted update for five/fifteen minutes.
- A route updated shortly before 03:00 also becomes live green under the same new live epoch.
- No critical modal after 16:00, including a red transition scheduled on the exact boundary; off-hours degradation remains visible.
- ES-only, VX-only and ES+VX route combinations with independent updates.

**Exit criteria**

- The monitor cannot enter live yellow or live red outside `LiveTrading`.
- Off-hours age can enter `OffHoursDegraded` but cannot trigger feed stop/restart or a live-critical modal.

### MDF-05 — API runtime and readiness behavior

**Work**

- Keep automatic feed start/stop tied to 18:00/17:00 market lifecycle boundaries.
- Gate route-freshness severity in `MarketDataRuntimeHealthCheck` on the authoritative market state.
- During `OffTrading`, expose feed-running state, timestamps and counters; report `OffHoursDegraded` after fifteen minutes while leaving the feed owned and the core API ready.
- During live trading, continue to degrade readiness for yellow/red route health according to the approved health contract.
- Ensure the API health response reports market state and whether health is live, off-hours active/degraded, or inactive.

**Tests**

- Health endpoint integration tests at 02:59, 03:00, 16:00, 17:00 and 18:00.
- Feed remains the same runtime epoch across 03:00 and 16:00.
- Off-hours accepted-update age above fifteen minutes reports degraded health without stopping the feed or making the core API unavailable.

**Exit criteria**

- API health distinguishes non-critical `OffHoursDegraded` from live-trading red and keeps the core API ready.
- Feed lifecycle still follows the 18:00/17:00 value-date session.

### MDF-06 — UI monitoring and operator presentation

**Work**

- Start the UI evaluation loop for the application lifetime, but drive it from the authoritative market-state snapshot rather than starting it only under one startup condition.
- Transition the health monitor at every authoritative session revision.
- Display `Feed Health: Off-hours Active` through fifteen minutes and `Feed Health: Off-hours Degraded` afterward; display `Feed Health: Stopped` during `Closed` when no feed is active.
- Correct green text to "accepted updates within 5 minutes."
- Suppress live-critical stale-feed dialogs outside `LiveTrading` while retaining the off-hours degraded indicator and status-console telemetry.
- Keep menus and read-only screens available in every state.

**Tests**

- View-model tests with manual time across 03:00 and 16:00 without UI restart.
- UI binding tests for off-hours active/degraded and live green/yellow/red labels and colors.
- Verification that off-hours degradation is visible but no stale-feed modal appears at 18:00, overnight, after 16:00 or during closed hours.

**Exit criteria**

- UI state changes at 03:00 and 16:00 from the API decision without reconstructing Eastern time locally.

### MDF-07 — Feed-command terminal-event reliability

**Work**

- Trace `StartMarketDataFeedCommand` from command persistence through event projection, event-actor execution and the UI status consumer.
- Guarantee exactly one correlated complete or fail terminal event for every accepted start/stop command.
- Add bounded query-based reconciliation when the command is accepted but the UI misses the terminal notification.
- Keep terminal-event failure separate from freshness health and assign time/state-appropriate operator severity.
- Ensure retries are idempotent and cannot create duplicate Databento epochs or duplicate ES/VX streams.

**Tests**

- NATS integration test from typed command through terminal event.
- Injected lost, delayed and duplicated terminal notifications.
- Event-projector restart and UI-listener reconnect tests.
- Automatic off-hours timeout produces lifecycle warning without freshness alert; live-hours failure remains actionable.

**Exit criteria**

- The persisted-without-terminal condition observed at 19:41 cannot recur silently.
- Reconciliation determines the actual backend feed state before presenting a timeout result.

### MDF-08 — BDD and unit qualification

**BDD scenarios**

- `OffTrading` feed with an update less than fifteen minutes old is `OffHoursActive`.
- `OffTrading` feed without an accepted update for more than fifteen minutes is `OffHoursDegraded` and remains running.
- A route degraded at 02:59:59 becomes live green at 03:00; the off-hours degradation remains telemetry only.
- With no accepted update after 03:00, the route becomes yellow after five minutes and red after fifteen minutes; an accepted update resets that live timer.
- A valid accepted update returns the route to green.
- At 16:00, health changes immediately to the appropriate off-hours state and no live-critical alert is emitted.
- At 18:00, the next value-date feed may start and begins `OffHoursActive` from route activation.
- At 17:00, the feed stops and health becomes inactive.

**Unit suites**

- Shared market-state policy and transition scheduler.
- Monitoring-window resolver.
- Per-route health state machine.
- UI alert-classification and status-text helpers.
- Terminal correlation and reconciliation.

**Exit criteria**

- All boundary and state-transition tests pass under `FakeTimeProvider` with no wall-clock sleeps.

### MDF-09 — Integration and process qualification

**Work and tests**

- Run API + NATS + actor integration tests with typed market-session queries and feed terminal events.
- Exercise ES-only, VX-only and combined ES/VX feeds.
- Keep the process alive across 02:59:59→03:00, 15:59:59→16:00, 16:59:59→17:00 and 17:59:59→18:00 using controlled time.
- Validate API health JSON, UI typed state and backend runtime counters agree at every boundary.
- Inject Databento silence, burst traffic, delayed source timestamps and reconnects during both live-trading and off-trading windows.

**Exit criteria**

- No component reports live yellow/red outside `LiveTrading`; `OffHoursDegraded` is reported after fifteen minutes.
- During `LiveTrading`, every accepted ES or VX cache update is reflected in the correct route's freshness state.
- No duplicate feed epoch or stream is created across permission-only transitions.

### MDF-10 — UI acceptance, verification and documentation closure

**Interactive journeys**

1. Start during `OffTrading`; verify `OffHoursActive`, transition to `OffHoursDegraded` after fifteen minutes without data, and confirm the feed remains running without a critical modal.
2. Cross 03:00 with both recently updated and already degraded routes; verify both start green without restarting the UI or feed, then independently follow the five/fifteen-minute live thresholds.
3. Stop ES updates while VX continues; verify independent route status and combined presentation.
4. Cross yellow and red boundaries; verify exactly one actionable alert on red.
5. Restore ES; verify green recovery.
6. Cross 16:00; verify immediate off-hours classification, no live-critical modal and continued exit-only access.
7. Cross 17:00 and 18:00; verify stopped then next-session feed lifecycle with inactive then off-hours-active health.
8. Inject a missing terminal event; verify reconciliation and lifecycle-specific messaging rather than a false freshness message.

**Documentation**

- Update the authoritative value-date/live-trading policy.
- Update the VDS implementation plan and UI implementation details.
- Record command/event subjects, health endpoint samples and test evidence.

**Exit criteria**

- BDD, unit, integration, process, UI and verification suites pass.
- The final evidence explicitly distinguishes feed lifecycle health from live-trading freshness health.

## 5. Required verification matrix

| Time Eastern | Expected state | Feed lifecycle | Freshness monitoring |
|---|---|---|---|
| Sunday 17:59:59 | Closed | Stopped | Inactive |
| Sunday 18:00 | OffTrading/Monday value date | Starts or available | `OffHoursActive` from route activation |
| Monday 02:59:59 | OffTrading | Available | Active through 15 minutes; degraded afterward |
| Monday 03:00 | LiveTrading | Same feed epoch; new health epoch | Green baseline |
| Monday 03:05 | LiveTrading | Same feed epoch | Yellow after 5 minutes without a post-03:00 update |
| Monday 03:15 | LiveTrading | Same feed epoch | Red after 15 minutes without a post-03:00 update |
| Monday 15:59:59 | LiveTrading | Same epoch | Enforced |
| Monday 16:00 | OffTrading | Same epoch | Active/degraded from actual last-update age |
| Monday 17:00 | Closed | Stopped | Inactive |
| Monday 18:00 | OffTrading/Tuesday value date | New value-date epoch | `OffHoursActive` from route activation |
| Friday 17:00 | Weekend closed | Stopped | Inactive |

Live thresholds are relative to the later of the 03:00 live-health baseline or the last accepted update. Off-hours degradation uses the actual last accepted update or route activation time. The 03:00 transition resets health severity but does not erase off-hours telemetry.

## 6. Completion evidence

### 6.1 Gate ledger

| Gate | Status | Evidence |
|---|---|---|
| MDF-01 | Complete | Authoritative policy, VDS plan and UI implementation details now distinguish lifecycle, position permission and freshness. The production consumer inventory is recorded below. |
| MDF-02 | Complete | `FuturesMarketState` is serialized in `MarketSessionReadModel`; the ambiguous `IsLiveSessionOpen` transport field was retired; exact state, validation, revision and MessagePack round-trip tests pass. |
| MDF-03 | Complete | Shared Eastern policy resolves `Closed`, `OffTrading` and `LiveTrading`, including 03:00/16:00/17:00/18:00 and DST-aware transitions. |
| MDF-04 | Complete | Shared and UI state machines implement live five/fifteen-minute health, off-hours fifteen-minute degradation, independent ES/VX updates and a new green 03:00 epoch. |
| MDF-05 | Complete | API runtime health consumes the API-owned session authority and the shared accepted-cache-update policy; off-hours degradation retains feed ownership and live health resets at 03:00. |
| MDF-06 | Complete | The UI monitor follows authoritative session revisions, exposes off-hours labels/colors, corrects the five-minute text and suppresses non-live critical dialogs. |
| MDF-07 | Complete | Start/stop projections use durable replay, lifecycle APIs remain idempotent, duplicate terminals are fenced, and a missed terminal triggers typed backend runtime reconciliation with value-date fencing. |
| MDF-08 | Complete | Deterministic BDD and unit scenarios cover all approved states, boundaries, delayed timestamps, independent routes, recovery, duplicate terminals and reconciliation. |
| MDF-09 | Complete | Typed market-session transport, runtime-status query, start terminal and stop terminal integration tests pass through the application/actor boundary. |
| MDF-10 | Complete | UI rendering/system tests pass for the new indicator palette, documentation is current, and the regression suites below are green. |

### 6.2 Production consumer inventory

| Consumer | Classification | Authoritative input |
|---|---|---|
| `FuturesTradingValueDate` | value-date/feed lifecycle | Eastern 18:00 open and 17:00 close |
| `FuturesMarketSessionPolicy` and API session authority | market-state decision | `Closed`/`OffTrading`/`LiveTrading` plus next transition |
| `MarketDataFeedMonitoringWindow` | live freshness baseline | authoritative 03:00-16:00 state policy |
| `MarketDataFeedSessionHealthPolicy` | provider-neutral route freshness | state, activation, accepted hot-cache timestamp and route ownership |
| `MarketDataRuntimeHealthCheck` | API readiness/telemetry | API session authority plus DataBento route counters |
| `IFMAppViewModel` feed lifecycle | start/stop intent | `IsMarketOpen` (18:00-17:00), never position-entry state |
| `IFMAppViewModel` feed health | operator freshness | authoritative `MarketState` plus accepted ES/VX updates |
| `PositionEntryWindow`/Trade Orders | entry permission | `LiveTrading` only |
| Trade closing/reduction policy | exit permission | `LiveTrading` or `OffTrading`, never `Closed` |
| Runtime-status reconciliation | command lifecycle | provider-neutral typed NATS/HTTP query and matching value date |

### 6.3 Passing verification on 2026-08-31

- `TomasAI.IFM.Domain.MarketData.UnitTests`: 146 passed.
- `TomasAI.IFM.Application.MarketData.UnitTests`: 89 passed.
- `TomasAI.IFM.Domain.MarketData.Feed.UnitTests`: 502 passed.
- `TomasAI.IFM.UI.Net.Presentation.UnitTests`: 279 passed.
- `TomasAI.IFM.Application.Api.IntegrationTests`: 212 passed.
- Typed market-session actor/API integration: 1 passed.
- Typed runtime-status plus start/stop terminal actor integration: 3 passed.
- Dashboard indicator rendering/system verification: 7 passed.
- Total recorded passing tests: 1,239; failed: 0; skipped: 0.
- `git diff --check`: clean after documentation closure.

The controlled-time BDD coverage is the executable acceptance evidence for 03:00 and 16:00 journeys; it does not require waiting for wall-clock market boundaries or changing the running feed epoch.

## 7. Completion definition

The correction is complete only when feed lifecycle remains governed by market/value-date hours, off-hours degradation is visible after fifteen minutes without causing feed lifecycle action, live-trading five/fifteen-minute severity is accurately enforced and presented, every accepted feed command reaches a reconciled terminal state, and all gates MDF-01 through MDF-10 have passing recorded evidence.
