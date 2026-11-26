# Realtime mailbox session evidence tools

The low-level evidence tools do not activate a mailbox backend or submit orders. The explicit operational controller described below can bootstrap and gracefully stop/restart the development profile. `next-normal-sessions.json` prepares **Nov 30 and Dec 1, 2026**, following the CME/CFE Thanksgiving holiday/early-close dates. `calendar-evidence-20261125.json` records the official sources. Each tape window includes overnight, cash open/close, EOD, maintenance and the next 6 p.m. ET rollover. The two windows overlap around rollover; reconcile original source identities instead of replaying that overlap twice.

**Historical November 25 preflight:** the Core-NATS observer was diagnostic, and missing owner/source bindings blocked eligible arming. That earlier preflight reported 503 for a Daily source gap and legacy account evidence without a positive emulator/PG revision join; it is not a current live health observation. The dedicated historical host and frozen native registration/book/mapper checkpoint are implemented; see [Historical replay](Historical-Replay.md). A native checkpoint alone does not freeze every API/database owner. Use the controller's current runtime, arm and owner-boundary receipts; do not label prepared plans, partial tapes or raw source prefixes complete captures.

## Run offline checks

### Isolated owner-store restoration

`session_restore.py` restores the actual one-PostgreSQL/one-Scylla snapshot topology and exact owner files. It requires a new output directory and freshly empty containers on an exclusive labelled Docker bridge network named `ifm_session_<12-lowercase-hex-runId>`. Containers must have that prefix and label `ifm.session-qualification=<runId>`; persistent volumes require the same label. Host-directory mounts and shared networks are refused. The host-reachable replay bridge publishes only explicit loopback ports; Docker Desktop internal networks do not expose those host ports. The original source image IDs, recorded by `session_tool.py snapshot`, must match the restore images exactly.

The targets JSON has `version:1`, `runId`, `postgres:[{kind:"postgres",container,database}]` and `scylla:[{kind:"scylla",container}]`. Provision disposable containers separately; this command never creates containers or changes the live profile.

```powershell
python scripts/RealtimeMailboxSessions/session_restore.py --snapshot verified-snapshot --targets isolated-targets.json --output new-restored-state
# Perform the verified restore, still without starting any application:
python scripts/RealtimeMailboxSessions/session_restore.py --snapshot verified-snapshot --targets isolated-targets.json --output new-restored-state --execute
```

PostgreSQL uses its custom dump with fail-on-error. Scylla restores the schema, maps original SSTables to newly created table IDs, uploads base-table components and refreshes them with streaming; secondary-index/view data is rebuilt rather than importing derived SSTables. Owner files are copied byte-for-byte. The result remains `RestoredNotReplayed`, `qualified:false`, `apiStarted:false`. A successful restore alone does not establish a native checkpoint, complete captures or owner-outcome equivalence. The historical host and native checkpoint now have dedicated implementations described in [Historical replay](Historical-Replay.md).

The disposable restore check uses real PostgreSQL and Scylla containers, verifies decimal/blob/sequence values, changed table UUID mapping, secondary-index query results and exact owner bytes, then removes its own containers, volumes and network:

```powershell
$env:IFM_TEST_SESSION_RESTORE_DOCKER='1'
python -m unittest discover -s scripts/RealtimeMailboxSessions -p test_session_restore.py -v
```

### Other offline checks

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py validate --plan scripts/RealtimeMailboxSessions/next-normal-sessions.json
python -m unittest discover -s scripts/RealtimeMailboxSessions -p test_session_tool.py
```

## Diagnostic recording

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py record --plan scripts/RealtimeMailboxSessions/next-normal-sessions.json --output E:/IFM/SessionQualification/diagnostic-new-run
python scripts/RealtimeMailboxSessions/session_tool.py verify-tape --capture E:/IFM/SessionQualification/diagnostic-new-run
```

Recording connects only to explicit loopback NATS, subscribes without a queue group, and never publishes an application message or sends an ACK. It copies original MSG/HMSG subject/reply/header/body bytes plus observed arrival order/time, keeping a SHA-256 chain across bounded segments. Finite payload, bytes and time budgets prevent unbounded memory/disk use. It refuses authenticated/TLS brokers until a reviewed credential adapter exists; no inline secrets are accepted. Disconnect/refusal/slow-consumer/corruption is recorded as incomplete. There is no gap-masking reconnect. Socket timeout polling retains partial frame bytes. A capture interrupted midframe is invalid rather than silently accepted.

