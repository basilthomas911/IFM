# Indicator seeding and five-minute health verification — 2026-11-09

The active Development indicator inventory qualifies all **76 checks**. This includes ES intraday indicators, Daily EMA20/50/200 baselines, Daily/Weekly/Monthly ITI state and actual Regime Discovery consumer captures, session VWAP, VX term structure, and front/back VX Bollinger history. RealizedVolatilityPercentile and PriorVolatilityComposite remain explicitly inactive, as authorized. Daily TDI is not an active producer; TDI is audited for its six supported intraday intervals.

## Corrections

- Cache ordering now uses the original market observation time before generation-local sequence numbers. A lower sequence from a newer producer generation cannot discard newer market data; delayed seed observations cannot overwrite newer live observations.
- Historical ES seeds and live producers use the same unadjusted front-continuation identity. A warm old contract-only projection cannot mask a cold live stream.
- Owning EMA and Bollinger seed commands repair incomplete retained history without regressing newer state. Bollinger uses its existing full-state event, verified through PostgreSQL reload and duplicate retry.
- Five-minute reconciliation and four-hour completed-interval watermarks prevent a one-time startup success from suppressing subsequent required updates. VX front/back histories are warmed independently with bounded concurrency.
- VX historical seeding uses original trades across both supported publishers. Separate publisher OHLCV rows cannot safely be treated as interchangeable complete bars. Missing source ranges are batched, with estimate-driven splitting and a 250,000-record bound. A final source marker is required; paid acquisition remains disabled. Databento identifies the exchange and off-market publisher distinction in its [CFE dataset documentation](https://databento.com/docs/datasets/xcbf-pitch).
- Readiness accepts the durable Bollinger projection's same-observation EMA20 centerline. It does not require a transient cache to receive a new four-hour event before recognizing retained warm state.

## Monitoring and display

The API audits immediately and every five minutes after qualification. Initialization retries every 30 seconds, with an independent five-second deadline supervisor and a bounded, non-overlapping audit. The startup qualification budget is five minutes. Failure discovered by a periodic audit is reported immediately by that audit.

Each result exposes contract, interval, indicator, warm/valid state, original source time, expected completed interval and failure reason. Four-hour freshness follows exchange-calendar completed bars rather than requiring a new four-hour observation every five minutes. Quiet VX inputs retain source-appropriate qualification. Health does not add a global trading workflow gate.

An unresolved incident is retained across restart. Error 26901 is published once per incident through the existing status/error path; recovery is published separately. Persistence errors remain visible and do not stop the monitor. Missing data remains failed until verified recovery; timestamps and market values are never fabricated.

Rust eGui projects the typed API readiness response into Operations / Processing stages and its existing detail tree. Independently healthy rows remain green. Structured HTTP 503 readiness responses remain inspectable. Stale audits become unavailable instead of retaining old green rows. The existing error dialog receives one notification per incident.

## Tests

| Suite | Passed | Skipped |
| --- | ---: | ---: |
| Analytics unit | 1,294 | 0 |
| Analytics BDD | 483 | 0 |
| PostgreSQL targeted snapshot integration | 11 | 0 |
| Analytics integration | 48 | 1 |
| LivePipeline isolated NATS integration | 340 | 0 |
| Native Host | 26 | 0 |
| MarketData unit | 850 | 5 |
| Trade unit | 1,716 | 0 |
| **.NET total** | **4,768** | **6** |

The skips retain existing environment-dependent coverage. New coverage includes cache generation ordering, same-timestamp deduplication, cold-prefix repair, full-state snapshot reload, repeated seeds, two-publisher raw trade history, bounded source splitting, virtual-time deadline/periodic checks, hung-reader non-overlap, persistent incidents and storage failures.

Rust verification passed 26 tests: notification delivery (11), HTTP health transport (7), indicator projection (2), Operations behavior (4), live read-only acceptance (1), and isolated closed-market compatibility (1). The live acceptance projected all 76 actual API observations into green Operations rows and verified all three Regime Discovery consumer horizons. The closed-market compatibility test was run explicitly with an isolated NATS endpoint because it is normally ignored without that fixture.

## Before/after observations

| Measurement | Before | After |
| --- | --- | --- |
| Cache ordering regression cases | Three reproduced failures | All pass |
| Full active inventory qualification | Not proven; short-frame warmth masked cold consumer/VX state | 76/76 qualified |
| Retained-state restart to full audit | No comparable full-inventory baseline | **2 min 1 sec** acceptance run; **1 min 21 sec** final package restart |
| Full readiness audit | No independent full-inventory audit | **88.9 ms** initial, **145.5 ms** and **99.5 ms** recurring; **106.7 ms** final package restart |
| VX multi-session source requests | Per-session requests | One bounded combined range when estimate permits; split before open when oversized |

These are measured functional and operational results, not a claimed controlled speedup between unlike sessions. The earlier cold missing-history recovery took **8 min 46 sec** and encountered source RPC timeouts. Its health correctly failed and retained the incident until real data recovered. Retained-state restart meets the five-minute target; arbitrary uncached provider recovery is not guaranteed to finish within five minutes and instead must report its error by that deadline.

## Live acceptance evidence

API PID 74280 started at 07:30:03 EST. All 76 observations qualified at 07:32:04. Consecutive scheduled audits at **07:37:09** and **07:42:14** again reported 76/76 with no failed entries. Actual Daily, Weekly and Monthly Regime Discovery snapshot requirements qualified; this does not imply that every later trading decision must result in a trade. No synthetic trading triggers were injected.

After installing the Operations matrix package, the final Development restart started API PID 48684 at **07:49:30**. All 76 checks qualified at **07:50:50**, approximately **1 minute 21 seconds** later. The audit took **106.7 ms**. Rust eGui PID 29780 runs the verified matching executable, with its full debugging PDB deployed. The typed live Operations acceptance test passed again against this final restart; this verifies the actual controller transport/projection, not a manual visual inspection of the desktop. The native Host reports Ready, and Server Manager owns the API, Host, scheduler and UI.

A genuine Daily workflow started at **07:50:40** passed Regime Discovery and reached Trade Selection, ending with `TS.ASSESSMENT.CONDITION`. Earlier initialization-period workflow failures remain historical records. New Weekly and Monthly workflows also passed Regime Discovery during final startup. The final health captures and owned process identities are under `final-package-restart` in the evidence directory.

Raw logs, TRX results, restart health samples, source tests and Rust output are retained under `E:\IFM\IndicatorHealth\20261109`. Earlier failing regression outputs are retained alongside their successful reruns. The Development API, native Rust Market Data Host, scheduler and full-debug Rust eGui package are restored. No commit or push is part of this request.

## Follow-up: Market Condition ATR freshness and inactive optional quality

The subsequent workflow investigation found that Market Condition treated a completed one-minute ATR as a tick with a 15-second lifetime. Its freshness now follows the original observation's completed-minute bucket, allowing the next minute's completion plus the existing configured publication grace. With a 15-second grace, a missing next bar is rejected at the boundary; original market timestamps are preserved. Quote, trade, feed and calendar deadlines are unchanged.

Missing realized-volatility percentile and unsupported Daily TDI are now informational `RD.DATA.OPTIONAL_INACTIVE` evidence. Absent inactive realized volatility has zero effective weight in confidence coverage. Available supplied evidence retains its existing calculation, while missing active intraday TDI still penalizes quality. No new producer or algorithm is enabled. Required-source checks and all fusion restrictions remain enforced, including the inclusive VX backwardation cutoff of 1.05.

Verification passed **1,728 Trade unit tests**, **55 Trade BDD tests**, and **8 PostgreSQL integration tests**, with no failures or skips. New cases cover ATR acceptance and expiry at the exact completed-bar/grace boundary, original timestamps after persistence, all three assessment horizons with atomic commit/rollback, inactive versus missing-active quality handling, and VX ratios immediately below, exactly at and above the cutoff.

The updated API was deployed and restarted at **08:40:54 EST**, PID 127388, with matching built/deployed library hashes. At **08:46:49**, all 76 indicator checks qualified, including actual Daily, Weekly and Monthly consumer snapshots. The 08:43 Daily workflow accepted a 24-second-old completed ATR with Normal stress and High regime quality; its only selector blocker was observed Poor liquidity. Daily workflows at approximately **08:50** passed every global selection check and correctly rejected the two directional candidates for the neutral, range-bound market (`TS.NO_COMPATIBLE_CANDIDATE`).

A fresh Weekly workflow at **08:50:21** passed Regime Discovery with **Acceptable** quality and confidence **0.707820**. Both inactive optional inputs were informational. Market Condition accepted its 22.5-second-old completed ATR with **Normal** stress and no input limitations. Its NoTrade result preserved the existing assessment-confidence and liquidity rules: **0.462400** confidence against a **0.5** minimum, and Poor liquidity. Fresh live Monthly verification still depends on a new genuine ITI trigger; its pre-deployment record is not counted as post-deployment acceptance. Monthly assessment behavior and PostgreSQL persistence are covered by the successful all-horizon tests.

The next scheduled audit at **08:51:59** again qualified **76/76**, taking **123.6 ms**. No API Error/Fatal records were present after deployment. Monitoring through **08:54 EST** found no new Monthly ITI/workflow; its latest workflow remained the pre-deployment 07:50 record. This is an outstanding live-verification dependency, not a claim of completed Monthly live acceptance. Development remains running.

Follow-up evidence is under `E:\IFM\IndicatorHealth\20261109\atr-optional-fix`, including TRX results, build/deployment logs, live health and decoded persisted decision evidence. Historical results are preserved; no duplicate or artificial ITI events are submitted for acceptance.

## Monthly live acceptance and remaining market-open overload — 09:50 EST

The Monthly trigger dependency is now resolved. Genuine workflow `01a1c0c9-a0a1-7c52-a65f-c2b4257f2beb`, started **09:50:01 EST**, passed Regime Discovery and reached Trade Selection. Its completed one-minute ATR source was **09:49:59.929997**, age **3.719 seconds** at assessment. Market Condition was Available, with **Normal** stress, **Healthy** input quality and no limitation reasons. Daily TDI and realized-volatility percentile were both `RD.DATA.OPTIONAL_INACTIVE` / Information. Fused regime quality remained Degraded for genuine **DirectionConflict** and **Transition** restrictions, with an Exhausting trend; it was not penalized for inactive optional inputs. The resulting `TS.ASSESSMENT.CONDITION` NoTrade preserves the configured restrictions.

An earlier fresh Monthly workflow at **09:39:58** exposed a separate real ingestion failure: its ATR was approximately **480 seconds** old. A Weekly workflow at **09:44:29** encountered the same stale source. The corrected freshness rule properly rejected those inputs; they must not be accepted by extending the deadline.

The overload investigation found:

- Market-open traffic began filling 2,048-message realtime mailboxes around **09:30**.
- At **09:32:33**, the shared Realtime actor-type admission budget rejected ES `FuturesTradeSessionBarSignal.Updated` messages with `ActorTypeMessageLimit`. The configured default actor-type limit is **8,192 messages**. Unrelated realtime consumers share this budget.
- These messages are classified Optional on Core NATS. The rejection path disposes them without redelivery. The logs explicitly record repeated ES bar-route drops through **09:43:31**.
- The accumulator suppresses incomplete intervals after a trade-ordinal gap rather than publishing false complete OHLCV bars. One-minute indicator rows therefore jump from **09:31:59** to **09:44:59**, with twelve missing completed minutes. The audit correctly raised error 26901 at **09:38:14** and returned to 76/76 at **09:45:09**.
- The short-frame historical warm-up cache currently skips frames already initialized that day; it does not itself repair this mid-session dropped-trade interval.

This remains an ingestion/admission reliability issue. Its correction requires isolated capacity and a loss/replay contract for critical bar inputs, together with bounded repair from authoritative history; merely increasing queue sizes, relaxing ATR freshness, clearing genuine restrictions or publishing partial bars would not establish correctness. This monitoring/investigation request made no further production code or configuration changes. At the **09:50:14** audit all 76 checks were warm again.

Evidence, including decoded persisted results for both Monthly workflows, overload messages and indicator audit history, is retained under `E:\IFM\IndicatorHealth\20261109\monthly-live-verification`. The latest Monthly result is in `latest-monthly.json`.

## Realtime admission protection and historical gap repair

The reported OneMinute application error refers to the **09:38:14 EST** incident, whose observations stopped at **09:31:59**. All eight failures shared one missing bar stream, rather than eight independent calculation failures. The audit recovered at 09:45 when complete bars resumed. Its retained error notification does not establish that a later audit remains unhealthy.

Critical realtime destinations now retain their owned input under admission pressure, with bounded protected dispatch stripes and **1,024 messages / 16 MiB reserved within the existing global and realtime hard limits**. Protection covers FuturesMarketPrice, FuturesTradeSessionBarSignal and the EMA, RSI, ADX, ATR, MACD and TDI downstream actors. Both pooled and legacy payload transports use the same policy. Admission retries re-resolve replacement actor generations, preserve ordering and release pending payloads on shutdown. Ordinary optional traffic retains its existing overload policy. No total admission limits were increased.

The minute initialization loop now requalifies previously seeded 15-second and one-minute frames against original committed observations. A stale frame triggers single-flight repair using 48 qualified original bars from the active native owner, then the existing owning seed commands. Failed acquisition stays retryable and must continue through authoritative native history; the same overloaded local tick persistence cannot qualify a repair. RSI13/TDI and durable Bollinger outputs must also catch up before repair completes. Fresh frames do not replay history. Freshness requirements, complete-bar checks and valid NoTrade restrictions remain unchanged.

Core NATS remains non-durable across process failure. Bounded waiting prevents actor-admission drops while the consumer is running; restart and source gaps still require original-history requalification. This does not make ordinary optional storage/telemetry traffic lossless.

### Verification

| Suite | Passed |
| --- | ---: |
| Shared unit | 371 |
| NATS unit | 138 |
| Isolated NATS overload integration | 7 |
| LivePipeline integration | 344 |
| Trade unit | 1,728 |
| Trade BDD | 55 |
| PostgreSQL indicator snapshot integration | 11 |
| Rust Host native indicator seed tests | 21 |
| **Total** | **2,675** |

All listed tests passed with no skips. Coverage includes reserved count/byte capacity, unchanged hard limits, shutdown ownership, replacement generations, exact FIFO delivery of **10,000 inputs per transport mode**, native repair failure/retry, original timestamps, committed dependency requalification and no repeated healthy-frame replay. Older startup fixtures now supply the complete committed read models that their accepted commands claim to produce.

The API was deployed at **10:28:08 EST**, PID 35432, with verified matching Core, actor runtime and NATS assembly hashes and all eight protected destination names in deployed configuration. Server Manager owns the API, native Rust Host, scheduler and Rust eGui. Startup reports eight activities satisfied, with no failed or skipped activities.

| Operational measurement | Before | After |
| --- | --- | --- |
| Protected ES bar input under shared admission pressure | Explicit Optional drops; twelve missing completed minutes | Both isolated 10,000-input tests delivered every input in order without duplicates |
| Cached short-frame initialization after a gap | Skipped for the rest of the date | Requalifies committed outputs and repairs from native history; recovery and retry verified in integration tests |
| Live full-inventory readiness | 09:38 incident; recovered at 09:45 | 76/76 at **10:30:22**, **2 min 14 sec** after process start |
| First recurring full audit | Prior incident described above | 76/76 at **10:35:27**, **122.1 ms** audit |
| Second recurring full audit | Prior incident described above | 76/76 at **10:40:32**, **137.5 ms** audit |
| Read-only native history request, actual deployed Rust Host | No comparable controlled baseline | **48 fifteen-second bars in 1,523 ms; 48 one-minute bars in 1,116 ms** |

The native requests returned original complete valid observations, with exact timeframe identities and current completed intervals; they did not submit trading commands. These timings include the live RPC and cache/replay path, not a controlled end-to-end repair benchmark or claimed speedup. PostgreSQL shows one advancing ATR observation for every completed minute from 10:28 through 10:39, with no missing minute in that interval. At the final audit, all eight OneMinute outputs have original source times of **10:39:59.964445 EST** (EMA/Bollinger retain sub-microsecond precision). No protected destination admission drops were logged after deployment. Six ordinary TickAggregationRealtime optional-route drop log records remain; they are not counted as protected delivery success or eliminated by this change.

Fresh Daily and Weekly workflows after restart pass Regime Discovery and reach Trade Selection. Their persisted assessments have Healthy input quality and no missing-input limitations; ATR sources advance normally. The Daily 10:32:42 result is `TS.NO_COMPATIBLE_CANDIDATE`, and Weekly 10:31:13 is `TS.ASSESSMENT.CONDITION`, preserving actual trading restrictions. The first Daily trigger during startup failed before structure restoration. Separate Trade Selection deadline failures remain: four `TS.TIME.EXPIRED` and one `PipelineTimedOut` were recorded by 10:39 alongside six completed NoTrade workflows. This indicator/admission fix does not claim to resolve every downstream workflow deadline. The latest Monthly at this point remains 10:20:38, before this restart, so it is not counted as a fresh post-deployment Monthly acceptance.

Evidence is retained under `E:\IFM\IndicatorHealth\20261109\realtime-admission-repair`, including TRX results, deployment hashes, actual native history timing, health captures and decoded workflow results. The active Rust native seed implementation already supports both short frames; the legacy C# Host was left unchanged.

## Trade Selection processing deadline correction

The saved requests established a second, independent failure: required reference quotes have a **two-second** market validity. That permission expiry was also truncating the selector's loading/calculation/recording deadline, after Market Condition and workflow persistence had already consumed part of it. One recorded selector request had only **217 ms** left; several had approximately **1.27 seconds**. A later NoTrade refusal could consequently be replaced with a timeout despite granting no trading permission.

Selection processing now uses the existing minimum of workflow expiry, binding validity and `RequestedAtUtc + MaximumExecutionMilliseconds`. The configured execution limit is unchanged. The frozen evaluation must still precede assessment expiry. Result validity still includes the **original assessment expiry**, unchanged; future clock skew does not extend it. At workflow acceptance a verified NoTrade refusal can be recorded after permission expiry with its original reason and timestamps. A Selected result still requires unexpired evidence before composition or reservation, including at the exact expiry boundary. Workflow expiry and execution timeout checks remain enforced. Historical requests retain their saved identities and deadlines; no historical failed workflow is silently rewritten.

The Trade Selection specification now explicitly distinguishes bounded processing from permission to trade. Regression cases cover short-lived evidence, frozen evaluation at the expiry boundary, late NoTrade completion with unchanged reason, rejection of expired Selected results without a reservation, and actual execution deadline expiry. No liquidity, confidence, regime, session, event or candidate restrictions were weakened.

The API restarted at **10:48:13 EST**, PID 133448. Deployed Trade and contracts DLL hashes match their built outputs. All 76 indicators qualified at **10:49:01**. Genuine Daily and Weekly workflows completed Regime Discovery, Market Condition and Trade Selection after restart:

| Horizon / start EST | Workflow | Final decision |
| --- | --- | --- |
| Daily 10:49:34 | `01a1c100-235d-710b-bb9f-4c8f0c3731c6` | NoTrade / `TS.EVIDENCE.UNKNOWN`; recorded Unknown stress due to the volatility baseline, with fresh ATR |
| Weekly 10:51:20 | `01a1c101-c19f-708d-ba65-130ce8dd5ada` | NoTrade / `TS.ASSESSMENT.LIQUIDITY` |
| Daily 10:51:20 | `01a1c101-c1bc-7e9a-81da-4a3764cfab76` | NoTrade / `TS.ASSESSMENT.LIQUIDITY` |
| Daily 10:52:03 | `01a1c102-6b45-7b1c-a54d-e288bc6f3cf6` | NoTrade / `TS.NO_COMPATIBLE_CANDIDATE` |

These four requests completed approximately **0.60–0.83 seconds** after selector request time, with no Trade Selection timeout. Their original evidence expiry remains in the persisted result. This is a live operational observation, not a controlled performance benchmark. Monthly live acceptance is **explicitly deferred at the user's request**; the pre-restart 10:20 Monthly workflow is not counted as post-restart verification. Monthly behavior remains covered by the all-horizon BDD and integration scenarios.

Unit verification passed **1,731 tests**, and BDD verification passed **58 tests**, with no skips. Raw live decision payloads, request/result timing, build hashes and TRX outputs are under `E:\IFM\TradeSelection\20261109`. An initial broad integration filter also selected unrelated Risk and Order Composition scenarios through a shared partial test class; that run was interrupted and is not reported as passed. The final focused run covers the changed selector boundaries and durable recovery paths.

The final focused **8 NATS/PostgreSQL integration tests passed**, with no skips, in **27 seconds**. They include Daily/Weekly/Monthly NoTrade persistence after quote expiry, reload from the completed snapshot, ordinary duplicate replay, append-failure recovery, actual calculation deadline expiry and durable reservation handoff. **Total: 1,797 tests passed.** Through **10:56 EST**, all five post-restart workflows (four Daily, one Weekly) completed with NoTrade and **zero Trade Selection timeouts**. Development remains running; Monthly live verification remains deferred as requested.
