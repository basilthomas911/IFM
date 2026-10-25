# Option recovery deadline and restart claim correction

## Required outcome

For option-feed faults, continue recovery under one original 120-second deadline. Only verified end-to-end recovery may finish early. Step exceptions cause a repair/retry, not a terminal state. At expiry, retain and publish the UI failure and hand off to the external API restart controller. API restart and saved-feed restoration have a separate 120-second budget.

A durable restart claim means an attempt is outstanding or failed. Persist verified success before clearing the claim. Completed operation results remain for duplicate-delivery idempotency. A later independent failure may restart the API after a previous verified success. Failed attempts remain blocked until explicit recovery handling or session expiry.

## Implementation

- API coordinator retries reset, Host, lease and verification exceptions within the same episode. Each attempt receives the same deadline. Uncooperative operations cannot delay the independent failure observer.
- Options Host recovery retries beyond three attempts until success or deadline. Futures recovery policy is unchanged. Concurrent callers join the existing options deadline and generation result.
- Host serializes scope acquisitions, retains one canonical pricing context across owners, and prevents stale renewals from reverting a shared scope. Restored owners use one requalified context per physical scope; committed ownership is restored as well as temporary leases. Released/expired temporary leases are not resurrected.
- Production recovery rebuilds pricing contexts from current backend reference/rate providers. Worker conflict diagnostics identify digest, ownership, physical-definition or validity-limit mismatches.
- Healthy publication checks cancellation before and after notification, preventing a late recovery return after expiry.
- Desktop failure publication has a bounded wait for producer startup; terminal failures remain available for delivery retry. Cleanup remains separate from API deadline supervision.
- External restart retries launch failures and verification exceptions within its original budget. It does not restart an already launched replacement merely because a verification read failed.
- Restart claims are cleared only from durable verified results. A cross-process file lock serializes claim creation/release; startup also reconciles a successful result saved before a manager crash, including legacy successful claims.

## Verification and acceptance

Automated integration uses a separate JetStream broker, actual named dataset child processes and actual process supervisor restart/termination. Synthetic market input is used for destructive process/failure matrices so tests do not publish test quotes to trading subjects.

Required cases: more than three restoration failures followed by success; permanent restoration failures through 120 seconds; concurrent recovery joins; stale shared-owner renewal followed by process replacement; broken control pipe; stopped process cleanup; futures isolation; uncooperative reset and verification; durable UI failure delivery; marker retained until proof; duplicate request idempotency; verified success releasing a claim; failed restart retaining it; and two independent actual API-process replacement episodes.

Live desktop acceptance is distinct: deploy matching Host/Worker/API/Manager binaries, retain enabled-trade database intent, arm the existing five-minute Happy/Deadline verifier, confirm fresh four-leg quotes/Greeks and new position/plan publication, verify API-only replacement and restored trade intent after the first deadline, and confirm the terminal dialog when final recovery fails. Record operation IDs, deadlines, process IDs and persisted results. Do not mark acceptance complete from component-test passes alone.

## Verification results — 23 October 2026

