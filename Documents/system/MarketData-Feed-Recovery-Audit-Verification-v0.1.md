# Market Data Feed Recovery Audit Verification

## Scope and status

### October 29 evening loaded-trade live acceptance

Trade `101.701.1901.1501` was opened in eGui with Live Feed enabled for API
ValueDate 2026-10-30. All four saved recovery contracts were verified in
`trade.live_feed_recovery`; four current-generation quotes and 22/22 ready scopes
were observed. The user confirmed advancing plans, green Options Health and the
"Live monitoring is verified" success message after controlled recovery.

The Options-only recovery request at 19:39:01 EDT replaced its worker and
generation. The test observed complete resource proof, all four post-injection
quotes and 22/22 ready scopes within 10.015 seconds. Futures dataset identities
and API/Host/eGui/scheduler process IDs stayed unchanged. Native audit episode
`f9ace818-5d8c-6d6a-fe9e-64779e6bb3dd` persisted Started, ConnectionRestored,
Completed and MonitoringRestored; eGui durably logged receipt of the final
notification. Its monitoring-restored timestamp was 19:39:04.183 EDT.

The subsequent 19:39:29–19:54:29 EDT soak ran for 900 seconds with 46 samples.
Every sample showed healthy Options and futures, 22/22 scopes ready and positive
native resource proof. No process restarted, no observer query failed, no new
recovery episode occurred and eGui logged no new diagnostic errors during the
window. PostgreSQL contained 1,667 new plans for the trade/date, with actual
trade-plan sequences 145 through 1,811. The passive realtime sample field named
`sequence` is Position.SequenceId; it is not this per-date trade-plan sequence.

Evidence: `.artifacts/live-trade-verification-20261029-evening/verification-summary.json`,
`controlled-recovery.jsonl`, `recovery-audit.json`, `soak-samples.jsonl`,
`soak-complete.json`, and `trade-progress.jsonl`. Windows computer-use initialization
failed with os error 3, so visible UI acceptance relies on the user's explicit
confirmation, supported by backend/notification evidence. No production source
changes or application restarts were made during this live verification. This
passes the current loaded-trade/reset/15-minute baseline; it is not an assertion
about untested trading actions or every future recovery failure mode.

### October 29 evening market-open investigation

The red Options indicator was confirmed against native backend status, not just
the UI. Recovery operation `76835d1e-1e14-f82b-a7f1-e9439156d55a`
started at 19:08:33 EDT after a raw-reference broker connection failed with
Windows socket error 10022. Its `ResetCompleted` record at 19:10:22 confirms
retirement and replacement of the dataset resources; it does not prove monitoring
readiness. Replacement setup waited for the quiet `ES20270319` underlying quote.
The terminal `Failed` record at 19:12:22 and native `StoppedError` status confirm
that this episode did not recover. Both futures datasets remained healthy.

The corrective package replays the temporary underlying MBP-1 book from the
API-selected session opening and awaits its replay-completed record. Provider
timestamps, decimal arithmetic and existing quote validation remain enforced.
The SDK fixtures cover replay followed only by heartbeats. Four background SDK
tests and the actual managed/native DLL lifecycle qualification passed. The
corrected DLL (`d29c75a2a0b33f86d04c0f6e311716468b6271b85949642866a31d74484e7a08`)
was deployed at 19:27 EDT. Automatic Quartz occurrence
`28c2da3c-778e-dcd7-09a5-0743f49c8886` then ran from 19:30:03.298 to
19:30:16.681 EDT, returning `BusinessCompleted`, exit 0 and ValueDate
2026-10-30. Native owner `4aa1c42c-c398-9c15-f6ee-5f274f4dbd6d` was joined
and replaced by `bbda4414-4598-427a-e845-fec479165df4`; all three initial
dataset identities changed. API 53180, Host 37440, eGui 72636 and scheduler
72688 stayed running throughout this opening occurrence. Temporary qualification
schedule `364d30f0-d85d-4b40-8127-a894e8b5771f` was disabled and removed
through the command actor afterward; ordinary opening/closing schedules remain.

The subsequent native background policy/coverage refresh completed, and operation
`82df4f33-2630-d9c5-47d5-b460d6d30190` recorded `MonitoringRestored` at
19:30:53.962 EDT. A 19:31:49 backend sample verified all three datasets healthy,
18 requested background scopes, 18 ready scopes and positive resource proof.
The 19:32 ES outlook query reported Green, no missing inputs, available ITI and
daily analytics, volume 17,156 and a current provider timestamp. These are backend
checks; no new loaded-trade or visual UI acceptance is claimed by this occurrence.
Evidence is under `.artifacts/market-open-clean-start-20261029/`, including
`replay-before-open.json`, `replay-automatic-status.json`,
`replay-post-open-stable.json`, `replay-disable.json`, and `replay-remove.json`.

A separate manual scheduler Run Now qualification exposed an existing durable
consumer cursor ahead of the current EventStream. That manual request was rejected
through the command actor to prevent later execution; no stream or consumer was
deleted. The automatic Quartz opening path above was verified independently.

The earlier 19:08 market-open occurrence completed successfully in 9.617 seconds,
but its completion gate proves futures readiness only. That receipt must not be
presented as proof that the independent Options background initialization passed.

This report accompanies `MarketData-Feed-Recovery-Audit-Implementation-Plan-v0.1.md`.
Implementation records native recovery decisions without moving dataset recovery into C#.
Implementation and isolated audit verification completed on 2026-10-29. The additional public-recovery observer mismatch is corrected and its explicit qualifications pass. Final full native regression passed (432 tests, zero failures), and the authorized separate acceptance session was launched. The live baseline/trade/visible-UI gate is not yet claimed complete.

## Delivered path

