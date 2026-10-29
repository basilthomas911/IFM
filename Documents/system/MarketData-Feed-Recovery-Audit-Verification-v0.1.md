# Market Data Feed Recovery Audit Verification

## Scope and status

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
