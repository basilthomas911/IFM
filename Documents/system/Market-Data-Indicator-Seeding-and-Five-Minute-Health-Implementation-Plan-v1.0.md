# Market data indicator seeding and five-minute health implementation plan

Date: 2026-11-09

Status: proposed implementation; this document does not change runtime behavior.

## Required outcome

Every enabled market-data indicator must have qualified historical initialization, restored current state, an attached live producer where applicable, and usable projected observations for its consumers. Complete this within five minutes of startup, session activation, contract rollover, or activation of a new indicator configuration. If qualification cannot complete, publish a visible error by that deadline. Never report success based solely on command acceptance or a cached initialization flag.

Run a complete indicator audit immediately and every five minutes thereafter. Separate monitoring from initialization so a blocked replay cannot delay the audit or its deadline. Check deadline expiration on a short, bounded timer; do not wait for the next five-minute audit to raise an overdue startup error. An ordinary audit failure is reported in that audit, without granting another five-minute grace period. Recovery retries continue independently with bounded backoff.

This is a readiness and error-reporting guarantee, not a promise to manufacture unavailable historical data. Preserve source qualification, actor ownership, sequence validation and event-sourcing conventions.

## Evidence and current gaps

- `FuturesIntradaySignalActivationProfile` activates 15-second, one-minute, five-minute, 15-minute, hourly and four-hour RSI/ATR/ADX/MACD identities. RSI13 supplies TDI; RSI14 is a separate consumer requirement.
- `StoredSessionIndicatorWarmup.WarmAsync` restores 15-minute, hourly, Daily and four-hour frames plus its two short-frame paths. It is not an exhaustive registry of all indicators and intervals.
- The session startup service retries warm-up approximately once per minute but gates its work on feed/supervisor readiness. Initialization and warm-up hints are retained by date/frame. These hints do not independently prove continuing health.
- The recent short-frame repair verified warm, valid 15-second/one-minute observations. It did not qualify every Regime Discovery requirement.
- Daily workflow `01a1c00b-dc44-70e1-9ebe-a944f642b084`, started at 06:22:45 EST on November 9, failed initialization with 23102. Its recorded diagnostics listed stale hourly/four-hour ADX, DI, MACD and ATR inputs and missing hourly realized-volatility percentile/prior volatility composite. PostgreSQL nevertheless held hourly/four-hour observations through approximately 06:00 EST. Trace producer, projection and consumer cache separately before assigning the root cause.
- The snapshot provider evaluates age using source event time and configured maximum age. Audit whether those settings fit completed-bar publication and exchange-session boundaries; do not simply refresh timestamps or relax them globally.
- Source searches found volatility metrics referenced by requirements/calculations/tests, but did not establish a production publisher. Verify ownership, actual runtime requirements and historical definitions before claiming these dependencies are available or mandatory.

## 1. Establish an exhaustive indicator and timeframe registry

Create one typed registry used by activation, seeding, audits and readiness reporting. Each entry identifies market series/contract role, configuration and version, interval, owner, historical source, minimum history, snapshot/replay requirements, live source, projection, cache key, consumers and freshness policy. Enumerate active ES, VX front/back and other configured consumers rather than hard-coding ES alone. Include active option-pricing dependencies, without allocating feeds for unused contracts.

Cover the full enum: Daily, Weekly, Monthly, Quarterly, TenSeconds, FifteenSeconds, OneMinute, FiveMinutes, TenMinutes, FifteenMinutes, ThirtyMinutes, OneHour, WeekMonthBridge and FourHours. `None` is invalid. A workflow horizon is not automatically its indicator interval: retain Daily EMA20/50/200 trend baselines for Daily, Weekly and Monthly workflows.

For every indicator/interval pair, record enabled, supported but inactive, or not applicable with a concrete reason. Support seeding/auditing all valid enabled pairs, not just the current six intraday activations. A configured pair with no producer/seeder is an explicit configuration error. Do not silently exclude enabled pairs or create meaningless combinations for session-specific indicators.

| Indicator/dependency family | Qualification to implement |
| --- | --- |
| Session bars and sampling | Correct exchange boundaries, complete historical windows, qualified gaps, ordered/idempotent replay and independent interval accumulators |
| EMA20/50/200 and slopes | Retained accumulator history and Daily baselines; at least the calculator's full history requirement, including slopes; distinguish trend baseline from intraday Bollinger centerline |
| RSI13/14 and slopes | Both identities, Wilder initialization, correct periods, full state needed for restart |
| ADX14, +DI14, -DI14 | True-range/directional-movement and smoothing warm-up; warm and valid values for all required outputs |
| ATR14 and baseline ratio | ATR and its independently qualified baseline; finite positive denominator; Daily versus session aggregate ownership |
| MACD 9/12/26 | Fast/slow EMA and signal smoothing ready; correct histogram and retained state |
| Bollinger bands | Historical price window, centerline, variance, width baseline ratio and position; durable EMA-to-Bollinger completion |
| TDI | RSI13 history, all configured smoothing/band dependencies, current schema and source timestamp |
| VWAP | Actual trade price/volume, session resets and configured anchor; OHLC alone cannot reproduce exact VWAP |
| ITI signals | Existing aggregate/group extreme and reversal state, threshold/configuration and historical continuity; restart must not emit duplicate trading triggers |
| Structure and volatility inputs | Rolling highs/lows, ranges, breakout distance, EMA interaction; define production ownership/history for realized-volatility percentile and prior volatility composite |
| VX term structure | Correct front/back mapping, synchronized qualified inputs, ratio and front level; no spot-VIX substitution |
| Option volatility/pricing dependencies | Existing producer-defined underlying, curve and volatility requirements; quiet option feeds do not require a new option tick to prove subscription readiness |