- API coordinator/runtime, verifier, handoff and real JetStream notification matrix: 33 passed, no failures or skips (`.artifacts/recovery-window-api-final-tests.log`).
- Host/worker process and pricing matrix: 36 passed, no failures; one existing opt-in maximum-scope stress case skipped (`.artifacts/recovery-window-process-final-tests.log`). Permanent failure ran for the actual 120-second recovery budget; total test including cleanup was approximately 122 seconds. Futures remained isolated.
- External controller/process/UI-delivery matrix: 9 passed, no failures or skips (`.artifacts/recovery-window-manager-final-tests.log`). Actual process replacement succeeded twice despite four launch exceptions and four proof exceptions per episode.
- Matching Development binaries were deployed. Natural option recovery episodes verified in approximately 17–56 seconds with fresh downstream evidence.
- Explicit Happy injection `70e2b2a7-1e97-4b71-be39-77590b1ac9ff` fired at 16:27:49 Eastern after its full five-minute countdown. Host replaced options PID 124648 with 81168 and reported healthy after approximately 13 seconds, but API end-to-end verification did not pass. The independent deadline failed at 120.005 seconds; futures PIDs/generations were unchanged. This is a failed worker-only acceptance case, not a pass.
- That failure exercised actual external escalation: API PID 119892 was replaced with 96540. Saved trade `101.701.1901.1501` restored and the durable result became `Recovered` at 16:30:54 Eastern, approximately 64 seconds after handoff. Host/UI/futures remained running. The restart claim was absent after verified success. Evidence: the operation report under `.artifacts/option-recovery-verification/` and Server Manager's persisted operation result.
- The user confirmed the actual Rust UI failure dialog appeared for the 16:29:49 deadline.
- Repeat Happy request `c5e1232c-716a-4e0f-9f29-067698070b1b` completed its five-minute timer, but a concurrent natural recovery changed the worker generation. Host rejected the stale fault target before injection (`Fault target generation changed`). This is an inconclusive harness attempt, not a production recovery pass or failure. No fault from this request was applied.
- Per-leg capture verified natural episode `31feb569-dbc1-400f-bed7-15c4ec482136` recovered at 16:34:59, approximately 44 seconds after detection. The full freshness/publication predicate passed; it did not finish merely on worker readiness.
- Forced-deadline request `1d00382d-ae0a-45f7-8735-058c79dff33d` likewise reached its five-minute timer but was rejected before injection because its target generation changed. It provides no new recovery acceptance evidence.
- The Development verifier now refreshes only a raced-generation target within its original ten-second injection budget, without restarting the five-minute countdown. Other errors are not suppressed. Its 13 focused tests passed (including three new race/error/deadline cases).
- Reloading the API to deploy that harness correction encountered a separate manual deployment failure. The API was stopped, the tested Core assembly copied, and deployment request `3cb11f81-50b3-42ff-8301-c113bb6be562` submitted to the existing Manager. Manager rejected the stopped process identity on every retry and emitted terminal failure after 120 seconds. This is not a successful deployment or a fault-recovery acceptance pass. Host, UI and futures stayed running. The user was asked to use the tray's `Reset API and UI` action to restore the Development stack. Preserve the failed deployment result as evidence.
- A separate real-process regression also passed after externally terminating the API before the second restart. Thus the manual live identity rejection has not been reproduced by that test and is not considered resolved.
- Development was restored using Manager's existing controlled shutdown pipe and a fresh Manager launch at approximately 16:51. This was a manual full-stack deployment restart, not API-only fault recovery. Only the known manual-deployment claim was archived to `.artifacts/recovery-window-failed-manual-deployment-claim.json`; the failed operation result was retained. No unrelated claim was removed. Corrected-Happy operation `523dcc82-f205-4a16-89dd-c4b7dc5cd6ab` was queued with a full five-minute delay for the fresh API.
- That first full-stack reload correctly rejected the mixed deployment: the changed Core DLL did not match the original deployment manifest. The complete isolated tested API build output, including its manifest and dependencies, was subsequently staged. All three required artifact SHA-256 hashes were checked against the generated manifest before restarting. Session `637eaa7756f3400d95dadb3071204328` started API PID 79168, Host PID 117556 and UI PID 68864. The queued Happy request was consumed by that API; its uninterrupted timer began around 16:52:47 and targets injection around 16:57:47. This manual deployment correction is not recovery acceptance evidence.

- Final explicit Happy operation `523dcc82-f205-4a16-89dd-c4b7dc5cd6ab` injected successfully at 16:57:47.042 Eastern after the full countdown. Recovery verified at 16:58:25.144: **38.042 seconds, Passed**. Options PID changed from 15424 to 93420. Futures PIDs 14088 (VX) and 125164 (ES), and both futures generations, remained unchanged and healthy. The production verifier required four post-start quotes and risk calculations, an advancing position, a complete plan, and completed downstream publication. At 16:58:20 two retained risk timestamps were still before the recovery start; the runtime correctly waited rather than accepting worker readiness alone. Evidence: operation report and `.artifacts/recovery-window-final-observations.jsonl`.
- At 16:59:37 API PID 79168 reported saved feeds restored and trade `101.701.1901.1501` proven. No active restart claim remained. No additional fault was armed.
- The already-exited process regression also passed using the **deployed Manager DLL**, not just the isolated source build (`.artifacts/recovery-window-deployed-manager-exited-test.log`). The manual handwritten deployment handoff's identity rejection remains retained as an unreproduced deployment issue; it is not counted as a successful recovery. Subsequent full matching deployment restored the stack. Do not bypass identity checks or rewrite the failed operation result.

