# Option Feed Bounded Recovery Implementation Plan

## Objective and release scope

Restore option monitoring within one two-minute recovery deadline or deliver an explicit retained UI error. Discard obsolete disposable monitoring work; preserve financial commands, orders and fills. Separate Futures workers remain running.

Automatic API restart remains disabled in this release. A later stage permits one API-only restart with a second two-minute deadline. That escalation is not implemented or enabled here.

## Live notification acceptance

On October 22, 2026, the running API published a real two-minute option-pipeline failure for operation bb1d3e6a-371e-44da-b5df-51f2d8bf87cc. The user confirmed seeing the failure dialog in Rust UI. This satisfies the live notification acceptance gate. The older UI may caption it Economic Calendar Error; updated presentation uses an application/option-pipeline error caption.

This proves notification delivery, not deployment or live execution of the new emergency reset. Matching Host, worker and API binaries must be deployed together. The running development stack still uses the earlier notification implementation.

## Implemented recovery workflow

1. **Keep latest complete snapshots.** IronCondorTradePositionRealtime replaces preceding pending StrategyPositionChanged and IronCondorMonitoringInputsChanged notifications per position and verb. Evicted messages are disposed exactly once and accounting released. An active message remains owned by its handler. Reply requests and financial traffic are not coalesced. Raw quote and control queues retain their existing policies.
2. **Detect independently.** A hosted observer checks option realtime mailbox state and fresh-quote/plan progress outside affected actor queues. One coordinator owns each session-scoped episode, operation ID and deadline. Ten-second progress detection is a trigger, not another recovery timeout.
3. **Reset API monitoring generations without draining.** Replace IronCondorTradePositionRealtime and FuturesOptionRealtime contexts, discard pending work, cancel/detach old handlers and fence late effects. Arbitrary in-process .NET handlers cannot be physically terminated in isolation.
4. **Replace the Options process immediately.** Pass the absolute deadline through IMarketDataApi. Host terminates the old Options worker without waiting for its hung control operation, starts a new process/generation and restores desired manifests and leases. Futures processes remain running. Host automatic Options recovery uses immediate replacement too.
5. **Verify usable progress.** Readiness alone is insufficient. Require fresh quote/risk observations for all four legs, a newer position, a complete new plan and successful downstream publication covering its route generation and sequence. Reject unchanged process identity after requested replacement. Join an already-running Host recovery instead of competing with it.
6. **Enforce one deadline.** Reset, replacement, restoration and verification share two minutes total, even if an adapter ignores cancellation. Late completion cannot clear failure. Host receives the absolute deadline. Short bounded delivery attempts do not extend recovery.
7. **Notify independently.** Retain failure and retry acknowledged JetStream delivery with a stable operation ID. The pipeline publisher runs separately from Host status polling, so a hung query cannot suppress the error. Delivery requires broker/UI connectivity; retained failures retry when connectivity returns. UI deduplicates the episode.
8. **Respect session close.** Session close/replacement cancels the episode and clears retained session failure. No automatic API restart is enabled.

## Verification

Automated coverage includes latest replacement under an exhausted shared budget, concurrent writers, reply protection, exactly-once disposal, fast actor replacement and old-generation fencing. Coordinator tests cover success, noncooperative work, late completion, session cancellation, single flight and the exact default boundary (recovering at 119 seconds; terminal at 120 seconds using a controlled clock).

Integration tests use the real hot cache to require complete fresh plan/publication evidence. Isolated JetStream tests prove retained error delivery even while Host status never returns. Real child-process tests replace a hung Options worker, restore temporary and committed leases and preserve Futures process identity. Restored subscriptions may precede the first snapshot; tests wait for actual complete snapshot evidence.

Test logs are in .artifacts/option-full-recovery-integration-tests.log, .artifacts/option-recovery-real-process-tests.log and .artifacts/option-recovery-queue-tests.log. Isolated build output avoids replacing binaries in the running stack. Final counts are recorded in the completion report.

Latest completed runs: 22 coordinator/runtime/notification/cutover tests, 6 real-process recovery tests and 10 queue/replacement tests passed, with no failures or skips. These tests use synthetic market inputs and isolated processes/broker; real live-market reset verification remains pending deployment.