Deliver a machine-readable inventory and an audit test that fails when a new enabled family, configuration or timeframe lacks a registry entry.

## 2. Correct historical seeding and restart restoration

1. Calculate minimum history from each actual algorithm and baseline configuration. Replace assumptions that one fixed 48-bar window proves readiness for every family. EMA200, volatility ranking and baseline ratios may require longer history than short oscillators.
2. Restore the latest verified full-state snapshot and replay its tail through owning actors. Prove the snapshot contains recursive accumulators, history windows, baseline state, watermark/sequence and configuration identity. Do not substitute a creation-only event that cannot rebuild current state.
3. Read existing qualified PostgreSQL history first; use the Rust Market Data Host for missing history. Share replay across dependent indicators/intervals and reuse the durable four-hour seed cache with correct contract, date, configuration and source-version invalidation.
4. Handle overnight sessions, weekends, DST, rollover and truly empty intervals using the exchange calendar and existing source semantics. Distinguish a quiet interval from data loss. Do not fabricate trades or pad prices to satisfy warm-up.
5. Attach every restored live processor without resetting its accumulator. Start independent families/intervals with bounded concurrency and individual deadlines. Keep ingestion and other healthy frames running if one dependency fails.
6. Await durable command completion, projections and cache publication, then verify actual outputs. Restart progress markers are optimization hints, never the health authority.
7. Reconcile producer identities with contract and continuous-series consumers. Resolve aliases explicitly, preserve source provenance and prevent older snapshot restoration overwriting newer live observations.

## 3. Fix the remaining Regime Discovery input path

- Trace the hourly/four-hour observations from actor commit through projection and `RegimeDiscoverySignalCacheAdapter` to the exact `RegimeDiscoveryMarketSignalSnapshotProvider` key used by a fresh workflow.
- Compare source timestamp, current bar boundaries, configuration ID, schema and configured maximum ages. Correct lost publication, obsolete cache entries or inappropriate interval policies at their source.
- User decision: keep realized-volatility percentile and prior volatility composite explicitly inactive. Audit all existing active producers; do not add new financial algorithms for these optional inputs.
- Distinguish optional evidence from required evidence in diagnostics; report optional missing inputs without misrepresenting them as the sole reason capture failed.
- Verify actual effective parameter sets for Daily, Weekly and Monthly. Preserve the user's Daily EMA baseline decision and timeframe independence. Aggregate health must not globally block a workflow whose own required inputs qualify.

## 4. Implement five-minute auditing and bounded recovery

Add a dedicated API-hosted indicator health coordinator with injected `TimeProvider`. Audit immediately at activation and every five minutes. Use independent lightweight deadline supervision, bounded per-entry reads and no overlapping audits. A slow or unavailable feed/provider must produce a failed/blocked entry, not suppress the monitor.

For each enabled entry check:

- Identity, operational ValueDate/session and configuration match.
- Required history/accumulator coverage, warm flag, validity and numeric/domain checks.
- Latest expected completed interval and source time; publication/projection lag and cache revision.
- Live attachment/subscription and progress evidence appropriate to the source.
- Read-model and consumer snapshot agreement; process liveness alone is insufficient.

Use states `Initializing`, `Warm`, `Recovering`, `Failed` and `ScheduledStopped`. Record first failure, last success, last audit, source timestamp, expected interval, attempts, deadline, reason and blocked dependencies. Warm-to-failed transitions are observable even if initialization was previously marked complete. ScheduledStopped applies only when session authority/configuration permits; it must not hide incomplete required historical initialization.

Freshness is interval- and session-aware. A completed four-hour observation does not need to change every five minutes. Check it against the latest expected completed four-hour interval plus a bounded publication tolerance. A source can be warm yet stale, so report both dimensions. Retain configured trading/consumer age constraints and reconcile inappropriate settings explicitly rather than silently extending them.

Repair only failed dependencies using idempotent owning commands and bounded retries. Do not restart the entire feed, replay healthy intervals, reset financial state, or publish artificial fresh timestamps. After any successful repair recheck the projection and consumer snapshot immediately. Retain one unresolved incident across repeated audits and restarts; a process restart must not silently reset an outstanding failure's age.

## 5. Expose errors and health in Rust eGui

Publish typed health results through the existing operational query/event and application-error paths. Show per-family/interval state in Operations with the precise reason and last qualified source time. Display red for failed required indicators and their dependent workflow readiness; show healthy unrelated frames independently.