1. Native supervisor captures immutable incident facts with stable OperationId, per-fact RecordId, original timestamps, API ValueDate and actual owner identities.
2. An independent bounded file outbox sends existing NATS Function requests. A matching persisted completion is required before deletion.
3. `MarketDataFeedRecoveryRecordFunctionActor` forwards to `MarketDataFeedRecoveryCommandActor`. Aggregate persistence precedes function completion persistence and acknowledgement.
4. `MarketDataFeedRecoveryStartedEvent` supplies snapshot loading. Ordered tails restore later transitions and duplicate identities.
5. The durable projector writes `market_data_service.marketdata_recovery_log`, with idempotency and conflict checks. Replay repairs unavailable projection storage.
6. Commit/projection logs reach the existing server file and OTLP sinks. UI notifications remain independent of audit success.

## Automated evidence

| Gate | Result/evidence |
| --- | --- |
| Full market-data feed unit suite | 614 passed, zero failures; `.artifacts/recovery-audit-all-unit.log` |
| Full feed BDD suite | 333 passed, zero failures; `.artifacts/recovery-audit-all-bdd.log` |
| Real-service integration | Eight passed against isolated PostgreSQL and NATS, zero failures/skips; `.artifacts/recovery-audit/integration.log` and `recovery-audit.trx` |
| Standalone UI notification tests | Ten passed; `.artifacts/recovery-audit-ui.log` |
| Hybrid app notification tests | 32 passed; `.artifacts/recovery-audit-hybrid-ui.log` |
| API core build | Zero warnings/errors; `.artifacts/recovery-audit-api-build.log` |
| Full native regression | 432 passed, zero failures across 104 test executables; 18 explicitly ignored scenarios in the regular command; `.artifacts/recovery-audit-native-regression.log` |
| Strict native Clippy | Passed with warnings denied across all targets; `.artifacts/recovery-audit-clippy.log` |
| FFI lifetime/facade verification | Passed: facade routes, 1,107 exact C# corpus matches, 1,000 create/cancel/dispose cycles, and socket cleanup with a fully starved CLR ThreadPool; `.artifacts/recovery-audit-facade.log`, `recovery-audit-ffi-runtime.log`, `recovery-audit-ffi-starvation.log` |
| Isolated native Host package | Built and SHA-256 verified; `.artifacts/rust-marketdata-recovery-audit-package/Debug/native-package.json` |
| Isolated API package | Published successfully with isolated build output; `.artifacts/recovery-audit-api-package-isolated.log` |
| Development eGui executable | Built successfully; `.artifacts/recovery-audit-ui-package/TomasAI.IFM.UI.Rust.eGui.exe` (existing unused-variant warning remains) |

Integration uses actual actors, state repositories and source-event persistence with real PostgreSQL. Its explicit actor-dispatch fixture substitutes surrounding context/routing; it does not claim an entire production API boot. A separate case uses the production durable projector and real JetStream process/replay queues/checkpoints. The native delivery case uses the actual Rust outbox and NATS request/reply.

The generic native command intentionally leaves long production-duration and managed-tool fixtures ignored. Its audit-delivery scenario was separately exercised three times by the real-service integration harness. Two other production-duration scenarios were explicitly invoked, with one pass and one failure documented below. The regular regression count must not be read as acceptance of those ignored scenarios. Machine-readable results and the final native package hash are in `.artifacts/recovery-audit/verification-summary.json`.

### BDD and failure cases

- Soft recovery preserves identity and reports transport readiness separately from complete monitoring.
- Deadline escalation records actual reset entry, owner retirement/replacement and final outcome.
- Missing predecessors, changed immutable identity, duplicate sequence conflicts and digest tampering are rejected.
- Replaying 607 duplicate deliveries produces one distinct source fact and one row.
- Snapshot plus tail restores incident state and duplicate indexes.
- A crash between aggregate commit and function completion reconciles without another source fact.
- A deliberately lost first reply waits the real 120-second recording deadline and then retries successfully.
- Expired recording attempts do not poison later attempts.
- Source events remain committed while the projection database is unreachable; replay restores the table afterward.
- Cold additive schema creation works when deployment-wide schema initialization has not run.
- The production projector survives JetStream replay restart and updates the durable checkpoint.
- File logs and an actual HTTP/protobuf OTLP receiver contain the same IDs and transition properties. Preserved evidence: `.artifacts/recovery-audit/recovery-server.log` and `recovery-otel-received.pb`.
- The actual native outbox delivers soft completion, complete reset-success and terminal reset-failure timelines through real NATS and the recording actors. SQL preserves every payload exactly. Copied timeline evidence: `.artifacts/recovery-audit/recovery-native-timeline-*.json`.
- UI duplicate reset messages produce one notification; a distinct later failure with the same OperationId remains visible.
- Native tests cover bounded admission/reserved terminal capacity, disk budget, partial and corrupt files, integrity conflicts, malformed/compressed acknowledgements, actual dataset task retirement while the audit broker stalls, and shutdown flushing without broker acknowledgement.

## Compatibility and fixes discovered during verification