Status: required primary live happy-path, 120-second UI failure, API-only replacement/saved-feed restoration and verified claim-release evidence obtained. The focused automated matrix has 81 unique passing cases across the original matrix and three additional harness regressions; one existing opt-in stress case was skipped. Two generation-race requests were inconclusive before injection, and the manual deployment failures remain explicitly recorded above. Second live forced-deadline injection was not completed; repeated restart eligibility and failed/late/uncooperative operations were covered by real-process/JetStream integration tests. No claim of exhaustive failure-proof recovery is made.

## Offline investigation of the first Happy timeout

No service resets or fault injections were performed for this investigation. PostgreSQL was queried inside a read-only transaction; application source was not changed. Extracted evidence is under `.artifacts/first-happy-timeout-investigation/`.

**Confirmed acceptance blocker: `ES20261120C8100` retained its pre-recovery quote and risk from the old worker generation throughout the recovery window.** This makes the four-leg freshness predicate in `OptionPipelineRecoveryRuntime` false regardless of worker readiness or plan count.

All times below are Eastern, 23 October 2026:

| Evidence | Time/result |
| --- | --- |
| Recovery started | 16:27:49.874 |
| Replacement worker healthy | 16:28:03.130 |
| New-generation fresh P7375 and P7325 inputs present in stored plans | 16:28:04.122 |
| New-generation fresh C8050 inputs present in stored plans | 16:28:13.990 |
| C8100 latest quote retained throughout original window | 16:27:42.3777312 |
| C8100 latest qualified risk retained throughout original window | 16:27:44.3111472 |
| C8100 retained generation | `01a16a71-e203-77ff-b180-9af59032240b` (old) |
| Replacement generation supplying the other legs | `01a16a72-ed66-7d4c-8518-6f498057545b` |
| Persisted plans calculated within original recovery window | 342, all `CalculationStatus=Complete` |
| Stored plans satisfying all four post-start quote/risk timestamp checks | 0 |
| Original deadline failed | 16:29:49.876 |
| First stored plan in the queried extended interval with refreshed C8100 quote and risk | 16:30:53.793, after API escalation |

Position sequence advanced from 2086621 to 2086977 in the 342 plans. The pipeline was calculating; it was not stuck waiting to execute the plan function. A complete calculation can use retained leg values, so `CalculationStatus=Complete` alone is insufficient recovery evidence.

Worker logs contain successful MBP-1/trades/statistics subscription acknowledgements and replay-completion messages; no provider fatal error appears in the extracted worker window. Individual lease fencing/old-generation renewal failures occurred during replacement and ceased before the healthy result. Those logs do not identify the actual symbol list for each acknowledgement or record each contract's raw quote receipt/publication/admission, so they cannot prove whether C8100 was quiet, missing from a restored subscription, or had an early update dropped.

Two concrete design gaps merit follow-up:

1. Native quote subscriptions use `LiveBlocking.Subscribe` without a replay start. The deployed SDK explicitly sends `snapshot=false`; this is natural refresh. The recovery therefore relies on usable quote progress for every option after reconnection. Databento's documented `snapshot=true` facility is MBO-only, so simply switching the MBP-1 call to `SubscribeWithSnapshot` is not a valid proposed fix. Source: https://databento.com/docs/api-live/basics.
2. Worker publications are forwarded while restoration is in progress, but API option admission accepts the new generation only once Host reports Healthy (the API synchronizes status periodically). `DatasetPublicationIngress` rejects frames outside admitted generations. Host read synchronization bootstraps futures current values; it does not replay the latest individual option observation for each restored contract. An early option publication can consequently be lost without a per-contract catch-up. This is a code-supported failure mechanism, **not proof that it occurred for C8100 in this incident**; raw receipt/admission telemetry was not retained.