`qualified` remains false even if a diagnostic tape has no apparent gaps. Core publication does not prove all original provider sources arrived, the captured recipient set, or authoritative initial owner state. No automatic diagnostic-to-authoritative promotion exists.

## Actual snapshot/export mechanism

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py snapshot --plan reviewed-plan.json --output E:/IFM/SessionQualification/new-snapshot
python scripts/RealtimeMailboxSessions/session_tool.py snapshot --plan reviewed-plan.json --output E:/IFM/SessionQualification/new-snapshot --execute
```

The first form validates a real cooperative fence; it does not fabricate one. `--execute` exports real PostgreSQL custom dumps, Scylla SSTable snapshots/schema and owner file roots only while a matching held lease proves every required owner frozen. It rechecks the lease throughout and afterward. No configured fence, missing participant, expired/replaced lease, file mutation, export error or budget failure can produce a qualified snapshot. All existing output directories are refused. Snapshot provider containers/databases/keyspaces and file roots must first be bound to the reviewed live/isolated topology. No commands are run through a shell and provider errors are not printed with credentials.

Scylla snapshot creation writes a native backup tag; this is an explicit snapshot operation, not a read-only query. The tool does not delete source snapshots. Stop/restart, restore and retention require separate reviewed procedures. The exported snapshot is still `qualified:false` until restoration, original source floors and owner outcomes are verified.

The operational controller holds native forwarding while exporting the atomic native image and immutable prefixes, then gracefully stops the API and native host as well as scheduler/UI writers. It proves their exit and acquires actual owner leases before exporting PG/Scylla/files, then restarts capture-only owners with their original source roots and cursors. A replacement native process does not inherit the retired process's forwarding lease. The future baseline and complete captures remain pending; use actual controller and task receipts for their state. `Stop-IFMDevelopment` force cleanup is not a clean boundary certificate.

## Native forwarding/prefix hooks

The optional native exports require `IFM_SESSION_CAPTURE_BOUNDARY_ENABLED=1` and existing required source capture. They never call `NativeOwnedSession.Stop` or retire a provider generation. Every relay send holds a forwarding guard through its request/reply; hold waits for active guards and prevents new ones while input capture continues. Core-only operation refuses a hold. Only the matching nonzero lease may release it.

Optional Host endpoints are disabled unless Development, `SessionCapture:Enabled=true`, a loopback caller and `SessionCapture:AccessToken` of at least 32 characters supplied in `X-IFM-Session-Capture` all match:

- `GET /internal/session-capture/native/status`
- `POST /internal/session-capture/native/hold/{hexLease}`
- `GET /internal/session-capture/native/prefix/{hexLease}`
- `POST /internal/session-capture/native/release/{hexLease}`

A forwarding status is explicitly `CrossOwnerSnapshot:false`. The source prefix manifest contains exact append-only journal lengths, original queued heads and the forwarding lease. It explicitly says mapper/required-intent checkpoint false. Copy immutable prefixes outside the producer lock:

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py copy-prefix --plan reviewed-plan.json --prefix original-native-prefix.json --output E:/IFM/SessionQualification/new-prefix-copy
```

The native lease must remain held until copying completes. Backend late capture requests must also drain before claiming an API snapshot boundary. Preserve ongoing raw/source tails; never truncate a source journal to match a copied prefix.