- The optional backend health field is additive. Unit tests exercise old nine-field status decoding and current ten-field roundtrip. The original C# backend identity baseline remains unchanged; only the exact documented extension is allowed by the native preservation guard.
- Existing feed BDD setup lacked authoritative IValueDate/PrepareSession mocks from a prior refactor. The fixture was corrected; its behavioral assertions were preserved.
- Native disk exhaustion and a Windows executable lock during overlapping test invocations were build orchestration failures. Generated package artifacts were cleaned with Cargo and tests rerun serially. They are not accepted as passing results.
- The spool reader/writer now serializes its own file operations, preventing a scanner from treating an active temporary write as corrupt.
- Projector durability is required for audit facts even when a deployment makes unrelated projectors transient through an allowlist.
- Immediate UI reset/failure notices reuse the actual fact RecordId and original occurrence time. Repeated soft replacements update the recorded physical owner correctly.
- A fast factory task can produce readiness before synchronous construction returns its identity. Audit capture now defers restored/completed facts until that identity and ResetCompleted are recorded. Actual feed readiness continues immediately. A deterministic native test and managed full-timeline replay test cover this ordering.
- A factory returning at the terminal boundary cannot append ResetCompleted after a failed/interrupted incident. The terminal-boundary test passes on the final native build.
- Feed transport fixtures deliberately leave the new audit function unanswered instead of misclassifying it as a diagnostic message and closing their broker. The separate real-service suite supplies durable recording acknowledgements. Backend-status ABI fixtures now verify the additive tenth field and its absent/null state.
- The original preservation baseline predates the earlier authorized historical-availability fix. `HistoricalAvailabilityCompatibility.json` records only its exact reviewed fragments (availability query and published-session boundary). The guard reverses those fragments before verifying the original hashes; it does not accept arbitrary source drift.
- Eight existing native transport tests inspected financial views immediately after the public recovery observer. Following the transport/readiness correction, these tests now explicitly await financial readiness before preserving their pricing, volume, ownership and publication assertions. The isolated 59-test transport suite passes; the intermediate failed run is retained as `.artifacts/recovery-audit-native-pre-readiness-tests.log`.
- Earlier manually invoked fixtures left 49 synthetic frozen-clock records in the default development outbox. They were identified by their exact fixture timestamps and ValueDate and moved, without deletion, to `.artifacts/recovery-audit/default-fixture-spool-backup`. Final regressions and live acceptance use explicit separate outboxes. The default contained no remaining committed records after this cleanup.

## Remaining live acceptance gate

The separate updated Host/API/eGui acceptance session was started after all automated gates passed, as authorized. Its packages are `.artifacts/recovery-audit-api-package`, `.artifacts/rust-marketdata-recovery-audit-package/Debug` and `.artifacts/recovery-audit-ui-package`; normal VS Code launch configuration is unchanged. It uses `.artifacts/recovery-audit/live-outbox` as a persistent acceptance outbox.
Before injecting a fault into updated binaries, establish the required API/UI baseline: futures bars, ITI/signals, analytics and system-console updates. Then verify a recoverable native fault and the real two-minute escalation while an unexpired trade has Live Feed enabled. Confirm the correlated timeline in source events, SQL, server logs and the visible UI.

Do not interpret healthy current transport or the earlier incident `74e1fd86-b113-441f-8773-3f058a06becb` as retrospective acceptance. No live trading processes were stopped for isolated automated tests. No older IFM processes were running when the authorized acceptance session was launched.

### Updated session evidence

The updated Manager started at 04:01:36 Eastern on 2026-10-29. Native Host PID 70168, API PID 66440 and eGui PID 27644 run the separate packages. API launch readiness is Healthy with all eight startup activities satisfied. The authoritative ValueDate is 2026-10-29. The recovery-readiness probe confirms saved trade `101.701.1901.1501` was restored. The native status query confirms both futures datasets and Options are Healthy, native resource proofs are complete and no replacement is pending.

A real startup broker failure (Windows socket error 10022) produced operation `0d4274c1-3538-e67c-ca1f-2cd84e0e384f`. Its persisted timeline is:

| Sequence | Fact | Elapsed | Proof |
| --- | --- | --- | --- |
| 1 | Started | 0 ms | Actual original broker error retained |
| 2 | ConnectionRestored | 175 ms | Different physical owner, old resources retired, provider activity verified |
| 3 | Completed | 175 ms | TransportOnly; monitoring remains explicitly false |
| 4 | MonitoringRestored | 11,576 ms | MonitoringReady=true |

The operational query reads these facts from the SQL projection. Actual production actor commit and projection log entries contain the same IDs. Evidence is `.artifacts/recovery-audit/live-startup-recovery-timeline.json`, `live-native-status.log` and the current Manager/server log. The audit health query reports zero pending/undurable records and zero capture, write, rejection or delivery failures. Fresh ITI commands, Market Outlook processing/publication, EOD analytics and trade-plan processing advance in the live server logs.

A later incident `ec296386-fab0-9a1a-3f7e-30109168f5e4` also recorded Started, ConnectionRestored and Completed in 181 ms. Its scope remains TransportOnly; no later monitoring proof is inferred. The final SQL snapshot is `.artifacts/recovery-audit/live-all-records.json`.

This establishes actual startup recovery recording; it does not substitute for a visible-screen check or a newly injected terminal-dialog test. Some older operations-health stage counters remain Inactive/Red while the native authority reports Healthy, so the aggregate dashboard alone is not accepted as native lifecycle proof. No new error records were found for the current eGui PID at the check.

Windows UI inspection is blocked before app selection: initialization fails with `failed to write kernel assets: The system cannot find the path specified (os error 3)`, also after resetting and retrying. The user has been asked to confirm the visible baseline and open the trade. Further injected live faults are held until that baseline is confirmed, as requested. No terminal live-UI acceptance is claimed from the automated notification tests alone.

## Additional production-duration qualification and correction

The explicit `native_options_reference_stall_fails_once_at_original_120_seconds_and_joins` check passed in **120.03 seconds**.

The separately invoked, normally ignored `native_public_owner_deadline_returns_terminal_result_and_publishes_notice_without_competing_timer` check **failed**: its public recovery observer did not return within the test's 124-second limit. Evidence is `.artifacts/recovery-audit-public-deadline.log`. This failure is not included in the ordinary native regression's passing count.

The new retained journal provides concrete evidence for that check, copied to `.artifacts/recovery-audit/public-deadline-diagnostic-timeline.json`:

- Operation `268238b3-a2cb-702f-f306-92cdeb1e56f4`: requested recovery restored a different physical owner and completed at 21 ms with `CompletionScope=TransportOnly`, `MonitoringReady=false`.
- Operation `1025f3b1-1665-0d43-a676-6ccb3ccc099d`: a later publication deadline produced Started, ResetStarted, ResetCompleted, ConnectionRestored and Completed records. The replacement transport completed at 7 ms; monitoring remained unverified.
- The fixture selected for the purportedly unrecoverable case is `AckOnly`, which emits heartbeat records every five seconds and supplies no quotes. That fixture is not proof of a broken provider connection under the current heartbeat policy.
- The public `recovery_result` path awaits `wait_ready`, which requires full readiness, while the native incident can correctly complete at transport scope. A caller can therefore remain waiting after the transport incident has completed. A status watch can also coalesce the intermediate failed state while the supervisor enters its reset phase.

This exposed a qualification/observer-contract mismatch outside the recording path. It was corrected in the Rust public observer: `Recovered` and `CleanupConfirmed` report recovered transport after the native supervisor has retired the previous owner; `LiveFeedEnabled` remains false until full financial readiness is proven. The current C# backend is unchanged. No monitoring proof is manufactured and no production timeout was extended.

The added nonignored quiet-heartbeat test passed in **0.08 seconds**, verifies a changed physical generation and unchanged sibling dataset, and explicitly asserts that financial readiness remains false. The fatal fixture now stalls its actual SDK metadata response. Its explicit production-duration test passed in **240.03 seconds**: the original failure notification arrives at 120 seconds, the supervisor executes its existing independent final-reset phase, and a failed result and joined resource tree are verified when that phase also fails. The test's outer 244-second guard observes both existing phases; it does not add a new production recovery timer. The broker deliberately does not acknowledge audit delivery, proving that record retention does not block dataset retirement.

The final packaged native DLL also passed the 1,107-case exact C# corpus comparison, 1,000 lifecycle cycles and CLR ThreadPool-starvation socket cleanup check after this correction. The final full native regression passed: **432 tests across 104 suites, zero failures**. Strict Clippy also passes with warnings denied.

## Limits of the evidence

### Outcome-dialog follow-up verification

The subsequent visible error was correlated to operation `5733cfda-737f-50ba-43a1-61518a192ad7`. Its audit shows Started and ResetStarted at zero elapsed, old resources retired at 12 ms, and transport completion at 205 ms. Its supplied incident deadline was already expired. This is not accepted as a real two-minute wait. The Options loop previously selected the minimum deadline across healthy pending components as well as actual failures. It now selects budgets only from active failures, retaining any active setup deadline and never extending a genuine failed component's original deadline. Isolated tests verify that a stale healthy output budget cannot immediately expire a new policy failure.

The standalone follow-up adds explicit native completion notifications, queued behind any unacknowledged error, with centered OK and equal-sized red/error, green/success and yellow/warning vector icons. A transport-only success explicitly says live monitoring is not yet verified. A later monitoring proof gets a distinct correlated confirmation. Healthy startup and quiet activity do not fabricate an incident.

Focused automated verification after these changes: **132 native tests passed** (library, complete transport suite and recovery authority suite); **14 UI tests passed**, including all three icon bounds, horizontal button centering, malformed/oversized outcomes, duplicate delivery and a real isolated NATS push while backend queries have no responders. A final native socket test confirms the dedicated notification subject and immutable retry identity. Strict native Clippy passed with warnings denied. The packaged DLL passed 1,107 exact C# comparisons, 1,000 lifecycle cycles and socket cleanup under CLR ThreadPool starvation. The static-CRT eGui package passed the PE/import audit. These follow-up counts are distinct from the earlier 432-test full regression above.

The updated packages were activated after these checks, using a clean managed-session shutdown. Current Host PID 55268, API PID 69212 and eGui PID 15272 use `.artifacts/recovery-outcome-native-package/Debug`, the unchanged audit API package, and `.artifacts/recovery-outcome-ui-final-package`. API startup satisfies all eight activities. Live operation `a8f0dc12-ca8a-3443-0cfd-c840a8087418` recorded transport completion at 176 ms; eGui logged its matching confirmation. On 2026-10-29 the user explicitly confirmed: **“Yes, green check and centered OK are visible.”** This accepts the visible success-dialog layout and live receipt. Red/warning geometry and error-then-success queue behavior are covered by the automated tests, not a claimed separate human observation. Trade-monitoring restoration is not inferred from this transport-only result.

Evidence: `.artifacts/recovery-outcome-regression.log`, `recovery-outcome-ui-complete.log`, `recovery-outcome-native-wire-final.log`, `recovery-outcome-clippy.log`, `recovery-outcome-ffi-*.log`, `recovery-outcome-live-timeline.json` and the current UI durable error/outcome log. The old zero-elapsed incident remains an accurately recorded historical failure; it was not rewritten as a successful two-minute wait.

- Synced committed outbox records survive qualified process restart; power-cut/storage-controller behavior was not tested.
- Audit capacity is finite. Explicit capture/write/delivery health failures identify loss or unavailable persistence; they never masquerade as successful recovery.
- Quiet provider heartbeats verify connectivity. They do not establish usable pricing, persistence or trade-monitoring readiness.
- UI Rust tests verify event handling and deduplication. Visible live-dialog acceptance remains a separate human/UI check.
- Each deployment scope/outbox directory has one native Host owner. Concurrent independent Hosts must use separate outbox locations.

## Live trade-plan investigation: 2026-10-29, 05:00–05:08 Eastern

The user's report of a recovered feed without new plans exposed a distinction between historical recovery notifications and current dataset readiness. Operation `8b31222c-f439-33d5-65bf-eb3f6ad5e830` reported monitoring restored at 04:55:17. Options then reset under operation `8875bcd4-e9d7-6d93-12fa-48eee69c5479` at 04:58:16. At 05:00:57, the replacement had a healthy connection but `ReplacementPending=true`, incomplete native resource proofs and no ready monitor scopes. The earlier success notification did not describe that replacement.