## Controlled live verification (Development only)

The API's OptionRecoveryLiveVerificationService is dormant until a local request is explicitly armed. Development configuration enables the capability; both API and Host require Development mode. The public IMarketDataApi carries an exact Options worker/generation fault request, and Host refuses stale identities or Futures identities. No orders are submitted and automatic API restart remains disabled.

Control verification passed: 29 coordinator/runtime/notification/control tests and 4 real-process/injection tests, with no failures or skips. Checks include disabled and Production rejection, stale/Futures target rejection, genuine process replacement and local verification-file failure without faulting API startup. Logs: `.artifacts/option-live-verification-tests.log` and `.artifacts/option-verification-injection-tests.log`.

After starting matching Host/API/UI binaries, load the trade and turn Live Feed on. Arm the happy-path test with `./scripts/Development/Arm-OptionRecoveryVerification.ps1 -Mode Happy -DelaySeconds 300`. It waits for exactly 300 elapsed seconds and hangs the current Options worker regardless of stale quotes, plan freshness or prior recovery failure. The timer never resets because the starting state is degraded. The normal production recovery coordinator then resets monitoring generations, replaces the process, restores leases and verifies fresh end-to-end progress under its actual 120-second deadline. Open trade targets are captured without requiring recent quotes.

The previous health-gated baseline prevented injection while the four November legs were stale. That gate was removed at the user's instruction. Freshness and publication checks belong after injection. An explicitly armed Development test can replace a previous failed recovery episode; automatic production episode behavior is unchanged.

Each request expires and is consumed before injection; API restart cannot replay it. Results are written to `.artifacts/option-recovery-verification/<operation-id>.json`, including before/after worker identities, elapsed time, state and Futures isolation. Requests are not armed automatically at startup.

After the happy-path result is reviewed, separately arm `-Mode Deadline`. This repeats worker injection and real recovery, but deliberately withholds completion at the coordinator's verification boundary until its real 120-second deadline expires. It does not directly construct or publish an error dialog. The normal retained-failure publisher must deliver the error. The report cannot certify visible UI presentation: human confirmation remains required. This second mode tests deadline/notification failure handling, not a naturally occurring provider outage.

## Live happy-path result — October 22, 2026

Operation `7e6f7763-26cb-45e9-94f0-344845d20bbd` injected the worker hang at 13:26:38.54 Eastern after the fixed 300-second timer. The test started with stale November option values. Recovery completed in 14.86 seconds at 13:26:53.47 Eastern, proving fresh four-leg quote/risk evidence, a new complete plan and downstream publication. Options PID changed from 104812 to 103692 with a new generation. Futures PIDs 104816 and 107440 and API PID 62404 remained unchanged. No API restart or execution command was issued.

Evidence: `.artifacts/option-recovery-verification/7e6f7763-26cb-45e9-94f0-344845d20bbd.json`. The previous run `130db268-be1e-4a48-b5d0-d1bb5deabf60` aborted before injection because atomic report replacement encountered Windows file sharing denial. Report writes now fail independently of the countdown/recovery path; the locked-reader regression and nine other verifier checks passed. Monitoring reads use FileShare.ReadWrite plus FileShare.Delete. The report-writing fix is in source; this live run used the existing binaries with corrected monitoring reads.

The subsequent controlled deadline test is recorded below.

A read-only follow-up at 13:28:26 Eastern confirmed continued option observations and complete plan calculation beyond the restart. Some individual observations had passed their five-second display-validity window and the plan was momentarily behind the latest position sequence; this is not a claim that every displayed freshness flag stays Available continuously. Follow-up evidence: `.artifacts/option-recovery-verification/7e6f7763-26cb-45e9-94f0-344845d20bbd-followup.json`.

## Live deadline/notification result — October 22, 2026

Operation `5c93def0-bab0-402d-a92e-e5003bc7719a` injected the Options-worker hang at 13:34:49.54 Eastern after the fixed countdown. The worker was replaced (103692 to 100616), while test mode deliberately withheld recovery completion. The coordinator entered Failed after 120.017 seconds. An independent read-only NATS observer received the real production error event at 13:36:49.98 Eastern, approximately 0.4 seconds after the deadline. API PID 62404, Rust UI PID 91796 and both Futures worker identities remained unchanged. API restart remained disabled.