At the five-minute startup/recovery deadline, or immediately when a periodic audit detects failure, raise one application error per incident with affected contract, indicator, interval and reason. Example: `Indicator initialization failed: ES20261218 OneHour ATR14 — baseline ratio unavailable after 5 minutes.` Group simultaneous entries into one readable dialog with details. Do not repeatedly open the same dialog every five minutes. Keep the error inline until recovered; publish recovery and allow a new failure episode to notify again.

## 6. Required verification

### Unit tests

- Registry completeness for every enabled family/configuration/interval; invalid or unsupported activation fails explicitly.
- Exact history requirements and one-below/at-boundary cases for every calculator and its baseline.
- Snapshot round-trip and tail replay reproduce uninterrupted state, including wider-history requests.
- Completed-bar freshness for all supported intervals, overnight/DST/weekend/rollover boundaries, future clock skew, quiet periods and session closure.
- Contract/continuous alias identity, cache monotonicity, schema/configuration change and read-model/cache disagreement.
- Virtual-time tests: no startup alarm before five minutes, alarm at deadline, immediate and five-minute periodic audits, no overlapping audits, per-entry timeout, no skipped audits when ingestion is unavailable.
- Failure deduplication, unresolved restart persistence, recovery transition, new incident after recovery and UI message mapping.

### BDD tests

- Fresh startup qualifies all enabled inputs within five minutes using existing history.
- Warm restart preserves indicator state and resumes live updates without duplicate ITI/workflow triggers.
- An hourly/four-hour failure leaves independent short-frame and workflow readiness running.
- Missing historical data yields a visible, precise error by the deadline; later source recovery warms the indicator and clears its incident.
- Quiet off-hours options, market close/reopen, contract rollover and parameter changes produce correct health, not false readiness or endless recovery.
- A stale cache with current PostgreSQL data is detected and repaired before a fresh workflow captures its inputs.

### PostgreSQL/NATS/native integration tests

- Seed each enabled family/interval through its owning actor; verify durable full-state snapshots, restart loading, tail replay, projected rows and consumer-visible keys/values.
- Exercise cold start, warm start and a gap after shutdown using the Rust historical source and four-hour cache. Prove shared replay does not multiply provider calls.
- Verify delayed/lost projection delivery, duplicate delivery, cache reconstruction and reconciliation after restart.
- Test the current hourly/four-hour 23102 regression with real effective parameter requirements and source timestamps.
- Use real volatility producer outputs, not fabricated market values, to qualify required volatility inputs.
- Verify health querying, incident persistence and NATS error/recovery delivery; Rust tests cover typed DTOs, one-dialog behavior and Operations rows.

Run the affected Analytics, MarketData, Trade, LivePipeline and Storage suites plus Rust eGui tests when its view/protocol changes. Preserve test isolation and never seed synthetic verification records into active Development financial data.

## 7. Timings and live acceptance

Capture before/after measurements against the same verified data cut and configuration: acquisition, per-family/interval initialization, projection/cache lag, full audit duration, restart-to-all-warm, provider replay count, PostgreSQL rows/queries and allocations where material. Distinguish cold startup, retained snapshot startup and missing-history recovery. Never present unlike live sessions as a controlled speedup.

After deployment, perform a real Development restart and retain the complete matrix. Within five minutes, every enabled entry must either qualify or produce its named error; any unresolved required failure means the implementation is not qualified. Observe at least two consecutive five-minute audits with current values and no false alarms. Exercise one isolated failure/deadline/recovery scenario without disrupting live trading data. Verify fresh Daily, Weekly and Monthly consumer snapshots and genuine workflows as new ITI triggers arrive; historical failures remain historical records. Do not inject trading signals to force live acceptance.

Final report includes actual effective configurations, all indicator/interval outcomes, source/expected timestamps, tests and before/after timings, any remaining data/definition blockers, and confirmation that API, Rust Market Data Host, eGui and scheduler were restored. No paid data acquisition is authorized by this plan.

## Implementation sequence and completion gates

| Step | Deliverable | Completion gate |
| --- | --- | --- |
| 1 | Full registry, current-state matrix and baseline timings | No enabled dependency omitted; current 23102 failures reproduced |
| 2 | Correct per-family seeding and snapshot restoration | All registered seeders produce qualified state in isolated tests |
| 3 | Projection/cache/volatility and freshness fixes | Real consumer snapshots qualify without weakening required-data checks |
| 4 | Independent five-minute monitor and recovery | Deadline, periodic audit, failure persistence and deduplication tests pass |
| 5 | Operations/error UI | One clear incident message and persistent status, recovery visible |
| 6 | Full suites, live restart and two audit cycles | All enabled indicators qualify within five minutes and remain qualified; failures accurately reported |

Implementation and live qualification are complete for the current active producer inventory. All 76 checks qualified after restart and through two consecutive five-minute audits. Optional realized-volatility percentile and prior volatility composite remain inactive by user decision; Daily TDI is not a supported active producer. See [verification report](Market-Data-Indicator-Seeding-and-Health-Verification-2026-11-09.md) for fixes, test results, source acquisition limits, measured timings and cold-recovery limitations. The five-minute requirement is a qualification-or-error deadline, not a guarantee that an unavailable historical provider will recover within that time.