## Matched runs and reconciliation

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py replay-plan --plan reviewed-plan.json --capture authoritative-tape --snapshot verified-initial-snapshot --output matched-run-plan.json
python scripts/RealtimeMailboxSessions/session_tool.py reconcile --expected original-owner-outcomes.json --actual isolated-owner-outcomes.json --output reconciliation.json
```

The planner produces Memory and File chains from the **same initial snapshot and source hashes**. Restore baseline once per mode, carry session-one state into session two, and include all source events through maintenance/rollover. Do not restore the first-session baseline immediately before the second session. Every actual replay still needs isolated broker/database identities, file emulator, original pacing and the production owner runner; this command prepares the plan and does not falsely claim execution. It refuses a missing/changed snapshot and reports Core-only/missing source-ledger blockers.

The reconciler compares source/recipient identities, raw rows, indicators, workflows, orders/fills, exact cash/holds, positions, final plans, EOD and source cursors. Use bounded manifest digests of indexed streamed evidence ledgers rather than giant JSON arrays. Missing required families, unknown pending/refusal counts, gaps, incomplete or non-normal sessions fail. Matching total counts or P&L alone cannot pass. Additional synthetic owner/fault scenarios must remain labelled and separate from actual complete normal-session data.

No background recorder, service pause, transport activation or eligible session trial is started merely by checking these tools into the repository.

## Capture-only original-flow archive and prepared jobs

`Actors:SessionCapture` selects a separate bounded source archive; it never enables the new mailbox backend. The API refuses this mode outside Development or together with `Actors:RealtimeMailboxes:Enabled=true`. Native protocol2 source receipts follow flushed archive body and receipt ownership, then the existing Core mirror. All native batch members are archived before ordered mirroring. Upstream retains a source if mirroring fails. Managed realtime producers and the actual JetStream account observation producer record their original bytes and frozen origin/recipient manifest before publishing those exact bytes through their existing transport. The compatibility producer forwards these optional ports and propagates recording failures. Source ownership receipts are explicitly not domain-handler success or completed-session evidence.

`prepared-capture-overlay.json` and `nov30-dec1.capture-job.json` define the Nov 30 and Dec 1 normal exchange sessions, including EOD and the following 6 p.m. ET rollover. The capture-only Development profile was applied and its source protocol verified on November 26 before the subsequent Windows reboot; that historical result does not mean the profile is currently running or the future job is armed. The scheduled initial owner/source/clock baseline and both complete captures remain pending. Tokens are supplied through the existing protected CurrentUser DPAPI file and inherited environment. Run a read-only preflight:

```powershell
./scripts/RealtimeMailboxSessions/Start-PreparedSessionCapture.ps1
```

It refuses missing owner snapshots, legacy/unequal emulator-PG revisions, source gaps/unhealthy readiness, absent protected token, unavailable archive capability and unqualified capture storage budgets. No process or configuration is changed by preflight. `-Execute` starts only a bounded read-only readiness/capability witness after all checks pass; it does not apply the overlay, restart the profile or create the financial baseline. No job is armed automatically.

The authenticated loopback Development-only API exposes `/internal/session-capture/status` and `/internal/session-capture/prefix`. The latter holds a short commit fence to describe immutable file prefixes, then recording may append while export copies/hashes those prefixes outside the fence:

```powershell
python scripts/RealtimeMailboxSessions/session_tool.py copy-archive-prefix --job scripts/RealtimeMailboxSessions/nov30-dec1.capture-job.json --output E:/IFM/SessionQualification/new-archive-prefix
```

An offline `--prefix saved-prefix.json` uses the same bounds and root checks. The exported manifest states `archiveOwnershipOnly:true`, `ownerSnapshot:false`, `mapperCheckpoint:false`, `qualified:false`. It rejects traversal, duplicate paths, truncated prefixes and configured byte-budget excess. Source archives and diagnostic Core taps cannot substitute for the matching initial financial/configuration/source state or final full-session reconciliation.

## Operational decisions versus bulk cold stress

Initial replay snapshots also require `snapshot.sourceArchive` with the original saved `GetCommittedPrefix` metadata path, SHA-256, session ID and recorder directory. The held `source-outbox` owner must bind that same metadata hash/session to its real checkpoint. Snapshot export copies the exact committed prefixes and an `initial-archive-boundary.json` owner file under the same held fence. The restored replay configuration points `InitialArchiveBoundary` and `InitialArchiveBoundarySha256` at that certified file. Replay verifies the actual frame commits, receipt counts and original prefix hashes and starts **after** its last committed ordinal. A hand-entered ordinal or timestamp cannot replace this proof; an archive-only prefix is not a financial-owner snapshot.

The two-second workflow gate measures prepared cached decisions and their local continuation. The cold 1,000-identity/ 3,000-consumer workload is a separate explicit test-only stress exercise; the full harness currently includes it whenever explicitly invoked; its raw maximum/pass flags remain available and do not become decision-pipeline evidence. Default mailbox operation recovers durable pending recipients and creates other consumers on demand. No explicit required-consumer manifest or readiness barrier is introduced. The machine-readable plan carries this classification into generated replay plans. Session captures, matching owner state, recovery/capacity checks and activation qualification remain separate requirements.

The full qualification harness still includes its cold phase when explicitly invoked. No new opt-in branch or default-off harness switch was added; the stress workload does not run in ordinary application startup.

## Drained cutover and rollback rehearsal

`cutover_tool.py` implements the simple **fully drained** rehearsal: it does not migrate pending work to a new epoch. It takes exclusive existing `owner.lock` handles for stopped native/backend source owners; reads original source frames, commit markers, full-source handler receipts, native capture receipts and namespace/protocol markers; checks hashes and refuses torn/uncommitted/corrupt or pending work. It never repairs, truncates, prunes or renames owner journals. Native API-capture receipts alone do not prove domain handler completion, so both owners must be independently empty.

```powershell
python scripts/RealtimeMailboxSessions/cutover_tool.py --backend-sources E:/disposable/old-epoch/sources --native-sources E:/disposable/native-scope --baseline-config E:/disposable-config/appsettings.json --output E:/new-disposable-cutover-rehearsal --epoch reviewed-new-epoch
```

The command only writes a newly created isolated directory. It preserves the disabled original configuration byte for byte, generates a review-only candidate with a distinct epoch and isolated owner path, disables capture-only routing in that candidate, switches the isolated configuration, verifies its readback, rolls it back, verifies the exact original hash and rechecks original journal inventories. It launches no API, provider, broker or order process. Its report explicitly says `activationPerformed:false` and `liveCutoverQualified:false`; candidate configuration is never applied to the development profile. Existing output or overlap with source/config owners is refused.

For a future separately controlled development trial, the same tool exposes the existing authenticated loopback native forwarding hold/release, with exact owner-lease readbacks. These operations require `--execute-native-control` and a protected token environment variable. Their availability or an earlier finite native hold does not certify a stopped-owner boundary, scheduled capture or transport activation:

```powershell
python scripts/RealtimeMailboxSessions/cutover_tool.py --native-control hold --native-url http://127.0.0.1:PORT --token-env IFM_SESSION_CAPTURE_ACCESS_TOKEN --execute-native-control
# Record the returned lease. Release requires that exact hexadecimal lease:
python scripts/RealtimeMailboxSessions/cutover_tool.py --native-control release --native-url http://127.0.0.1:PORT --lease ORIGINAL_HEX_LEASE --execute-native-control
```

Native hold preserves ongoing raw/source capture and waits for active forwarding calls; it is **not** a backend actor/financial-owner drain. A complete live cutover still needs independent backend quiescence, exact ownership reconciliation and qualified matched-session evidence. Current tooling does not invent that missing proof from an empty mailbox counter or `/health/ready`. Activation remains disabled. Validate the rehearsal and refusal behavior with `python -m unittest -v test_cutover_tool` from this directory.
# Operational two-session controller

The concrete job configuration is `D:/IFM/SessionQualification/nov30-dec1/control/controller.json`. The protected token is the existing CurrentUser DPAPI file beside it; it is never embedded in configuration, arguments or receipts.

`Run-SessionCaptureController.ps1 -Config <config> -SecretFile <dpapi> -ValidateNow` checks the actual capture-only runtime and freshly measures PostgreSQL, Scylla, native roots and disk capacity. `Arm-SessionCaptureController.ps1 ... -Register` additionally registers a bounded, current-user Windows task and reads back its action. Its receipt explicitly requires an interactive logon; it does not claim capture has started. Ordinary validation does not register a task or restart a profile.

The scheduled wrapper performs the explicit prequalification bootstrap before the initial boundary. Use the full `control/controller.json` for `Start-CaptureDevelopmentProfile.ps1`; an old minimal bootstrap configuration cannot safely identify a running capture profile. Before replacing an already-running capture profile, bootstrap uses the authenticated production-pause, source relay and API drain sequence, then shuts native down while the API is still alive. The original database, raw and native source roots are preserved. Durable actor mailboxes remain disabled during original capture. The configured Sunday noon boundary provides several hours to finish the real baseline before the first 17:50 ET capture window.

The actual boundary holds manager recovery, gracefully closes UI and scheduler writers, then pauses new native production without retiring its registration, book or mapper. Accepted owner outputs drain before that pause is sealed; the source relay remains running until the actual pending ownership count reaches zero. It then holds native forwarding, parks only the recorded timer-root producers, and repeatedly verifies the real actor/raw/archive/ordered-ITI drain. It captures the actual financial owner and its converged account projection, original native image/backend snapshot and immutable source prefixes. It then gracefully stops the API and native host, independently checks process exit, and acquires genuine existing owner leases or exact-file handles. No missing `owner.lock` is fabricated. The full initial PostgreSQL/Scylla baseline is exported once. Root-specific restore identities prevent similarly named receipts from different journals colliding.

The final boundary exports actual business outcomes from the stopped original stores, with the complete source archive copied once into its protocol epoch directory. It does not create a second full database baseline. Memory and File replays restore separately, use the same pinned baseline and frozen runtime, preserve original pacing, and run serially. A comparison alone is not complete source/session qualification; the controller keeps `qualified:false` until the separate coverage requirements have been proved.

`Run-SessionCaptureController.ps1 ... -BoundarySmoke -OutputDirectory <new-directory>` exercises the real hold, drain, checkpoint, graceful stop, exact owner leases and restart, without exporting the large database baseline. Its receipt says `OwnerLeaseSmokePassedNoDataBaseline`. This is an operational development action, not a read-only command.

Completed phases are hash-bound. Waiting and original-capture phases can resume after verifying their existing artifacts. A partial boundary, missed initial window, changed runtime or failed proof retains evidence and ownership for inspection; it is never retried with a fabricated new budget or overwritten as successful.

The scheduled task requests WakeToRun and verifies that setting on registration. Resume after the initial time is permitted only for a hash-verified CapturingOriginalFlow phase, including the original baseline, effective configuration and complete frozen runtime tree; a missed or partial initial baseline remains blocked. Runtime sampling reads the authenticated ordered-ITI owner status and rejects a failed or missing owner, while allowing a closed market or transient in-flight work without imposing timestamp freshness. PostgreSQL dump and Scylla archive exports retain a practical six-hour wall-clock ceiling, finite byte limits, an independent watchdog for silent providers and an incremental exact-byte SHA-256; schema-only commands retain their ten-minute bound. These operation budgets do not relax the two-second decision deadline or the original real-time replay pacing.

## Verified arming — November 26, 2026

Task `IFM-OriginalSessions-202611300001` is registered and its UTC trigger, action and WakeToRun setting were read back. It starts Sunday November 29 at 11:45 a.m. ET; the original owner baseline is scheduled for noon. The two capture windows are November 29 17:50–November 30 18:10 ET and November 30 17:50–December 1 18:10 ET. The configured actions then perform serial Memory/File production-handler replays and exact outcome reconciliation. Windows must remain signed in for this current-user interactive task.

The armed configuration sets `bootstrapSkipBuild:true`: bootstrap starts the verified binaries without rebuilding them. This preserves the pinned runtime hashes across a Git commit, which can otherwise change embedded source-revision metadata. Frozen-runtime and loaded-assembly checks still apply; modified or replaced binaries must be reviewed and pinned again before capture.

The actual no-force owner-boundary smoke passed, including the converged financial checkpoint, matching native image and prefixes, stopped writers, real exclusive owner handles, and subsequent profile restart. Evidence is `D:/IFM/SessionQualification/nov30-dec1/boundary-smoke-20261126-0955/smoke.json`. The API returned 200 from full `/health/ready` after restart, and Rust eGui was open and responding. The earlier failed cuts remain explicitly unqualified.

The boundary waits for feed startup to settle before pausing its required callbacks. Raw-prefix retention is bound to the same production lease and survives native shutdown; matching resume alone releases it. On-disk and HTTP certificates compare every field/value despite their different JSON property-name casing. Original directory bindings use filesystem identity so Windows verbatim paths do not defeat a valid binding. Trigger readback compares UTC instants, preventing PowerShell JSON date conversion from silently shifting the schedule.

Native verification passed 185 tests, with 13 intentional manual skips. The final Python suite passed 70 tests with four explicit fixture skips; real-broker raw-retention, disposable PostgreSQL source recovery, host authorization and actual child-process lifecycle tests also passed. Capacity was freshly measured before arming. No full session or Memory/File result has been produced yet: the required sessions are in the future. Both durable activation and qualification remain false until the complete source, outcome, coverage, retention, load and two-second gates pass. Current status is recorded in `nov30-dec1.capture-job.json` and the protected controller arm/readback receipts.