Evidence: `.artifacts/option-recovery-verification/5c93def0-bab0-402d-a92e-e5003bc7719a.json` and `5c93def0-bab0-402d-a92e-e5003bc7719a-notification.json` in the same directory. The observer did not publish a failure or consume/acknowledge the UI's durable queue. Backend timeout and notification publication passed, and the user explicitly confirmed that the Rust UI dialog was visible. The live UI acceptance gate is satisfied for this new recovery implementation. This was an injected completion fault, not evidence of a naturally unrecoverable provider outage. No automatic API restart was enabled by this verification.

## API-only restart escalation requirements

Persist desired live trade identities, session/value date, subscription owners, operation ID and one-restart allowance outside API memory. Add API-only forced stop/start to an external supervisor. Do not use RestartAllAsync, replay old market queues or replay execution commands. Reload established trades, reconcile financial state, restore desired subscriptions, reconnect UI listeners and verify fresh progress within a second two-minute deadline.

After that one restart, publish a persistent final error from outside the API even if startup fails. Expire session registrations and allowance at market close; reject expired registrations at startup if close was missed. Add crash, startup failure, one-restart-only and session-expiry integration tests before enabling this stage.

The controller implementation and automated verification for these requirements are recorded below. Deployment and live acceptance are the remaining gate.

## Durable live-feed restoration — October 22, 2026

The selected restoration mechanism is PostgreSQL `trade.live_feed_recovery`, created additively by the normal Trade schema initializer. Its primary key is `(value_date, trade_id, contract_id)`; `updated_at` records the last enable/restore. The canonical trade identity includes portfolio, fund, order and trade. This table records desired monitoring, never market-event queues or execution commands.

1. Explicit Live Feed On loads the persisted established trade, validates that it is open in the active exchange value date, atomically replaces its contract registrations, then acquires its option leases. Save happens first so interrupted acquisition can be retried at startup.
2. Host owns these leases independently of the UI view. A stable trade/date owner makes repeated restoration idempotent. The provider owner also includes the current option-ownership generation so a retired worker session cannot permanently prevent restoring durable intent.
3. API startup waits for actor readiness and the active Host session, reads only that value date, and calls the same load-and-restore operation for each trade. One failing trade does not prevent trying other trades. Restoration has a two-minute overall budget; expiry publishes an error through the durable UI channel.
4. Loading an open Iron Condor in the UI checks the same registration. If present, it attaches live listeners and enables monitoring. Historical read-only views do not restore feeds. The Rust bridge does not overwrite the restored live view with a saved position after loading.
5. Explicit Live Feed Off deletes that trade/date registration before releasing all its contracts. Release failures are collected so all contracts are attempted and a cleanup error remains visible. Closing/disconnecting a view, an API shutdown, and automatic recovery cleanup preserve desired monitoring. Market close explicitly clears completed and older session registrations. Older dates are never restored at startup.
6. Failed persistence starts no leases. Failed acquisition retains intent. Closed or deleted trades are removed when restoration attempts to load them. Repeated enable replaces the existing contract set without duplicate rows; other trades and value dates are unaffected.

Persistent monitoring restoration supplies the live-trade intent for the API-only restart controller described below. Live restart acceptance remains required after deploying the updated API and Server Manager together.

Verification for this change: 5 recovery-service tests, 1 isolated PostgreSQL integration test, 2 startup restoration tests, and 16 established Iron Condor view-model tests passed (24 total). The tests cover interrupted acquisition, persistence failure, repeated enable, explicit Off, failed release of all four contracts, current-date filtering, closed trades, market close, startup continuation after an individual trade failure, automatic UI restoration without a supplied date, and historical-view exclusion. API Core, MarketData Host and Rust bridge compilation passed; the final bridge build reported zero warnings and zero errors. Logs are under `.artifacts/trade-feed-recovery-*.log`. Running development processes were not restarted; live restart acceptance remains outstanding.