The later operation recorded transport completion at 170 ms, but monitoring restoration at 221,770 ms. This exceeds two minutes for full monitoring restoration. Transport completion and subsequent financial readiness must not be interpreted as the same acceptance gate. Started and ResetStarted also had zero elapsed in this operation; the earlier active-failure deadline correction has therefore not eliminated every immediate-expiry path. These remain outstanding findings, not accepted recovery behavior.

Read-only queries using the standalone UI's actual backend services verified that processing subsequently resumed for trade `101.701.1901.1501`, ValueDate `2026-10-29`. At 05:03:48–05:03:53, persisted plan revisions advanced from 48269 to 48277 and realtime snapshot revisions from 4276 to 4296, with current-generation quotes on all four legs. At 05:04:10, the native workload reported replacement complete, resource proofs complete and all four monitor scopes ready. A second independent query at 05:07:24–05:07:28 verified persisted plans 48725 to 48731 and realtime snapshots 5508 to 5523, with last market observations advancing from 05:07:23.494 to 05:07:27.816.

The notification receiver deduplicates record identities and queues historical outcomes, but does not supersede an older queued success when a newer dataset incident begins. Thus a success dialog is historical evidence, not continuous proof of the latest generation. This is a code-confirmed presentation risk; no screenshot or exact dialog-display-time proof is available on this workstation. Backend advancement is verified; visible chart repaint is not independently verified.

Evidence: `.artifacts/live-trade-plan-diagnostics.log`, `.artifacts/live-trade-plan-diagnostics-recheck.log`, `.artifacts/live-native-observations.log` and `.artifacts/recovery-outcome-current-status.log`. The explicitly ignored `live_trade_diagnostics` test makes read-only current-plan, persisted history, realtime snapshot and saved-leg queries; both explicit runs passed. The qualification CLI's `--inspect` option only reads native status and publication snapshots. No fault was injected, no feed intent changed and no running process restarted for this investigation.

## Additional live acceptance: 2026-10-29, 05:50 Eastern onward

The user explicitly deferred recovery-duration acceptance and authorized the remaining live tests. The running Development native Host/API/eGui were retained. No order command was submitted. Production deadlines and recovery behavior were not changed.

- **Per-trade stop/start:** the standalone UI's existing per-trade client disabled live feed for `101.701.1901.1501`, verified saved intent was false, then re-enabled it successfully. The ignored live test always attempts to restore the original enabled intent before asserting stop evidence. Full readiness subsequently returned before the controlled fault test proceeded. The command acknowledgment alone was not accepted as data recovery.
- **Two successive controlled recoveries:** the qualification CLI's explicit `--live-trade development` mode waited for complete native readiness, requested recovery through the existing opt-in Development fault API, then required a new Options worker identity and generation, all scope proofs, unchanged Host/PID and unchanged healthy futures ownership. It repeated this once after successful restoration. This exercises the real native supervisor and resource replacement; it does not emulate a blocked provider socket or force deadline expiry.
- **First cycle:** native audit operation `b65d97b9-f7c2-8ff2-77cc-e5db54e89e46` restored transport at 184 ms and monitoring at 290,198 ms. The outer observer reported 290,557 ms. Generation changed from `f061d190-4906-8e53-a975-985ec1438d69` to `8e87593f-3144-43ce-72e4-3c3b00c86cb2`.
- **Second cycle:** native audit operation `fade35aa-6c85-05b0-dc0d-260bdd51ea88` restored transport at 195 ms and monitoring at 33,815 ms. The outer observer reported 34,513 ms. Generation changed to `329bd50a-86fb-619e-7d9a-f6868e89f139`. Both incident records attest old resource retirement; monitoring records have no missing proofs. Native audit incident IDs differ from the caller's Development request IDs; generation identity and timestamps correlate these records.
- **Isolation:** ES publication sequence advanced 3240 to 4132 during the first recovery, then to 5441 after both cycles. VX sequences advanced 2793/2797 to 3561/3570 during recovery, then 4191/4193 afterward. Futures generations remained `6ddbc244-3b29-a378-d73a-83b6f2a0e359` and `76a08cf9-fcf4-4ab0-07b4-e43ccee4cf55`; the qualification observer checked healthy unchanged futures ownership throughout both cycles. API logs also show continued ITI command processing during recovery.
- **Actual post-recovery processing:** at 06:00:12–06:00:17 Eastern, persisted plans advanced 52036 to 52048, realtime snapshots 2028 to 2056, and all four leg risk snapshots referenced the final Options generation. Fresh market observation timestamps advanced too. Recovery audit pending/undurable records and capture/write/delivery failures were zero.
- **Visible acceptance:** the user confirmed all three were visible: advancing plans, green Options Health and the verified-monitoring success dialog. Durable UI logs independently record receipt of both transport-only and monitoring-ready outcomes for both incidents.

Evidence: `.artifacts/live-stop-start-acceptance.log`, `.artifacts/live-native-repeated-recovery.log`, `.artifacts/live-final-recovery-progress.log`, `.artifacts/live-isolation-{before,during,after}.json.log` and the per-operation `live-recovery-*.json` files. The read-only final progress test passed. The qualification project built with zero warnings/errors before live injection. An initial safety refusal while monitoring was unready injected no fault; the runner was adjusted to wait for readiness rather than bypass that prerequisite.

A passive stability observation began at 06:01:07 Eastern and was stopped at the user's request for a break. It is not counted as a completed fifteen-minute test. Forced-deadline acceptance remains deferred, and market-close/EOD acceptance requires the actual scheduled close; neither is inferred from these successful controlled recoveries.

### Restarted fifteen-minute stability test: 09:27:52–09:42:54 Eastern