Recommended follow-up: record the restored contract list plus per-contract first receipt, first publication, admission rejection and first qualified-risk evidence for each recovery operation; retain/deliver the latest current-generation observation across admission promotion; and define bounded recovery of an individually missing leg without refreshing old exchange timestamps or relaxing the two-minute deadline. A quiet-leg/admission-race integration test can be run offline before the next live window. The exact upstream reason for C8100's missing update remains unproven.

## Admission catch-up and exact-leg repair implementation

Implemented after the offline investigation; the running Development stack was not redeployed, restarted, or fault-injected for this work.

- Host retains one latest option observation per contract before transmitting it. Storage is bounded to 2,048 contracts; read snapshots rotate through at most 128 observations and 512 KiB of payload per capture. Replacement generations clear old retained values. This is latest-value catch-up, not historical replay.
- The API reconciler admits the Healthy replacement generation and feeds retained observations through the same serialized option ingress as live observations. Ordering and duplicate suppression are per contract, so activity in another leg cannot discard a quiet leg's earlier frame. A failed publication does not consume that frame's delivery identity. Quote and calculation timestamps remain unchanged.
- Restored scope/lease/contract lists, first Host quote and qualified-risk receipt per generation, rejected admission observations, successful API catch-up, and unresolved recovery criteria are logged. Pending recovery diagnostics include each missing contract, quote/risk timestamps, and source generation. The terminal UI event includes the last pending recovery detail.
- After ten seconds of awaiting proof from the restored Healthy worker, each missing exact-contract subscription gets one reconnect attempt in that pass. This gives admission catch-up a chance before reconnecting a quiet leg. The reconnect retains pricing inputs, statistics, and business ownership and does not restart unrelated connections. It uses the original episode deadline through NATS and the worker command channel; there is no additional five- or thirty-second recovery timeout.
- Repair does not create missing desired subscriptions or re-save a released lease. Turning Live Feed off removes Host intent before waiting for physical release. A missing or released subscription is reported explicitly rather than recreated by repair.
- Reconnecting is not successful recovery evidence. All four quotes and qualified risks must have post-start timestamps and the replacement generation, with a complete advancing plan, confirmed downstream publication, and open processing mailboxes. Unusable/absent data continues to the original 120-second deadline and produces the existing UI failure/escalation event.

### Offline verification

Tests use synthetic data and processes, an isolated NATS broker/subject scope, and isolated build/publish output under `.artifacts/option-catchup-offline-host`. No provider connection or live trading fault injection is used.

Coverage includes early publication before admission followed by a quiet contract; another contract advancing first; duplicate and out-of-order frames; retired generation fencing; unchanged timestamps; retry after failed publication; bounded rotating retention; exact-contract reconnection with ownership preserved and other connections unaffected; real Host/NATS/API receiver catch-up; absent and released subscription errors; successful fresh end-to-end repair; missing subscription, no quote, and noncooperative repair reaching the original deadline; and fresh-looking data from the wrong generation remaining unverified.

A separate real-process test hangs the synthetic option worker and supplies an eight-second repair deadline. It verifies that repair waits beyond the normal five-second worker command timeout, terminates at the supplied deadline, and permits subsequent immediate process replacement. The full 120-second terminal behavior is checked using the production runtime/coordinator with an injected clock, including a repair task that never completes.

Results: **92 passing tests, zero failures, one existing opt-in stress test skipped**: 69 market-data/worker/transport regression cases, 22 runtime/coordinator cases, and the separate hung-worker deadline case. Final logs: `.artifacts/option-catchup-final-tests.log`, `.artifacts/option-runtime-final-tests.log`, and `.artifacts/option-catchup-hung-nats-test.log`. The isolated broker was stopped after verification.

Live acceptance of these new catch-up/repair changes remains pending the next trading window. Offline success does not establish whether Databento will naturally refresh a particular quiet contract; no historical quote is relabelled as fresh to bypass that condition.