### Missing-table startup correction

Live use exposed PostgreSQL `42P01`: the full schema initializer is a deployment/qualification path and is not executed by normal API startup. The first integration test used that full initializer, which hid this gap. `live_feed_recovery` is now an individually managed additive schema object, and normal `RunServerAsync` creates it before starting hosted services. Full deployment initialization also retains it. The PostgreSQL regression now runs only this narrow migration twice before exercising persistence, proving fresh-database creation and idempotence without full schema setup. The checked-in DDL was also applied to the running development database (`ifm_application_development`) and table readability verified, without restarting applications or workers.

## API-only restart controller implementation

The Development Server Manager owns the escalation, enabled by `ServerManager:ApiRecoveryRestartEnabled`. The API does not kill or relaunch itself. API startup from a direct debug profile has no automatic restart controller; the API + Rust UI managed launcher must be used for acceptance.

1. After acknowledged publication of the first recovery error through the existing UI event channel, the API atomically writes a local restart request. It contains the manager ownership session, exact API PID/start time, exchange value date/session end, original recovery deadline, operation ID and trade identities read from `trade.live_feed_recovery`. A failed UI publication cannot write the request. No saved live registrations means no automatic restart.
2. Server Manager waits until the initial recovery deadline when a recovery error occurred early. It rejects stale, expired or unowned requests and requests received after market close. Before termination it atomically creates and flushes one allowance record for that value date outside API memory. Duplicate requests reuse the existing result. Any subsequent failure for that date produces a UI error without another restart. Expired allowance records are removed at session end.
3. One **additional 120-second budget** covers forced API termination, replacement startup, launch readiness and proof of restored processing. No graceful drain, process-tree kill, RestartAll operation or competing per-step readiness timer is used. An independent process handle protects against monitor disposal races and PID reuse. An already-exited known API can also be relaunched.
4. UI, MarketData Host and Futures workers are left running. The replacement API loads current-date registrations through the normal restoration service. No order, fill, financial execution command or old market queue is replayed by the controller.
5. The uncached `/health/recovery-ready` endpoint identifies the replacement API and reports restored subscriptions plus trade identities with four new quotes/risk values, a complete new Iron Condor plan and confirmed downstream snapshot publication. HTTP readiness or loaded historical positions alone cannot pass. All requested trades must be proven within the same restart budget.
6. Failed launch, missing progress, or expiry writes a durable result. Server Manager publishes the final application error using MessagePack over the existing acknowledged JetStream UI channel, independently of API availability. Failed delivery is retried from that result. A replacement API is not repeatedly killed or restarted if verification fails.

The shell distinguishes the initial pipeline error from the final Server Manager error even when both refer to the same operation. Deduplication includes the error source; relays of either stage do not reopen a notice or replace the final notice with the earlier one. The final dialog is titled **API Recovery Failed**.

Requests and results are in `%LOCALAPPDATA%/TomasAI/IFM/Development/ApiRecovery/<manager-session>/`; allowance files are in the parent directory and survive API/manager restarts. The request is not a second market-data event transport; it is an owned local process-control handoff. Trade contracts remain in PostgreSQL.

### Live acceptance procedure

Rebuild/restart the **API + Rust UI Development launcher, including Server Manager**. Load the trade and enable Live Feed. Confirm the capability endpoint reports `RestartControllerEnabled=true`; then arm:

```powershell
./scripts/Development/Arm-OptionRecoveryVerification.ps1 -Mode Deadline -DelaySeconds 300 -RequireApiRestart
```

This checks controller deployment only, without requiring a healthy quote/plan baseline before the fixed countdown. At five minutes the fault fires. At the first recovery deadline the normal UI error is published, then the API alone is replaced. Record the old/new API PIDs, unchanged Host/Futures/UI identities, restored database registrations and fresh plans. The external `result-<operation>.json` is authoritative for restart acceptance because the old API verifier may be terminated before its final report is written. On another failed recovery the allowance prevents restart and the external error must be visible. This live acceptance has not yet been performed; current running applications were not replaced while implementing the controller.

### Automated verification