The user explicitly resumed testing after the break. A fresh passive observation ran for fifteen minutes, with 60 readiness samples and no injected fault, changed feed intent, restarted process or production timeout change. The initial generation was `0f04dcbb-8b4c-df38-514a-1b732412e035`. Two samples were unready during one natural recovery; all other samples had complete native resource proofs and 22 ready scopes.

At 09:36:04.369, native incident `c878a44b-6fbf-02fd-9dcf-11263917146b` began with `Conflicting option trade economic content`. This is the native detached evidence outbox rejecting a different economic digest for an already-retained source identity, not evidence that Databento heartbeats failed. The source identity/digest conflict's underlying cause has not yet been established. The old resource tree was retired, transport restored at 184 ms and full monitoring restored at 21,634 ms. The final generation was `d39842b5-1a66-62d6-39fc-09d420280c38`, with all 22 scopes ready. A direct read after recovery verified plans 77181 to 77188 and current market observations.

The additional push observer ran for the last 742.62 seconds of the window, received 2,273 realtime trade publications and passed. Its final publication at 09:42:53 carried plan 77532, snapshot 71771 and a market observation at 09:42:53.305. Separate persisted-history queries verified plans 76440/76447 near the beginning and 77560/77564 immediately after the window. Thus health was not used as a substitute for actual trade-plan progress. The observer's first compile attempt failed on diagnostic timestamp serialization; that test-only issue was corrected before subscription and had no effect on the running application.

ES/VX retained their original healthy owner generations. ES publication sequence advanced 112497 to 194449; VX sequences advanced 54981/54982 to 85766/85767. Native audit pending/undurable records and write/delivery/capture failures remained zero at the final read. The dataset test satisfies the user's successful-recovery criterion, but it was not an error-free run.

**Separate UI failure remains:** the eGui process logged persistent `SlowConsumer(6)` events and warning publications. NATS's read-only connection monitor maps SID 6 to `ConsoleDelivery.SingleUI.messages`, whereas SID 7 is the independently advancing realtime trade stream. During the window, retained UI logs contain 16,317 connection events, 2,286 queued warnings and 28 recovery-outcome receipts, including repeated historical outcome identities. This is not accepted as a clean overall UI stability result. The notification worker's shared bounded seen-history and its reconnect/setup behavior on connection-watch changes require investigation; no specific corrective behavior is claimed tested. Visual confirmation of the screen after this natural recovery was requested and remains pending at report time.

Evidence: `.artifacts/stability-restart-summary.json`, `live-passive-stability-20261029-0927.jsonl`, `stability-realtime-push.log`, `stability-restart-plan-{start,end}.log`, `stability-recovery-plan-check.log`, `stability-restart-native-{start,mid,end}.log`, `stability-natural-recovery.json`, `stability-ui-nats-connection.json` and `stability-ui-events-summary.json`.

## Live-only console follow-up implementation

The user replaced the console replay requirement with transient live display and requested retention of exactly two viewport pages. The current warning must replace the preceding warning. The earlier live run remains a failed clean-UI stability gate; its evidence is preserved above.

The backend console producer now publishes only the existing Core NATS broadcast. The standalone subscribes directly to `Notify.StatusConsoleEvent.Logged.>` and has no console JetStream, catch-up, display receipts, HTTP setup or acknowledgement path. The obsolete console queue/history implementation and connect/ack endpoints were removed. Existing console-only broker configurations are not automatically deleted by this code; the new producer no longer writes to the old queue subject. Market-data recovery audit persistence is separate from this display-only console change.

The virtual list measures `ceil(body_height / (18 + row_spacing))` visible rows, including a partially visible last row, and retains twice that number. Two entries are used until the first layout measurement. Resize updates the shared capacity; shrinking evicts oldest entries and growing cannot restore removed messages. The current warning has one replaceable retained entry. Warning churn does not share or evict recovery-record deduplication identities.

The notification intake publishes bounded latest snapshots through a watch channel. A separately owned relay may wait for GUI availability without preventing console reception. Pending sidebar snapshots coalesce rather than growing an unbounded list. Only an actual disconnected-to-connected transition repeats reconnect refresh work. Bad console payloads log a diagnostic and replace the current warning; they do not terminate intake or open a fatal recovery dialog.

Verification includes viewport geometry/resize, two-page eviction and retained-ID deduplication, 1,050 warning replacements with recovery identity retention, existing notification/dialog/health tests, and real Core NATS delivery with JetStream disabled. The broker test sends 3,000 rows plus a duplicate while GUI output is stalled, verifies bounded latest delivery without slow-consumer warnings or pre-subscription replay, and then verifies continued delivery after malformed input. The backend producer integration test verifies that a message sent before subscription is not replayed and that a live message has no acknowledgement reply subject. Test results are captured in `.artifacts/console-live-final-acceptance.log` and `.artifacts/console-producer-verified.log`; the API executable build result is `.artifacts/console-live-api-final-build.log`.

These are isolated automated tests. The revised API and standalone UI have not yet completed a new live stability run together. The user's prior visual confirmation was that plans advanced while a warning/error remained visible, not a clean acceptance of this new implementation.

## Repeating automated verification

Run `scripts/Test-MarketDataRecoveryAudit.ps1` for the isolated PostgreSQL/NATS/OTLP tests. It creates disposable, uniquely named test containers and refuses a database name outside its dedicated test prefix. The normal trading database and running API are not used.

The other gate commands are:

```powershell
dotnet test TomasAI.IFM.Domain.MarketData.Feed.UnitTests
dotnet test TomasAI.IFM.Domain.MarketData.Feed.BDDTests
$env:IFM_MARKETDATA_RECOVERY_OUTBOX = Join-Path $pwd '.artifacts/recovery-audit/manual-native-test-outbox'
cargo test --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --offline --tests -- --test-threads=1
cargo clippy --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --offline --all-targets -- -D warnings
cargo test --manifest-path TomasAI.IFM.UI.Rust.eGui/Cargo.toml --offline --test feed_notifications
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests --filter FullyQualifiedName~IFMAppViewModelTests
```

