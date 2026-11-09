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