83 selected tests passed: 17 Server Manager/process tests, 20 handoff/coordinator/startup/verifier tests, and 46 shell/trade view-model tests. Coverage includes actual API helper-process force replacement while preserving other owned processes, relaunch of an already-exited API, stale PID/start-time refusal, durable duplicate and one-restart enforcement across controller instances, next-value-date reset, expired sessions, the full 120-second deadline with uncooperative restart or proof adapters, failed notification retry, no handoff before acknowledged UI publication, complete fresh-data/publication proof, and both UI error stages. An isolated real JetStream broker received and durably acknowledged the external failure event without an API process. The arming script's deployment guard was tested against the old running API and rejected it without changing pending fault state. Logs: `.artifacts/api-restart-manager-tests.log`, `.artifacts/api-restart-handoff-tests.log`, `.artifacts/api-restart-ui-tests.log`; compilation logs use the same prefix. The Development launcher builds the updated Server Manager and API together.

### Trade loading during an existing recovery

After deploying the controller, a natural recovery ran from 14:55:55 to 14:56:52 Eastern on October 22. A concurrent trade load hit `EnsureOptionsAsync` while the Options dataset was `Recovering`. The old code immediately threw the same readiness error for each of the four contracts even though the existing recovery subsequently succeeded. The running API's readiness endpoint confirmed restored feeds and a proven current plan for `101.701.1901.1501`; its four recovery registrations remained intact.

The Host now joins an existing Options recovery using the caller's deadline and releases its lifecycle lock between observations. It neither rejects that transient state nor launches a competing worker. After confirmed `StoppedError` cleanup it can start a fresh worker, rechecking the state under the lifecycle lock to avoid duplicate starts. `CleanupUnconfirmed` is reported explicitly and is never treated as confirmation of physical worker termination. No additional timeout is introduced. Cancellation preserves the saved registrations. Regression coverage includes recovery-to-healthy, recovery-to-confirmed-stop/restart, a recovery that never finishes, explicit unconfirmed cleanup, and complete four-contract trade loading across success and cancellation. This source fix requires the updated Host on the next deployment; the running recovery itself has already completed.


## Option startup diagnostics (2026-10-22)

`TomasAI.IFM.OptionFeed` emits nested Activity spans and structured stage logs for:

- Saved intent read/save, trade definition load, serialized enable/restore wait, each contract start.
- Subscription serialization, reviewed reference lookup/staging, calendar load, lease restoration.
- Reference qualification, pricing context preparation, route-plan persistence, statistics retrieval, worker acquisition including underlying-quote retries.
- Quote-to-position command wait, cold position state restoration, first durable update/reply, source commit, current-mark publication and projection admission.

Search logs for `Option feed stage`, `Stage`, `CorrelationId`, `OperationId` and `ElapsedMilliseconds`. Subscription stages and the first durable update log start/finish at Information. Realtime stage starts and ordinary finishes use Debug; incomplete/failed/canceled stages and durations of at least 1,000 ms use Warning. This threshold is diagnostic only and does not cancel processing. Exceptions captured at operation boundaries are attached to the final log. Stage interruptions are labeled Incomplete rather than falsely reporting success.

Nested logs retain an OperationId even without a trace collector. CorrelationId contains the trade/date, contract, lease/generation or originating command ID at the respective boundary. NATS Host RPCs forward W3C trace headers; position commands use the existing actor transport propagation. Subscription setup and later independent quote arrivals are separate trace operations; join them through the contract and trade/command identities rather than assuming one indefinitely open subscription span.

Trace export remains opt-in: enable `Telemetry:Metrics:Enabled` and `Telemetry:Traces:Enabled` for API and MarketData Host, with the existing OTLP endpoint/protocol configuration and a collector traces pipeline. The source is registered in the shared telemetry setup; Host now registers that setup too. Environment settings use `Telemetry__Metrics__Enabled`, `Telemetry__Traces__Enabled`, `Telemetry__Metrics__OtlpEndpoint`, and `Telemetry__Metrics__OtlpProtocol`. Ordinary stage logs do not require trace export. Rebuild/restart the affected binaries to activate diagnostics. No market payloads or credentials are added to these logs.