Serialize builds that share .NET intermediate outputs. Do not rebuild a Windows native test executable while an integration fixture is executing it. A recording attempt intentionally times out in the lost-reply test; the test succeeds only when its retry is durably reconciled.

## October 29 afternoon: stored trade and Options-only recovery

The user confirmed prompt display of stored trade data before live restoration,
then green Options Health and generating plans. Foreground stored load measured
136.6 ms; six-hour chart hydration completed separately in 586.9 ms. The hybrid
UI is unchanged. Automated tests cover retaining stored data during pending or
failed restoration, closing cancellation and no redundant saved-enabled Enable.

Controlled operation `0c473879-34cb-4f33-94e6-450cc19f36c7` was armed for five
minutes and injected at 15:15:27 Eastern. Native audit operation
`04ea4bb0-5df9-25bf-a3c4-35f15388352a` recorded resource retirement and
MonitoringRestored after 3,517 ms. By ten seconds independent observation verified
all four leg quotes were observed after injection in the replacement generation,
with 22/22 scopes ready. Host, API and both futures identities remained unchanged.
The durable UI receipt at 19:15:30.944505Z contains "Dataset recovery completed.
Live monitoring is verified." The user saw and dismissed a success dialog but
did not independently confirm its exact wording.

The next fifteen minutes (19:15:30.887664Z to 19:30:30.887664Z) contain 59 health
samples, all 22/22 ready and one Options generation. Trade snapshot revision
advanced 5733 to 12067; plan position revision advanced 2481467 to 2483715.
The passive observer recorded no disconnect/error. ES volume also advanced.
This passes the controlled-recovery and continued-processing gate.

Two natural recoveries started just afterward at 19:30:42Z and 19:31:26Z with
"Conflicting option trade economic content". Monitoring was restored in 6,830 ms
and 4,333 ms respectively; plans continued. The triggering economic-content
conflict remains a separate investigation. Its integrity check was not weakened.

Evidence: `.artifacts/dataset-only-acceptance-20261029-1423/` contains controlled
and passive journals, native snapshots and `post-reset-15-minute-verification.json`.
The embedded UTC timestamps, rather than the artifact folder label, identify
the actual test window.

Additional qualification found multi-block C# audit failure receipts were masked
by a compression decoder restriction. The corrected decoder retains its 8 KiB
limit and preserves underlying server errors. Twenty-three audit tests passed;
captured actual success and multi-block failure replies also passed. The latter
identifies missing predecessors in five frozen October 27 synthetic fixture
records. Those files were preserved with SHA-256 verification in
`.artifacts/qualification-outbox-archive-20261029-1524/`; no predecessors were
fabricated and persisted event history was unchanged. Pending audit records
reached zero. Two rejected files and historical cumulative counters remain.
Qualification executables now isolate their temporary outboxes; the managed
bridge rejects accidental use of the live root before opening native resources.

Repeated saved-load qualification failed against the old DLL and passed against
the corrected DLL. Final package SHA-256:
`f7909370a0822f9f6d448bc29dbd0ea36daf06f6f3c6583bf3c39399c3db4a86`.
It was deployed after the observation window. Its live repeated-load check is
recorded separately when complete; the fifteen-minute result above belongs to
the preceding deployed DLL.

### Final package live repeat-load verification

The final package started Native Host PID 40816, API PID 57364 and eGui PID
36052. At 19:35:03Z, a real Host `LoadTradeFeedRecoveryAsync` for saved trade
101.701.1901.1501 returned true in 78 ms. Options worker identity
83bd0ed2-c5d6-2504-439c-4d7caa4931a5, generation
5f3790ad-c8bf-5f69-761f-e9d7b3710b1a, Host PID and manifest revision 5 were
unchanged before/after. All 22 scopes and resource proofs remained ready; all
four captured leg quotes belonged to that generation with current observations.
Audit health after this startup showed zero pending records, retries, delivery
failures, write failures and capture failures, with the two previously rejected
files still counted. Evidence: `final-package-live-repeat-load.json` in the
controlled acceptance artifact directory. Final visible trade confirmation in
the restarted UI was requested separately.

### Reopened-trade follow-up failed: coverage replacement

The user's subsequent visible check reported deadline dialogs and a stopped
Options feed. This supersedes any inference that the brief final-package check
established ongoing stability. The earlier controlled reset result remains valid
for its recorded window; final reopened-trade acceptance has not passed.

Native operation `91f10cda-15fb-e3e2-a92f-9afcd3e3d026` recorded Started and
ResetStarted at 19:37:00Z, physical ResetCompleted at 19:37:00.837Z, then Failed
at 19:39:00Z. Physical reset completion did not mean monitoring restoration.
The replacement repeatedly waited for ES20270319 underlying quotes. At 19:41:49Z
the live endpoint reported Recovering with zero of four scopes ready.

The background refresh path discarded a newly resolved membership when reporting
a policy/coverage change. Some other change branches reported replacement before
resolving it at all. A healthy-provider quote wait could also consume a maintenance
budget that was then reused as an already-expired replacement deadline. Thus the
replacement could immediately hit its reset boundary and repeat cold acquisition.

The correction prepares replacement metadata while the current feed remains
owned, passes that copied recipe across retirement and recreates physical source,
reference and pricing resources from it. Planned maintenance starts its recovery
budget when replacement is ready; an explicit pending business handoff retains
its original deadline. Unknown acquisition errors continue to enter recovery.
No prices, current-generation outputs or readiness proofs are retained/fabricated.
Qualification includes a changed-policy SDK session-count assertion, unchanged
policy stability, removed-policy retirement, and virtual-time deadline cases.
Results and live acceptance are recorded after those gates complete.

The changed-policy SDK regression passed in 90.35 seconds: unchanged policy
kept two initial SDK sessions (bootstrap plus final), changed policy added only
one final session, and removing the last policy added none. Both existing idle
and cached-background recovery integration tests passed. Two virtual-time budget
tests and actual C#-to-DLL four-leg/cancellation/repeated-load qualification passed.
Strict Clippy passed. Evidence: `.artifacts/refresh-recipe-*.log`.

Corrected native package SHA-256:
`e9b1d1ea816a3fc3f726b8e75fab3bc1a320d7065957eaceaa18b8529d2af0a1`.
The corrected session started at 19:48:49Z (Host 73484, API 66408, eGui 27556).
Two subsequent real coverage-change operations exercised the corrected path:
`89219f2c-654b-c1b4-9510-6488fcb0ccef` verified monitoring after 21,303 ms;
`511a84b0-66fb-600c-50f2-d54bf7c94e25` after 8,124 ms. Both durably recorded
old-resource retirement, completion and MonitoringRestored. The matching final
notifications reached eGui. These are monitoring timings; connection restoration
alone was earlier and is not reported as complete monitoring recovery.
The longer follow-up observation and visible check are tracked separately.

The corrected live follow-up ran 310.61 seconds after first 22/22 readiness
(19:49:30.812971Z to 19:54:41.431099Z, 32 sampled observations). Three actual
coverage replacements recorded MonitoringRestored in 21,303 ms, 8,124 ms and
4,939 ms. There were zero Failed/ResetStarted records, zero probe errors and
zero backend-failure receipts in eGui PID 27556. Host/API/UI process identities
remained unchanged across that observation. Final state was Healthy, 22/22 scopes
ready, complete resource proofs and four replacement-generation leg observations.
Trade plans continued to persist. Audit pending/retry/delivery/write/capture
failure counts were zero; the two historical rejected files remained counted.
Evidence and exact generation/quote timestamps are in
`.artifacts/refresh-recipe-live-20261029-1549/verification.json` and its samples.
This passes the focused corrected-path backend follow-up; it is not a new
fifteen-minute soak or a substitute for the user's still-pending visible check.

The user subsequently confirmed: "Trade open; stored data appear promptly;
Live Feed enabled." At 20:34:42Z (16:34 Eastern), independent backend checks
reported Options Healthy, 22/22 scopes ready, no recovery deadline and persisted
plans advancing 110582 to 110583 within the preceding second. The recovery log
contained no Failed or ResetStarted records since the corrected session began.
This confirms the visible stored-load/enabled-feed check and current backend
progress; it does not imply a new screenshot or user confirmation of every
displayed live value.

## October 29 pre-close soak and scheduled EOD acceptance

The latest native package completed a fresh 918.11-second observation from
20:41:36.136599Z to 20:56:54.251639Z (16:41:36 to 16:56:54 Eastern).
All 45 samples had healthy datasets and 22/22 Options scopes ready, with four
current-generation leg observations. Plans advanced monotonically from 111509
to 113535. Processes and four saved live-feed contracts remained unchanged.
There were no recovery records, probe failures or durable UI error events.
The maximum observed age of an individual leg quote was 39.004 seconds; this
is quote age during quiet trading, not a measured delivery-latency guarantee.
Market Outlook price/volume advanced, daily analytics were Available, and ITI
changed from Stale to Available following a new intrinsic-time signal.

The authoritative actor-owned close schedule was enabled, desired revision 2,
with expression `0 1 17 ? * MON-FRI`, America/New_York. The matching Quartz
trigger was installed for 21:01Z. Static disabled seed settings and empty legacy
PostgreSQL schedule-definition rows were not treated as the active authority;
no schedule configuration was changed for this test.

The scheduled run `3479d0ba-cf15-7c2f-b651-4f80e8b71ec2` started at
21:01:00.471612Z and completed at 21:01:06.0877089Z with exit 0 and
BusinessCompleted. The API returned HTTP 200 and close run
`25d290eb-d8f6-4efc-9199-e678ab4c0fb8`, finalizing two positions for October 29.
The durable completed-EOD marker advanced to October 29; all three datasets
became Idle and today's recovery-intent contract count fell from four to zero.

Loaded trade 101.701.1901.1501 persisted EndOfDay position sequence 2492128,
ValueDate and LatestSealedValueDate 2026-10-29, with the 21:00Z session boundary.
Both the position-change event and realtime UI snapshot carried that same sealed
snapshot at 21:01:05.172Z/21:01:05.178Z. Its history row retains the first daily
mark's stable UTC key (October 28 23:04Z, inside the October 29 trading session);
the stored payload explicitly identifies October 29 and the final EOD sequence.
The unexpired position remains open, as expected for daily sealing.

At 21:03:08Z feeds remained Idle, plans remained stopped at 113912, and there
were no unexpected process restarts, recovery records or durable UI errors.
The API session reported no active date and IsMarketOpen/IsLiveTrading false;
the next operational date is October 30 with the next transition at 22:00Z.
Audit pending/retry/write/delivery/capture failures remained zero; two historical
rejected fixture files remained counted. Visible EOD/live-toggle confirmation
was requested from the user and remains separate from backend/event verification.

Evidence: `.artifacts/market-close-soak-20261029/` contains raw samples,
`soak-verification.json`, `close-verification.json`, canonical scheduler receipts,
stored EOD payloads, UI-bound position-event observations and the API session.
These observations pass the latest-package soak and scheduled-close backend gates.
They do not repeat the still-separate forced-reset acceptance on this package.

The user subsequently confirmed in the still-open trade view: "EOD is visible;
Live Feed is disabled." No close/reopen was required. This completes the visible
EOD/live-toggle check and accepts the latest-package fifteen-minute soak and
scheduled market-close gates together with the backend evidence above.
