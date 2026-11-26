# Realtime Actor JetStream Mailbox Qualification Results

**Date:** 2026-11-25\
**Status:** Comprehensive remaining-work audit completed: 22 core packages plus 2 adjacent account safety packages; 12 focused counterexamples verified. Production mailbox implementation/qualification remains incomplete
**Plan:** [Implementation stages](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v1.0.md)

## Comprehensive remaining-work audit — 2026-11-25

Investigation only, with root plus three concurrent subagents. The [full inventory](Realtime-Actor-JetStream-Mailbox-Remaining-Blockers-Audit-2026-11-25.md)
now enumerates **24 work packages: 22 core and 2 adjacent account safety items**, distinguishes
reproduced/static defects from policy/compatibility and unfinished qualification, and refreshes
all 25 destination event maps and 33 workflow boundaries. **12 focused counterexample scenarios
passed across four projects**, documenting current undesirable behavior/conditional incompatibility;
they are not repaired-behavior tests. The rejected same-version parameter hypothesis is recorded.
No new BDN/full-regression/live-health qualification or application fix is claimed. The machine-readable
backlog and full source/test evidence support the next parallel implementation plan.

## Regime Discovery completion recovery - 2026-11-25

The cached-result completion replay blocker is repaired. RegimeFunctionTerminalDelivery now
captures CompleteRegimeDiscoveryCommand as well as failure terminals before handoff. The original
result envelope, fingerprints, parameter evidence, IDs, completion time and publication provenance
are frozen under the same deterministic Function request ID/original source hash. Pending startup
and source retries send the exact saved command without recalculation, input preparation or
completion remapping; confirmed outcomes are archived and completed replay sends nothing.

WorkflowRegimeFunctionFailures retains its legacy public/configuration/directory name but now
stores either terminal kind in one journal, so an original failure cannot be replaced by a newly
computed completion or vice versa. New frames are binary v2 with an explicit failure/completion
kind byte; v1 failure-only pending/sent frames remain readable. Pending v1 records can be confirmed
and archived using the new writer. Existing sent v1 records remain valid without mandatory rewrite.
Null legacy origins remain unknown rather than being invented during recovery.

The existing directory override AppSettings:WorkflowRegimeFunctionRecovery:Directory and .wgf
extension remain unchanged. Exclusive ownership, checksums, flushed atomic replacement, bounds
(8 MiB/frame, 4,096 pending, 128 cached) and unpruned sent history remain. Pooled streaming source
hashing is unchanged. Terminal frame serialization still uses bounded arrays; it is not a claim
that all result payload allocation is pooled. No application JSON processing, database decision
input reads, retry loops, strategy changes, deadline relaxation or financial reservations were
introduced. Authoritative stale/deadline/result guards remain: delivering an old command after
restart does not grant permission to advance an expired workflow.

Verification:

- API/benchmark Release build passed with zero warnings/errors. **2,267 Trade checks passed**:
  2,266 repair/regression checks plus the next-blocker counterexample. Nine new completion cases
  cover metrics absent/present with send failure, typed rejection and accepted-but-lost reply;
  exact result/command bytes and origin across retry/next-day startup without a calculation runtime;
  no repeated current-view/configuration/capture work; source-hash parity; completed suppression;
  terminal notifications; failed capture before send; changed/corrupt input; exclusive owner;
  interrupted confirmed-marker archival; and old-format pending/confirmed failure frame recovery.
  The existing eight failure recovery cases also passed against the extended journal.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-child completion cases passed** through
  the actual actor/local scheduler, real decision executor/runtime, completion mapper and journal.
  Modes: send failure, typed rejection, accepted-but-lost reply, death after acceptance/before the
  local marker, and completion before source ACK. Cold startup sends original completed result
  bytes/IDs/provenance without its old runtime; completed replay does not resend. Two requests
  leave two ordered receipts at the synthetic idempotent PostgreSQL receiver, with explicit source
  ACK and MaxAckPending=1. NATS remains alive; Memory source does not survive loss of NATS.
  Controlled configuration/snapshot inputs produce an incomplete/no-trade regime result through
  the real calculator. This is terminal-delivery qualification, not evidence of tradable regimes,
  production owner/projector/financial replay, or broker/OS-power-loss recovery.

### Regime completion BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 sources/invocation, no forced GC; tiering disabled only in the benchmark host. Per-source means.
Both sides use the actual workflow actor/local scheduler and real local decision runtime/model.
Controlled immutable configuration/snapshot provider outputs, owner replies and current view replace
external services. Before disables the journal; after includes capture/confirmation/provenance and
pooled hashing. New measures independent requests; CompletedReplay is primed, so the volatile side
reuses the calculation but remaps/sends another completion, while the repaired side recovers and
suppresses those operations. Setup/priming/cleanup and database/NATS IO are outside measurement.
Small/representative payloads are synthetic fixtures, not production owner-admission/capacity tests.

Serialized source/hash-input sizes: **1,935 bytes small**, **2,044 bytes representative**. Artifact RequestBytes fields describe the source.

| Payload | Case | Volatile mean | Frozen/pooled mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Small | New | 687.0 μs | 9,517.3 μs | 101.37 KB | 179.83 KB |
| Small | CompletedReplay | 461.8 μs | 449.4 μs | 71.4 KB | 33.22 KB |
| Representative | New | 580.7 μs | 11,426.9 μs | 102.48 KB | 180.96 KB |
| Representative | CompletedReplay | 397.1 μs | 487.5 μs | 72.34 KB | 33.72 KB |

All eight raw GC diagnostics recorded zero Gen 2 collections.
Durable IO adds cost. Short iterations and confidence intervals are retained in the full report.
Aggregate allocations are not direct LOH measurements; these probes do not establish live GC,
whole-pipeline two-second guarantees, burst capacity or full-session throughput. Result-size
scaling, pooling of journal frames, batching and history retention remain open.

### Next confirmed blocker: cached Market Condition completion changes timestamp on retry

Continuing the real actor/runtime audit with an assigned Daily futures assessment, a sealed
controlled crossed-book snapshot and the real decision executor/calculator returns a typed
unavailable/no-trade completion. A per-request immutable receiver accepts its command and loses
the reply. On retry one second later, the local runtime reuses the same assessment result bytes,
result/source ID and derived command ID. CompleteMarketConditionAssessment nevertheless sets
CompletedAtUtc from the new clock value, so the same command identity carries different bytes.
The test verifies both failed deliveries, equal result/command/source IDs, changed completion time
and absence of a selector initialization failure (isolating the timestamp change). This is an
actual local mapping counterexample, not an observed live duplicate financial effect.

Next work must freeze the original Market Condition successful terminal before dispatch and
qualify retry/restart, with owner result-proof/deadline guards retained. Stage 1 stops here. Other
terminal/callback paths, production mailbox stages 2–9, MarketOutlook policy, retention/throughput
and both full-session trials remain unqualified. No live profile restart, backend activation,
financial repair or readiness weakening occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-build.log)
- [2,267 Trade checks](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-trade-tests.log)
- [10 abrupt-child cases](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-restart.log)
- [BenchmarkDotNet](../../.artifacts/realtime-mailboxes/20261125-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeCompletionBenchmarks-report-github.md)
- [Assessment completion counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowAssessmentCompletionTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-destination-audit-progress.json)

## Regime Discovery Function-failure recovery - 2026-11-25

RegimeFunctionTerminalDelivery freezes the original FailRegimeDiscoveryCommand before its first
handoff. The production-registered WorkflowRegimeFunctionFailures journal uses the deterministic
Function request identity (workflow ID, Regime Discovery stage and input revision) and the exact
original source snapshot hash. Pooled streaming MessagePack/SHA-256 matches contiguous hashing.
Failure event/source and derived command IDs, timestamps, parameter IDs/version/hash, diagnostics,
correlation/causation and creation run/generation provenance are preserved rather than regenerated.

Matching active sources recover before current-view/freshness checks and input preparation. The
Function dispatch also checks recovery before running freshness or calculation. Already captured
failures cannot turn into a different stale-signal or initialization outcome after a delayed retry.
Startup sends pending original commands without reloading configuration, capturing market inputs
or requiring source redelivery. Only a successful typed owner reply permits confirmation/archive;
completed replay sends nothing. Ambiguous replies require identical at-least-once resend to an
idempotent owner. Later terminal notifications retain their ordinary handling. Initialization
failures, stale-trigger handling and successful completions retain their existing distinct paths;
strategy rules, 30-second freshness, deadlines and authoritative workflow guards are unchanged.
No new database input reads, financial reservation or in-decision retry loop was added.

Directory: LocalApplicationData/IFM/workflow-regime-function-failures by default, overridden with
AppSettings:WorkflowRegimeFunctionRecovery:Directory. Single-owner versioned/checksummed .wgf
binary frames store command and origin; flushed writes use durable atomic replacement. Bounds:
8 MiB/frame, 4,096 pending, 128 cached records and unpruned file-backed sent history. Null-root
storage is volatile isolated-test scope. Capture/write failures prevent send; changed input,
corrupt frames and competing writers fail closed. No application JSON processing was introduced.

Verification:

- Release API/benchmark build: zero warnings/errors. **2,258 Trade checks passed** (2,257 repair/
  regression checks and one next-blocker counterexample). Eight repair cases include metrics
  absent/present crossed with send failure, typed rejection and lost reply; exact retry and cold
  startup bytes/provenance; no repeated configuration/capture/current-view work; recovery before
  a now-stale trigger; accepted parameter identity; timeout failure mapping/cold recovery; later
  terminal handling; pooled hash parity; corruption/changed-source rejection; exclusive ownership;
  capture failure before send; interrupted confirmed-marker archival and completed suppression.
  Controlled real parameter validation and input capture succeed before actual Function failure
  mapping; this is not an initialization-failure-only test.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-child cases passed** through the actual
  workflow actor/local scheduler and file journal: send failure, typed rejection, accepted-but-lost
  reply, death after acceptance/before local confirmation, and completion before source ACK.
  Next-day startup and retained-source replay preserve original terminal payloads and provenance.
  Two requests leave two ordered receipts at a synthetic idempotent PostgreSQL receiver; source
  MaxAckPending=1 prevents overtaking, with NATS remaining alive during API-child death. Synthetic
  initialization/configuration/market inputs are used. These are not production workflow/financial
  application or broker/OS-power-loss proofs, and Memory source durability ends if NATS is lost.

### Regime Function failure BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 source snapshots per invocation, no forced GC; tiering disabled only in the benchmark host.
Values are per source. Both sides execute the actual actor/local scheduler, real parameter/hash
validation and actual unavailable-runtime Function-failure mapping. Before has no journal; after
adds durable capture/confirmation, provenance and pooled source hashing. Configuration and snapshot
providers and typed owner replies are controlled; their immutable outputs are prepared outside
measurement. Database/NATS IO, actual regime calculation and financial effects are excluded.
CompletedReplay is primed: the volatile side prepares/remaps/sends again, while the repaired side
verifies the saved source and suppresses all of that work. Small and representative synthetic
fixtures differ by admitted parameter content; neither is a live market-workload qualification.

Serialized source/hash-input sizes: **1,935 bytes small**, **2,044 bytes representative** (RequestBytes artifact keys refer to the source hash input).

| Payload | Case | Volatile mean | Frozen/pooled mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Small | New | 697.4 μs | 11,588.4 μs | 84.26 KB | 139.48 KB |
| Small | CompletedReplay | 638.2 μs | 427.8 μs | 83.54 KB | 16.88 KB |
| Representative | New | 877.5 μs | 10,147.4 μs | 85.38 KB | 140.62 KB |
| Representative | CompletedReplay | 900.7 μs | 311.2 μs | 84.48 KB | 16.86 KB |

All eight raw GC diagnostics recorded zero Gen 2 collections.
Durable IO adds cost and allocations. Short-iteration warnings/confidence intervals remain in the
full report; aggregate allocations are not direct LOH measurements. These short component probes
do not establish live GC, complete-pipeline two-second latency, burst capacity or full-session
throughput. Larger first-use pool rents, journal batching and sent-history retention remain open.

### Previous Regime completion identity blocker (repaired above)

An actual actor/local scheduler test runs a real WorkflowCalculationRuntime and decision executor.
Controlled initialization succeeds and the calculation returns a typed completed result (the
synthetic missing-observation fixture is an incomplete/no-trade regime result, not a demonstrated
tradable regime). A per-request immutable receiver accepts CompleteRegimeDiscoveryCommand but
loses its reply. On retry one second later, the runtime reuses the same calculated result and
ProducedAt/CompletedAt timestamp, yet CompleteRegimeDiscoveryPipeline maps a fresh version-7 event
ID. The caller derives a new completion command ID and source/causation IDs. The test verifies
identical serialized result bytes and completion time, different command/source IDs and rejection
of the changed terminal bytes. This proves caller-side completion remapping; it is not evidence
of an observed live duplicate financial effect. Persisted remote Function replay may retain its
committed ID, but the local cached-result mapping still has this gap.

Next work must durably freeze/recover successful Regime Discovery terminal publication, including
result identity, before send. Preserve existing result/owner/deadline guards and qualify uncertain
replies, retries and restart. Stage 1 stops at this confirmed blocker. Other successful/failed
results, callbacks and producer paths remain unqualified. Production mailbox stages 2–9, the
MarketOutlook policy and both full-session trials remain incomplete. No live profile restart,
financial correction, readiness weakening or backend cutover occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-build.log)
- [2,258 Trade checks](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-trade-tests.log)
- [10 abrupt-child cases](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-restart.log)
- [BenchmarkDotNet](../../.artifacts/realtime-mailboxes/20261125-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeFunctionBenchmarks-report-github.md)
- [Completion counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowRegimeCompletionTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-destination-audit-progress.json)

## Market Condition assessment Function-failure recovery - 2026-11-24

AssessmentFunctionTerminalDelivery now freezes the original FailMarketConditionCommand before
its first dispatch. WorkflowAssessmentFunctionFailures is keyed by the same deterministic Function
request identity (workflow ID, Market Condition stage and input revision). Unlike the prior selector
journal, its hash covers the exact original triggering WorkflowStrategyStateUpdatedEvent using
pooled streaming MessagePack/SHA-256. This includes the admitted view and any frozen binding.
Legacy preparation passes both its prepared assessment snapshot and its original source; retries
recover against that original source without resolving configuration again. Derived failure event,
command and source/causation IDs, failure category/evidence, timestamps, revisions, correlation and
creation provenance remain unchanged. Failure identity is not regenerated during recovery.

Matching active Market Condition sources recover before initialization/current-view work, and
ExecuteAssessmentAsync recovers before evaluating. Startup drains pending captures without source
redelivery. Successful owner replies are durably confirmed/archived; completed replay sends nothing.
Later terminal notifications retain their normal handling. Unknown replies still require exact
at-least-once resend and an idempotent owner. Existing workflow owner guards, failure mapping,
strategy rules and deadlines are unchanged; no new database input reads or in-decision retry loops
were added. Successful assessment results and Regime Discovery outcomes are not covered by this
failure journal.

The production-registered directory defaults to LocalApplicationData/IFM/workflow-assessment-function-failures,
overridden by AppSettings:WorkflowAssessmentFunctionRecovery:Directory. The journal uses exclusive
ownership, versioned/checksummed .waf binary frames, preserved MessagePack command/origin data,
flushed temporary writes and durable atomic replacement. Bounds remain 8 MiB/frame, 4,096 pending
and 128 cached records with file-backed sent history. History is not automatically pruned. Null
storage is volatile isolated-test scope. Changed input, corrupt frames, conflicting writers and
capture failures fail closed. No application JSON processing was introduced.

Verification:

- Release API/benchmark build: zero warnings/errors. **2,250 Trade checks passed** (2,249 repair/
  regression checks and the next blocker counterexample below). Nine focused assessment cases
  cover six real assessment-view send/typed-rejection/lost-reply combinations with metrics absent
  and present, original command bytes and origin across retry/day change/cold reconstruction,
  streaming hash parity, timeout classification recovery, later terminal handling, legacy prepared
  input recovery before configuration access, competing writers, failed capture before dispatch,
  changed/corrupt input and interrupted confirmed-marker archival.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed** using the actual
  workflow actor/local scheduler, failure mapping and file journal: send failure, typed rejection,
  accepted-but-lost reply, death after acceptance/before local confirmation, and completion before
  source ACK. Next-day startup and retained-source replay preserve original commands and provenance;
  completed replay does not resend. Two synthetic requests produce two ordered PostgreSQL receiver
  receipts keyed by workflow request scope, with original payload consistency enforced. Source
  MaxAckPending=1 prevents overtaking; NATS stays running during API-child death. These are synthetic
  idempotent terminal-owner checks, not production financial/projector or broker/power-loss proofs.

### Assessment Function failure BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 snapshots per invocation, no forced GC; tiering disabled only in the benchmark host. Values
are per snapshot. The actual actor/local scheduler and unavailable-local-runtime failure mapping
run on both sides. Before disables the journal; after includes durable capture/confirmation,
provenance and pooled source hashing. Typed owner replies are substituted. NATS/database IO,
actual assessment calculation and financial effects are excluded. Setup/priming/cleanup are
outside measurement. CompletedReplay is primed: before remaps/sends again, while after verifies
the source and suppresses evaluation/handoff. Representative sources use the real assessment
fixture with synthetic IDs for independent cases, not production owner admission.

Serialized source/hash-input sizes: **2,909 bytes small**, **3,201 bytes representative** (the size artifact retains RequestBytes field names).

| Payload | Case | Volatile mean | Frozen/pooled mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Small | New | 187.7 μs | 11,880.5 μs | 31.66 KB | 73.89 KB |
| Small | CompletedReplay | 207.8 μs | 274.4 μs | 31.45 KB | 12.53 KB |
| Representative | New | 185.2 μs | 11,703.7 μs | 31.66 KB | 73.65 KB |
| Representative | CompletedReplay | 197.9 μs | 309.6 μs | 31.25 KB | 12.48 KB |

All eight raw GC diagnostics recorded zero Gen 2 collections.
Durability adds write/allocation cost. Short-iteration warnings and confidence intervals remain
in the report; no small speed difference should be treated as a proven throughput gain. Aggregate
allocation bytes are not direct LOH measurements. Short component runs do not establish live Gen 2
or full-pipeline two-second guarantees, full-session throughput or burst capacity. Pooled hashing
avoids a contiguous source output array; first-use larger rents are still possible. Batching and
history retention remain open.

### Previous Regime Function-failure blocker (repaired above)

Continuing the actual actor/scheduler audit passed configuration validation and snapshot capture,
then mapped an unavailable local runtime through the Regime Discovery Function failure path.
FailRegimeDiscoveryPipeline creates a fresh version-7 failure event ID on each attempt; the
caller derives a different FailRegimeDiscoveryCommand/source ID and timestamp. A controlled
per-request immutable receiver accepts the first command but loses the reply. The same source
retried one second later produces a different terminal outcome and cannot settle at that receiver.
The regression asserts both unsuccessful deliveries, different terminal/source IDs and times,
accepted parameter-set identity and FunctionRequest evidence, excluding initialization failure.
This is not proof of live duplicate financial effects: authoritative workflow guards may reject
the later stale command.

Next work must freeze the original Regime Discovery Function terminal/result before dispatch,
then qualify lost replies, retries and restart with current guards intact. Stage 1 stops here.
Other successful/failed result, reservation/callback and producer paths still require qualification.
Production mailbox stages 2–9, MarketOutlook policy and both full-session trials remain incomplete.
No live API/profile restart, financial correction, readiness weakening or backend activation occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-build.log)
- [2,250 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowAssessmentFunctionBenchmarks-report-github.md)
- [Next Regime Function counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowRegimeResultTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-destination-audit-progress.json)

## Trade Selection Function-failure terminal recovery - 2026-11-24

The selector Function-failure replay blocker is repaired. SelectionFunctionTerminalDelivery freezes
the original FailTradeSelectionCommand before dispatch, keyed by the accepted Function request ID
and exact MessagePack request hash. It retains failure/time-out-labelled evidence, command and
source IDs, expected revision, timestamps, trace fields and original creation provenance, including
run/generation and request causation when supplied. Confirmed owner replies are durably marked and
archived; pending records resume on actor startup; completed replay sends nothing. Unknown send
replies can resend identical bytes and still require an idempotent owner.

Matching active regular-selector notifications recover before current-view fencing or evaluation;
direct ExecuteSelectionAsync calls also recover before evaluation. Terminal notifications and the
legacy ReservationPending branch retain their normal handling. Existing reply identity/fingerprint,
unknown Function persistence outcome and authoritative owner guards are unchanged. No decision
retry loop, database input read, strategy/deadline change, financial reservation or readiness
relaxation was added. Successful selector completions and other reservation/callback paths remain
outside this failure-terminal qualification.

WorkflowSelectionFunctionFailures defaults to LocalApplicationData/IFM/workflow-selection-function-failures,
overridden by AppSettings:WorkflowSelectionFunctionRecovery:Directory. It uses an exclusive writer,
versioned/checksummed binary frames, MessagePack command/origin data, flushed temporary writes and
durable atomic replacement. Bounds are 8 MiB/frame, 4,096 pending and 128 cached records with
file-backed sent history. History has no automatic pruning; null storage is volatile isolated-test
scope. Capture failures prevent dispatch; changed/corrupt input and competing writers fail closed.

Request verification reuses the exact pooled streaming MessagePack/SHA-256 algorithm from the
composition repair, now shared as WorkflowRequestHash. Buffers are cleared/returned on completion
or exceptions, and stored hashes match the old contiguous serialization algorithm. No application
JSON processing was introduced. This does not retrofit pooling into all earlier journal hashes.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,241 Trade checks passed**:
  2,240 functional/regression checks plus the new assessment Function counterexample below.
  Eight focused selector cases cover six real selector-request send/typed rejection/uncertain
  reply combinations with metrics absent/present; unchanged command, request hash and provenance
  after clock change and cold reconstruction; startup-only recovery; completed suppression;
  timeout-labelled failure reconstruction; later terminal/reservation notifications; corrupt or
  changed data; exclusive writer; failed capture before dispatch and confirmed-marker recovery.
  Existing composition tests verify the shared hasher's original-byte compatibility as well.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed** with actual
  actor/local scheduler, failure mapping and file journal: send failure, typed rejection, accepted-
  but-lost reply, death after acceptance/before the local marker, and completion before source ACK.
  Next-day startup replays exact original command bytes/IDs; completed retained-source replay
  sends nothing; two requests leave exactly two ordered receipts in the synthetic PostgreSQL
  owner. MaxAckPending=1 protects source order and NATS remains running. Process-case inputs are
  synthetic; full real selector fixture requests are separately exercised in unit tests. These
  are not production financial/projector or broker/power-loss proofs.

### Selector Function failure BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 snapshots per invocation, no forced GC; tiering disabled only in the benchmark host. Values are
per snapshot. Actual repaired actor/local scheduler and local-runtime-unavailable Function failure
mapping run on both sides. Before disables the journal; after includes durable capture/confirmation,
provenance and pooled request hashing. Current view and typed owner replies are substituted.
NATS/database IO, actual selector calculation, financial side effects and setup/cleanup are excluded.
CompletedReplay is primed before measurement: before maps/sends again, while after verifies the
saved request and suppresses evaluation/handoff. Representative payloads use the real selector
fixture with independent synthetic command IDs for benchmark cases, not real owner admission.

Serialized request sizes: **2,739 bytes small**, **30,661 bytes representative**.

| Payload | Case | Volatile mean | Frozen/pooled mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Small | New | 152.5 μs | 12,372.9 μs | 14.17 KB | 53.45 KB |
| Small | CompletedReplay | 107.0 μs | 303.2 μs | 13.95 KB | 11.29 KB |
| Representative | New | 119.7 μs | 12,074.4 μs | 14.15 KB | 61.92 KB |
| Representative | CompletedReplay | 113.3 μs | 440.3 μs | 13.77 KB | 19.75 KB |

All eight raw GC diagnostics recorded zero Gen 2 collections.
Durability adds write/allocation cost; replay still verifies/decodes evidence. Short-iteration
warnings and full confidence intervals remain in the report. Aggregate bytes/op are not LOH bytes;
short diagnostic intervals do not establish zero whole-workload/live Gen 2 collections. Pooling
avoids a contiguous request output array but large size hints can still rent larger buffers on
first use. No live API latency/GC improvement, full-pipeline two-second deadline, concurrency/burst
capacity or full-session qualification is inferred. Batching and history retention remain open.

### Previous assessment Function-failure blocker (repaired above)

Continuing the actual actor/scheduler audit reproduces an unfrozen terminal in Market Condition
assessment execution, separate from its repaired initialization-failure branch. A real assessment
request/profile is admitted to the Function path; unavailable local runtime is mapped to a failure.
That mapping creates a fresh failure event ID for each attempt, which also changes the derived
FailMarketConditionCommand ID and source/causation IDs, along with failure timestamps. A controlled
per-request immutable receiver accepts the original outcome but loses its reply; retry one second
later under the same source produces a different terminal outcome and is rejected by that receiver.
The test observes two unsuccessful deliveries, different command/source event IDs and different
timestamps. It does not claim a same-command-ID collision, actual duplicate financial effects or
an observed live lost trade; authoritative workflow guards may reject the later stale outcome.

Next work must freeze the original assessment Function terminal/result identity before dispatch
and verify uncertain replies, retries and restart with authoritative guards unchanged. Other
successful/failed results, timeout/reservation/callback and producer paths still require audit.
Stage 1 stops at this confirmed blocker; production mailbox stages 2–9, MarketOutlook policy and
both full-session trials remain incomplete. No live restart, financial correction, readiness
weakening or backend cutover occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-build.log)
- [2,241 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowSelectionFunctionBenchmarks-report-github.md)
- [Assessment Function counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowAssessmentResultTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-destination-audit-progress.json)

## Composition Function failure and timeout terminal recovery - 2026-11-24

The Order Composition Function-failure replay blocker is repaired, including the timeout terminal
variant. CompositionFunctionTerminalDelivery captures the exact FailOrderCompositionCommand or
TimeoutOrderCompositionCommand before its first dispatch. WorkflowCompositionFunctionTerminals
keys that outcome by the original accepted request CommandId and records the SHA-256 of its exact
MessagePack bytes. Failure/reason fields, timestamps, expected revision, request/command IDs and
trace fields are retained. Derived command provenance is captured before staging, including request
causation and replay clock/run/generation metadata when supplied, and restored without restamping.

Matching Started/OrderComposition notifications recover this outcome before current-view fencing
or Function evaluation. Direct DispatchAsync calls also recover before reading the current view.
Later terminal-state notifications are deliberately not short-circuited by a request's saved outcome,
preserving their normal stopped-workflow handling. Startup sends pending commands; successful owner
replies are durably marked/archived; completed replay sends nothing. Unknown replies can resend
identical bytes and still require an idempotent owner. Required send failures are outside calculation
catches and propagate to delivery settlement. No trading/financial guard, deadline or strategy rule
changed; no database input read, decision retry loop or financial reservation was added.

The exclusive-owner binary store defaults to
LocalApplicationData/IFM/workflow-composition-function-terminals, overridden by
AppSettings:WorkflowCompositionFunctionRecovery:Directory. A versioned/checksummed frame explicitly
tags failure versus timeout commands and preserves origin metadata. Flushed temporary writes and
atomic durable replacement use the existing file primitive. Bounds are 8 MiB/frame, 4,096 pending
and 128 cached records with file-backed history. Sent history is not automatically pruned; null
storage is volatile isolated-test scope. Corruption, identity/content reuse, competing writers and
failed capture fail closed. Successful completion terminals remain outside this failure/timeout
journal's qualification; the earlier preparation-failure journal remains separate.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,233 Trade checks passed**:
  2,232 functional/regression checks plus the new selector Function-terminal counterexample below.
  Eight focused cases cover six real composition-request failure combinations (send/typed rejection/
  uncertain reply, metrics absent/present), exact retry after clock changes, file reconstruction,
  startup-only recovery, completed suppression, unchanged origin/run/generation/causation, timeout
  capture and cold reconstruction, later terminal notifications, corrupt/changed input, exclusive
  writer, failed capture before dispatch and confirmed-marker recovery. Full-request hashes match
  the former contiguous MessagePack/SHA-256 algorithm byte for byte.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed** with actual
  actor/local scheduler, failure mapping and flushed journal: send failure, typed rejection,
  accepted-but-lost reply, death after acceptance/before the local marker, and completed-before-ACK.
  Next-day startup resumes original failure bytes/IDs, retained replay suppresses completed work,
  and two requests leave exactly two ordered terminal receipts in the synthetic PostgreSQL owner.
  MaxAckPending=1 protects retained source order; NATS stays running. Failure inputs in these
  process cases are synthetic; real fixture requests and timeout reconstruction are separately
  exercised in unit tests. This does not prove production financial/projector or broker power-loss safety.

### BenchmarkDotNet: durability and request hashing

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 snapshots per invocation, no forced GC; tiering disabled only in the host. Values are per
snapshot. Both sides run the actual repaired actor/local scheduler and local-runtime-unavailable
Function-failure mapping. Before disables the journal; after includes durable capture/confirmation
and provenance plus pooled request hashing. Current-view and typed command receivers are
substituted; NATS/database IO, actual financial calculation and directory setup/cleanup are excluded.
CompletedReplay is primed outside measurement: before maps/sends again; after verifies the captured
request and suppresses re-evaluation/handoff. Representative requests come from the real composition
fixture, with independent synthetic command IDs for the benchmark cases; this is a payload-size
comparison, not proof of real owner admission for every generated benchmark identity.

Serialized request sizes: **2,795 bytes small**, **252,199 bytes representative**.

| Payload | Case | Volatile mean | Frozen/pooled mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Small | New | 107.8 μs | 11,925.5 μs | 16.67 KB | 56 KB |
| Small | CompletedReplay | 121.6 μs | 386.9 μs | 16.42 KB | 11.33 KB |
| Representative | New | 163.1 μs | 13,023.4 μs | 16.65 KB | 85.09 KB |
| Representative | CompletedReplay | 126.6 μs | 1,149.2 μs | 16.24 KB | 40.43 KB |

The initial durable prototype allocated a contiguous representative request array for hashing,
which was large enough for the LOH. That prototype's measured report is archived. The final code
streams the exact MessagePack encoding into incremental SHA-256 through an ArrayPool-backed writer,
returning/clearing buffers on completion and exceptions. It does not produce a contiguous request
array. Larger size hints may still rent LOH-sized buffers on first use; warmed pooling is not a
claim of zero cold LOH allocation. Other existing journals' hash implementations were not changed.

| Representative request | Unpooled durable mean | Pooled durable mean | Unpooled allocation | Pooled allocation |
| --- | ---: | ---: | ---: | ---: |
| New | 11,990.3 μs | 13,023.4 μs | 331.19 KB | 85.09 KB |
| CompletedReplay | 1,311.5 μs | 1,149.2 μs | 286.57 KB | 40.43 KB |

All eight final raw GC diagnostics recorded zero Gen 2 collections.
These are short diagnostic intervals, not proof that the entire workload or live API has no Gen 2
collections. Full confidence intervals, short-iteration warnings and raw counts remain in the logs.
Aggregate bytes/op are not directly measured LOH bytes. New capture pays durable write cost; no live
API latency/GC improvement or full-pipeline two-second deadline is inferred. Batching, history
retention, capacity, burst/concurrency and both full-session qualifications remain open.

### Previous selector Function-failure blocker (repaired above)

The continued actual actor/scheduler audit reproduces the analogous unfrozen terminal in regular
Trade Selection Function dispatch (separate from repaired reservation rejection). A real selector
request is present, the local decision runtime is unavailable, and actual Function failure mapping
builds FailTradeSelectionCommand. An immutable in-memory receiver accepts it and loses the reply.
With the original active projection visible, retry one minute later uses the same command ID with
different FailedAtUtc/MessagePack bytes; the receiver detects the conflict and retry cannot settle.
The test observes two unsuccessful deliveries and equal command IDs with different content.
This is a controlled receiver counterexample, not evidence of a live lost trade or real financial posting.

Next work must freeze the original selector Function terminal, preserve result/trace identity and
qualify unknown replies, retries and restart with authoritative guards unchanged. Other result,
timeout/reservation/callback and successful completion paths still need audit. Stage 1 stops at
this confirmed blocker; production mailbox stages 2–9, MarketOutlook policy and both full-session
trials remain incomplete. No live restart, financial correction, readiness weakening or backend
cutover occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-build.log)
- [2,233 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-restart.log)
- [Final BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionFunctionBenchmarks-report-github.md)
- [Archived unpooled report](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-unpooled-github.md)
- [Selector Function counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowSelectionResultTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-destination-audit-progress.json)

## Trade Selection reservation-rejection recovery - 2026-11-24

The reported legacy ReservationPending rejection-terminal replay blocker is repaired. When the
Portfolio API rejects a reservation, the workflow freezes the original FailTradeSelectionCommand
before dispatch. Its command/source/causation IDs, expected workflow revision, failure time and
rejection evidence are saved with a hash of the triggering snapshot. Replay checks this journal
before current-view fencing, request expiry or another ReserveCompositionAsync call. It resumes
the recorded rejection rather than repeating a financial request or replacing its outcome.
Startup resumes pending terminals; owner success is durably marked/archived; completed replay
sends nothing. Uncertain replies can resend identical command bytes, requiring owner idempotence.
Financial/owner guards remain unchanged. No new reservation, decision retry loop, database input
read, strategy rule or readiness relaxation was added.

WorkflowReservationFailures is production-registered, defaulting to
LocalApplicationData/IFM/workflow-reservation-failures and configurable through
AppSettings:WorkflowReservationRecovery:Directory. Its exclusive-writer store uses MessagePack,
versioned/checksummed binary frames, flushed temporary writes and durable atomic replacement.
Limits are 8 MiB/frame, 4,096 pending and 128 cached records with file-backed history. Sent history
is not pruned automatically; null storage is volatile isolated-test scope. Failed capture prevents
dispatch; changed/corrupt records, capacity/write failure and competing writers fail closed.
This qualifies the rejection terminal, not successful/unknown reservation outcomes, timeout or
late callbacks, financial side effects, or all other Trade Selection terminals.

Verification:

- Release API/benchmark build: zero warnings/errors. **2,225 Trade checks passed**: 2,224 functional
  and regression checks plus the new composition Function-terminal counterexample below. Eight
  reservation recovery/storage checks cover failed sends and typed rejection with metrics absent/
  present, clock-advanced retry, file reconstruction and next-day startup after request expiry,
  completed suppression, source mismatch, corrupt frame, exclusive writer, failed receipt write
  before dispatch, and a confirmed marker left in pending storage. Explicit call-count assertions
  verify only the original reservation call occurs and cold-start recovery makes no reservation call.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: command-send
  failure, typed terminal rejection, accepted-but-lost reply, death after acceptance/before the
  local marker, and completed-before-source-ACK. The actual actor/local scheduler and flushed
  journal reconstruct the exact rejection terminal on cold startup with the clock advanced one
  minute. Two retained snapshots produce exactly two ordered, byte-consistent terminal receipts
  at the synthetic PostgreSQL owner. MaxAckPending=1 protects retained input order. The Portfolio
  rejection and terminal owner are substitutes, not real financial reservation/posting evidence.
  NATS stays running during child death; broker/power-loss and production mailbox claims are excluded.

### Reservation-rejection BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 snapshots per invocation, no forced GC; tiering disabled only in the benchmark host. Values
are per snapshot. Both sides run the actual repaired actor/local scheduler and rejected-reservation
branch, with substituted current view, Portfolio API and terminal owner. Typed terminal replies
cross the real EventActorContext guard. Before disables the journal; after includes flushed capture/
confirmation. NATS/database IO, actual financial state changes and directory setup/cleanup are
excluded. CompletedReplay is primed outside measurement: before repeats the rejected reservation
and terminal handoff, while after verifies saved input and bypasses both calls.

| Case | Volatile mean | Frozen rejection mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| New | 107.70 μs | 11,689.34 μs | 13.27 KB | 49.38 KB |
| CompletedReplay | 91.19 μs | 322.00 μs | 12.48 KB | 13.05 KB |

All four raw diagnostics recorded zero Gen 2 collections.
Durable capture adds write/allocation cost. Full confidence intervals, raw GC counts and any short
iteration warnings are retained in the report/log. Aggregate bytes/op are not LOH bytes; no live
API LOH/Gen 2 improvement, full-pipeline two-second deadline or production capacity qualification
is inferred. Earlier batching, journal/cursor throughput, history retention and full-session work
remain open. No live restart, financial correction, readiness weakening or backend cutover occurred.

### Previous composition Function failure blocker (failure/timeout recovery repaired above)

Continuing the actual actor/scheduler audit reproduces a separate volatile terminal in
ExecuteCompositionFunction.DispatchAsync. An accepted frozen composition request is present, but
the local decision runtime is unavailable. The real Function-result mapping constructs a failure
terminal and sends FailOrderCompositionCommand. An immutable in-memory receiver accepts it and
loses the reply. With the original projection still visible, retry one minute later rebuilds the
same command ID with a different FailedAtUtc/MessagePack payload; the receiver detects a conflict
and retry cannot settle. The test observes two unsuccessful deliveries and equal command IDs
with different timestamps/content. It uses the real request fixture/handler/mapping and a
controlled receiver; it is not evidence of a live lost trade or a completed financial composition.

This Function-result branch is separate from the repaired preparation/cache-unavailable failure
journal. Next work must freeze the original composition Function terminal, preserve result/trace
identity and qualify unknown replies/retry/restart with authoritative guards. Other unjournaled
terminal, successful/unknown reservation and callback paths still need audit. Stage 1 stops at
this confirmed blocker; production mailbox stages 2–9, MarketOutlook policy qualification and
both full-session trials remain incomplete.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-reservation-build.log)
- [2,225 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-reservation-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-reservation-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowReservationBenchmarks-report-github.md)
- [Composition Function counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowCompositionResultTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-reservation-destination-audit-progress.json)

## Market Condition initialization-failure recovery - 2026-11-24

The Market Condition initialization terminal replay blocker is repaired. The original
FailMarketConditionCommand is now frozen in a production-registered binary journal before dispatch.
It retains the source snapshot hash, command/causation IDs, expected revision, failure category,
timestamp and diagnostic evidence. Replay checks this saved outcome before current-view/stage
processing or initialization, so clock/cache changes cannot replace it. Startup resumes pending
commands; confirmed owner replies are durably marked and archived; completed replay sends nothing.
Accepted-but-lost replies may resend the identical command and require an idempotent owner.
Existing owner validation is unchanged. No decision retry loop, database input read, trading-rule
change, new financial reservation or readiness relaxation was added.

WorkflowMarketConditionFailures defaults to LocalApplicationData/IFM/workflow-market-condition-failures,
overridden by AppSettings:WorkflowMarketConditionRecovery:Directory. The single-owner store uses
MessagePack in versioned/checksummed binary frames, flushed temporary writes and atomic durable
replacement. Bounds are 8 MiB/frame, 4,096 pending and 128 cached records with file-backed history.
Sent history is not pruned automatically; null storage is volatile isolated-test scope. Receipt
write failures prevent dispatch; corrupt/changed records, capacity failures and competing writers
fail closed. This qualifies initialization failures, not all Market Condition assessment results,
other pipeline terminals or the production retained mailbox implementation.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,217 Trade checks passed**:
  2,216 functional/regression checks plus the new reservation-terminal counterexample below.
  Eight Market Condition recovery/storage checks cover failed sends and typed rejection with
  metrics absent/present, original terminal on retry after clock advance, file reconstruction,
  startup-only recovery, completed suppression, source mismatch, corrupt frame, exclusive writer,
  failed capture before dispatch and a confirmed marker left in the pending directory.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: send failure,
  typed rejection, lost reply after owner acceptance, death after acceptance/before the local
  completion marker, and completed-before-source-ACK. The actual actor/local scheduler and actual
  flushed journal replay original terminal bytes on next-day cold startup. Retained source replay
  suppresses completed work; two snapshots leave exactly two ordered, byte-consistent receipts at
  the synthetic PostgreSQL owner. MaxAckPending=1 protects retained source order. NATS remains
  running. This is not production workflow/projector, financial-owner or broker/power-loss proof.

### Market Condition initialization BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 snapshots per invocation, no forced GC and tiering disabled only in the benchmark host.
Table values are per snapshot. Both paths run the actual repaired actor/local scheduler and
initialization-unavailable result construction; typed replies cross the real EventActorContext
guard. Before disables the journal; after includes flushed capture/confirmation. Command receiver
and input availability are substituted; NATS/database IO, live strategy calculation and directory
setup/cleanup are excluded. CompletedReplay is primed outside measurement: before reconstructs
and resends the failure, while after verifies the saved source and suppresses its handoff.

| Case | Volatile mean | Frozen failure mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| New | 192.8 μs | 8,189.9 μs | 23.5 KB | 72.75 KB |
| CompletedReplay | 259.1 μs | 267.4 μs | 22.69 KB | 16.55 KB |

All four raw diagnostics recorded zero Gen 2 collections.
Confidence intervals, raw GC counts and any short-iteration warnings remain in the report/log.
Durable capture adds write/allocation cost; replay still verifies and decodes recorded evidence.
Aggregate bytes/op are not LOH bytes. No live API LOH/Gen 2 improvement, full-pipeline two-second
deadline, production capacity or latency improvement is inferred from this component benchmark.
Earlier batching, journal/cursor throughput, retention and full-session qualification remain open.

### Previous reservation-rejection terminal blocker (repaired above)

Continuing the actual actor/scheduler audit reproduces a volatile terminal in Trade Selection's
existing legacy ReservationPending branch. The controlled Portfolio API rejects the reservation.
The workflow sends FailTradeSelectionCommand with a deterministic ID and current FailedAtUtc.
An immutable in-memory receiver accepts the first failure terminal and loses its reply. With the
original projection still visible and the reservation request unexpired, retry one minute later
changes the terminal timestamp/payload under the same command ID; the receiver detects the conflict
and retry cannot settle. The test observes two unsuccessful deliveries, equal command IDs and
different timestamps/MessagePack bytes. The reservation and terminal receivers are substitutes;
this is not evidence of a live lost trade, duplicated reservation or financial-owner failure.

Next work must freeze the original reservation-rejection terminal and qualify uncertain replies,
ordering, retries and restart without changing financial guards or adding decision retry loops.
Other legacy reservation/late-callback and unjournaled calculation/result paths remain unqualified.
Stage 1 stops at this confirmed blocker; production mailbox stages 2–9, MarketOutlook policy
qualification and both full-session trials remain incomplete. No live restart, financial correction,
readiness weakening or backend cutover occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-market-condition-build.log)
- [2,217 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-market-condition-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-market-condition-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowMarketConditionBenchmarks-report-github.md)
- [Reservation-terminal counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowReservationTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-market-condition-destination-audit-progress.json)

## Order Composition preparation-failure recovery - 2026-11-24

The reported Order Composition preparation-failure replay blocker is repaired. ExecuteOrderComposition
now freezes the original FailOrderCompositionCommand before its first dispatch. The source snapshot's
binary hash, original command ID, failure timestamp, correlation/causation, reason and revision are
retained in WorkflowCompositionFailures. Source replay checks that journal before current-view
fencing or preparation, so changed clock/cache conditions cannot rebuild the recorded failure.
Startup resumes pending commands; successful owner replies are durably marked and archived;
completed replays do not send again. Uncertain replies can resend the identical command and still
require an idempotent authoritative owner. Owner validation is unchanged.

The production-registered journal defaults to LocalApplicationData/IFM/workflow-composition-failures,
overridden by AppSettings:WorkflowCompositionRecovery:Directory. It uses an exclusive writer lease,
MessagePack payload, versioned/checksummed binary frames, flushed temporary writes and atomic durable
replacement. Limits are 8 MiB/frame, 4,096 pending and 128 cached records with file-backed history.
Sent history is not automatically pruned; null storage is volatile isolated-test scope. Corruption,
changed source content, capacity/write failure and competing writers fail closed. No new database
read, decision retry loop, strategy rule, financial reservation or readiness relaxation was added.
This fixes the preparation/cache-unavailable failure branch; composition Function results, timeout
commands and preparation-acceptance handoffs are not blanket-qualified by these tests.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,209 Trade checks passed**:
  2,208 functional/regression checks plus the new Market Condition counterexample below. Eight
  composition recovery/storage checks cover sends and typed rejection with/without metrics,
  unchanged failure across clock advancement, file reconstruction and startup-only delivery,
  completed suppression, changed source, corrupt frame, exclusive writer, failed receipt write
  preventing dispatch and a confirmed marker left in pending storage. The original changed-payload
  counterexample is replaced by successful recovery checks.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: send failure,
  typed rejection, accepted-but-lost reply, death after acceptance/before the local marker, and
  completed-before-source-ACK. Actual actor/local scheduler and actual flushed failure journal
  reconstruct the original command on next-day cold startup. Retained source replay suppresses
  completed work; two snapshots leave exactly two ordered, byte-consistent terminal receipts at the
  synthetic PostgreSQL owner. MaxAckPending=1 protects retained source order. NATS remains running;
  this is not production workflow/projector, financial-owner or broker/power-loss qualification.

### Composition preparation BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
16 independent snapshots per invocation, no forced GC; tiering disabled only in the benchmark host.
Table values are per snapshot. Both paths use the actual repaired actor/local scheduler, actual
cache-unavailable preparation failure construction and the actual EventActorContext typed command
reply guard, with substituted current-view and command receivers. Before disables the journal;
after includes flushed capture/confirmation. NATS/database IO, live strategy calculations and
directory setup/cleanup are excluded. CompletedReplay is primed outside measurement: the volatile
path reconstructs/resends, while the frozen path verifies source content and suppresses the send.

| Case | Volatile mean | Frozen failure mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| New | 133.4 μs | 12,094.6 μs | 11.57 KB | 45.8 KB |
| CompletedReplay | 122.9 μs | 206.4 μs | 11.16 KB | 10.49 KB |

All four diagnostics recorded zero Gen 2 collections.
New durable capture costs substantially more and allocates more. Completed replay avoids another
owner request but still hashes/verifies/decodes the saved outcome. Confidence intervals and short
iteration warnings are retained in the report; these component timings do not prove live API latency,
LOH improvement or a two-second full-pipeline deadline. Aggregate bytes/op are not LOH bytes. Earlier
batching, throughput, history-retention and full-session capacity qualifications remain open.

### Previous Market Condition initialization blocker (repaired above)

Continuing the actual actor/scheduler audit reproduces another volatile terminal in
ExecuteMarketConditionAsync. On initialization failure it builds FailMarketConditionCommand with
a deterministic ID but current FailedAtUtc. A controlled immutable in-memory receiver accepts the
first terminal and loses its reply. Retrying the retained snapshot one minute later changes the
timestamp/MessagePack payload under the same command ID; the receiver rejects that conflict.
The test observes two unsuccessful source deliveries, two attempted commands, equal command IDs
and different timestamps/content. This is a synthetic receiver counterexample, not a live trade
loss or production financial-owner test.

Next work must freeze the original Market Condition initialization terminal and verify unknown
replies, ordering and restart recovery with unchanged authoritative guards. Other unjournaled
pipeline terminal/preparation handoffs still require audit. Stage 1 stops at this confirmed blocker;
production mailbox stages 2–9, MarketOutlook policy qualification and both full-session trials remain
incomplete. No live restart, financial correction, readiness weakening or backend cutover occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-build.log)
- [2,209 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionBenchmarks-report-github.md)
- [Market Condition counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowMarketConditionTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-destination-audit-progress.json)

## Regime initialization: frozen terminal handoff recovery - 2026-11-24

The initialization-command masking blocker is repaired. StartPipelineAsync completes before the
Regime calculation try block. A required initialization-failure command send therefore propagates
failure to the already-repaired actor delivery boundary; it is never reclassified into a different
Function-request failure. No handler retry loop, additional database read, trading-rule change or
readiness relaxation was added.

The registered production context now owns WorkflowInitializationTerminals, an exclusive-owner
binary journal. It freezes the exact FailRegimeDiscoveryCommand and the source snapshot's binary
SHA-256 before the first send. Failed/uncertain sends preserve command/causation IDs, parameter
references, timestamps and failure evidence. Source replay checks this journal before current-view
fencing or initialization, so later clock/configuration changes cannot replace the recorded outcome.
Successful owner replies are durably marked and archived; completed source replays do not resend.
Actor startup resumes pending commands without requiring the old source to arrive again. Shutdown
does not run recovery. Identical uncertain sends remain at least once and require an idempotent
command owner; production owner validation is not weakened.

The default directory is LocalApplicationData/IFM/workflow-initialization-terminals, overridden by
AppSettings:WorkflowInitializationRecovery:Directory. Null storage is explicitly volatile for
isolated tests only. Frames use MessagePack, a versioned binary envelope and SHA-256 checksum,
flushed temporary writes and durable atomic replacement. Limits are 8 MiB/frame, 4,096 pending
records and 128 cached records with file-backed history. Sent history is not automatically pruned.
Corrupt/mismatched records, exclusive-writer conflicts and capacity/write failures fail closed.
Missing legacy records are not evidence of previously captured outcomes. This is an initialization
terminal journal, not the production JetStream mailbox adapter or a universal pipeline result store.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,201 Trade checks passed**:
  2,200 functional/regression checks plus the executed next-blocker counterexample below.
  Eight initialization recovery/storage checks cover sends and typed rejections with/without
  metrics, unchanged original terminal on retry after clock advancement, real file reconstruction,
  startup-only recovery, completed suppression, source mismatch, corrupt records, exclusive writers,
  receipt-write failure before dispatch and a confirmed marker left in the pending directory.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: send failure,
  typed rejection, lost reply after owner acceptance, death after acceptance/before local marker,
  and completion before source ACK. They exercise the actual actor/local scheduler and the real
  flushed binary terminal journal. Next-day cold startup resumes the original terminal; retained
  source replay suppresses completed work; two source snapshots produce exactly two ordered,
  byte-consistent command receipts in the PostgreSQL receiver substitute. MaxAckPending=1 keeps
  later retained input behind the unacknowledged source. NATS stays running during child death.
  This is not production workflow projector, financial reservation or broker/power-loss qualification.

### Initialization handoff BenchmarkDotNet

.NET 10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
one invocation processing 16 snapshots, no forced GC and tiering disabled in the benchmark host.
The table is per snapshot. Both sides use the actual repaired actor/local scheduler and actual
initialization-unavailable result construction. Before uses the volatile/no-journal context;
after uses the flushed file journal. The comparison measures durable freezing/confirmation cost,
not the former erroneous conversion of a thrown send into a second failure command. Command
receiver and configuration/input availability are substituted; NATS, database IO and live strategy
calculation are excluded. Setup/journal directory creation and cleanup are outside measurement.
CompletedReplay is primed before measurement: the volatile path reconstructs/resends its terminal,
while the durable path verifies the captured source and suppresses the completed handoff.

| Case | Volatile handoff mean | Frozen terminal mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| New | 136.77 μs | 8,515.48 μs | 21.91 KB | 58.73 KB |
| CompletedReplay | 97.85 μs | 201.38 μs | 18.47 KB | 11.01 KB |

All four raw diagnostics recorded zero Gen 2 collections.
Full confidence intervals, any short-iteration warnings and raw GC counts remain in the report/log.
Aggregate bytes/op are not LOH bytes, and no live API LOH/Gen 2 improvement or two-second full-pipeline
qualification is claimed. Durable freezing adds write cost on new initialization failures; earlier
batched persistence, journal/cursor throughput, history retention and capacity work remain open.
Other terminal/calculation results, account callbacks, native generation fencing, multi-host writers,
legacy uncaptured inputs and the production retained mailbox remain outside this qualification.
No live restart, financial correction, readiness weakening or backend cutover occurred.

### Previous composition preparation-failure blocker (repaired above)

The continued actual workflow actor/scheduler audit reproduces volatile terminal reconstruction
in ExecuteOrderComposition. A cache-unavailable composition failure emits FailOrderCompositionCommand
using snapshot.Id as its command ID and the current clock for FailedAtUtc. A controlled immutable
receiver accepts that original terminal but loses the reply. With the original workflow projection
still visible, retry one minute later rebuilds the same command ID with different timestamp/payload
bytes. The receiver detects the identity/content collision and the retry cannot settle. The test
observes two failed source deliveries, identical command IDs, different FailedAtUtc and conflicting
MessagePack bytes. The receiver is an in-memory idempotent substitute; this is not an observed live
trade loss or a production financial-owner recovery test.

Next work must freeze the original Order Composition terminal before dispatch and recover it across
unknown replies, retries and restart, with the same authoritative-owner guards. Related volatile
pipeline terminals still require audit. Stage 1 stops at this confirmed blocker; production stages
2–9, MarketOutlook policy qualification and both full-session trials remain incomplete.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-initialization-build.log)
- [2,201 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-initialization-trade-tests.log)
- [10 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-initialization-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowInitializationBenchmarks-report-github.md)
- [Order Composition counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowCompositionTerminalAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-initialization-destination-audit-progress.json)

## Workflow dispatch: required actor failure settlement - 2026-11-24

The executed active-workflow-read/false-success blocker is repaired at the actor boundary.
IntrinsicTimeStrategyWorkflowRealtimeActor now opts into PropagateHandlerFailures. A required
handler exception is logged and propagated to delivery completion even without mailbox metrics;
the source is not reported successful merely because the failure hook finished. The existing
EventActorContext command boundary already throws for an unsuccessful typed command reply;
tests now exercise that real boundary as well as thrown read/send failures. No decision retry
loop, database access, strategy eligibility rule, deadline or financial reservation was added.

Successful terminal handoffs, superseded notifications and already-terminal notifications retain
their existing behavior. Existing per-mailbox serialization awaits the current command handoff
before processing a later notification. Source retention/retry in restart tests belongs to the
explicit JetStream driver, not a new production mailbox adapter. This boundary fix is not a
claim that every internal workflow catch/financial side effect is recoverable; the next branch
counterexample below remains blocking.

Verification:

- Release API/benchmark build: zero warnings/errors. **2,193 Trade checks passed**: 2,192 functional
  and regression checks plus the new initialization-handoff counterexample below. The previous
  active-read counterexample now expects failed settlement. Eight additional checks cover read,
  thrown command send and typed command rejection with metrics absent/present; retry and actor
  reconstruction preserve the stale-trigger terminal command identity/content. Same-mailbox
  async ordering, superseded notifications and terminal notifications are also verified.
- **10 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: required read
  failure, command-send failure, typed command rejection, terminal acceptance with lost reply,
  and completion before source ACK. The actual actor/local scheduler processes two retained
  stale-trigger snapshots in order; their original terminal command bytes/IDs are preserved and
  the synthetic PostgreSQL terminal receiver has exactly two ordered receipts. MaxAckPending=1
  prevents the successor from overtaking the unacknowledged input. NATS stays running during
  child death. This qualifies the retained-source/command boundary with an idempotent receiver
  substitute, not production workflow state/projectors or all calculation/financial replay.

### Workflow dispatch BenchmarkDotNet

.NET 10.0.10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups/eight measured iterations,
2,048 invocations per iteration, no forced GC; tiering disabled only in the benchmark host.
Actual local actor/scheduler and the same public workflow handler run on both sides. Before uses
the original non-propagating base-actor policy; the disabled original failure logger is represented
by a no-op hook. Both sides use disabled loggers. After uses the production actor with required
failure propagation. Reads/commands are substituted, and typed rejection crosses the actual
EventActorContext command-reply guard. No database, NATS IO, full pipeline calculation or retained
result journal is included. Success is a stale-trigger No Trade handoff, not a live market strategy.

| Case | Before mean | Required-settlement mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| Success | 82.16 μs | 77.07 μs | 9.19 KB | 9.19 KB |
| ReadFailure | 83.14 μs | 149.18 μs | 7.4 KB | 12.73 KB |
| CommandFailure | 144.41 μs | 178.40 μs | 14.14 KB | 19.49 KB |
| TypedRejection | 139.36 μs | 175.87 μs | 16.42 KB | 21.78 KB |

Successful allocation is unchanged (9.19 KiB/op); timing confidence intervals overlap, so this
is not a successful-path speedup claim. Error paths now perform the required exception propagation
and delivery-failure handling; their old success result was incorrect. Extra exception processing
increases failure latency/allocation. All eight raw GC diagnostics recorded zero Gen 2 collections;
Gen 0/1 counts and confidence intervals remain in the report/log. Aggregate allocation is not
LOH allocation. No full API latency, live LOH/Gen 2 or production throughput improvement is inferred.
Earlier file-journal/cursor batching, retention/capacity and full-session qualifications remain open.

### Previous initialization-handoff masking blocker (repaired above)

Continuing the same workflow audit reproduced an internal exception-masking branch independent
of the now-repaired actor boundary. ExecuteRegimeDiscoveryAsync calls
FailRegimeDiscoveryInitializationAsync inside its calculation try block. If that required terminal
send throws before acceptance, the broad catch treats it as a Function-request calculation error.
It then builds and successfully sends a different FailRegimeDiscoveryCommand and settles the
source true. The actual actor/scheduler test observes two terminal send attempts, different
command IDs and different failure types; the replacement contains the original send exception
and has failure type Command. The first intended terminal was not accepted.

This is a synthetic initialization-failure case, not evidence of a live lost trade. It disproves
faithful recovery of the original intended terminal handoff. The actor propagation flag cannot
detect a failure already swallowed and replaced inside the handler. Next work must separate
calculation errors from required command delivery, preserve original terminal identity/evidence
through uncertain sends, and verify accepted-but-lost replies, retries and restart recovery.
Related broad catches and volatile terminal construction need review before claiming full workflow
replay safety. Stage 1 stops at this confirmed blocker; production stages 2–9, MarketOutlook policy
qualification and both full-session trials remain incomplete. No live restart, financial change,
readiness weakening or production durable-mailbox activation occurred.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-settlement-build.log)
- [2,193 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-settlement-trade-tests.log)
- [10 restart cases](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-settlement-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowDispatchBenchmarks-report-github.md)
- [Initialization-handoff counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowInitializationHandoffAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/workflow-settlement-destination-audit-progress.json)

## Broker observation publication recovery - 2026-11-24

The consumed-failed-publication blocker is repaired for the registered emulator adapter. The
financial observation journal is now the authoritative order-publication queue. A persisted cursor
advances only after a confirmed JetStream actor publication. The application/framework broker
ports expose recoverable-observation capability and acknowledgement, forwarded through the actual
FrozenEmulatorOrderExecutionBroker decorator. Other adapters retain their existing default
capability=false; no restart guarantee is inferred for an adapter lacking durable observations.

The bridge retries the same fact before moving to its successor, using a cancellation-aware
100 ms pause on failures. This is background publication recovery, not a strategy-decision retry.
Original observation IDs, deterministic event/command IDs, evidence hashes and timestamps remain
unchanged. Lost send replies can republish the identical event: delivery is at least once, not
exactly once. The existing authoritative command's observation-ID/content-hash replay guard must
remain active. Explicit per-order reconciliation still publishes facts with the same identities;
it does not advance the global journal cursor out of order.

Cursor saves share the previously repaired financial transaction boundary. Failed/uncertain cursor
saves freeze the exact candidate and resolve against persisted before/candidate evidence. Unknown
or out-of-order acknowledgements fail; duplicate acknowledgements do not save again. Cash,
positions and execution facts are unchanged by publication recovery. Legacy heads without a cursor
replay their retained observations, rather than inventing proof of prior publication.

The former 4,096-slot order callback channel is replaced by a one-slot coalescing wake-up hint;
work remains in the committed journal, including after construction/restart. Financial mutations
do not block waiting for an absent order reader to drain a physical callback queue. There is one
order reader per ledger. Account callbacks retain their existing separate path and are not covered
by this order-publication qualification. The file-store capacity guard remains fail-closed.

Verification:

- Release API/benchmark build: zero warnings/errors. **65 broker checks** passed, including ordered
  acknowledgement, duplicate/no-write replay, 5,000 restored pending facts beyond the former
  channel cap, and all prior financial-checkpoint regressions.
- **2,184 Trade checks** passed: 2,183 functional/regression checks, including eight executed
  publication failure/retry/reconstruction cases, plus the next-blocker counterexample below.
  Tests cover 1/2/4 legs, failed sends, accepted-but-lost replies, before/after cursor-write failures,
  bridge stop/start and ledger reconstruction. Successors do not overtake a failed fact.
- **20 abrupt-process cases** passed: 1/4 legs, JetStream Memory/File, before publication,
  after confirmed send/before cursor, before cursor save, after cursor save/lost reply and fully
  completed drain. They use the real emulator, bridge and flushed financial file. A controlled
  publication adapter performs real confirmed JetStream byte publication; a verification consumer
  checks ordered original identities and byte-identical duplicate sends. Financial cash, positions
  and observations are unchanged, and the cursor survives reconstruction. Memory/File NATS stays
  running; PostgreSQL is provisioned but is not substituted for the financial checkpoint.

### Broker observation BenchmarkDotNet

.NET 10.0.10 / BenchmarkDotNet 0.15.8, Release, InProcessNoEmit, five warmups/eight measured iterations,
eight independent prepared ledgers per invocation, no forced GC and tiering disabled in the host.
Each table operation drains one filled order: four routed facts for one leg, ten for four legs.
Initial financial matching/seeding and verification are outside measurement. The actual bridge,
application facade and file store are used, with a synchronous substituted publication receiver;
NATS network and downstream domain execution are excluded. Both sides read the same current
committed-journal source; before freezes the original unsettled bridge behavior. This isolates
publication/cursor cost, rather than claiming a complete historical transport baseline.

| Order legs | Before mean | Recoverable publication mean | Before allocation | After allocation |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 178.7 μs | 14,517.9 μs | 29.25 KB | 41.9 KB |
| 4 | 249.9 μs | 38,753.8 μs | 63.61 KB | 95.99 KB |

The after path confirms four or ten durable cursor saves. It is substantially slower and allocates
more than the unsafe no-settlement baseline. Bounded cursor batching or an append journal may
reduce that cost, but are not implemented or qualified here. The old path cannot safely replace
durable settlement. The before cases have short-iteration warnings; their fine timing differences
are not production speedup claims. All four GC diagnostics recorded zero Gen 0/1/2 collections.
Aggregate allocation is not LOH allocation, and no live LOH/Gen 2 improvement is claimed.

Full-checkpoint history growth, the 256 MiB capacity guard, retention and high-rate throughput
remain open alongside previous routing-journal issues. These tests do not prove power-loss safety,
multi-host ownership, legacy missing provenance, native generation fencing, domain financial
projector completion or account-callback durability. No live restart, financial correction,
readiness weakening or production mailbox backend activation occurred.

### Previous workflow actor false-success blocker (boundary repaired above)

Continuing the 25-destination audit reproduced a required-dispatch failure at
IntrinsicTimeStrategyWorkflowRealtime. The real actor/local scheduler receives a committed
TradeSelection-stage notification. Its required ReadActiveWorkflowAsync call is injected to fail;
it is invoked once, no stage execution proceeds, yet IActorDeliveryCompletion receives success
when mailbox metrics are absent. BaseEventActor logs/catches the exception and this actor does
not opt into PropagateHandlerFailures. A retained source would therefore be eligible for ACK
without successful dispatch. This is an executed boundary counterexample, not a claim of an
observed live lost trade or an end-to-end production JetStream test.

Next work must make required workflow handoffs fail settlement independently of metrics and
verify typed failure results, ordering, retries, terminal-command identity and restart recovery.
Stage 1 stops at this confirmed blocker; production stages 2–9 and two full-session trials remain
incomplete. MarketOutlook's final destination/drop-policy qualification also remains open.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/broker-observation-build.log)
- [65 broker checks](../../.artifacts/realtime-mailboxes/20261124-safety/broker-observation-broker-tests.log)
- [2,184 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/broker-observation-trade-tests.log)
- [20 abrupt-process cases](../../.artifacts/realtime-mailboxes/20261124-safety/broker-observation-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerObservationBenchmarks-report-github.md)
- [Workflow counterexample](../../TomasAI.IFM.Domain.Trade.UnitTests/RealtimeDelivery/WorkflowDeliverySettlementAuditTests.cs)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/broker-observation-destination-audit-progress.json)

## Emulator financial checkpoint recovery - 2026-11-24

The failed-save/successful-replay financial blocker is repaired. All matching and financial
mutations now share one root checkpoint boundary. A failed save restores the last confirmed
visible state and freezes the exact candidate, including original execution IDs, fees, prices,
timestamps and cash. Account snapshots report incomplete and forbid new risk while unresolved;
execution/progress reads fail closed. The next mutation resolves the stored head against the
previous/candidate checkpoint and attempts the identical save once. Unknown heads or unavailable
evidence block progress. There is no retry loop or recomputation of an uncertain fill.

Exact quote receipts and order-revision/quote-set match receipts prevent a replay from filling
the remaining quantity again. Latest books and zero-match outcomes survive reconstruction.
Multi-order quote fan-out has one financial save boundary. Place, Modify, Cancel, explicit offline
fills and verified price corrections use the same transaction mechanism. Facts are exposed to
observation channels only after the financial checkpoint succeeds; financial rollback never
undoes an already committed fact because a later publication failed.

The file store streams legacy-compatible JSON into a bounded temporary file, flushes it and uses
durable atomic replacement. An exclusive owner lease prevents two upgraded file-store writers.
Legacy heads retain their original cash/positions/observations and upgrade on the next mutation.
No absent historical quote provenance is invented. An unsaved calculation is not a confirmed fill:
after a before-write process death, the retained input replays against the prior committed head.
Original market timestamps and freshness requirements remain unchanged.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **63 broker checks** passed,
  including before/after-write failures for 1/2/4 legs, uncertain/unreadable/foreign heads,
  same-instance retry, partial fills, zero-match books, multi-order atomicity, legacy loading,
  corrupt heads, exclusive writers and mutation/offline retry coverage.
- **2,176 Trade checks** passed: 2,175 regressions plus the executed next-blocker reproduction below.
- **18 abrupt child-process recovery cases** passed using the real actor, application broker,
  matching ledger and actual flushed financial file: 1/2/4 legs, Memory/File source storage,
  before-save, after-save/lost reply and completed-before-ACK. Two retained partial-fill inputs
  resumed in order; original persisted execution IDs, cash, positions and remaining quantities
  survived reconstruction, with no duplicate execution or pending source. NATS stayed running.
  Disposable infrastructure also provisions PostgreSQL, but financial evidence is the real file,
  not a PostgreSQL financial-receipt substitute.

### Financial checkpoint BenchmarkDotNet

.NET 10 Release, InProcessNoEmit, five warmups/eight measured iterations, no forced GC, tiering
disabled in the benchmark host. Each invocation processes 32 independent prepared ledgers/files;
the table is per quote operation. Setup, initial orders/prefix quotes and cleanup are excluded.
Before freezes the original ledger and unflushed file writer; after uses the real recoverable
ledger and flushed file store. These timings include financial checkpoint IO. Snapshots are small
and fixed per case; this is not a growing-history or full-session capacity test.

| Legs | Operation | Before mean | After mean | Before allocation | After allocation |
| ---: | --- | ---: | ---: | ---: | ---: |
| 1 | Fill | 1,215.772 μs | 3,602.832 μs | 10656 B | 7816 B |
| 1 | NoMatch | 3.432 μs | 3,660.610 μs | 688 B | 8736 B |
| 1 | Replay | 2.047 μs | 1.109 μs | 360 B | 113 B |
| 4 | Fill | 1,568.233 μs | 4,006.913 μs | 20096 B | 12848 B |
| 4 | NoMatch | 3.866 μs | 3,550.593 μs | 832 B | 9118 B |
| 4 | Replay | 1.852 μs | 1.249 μs | 360 B | 188 B |

Fills allocate 27–36% less but take longer because the replacement now confirms durable storage.
NoMatch previously only changed a transient book; it now writes the book and completed quote
receipt. That additional approximately 3.6 ms write is required by this implementation's recovery
contract and is a throughput concern. Completed replay avoids another financial write. Short
iteration warnings affect the microsecond/no-write cases; their small speed differences are not
a production latency claim. All 12 raw GC diagnostics reported zero Gen 0/1/2 collections. Aggregate
allocation is not LOH allocation, and no live LOH/Gen 2 improvement is inferred.

Receipt history is not pruned and full checkpoints grow; the store's 256 MiB guard fails closed.
Append/batched persistence, bounded retention and production rate qualification remain open,
along with earlier Futures/FuturesOption journal-throughput issues. Power loss, multi-host writers,
legacy uncaptured-input reconciliation, native generation admission and automatic offline-worker
restart recovery are not qualified. No live profile restart, financial correction, readiness
weakening or production mailbox activation occurred.

### Previous broker publication blocker (order recovery repaired above)

The real BrokerOrderObservationBridge catches a failed Execution publication, logs it and continues
consuming observations. A deterministic test with the real emulator and file store injects one
failed Execution send while later Commission/OrderCompleted facts succeed. The execution remains
in the financial checkpoint, but stopping/restarting the bridge with a healthy publisher and
processing a fresh acknowledgement does not retry it: one failed attempt, zero accepted execution
publications. This is missing automatic delivery recovery, not lost financial evidence;
explicit reconciliation can still retrieve the saved fact.

Next work must durably retain publication ownership, preserve original observation/event identity,
and resume failed/partial callback publication across retries and restart before activation.
Stage 1 stops at this reproduced blocker. Production mailbox stages 2–9 and both full-session
trials remain incomplete.

Evidence:

- [Build](../../.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-build.log)
- [63 broker checks](../../.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-broker-tests.log)
- [2,176 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-trade-tests.log)
- [18 real-emulator restart cases](../../.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-restart.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EmulatorCheckpointBenchmarks-report-github.md)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/emulator-checkpoint-destination-audit-progress.json)

## BrokerOrderRealtime: required failure settlement and quote leases - 2026-11-24

The BrokerOrderRealtime swallowed-exception counterexample is repaired at the actor boundary.
`PropagateHandlerFailures` is enabled, so a thrown required broker quote-processing failure reports
failed source delivery even with metrics absent. Both futures and option quote handlers retain their
existing mapping and filtering rules. A normal zero-match result remains successful; no new trading
policy, quote timestamp, size, price, epoch or source sequence is manufactured. There is still one
broker call per applicable input, not a handler retry loop.

The actor now releases decoded futures quote segments in a `finally` after the awaited broker call.
This lease is separate from the serialized IActorMessage payload. Releasing the transport payload
did not return the decoder's pool slot. A blocked call keeps its decoded segment until it finishes;
both success and failure return it exactly once. Retrying uses a fresh decode of the retained source,
not a released pooled array. Option observations contain no such quote-segment lease. Latest-tail
book selection is unchanged: a missing final bid remains non-applicable and does not reuse an older
book from the batch.

No actor-level quote journal was added. The end-to-end broker acceptance contract must be made
recoverable before an actor receipt could truthfully mark financial settlement. Source restart
ownership in the tests belongs to the explicit Memory/File driver, not the current Core/local queue.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,175 Trade checks passed**:
  2,174 regressions plus one executed emulator checkpoint counterexample. Tests exercise failed
  settlement without/with metrics, same-source pre-accept retries, exact quote mapping, awaited
  successor order for futures/options, original UTC/epoch/size fields, decoder leases held through
  async waits and released on success/failure, and a non-executable final book.
- **12 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: futures/option
  quote, both source-storage modes, and failed-before-acceptance, accepted-but-lost reply and
  processing-completed-before-ACK cases. Two retained input quotes resume in order, preserve the
  exact original market book bytes and leave no pending source. PostgreSQL substitutes an idempotent
  quote-acceptance receiver; these cases do not prove production broker financial idempotence.
- A separate real application emulator, real matching ledger and actual file checkpoint check passed
  for a committed full fill followed by a lost reply: reconstructing the ledger from disk and
  replaying the quote did not create a second execution or change the committed cash balance.
  This narrow full-fill result does not qualify partial fills or save failures.
- The continued actual-emulator save-failure test reproduces the next blocker below. Successful
  delivery-boundary tests must not be interpreted as blanket financial retry/restart safety.
  Broker restart/power loss, native generation admission, partial fills and callback publication
  remain unqualified. No live API restart, readiness weakening, order/balance correction, broker
  configuration change or production durable-mailbox activation occurred.

### BrokerOrderRealtime BenchmarkDotNet

.NET 10.0.10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, ten warmups, eight measured iterations,
4,096 invocations, unroll one, no forced GC. Tiered compilation is disabled only in the benchmark
host. Actual actor/local scheduler; broker quote processing is substituted, with no financial file
checkpoint or broker network IO. Before freezes the former actor and option-forwarding helper;
after measures independent failure propagation and quote cleanup. Callback/history cleanup is
outside measured iterations. Successful cases return zero matches on both sides. On thrown failures
the old actor incorrectly reports success while the repaired actor reports failure, so this is the
cost of correct settlement rather than equivalent successful business work.

The benchmark uses unowned two-quote input arrays. This prevents the frozen baseline's decoder-lease
leak from exhausting the process-global pool and contaminating later cases. Real pooled decoding
and await/failure lifetime are tested separately. Transport decoding, pool warmup and actual emulator
matching/file persistence are not included in these timings.

| Quote | Broker outcome | Before mean | Required-settlement mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Futures | Successful / no match | 46.39 μs | 48.05 μs | 4.32 KB | 4.32 KB |
| Futures | Throws before acceptance | 242.12 μs | 340.30 μs | 29.5 KB | 39 KB |
| Option | Successful / no match | 46.35 μs | 53.82 μs | 4.83 KB | 4.83 KB |
| Option | Throws before acceptance | 235.92 μs | 284.46 μs | 30.64 KB | 39.55 KB |

No Gen 2 collections were observed in these component diagnostics.
Successful-input allocation is unchanged in these probes. Failure propagation adds exception handling
and allocation; the old error path is unsafe and is not a valid faster alternative. Gen 0 and Gen 1
counts and timing confidence intervals are retained in the raw report. Aggregate bytes/op are not
LOH bytes, and no live API fragmentation/GC improvement or small-latency speedup is claimed. Earlier
Futures/FuturesOption synchronous journal-throughput and history-retention qualifications remain open.

### Previous emulator failed-checkpoint blocker (financial recovery repaired above)

The real `EmulatorLedger.TryMatch` changes filled units, cash, positions, journal and observation
history before `CommitAndPublish` saves its checkpoint. An injected failure before the actual file
save throws to the repaired actor, so the first source correctly fails delivery. However, the same
ledger instance already regards the order as filled. A replay finds no working candidate, returns
zero matches, performs no checkpoint save, and the source is now reported successful. Reconstructing
the ledger from the unchanged file loses that in-memory execution and cash change.

This is executed with the actual application facade, matching ledger and file checkpoint, not the
PostgreSQL quote-acceptance substitute:

| Evidence | Observed |
| --- | ---: |
| First source successfully settled | False |
| Same-source replay successfully settled | True |
| Executions in memory after failed save | 1 |
| Executions after file-backed restart | 0 |
| Checkpoint save calls during replay | 0 |
| Initial synthetic cash | $1,000,000.00 |
| In-memory cash after failed save | $724,986.85 |
| Cash restored from file | $1,000,000.00 |

These are synthetic emulator cash-accounting values, not live losses or strategy P&L. No real trade
was submitted or corrected. The fault wrapper throws immediately before the real store save; the
previous durable checkpoint remains unchanged. This directly disproves safe automatic financial
replay after a failed save, even though the actor's exception acknowledgement is now correct.

Required next work: make quote matching and financial checkpoint settlement atomic/recoverable,
resolve uncertain saves against persisted evidence, preserve original accepted execution/fact IDs,
and verify same-instance retry, reconstruction, partial fills and recoverable callback publication.
Do not mark a quote complete merely because a mutated in-memory order no longer matches. Stage 1
stops at this confirmed blocker; production mailbox stages and two full-session trials remain open.

Evidence:

- [Build log](../../.artifacts/realtime-mailboxes/20261124-safety/broker-order-build.log)
- [2,175 Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/broker-order-full-tests.log)
- [12 Memory/File cases and actual-emulator blocker](../../.artifacts/realtime-mailboxes/20261124-safety/broker-order-disposable-proof.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerOrderSettlementBenchmarks-report-github.md)
- [Updated destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/broker-order-destination-audit-progress.json)

## FuturesOption routing: retained position and snapshot branches - 2026-11-24

The failed-position-update acknowledgement counterexample is repaired for the option tick path.
`FuturesOptionRealtimeActor` now propagates required failures independently of optional metrics.
The existing request/reply service must return success with the exact original position command ID;
a failed result, exception or wrong ID leaves the source unfinished. The matching command IDs and
Iron Condor/Vertical Spread command destinations remain unchanged.

`FuturesOptionRoutingReceipts` captures the full original source, native dataset/generation metadata
and exact target commands before any position send or cache mutation. Each confirmed target is
recorded separately, so a retry or restart resumes unfinished targets rather than resending successful
siblings or discovering new targets for an old observation. Commands retain price, effective UTC,
source sequence, route generation and full OptionTick payload, including independent mark/risk,
pricing-completion revision, calculation time and source generation. Binary creation/causation
metadata is retained without JSON processing or restamping old observations.

Option ordering deliberately differs from the Futures sequence cursor: repeated and reset provider
sequences, including zero, remain separate arrivals. Only an exact source/observation identity is
idempotent. The key retains the existing command-base identity: source event ID plus option source
generation, observation revision and pricing-update flag. A quote and its pricing completion may
reuse a source ID and sequence while remaining separate inputs. Reusing the same observation
identity with different content fails closed. There is no new maximum-sequence filter that would
silently suppress reset or asynchronously priced observations.

Each contract has its own reference-counted asynchronous lane. It drains pending older work before
processing a newer arrival; unrelated contracts do not share a send lane. Startup loads authoritative
routes and seeds the existing cache from established trades/positions, then resumes pending receipts
before registering fresh delivery. Confirmed branches are skipped; closed or newer-generation routes
explicitly supersede old pending branches. Missing, behind or conflicting route evidence blocks.
The old observation is never re-fanned out to a new position or route generation.

Snapshot notification publication is retained too. After source/command capture, the existing volatile
cache applies the observation and produces a whole-trade display snapshot. The exact notification
IDs, payload, monitoring generation, revision, UTC time and origin are then durably captured before
any notification is sent. A send failure retains its branch, and confirmed sibling notifications are
not regenerated. An accepted-but-ambiguous send may repeat the same event ID/payload; it does not
invent a newer revision or timestamp. If a crash occurs before notification capture, only an unsent
volatile display preparation is recomputed from the retained original source. Replay of a captured
notification does not write that old snapshot back into a new monitoring-generation cache. Existing
snapshot acceptance/freshness rules remain unchanged. UI notification send completion is a transport
boundary, not proof every UI subscriber consumed it. Separate lifecycle `OpenPositionRoutesChanged`
publication paths are not newly qualified by the tick-path proof.

Routing health now follows completion of the entire retained source, including journal and required
publication failures. A successful sibling cannot clear a still-failed source. Authoritative retirement
clears failure health only after all branches settle. The existing thirty-second health threshold and
readiness policy are unchanged. Each delivery attempts each unfinished branch once; there is no
retry loop in a trading decision. Actor startup and broker redelivery own recovery attempts.

The new journal is an exclusive single-owner binary store with checksums, versioned/8-MiB-bounded
frames, 4,096 pending captures, a 128-frame file-backed LRU, flushed writes and atomic replacement.
Completed history is sharded and retained; automatic pruning and total-history capacity are not
implemented. Common API DI requires the store through `AppSettings:FuturesOptionRoutingRecovery:Directory`,
default `%LOCALAPPDATA%/IFM/futures-option-routing-receipts`; null storage is explicitly volatile for
isolated tests. This is separate from the Futures tick journal and does not change its frame format.
Corruption and failed capture/stage/archive writes fail source settlement. Multiple API hosts,
legacy uncaptured deliveries, financial transaction rollback and backup restoration remain unqualified.
No source sequence or timestamp is fabricated to make a receiver accept old data.

Verification:

- Release API/benchmark build: zero warnings/errors. **2,164 Trade checks passed**: 2,163 regressions
  plus one executed next-blocker counterexample. Tests cover absent-metrics failed settlement,
  same-instance retry, partial position fan-out, wrong acknowledgements, lost replies, successor
  blocking, cold recovery, exact command and notification bytes/origins, quote/pricing identities,
  reset/repeated sequences, corrupt captures, capture/archive write failure, authoritative retirement,
  behind-generation blocking and source-wide routing-health recovery.
- **Six focused integration checks passed**, covering replacement-context wiring with the same required
  shared journal, independent actor generations, retired-generation cache/dispatch protection and
  existing readiness/quote-health checks.
- **32 disposable PostgreSQL + JetStream Memory/File abrupt-process cases passed**: quote and pricing
  completion, both source-storage modes, and eight fault boundaries (failed/throwing command,
  accepted command/lost reply, capture before first position send, completion before source ACK,
  failed snapshot send, accepted snapshot/lost reply and capture before first snapshot send).
  Each case retained ordered sources, four unique position-command receipts and four unique snapshot
  receipts in 1:A,1:B,2:A,2:B order. Startup resumed exactly the unfinished position/notification
  branches; duplicate source delivery created no additional accepted records. Recovery advanced the
  cache clock one day and verified original frozen payload/origin bytes rather than restamping them.
- The router actor, local scheduler, route index, cache and flushed journal are real. PostgreSQL
  substitutes **idempotent position-command and notification acceptance ledgers**. It is not the
  production financial position transaction, projector, broker fills or Rust eGui subscriber. The
  explicit test envelope retains source headers. JetStream owns ordered inputs with explicit ACK
  and MaxAckPending=1 and stays up through API-child exit. Broker restart/power-loss, native epoch
  admission/receiver sequence handling, production durable-mailbox ownership and multi-host operation
  are not qualified by these fixtures. The running Core/local queue has not gained restart durability.

### FuturesOption routing BenchmarkDotNet

.NET 10.0.10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, tiered compilation disabled only for
the benchmark host; ten warmups, eight measured iterations, 64 invocations, no forced GC. Real actor,
scheduler and cache; one Iron Condor and one Vertical Spread target, with two snapshot notifications.
The repaired path includes the actual flushed file journal. Position requests and notification
transport are substituted. Before freezes the original handler, actor and internal route/log helpers.
Fresh uses new observation identities; Replay repeats a completed observation. The old router
reapplies/re-publishes replayed observations; the repaired router returns the retained completion.
This is routing overhead, not end-to-end financial execution or broker throughput.

| Observation | Input | Before mean | Durable routing mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Quote | Fresh | 172.3 μs | 25,630.9 μs | 37.37 KB | 296.58 KB |
| Quote | Replay | 175.6 μs | 228.1 μs | 37.36 KB | 39.66 KB |
| Pricing | Fresh | 201.4 μs | 25,123.9 μs | 37.33 KB | 303.28 KB |
| Pricing | Replay | 208.4 μs | 306.5 μs | 37.34 KB | 40.4 KB |

No Gen 1 or Gen 2 collections were observed in these component diagnostics.
Fresh routing performs six durable frame replacements for this two-position/two-notification case
(source capture, notification capture, two publication confirmations, two position confirmations),
plus terminal archival. Durability substantially raises new-input latency and allocation. This is a
correctness repair and **is not qualified for high-rate rollout**. Group/batched persistence, less
repeat serialization and bounded history retention still need performance qualification before
cutover. Aggregate allocation per operation is not LOH allocation; no live GC or fragmentation
improvement is claimed. Fast before/replay samples have BenchmarkDotNet minimum-iteration warnings;
use the recorded error/stddev rather than interpreting small differences as an established speedup.
No live API restart, readiness weakening, order/balance repair or backend activation occurred.

### Previous BrokerOrderRealtime quote-settlement blocker (actor boundary repaired above)

The continued audit exercised the real BrokerOrderRealtime actor/scheduler with metrics absent.
`PublishMarketQuoteAsync` throws before acceptance; the actor still reports successful source delivery.
The counterexample records one failed broker quote attempt, zero accepted quotes and a successful
source acknowledgement. A manually forced retry succeeds after recovery, but an acknowledged durable
source would not request it. This is a quote-handler settlement proof with a substituted broker,
not a claim that a real fill was lost or duplicated. Repair failure propagation independently of
metrics, then qualify quote-buffer ownership, partial broker/fill effects, idempotent settlement,
ordering and cold restart. Throwing from the handler alone is disproven as a sufficient ACK policy.
Stage 1 stops at this executed blocker; production JetStream mailbox stages and the two full-session
Memory/File trials remain incomplete. Other unqualified paths are not marked complete.

Evidence:

- [Release build](../../.artifacts/realtime-mailboxes/20261124-safety/option-routing-build.log)
- [Trade checks](../../.artifacts/realtime-mailboxes/20261124-safety/option-routing-full-tests.log)
- [Focused integration checks](../../.artifacts/realtime-mailboxes/20261124-safety/option-routing-integration-tests.log)
- [32 Memory/File restart cases](../../.artifacts/realtime-mailboxes/20261124-safety/option-routing-disposable-proof.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesOptionRoutingBenchmarks-report-github.md)
- [Destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/option-routing-destination-audit-progress.json)

## Futures tick routing: durable branch settlement - 2026-11-24

The previous Futures routing failure/cursor counterexample is repaired. `FuturesRealtimeActor`
propagates required handler failures independently of optional metrics. A rejected position command,
exception, missing or mismatched acknowledgement leaves the original source unfinished. Existing
command request/reply still confirms the exact original command ID; no financial command is changed
to fire-and-forget. The in-memory lookup no longer consumes the source cursor before dispatch.

`FuturesRoutingReceipts` captures the source and exact fan-out commands before the first send, then
records each confirmed branch. Retry resumes unfinished branches rather than dropping the source or
resending confirmed siblings. Commands retain their existing deterministic IDs, prices, sequences,
route generations, original effective UTC timestamps and binary origin/causation metadata. Source
dataset and generation headers are explicitly restored before capture. A successful source advances
the durable cursor only after every branch is confirmed or authoritatively superseded. Valid
zero-target routing decisions are also retained, so an old ignored source cannot acquire new targets
on delayed retry. There is one position-command attempt per unfinished branch per delivery, not an
internal retry loop.

Each contract has a reference-counted asynchronous lane, released when idle. An unfinished source
must settle before a newer source for that contract; unrelated contracts do not share an asynchronous
send lane. The shared route index now serializes its short synchronous dictionary operations and
retains stable route-array snapshots. No lock is held over a network await in that index.

Actor startup loads the authoritative open-route snapshot, then recovers pending captures before
registering fresh tick delivery. Confirmed branches are not sent again. A newer route generation or
an absent position in the authoritative snapshot explicitly retires an unfinished old branch; missing,
behind or conflicting route evidence otherwise blocks recovery. An old tick is never fanned out again
to newly discovered positions. This does not relax receiver validation or invent newer source
sequences. Cursor scope is exact contract, value date, source dataset and source generation. It is
not a qualification of native generation admission or downstream sequence reset behaviour.

The journal is exclusive single-owner binary file storage: checksummed/versioned frames, flush and
atomic durable replacement, 8 MiB maximum frame, 4,096 pending captures, 128-frame file-backed LRU.
Pending and completed records are separated; completed history is sharded, retained and not pruned.
The configured directory is `AppSettings:FuturesRoutingRecovery:Directory`, default
`%LOCALAPPDATA%/IFM/futures-routing-receipts`; null storage is explicitly volatile for isolated tests.
The production context requires the registered store. Capture, stage, cursor or archive write failure
retains delivery responsibility and fails the source. Corruption blocks recovery. This is not a
shared multi-host transaction, a bounded history-retention implementation, or a repair for uncaptured
legacy deliveries. The current Core/local source queue itself has not acquired restart durability.

Verification:

- Release API/benchmark build passed with zero warnings/errors. **2,147 Trade checks passed**:
  2,146 regression checks plus the executed next-blocker counterexample. Coverage includes real
  actor/scheduler failed settlement and retry, successful-sibling suppression, wrong IDs, lost replies,
  ordered successor blocking, cold recovery, exact bytes/origin, scoped durable cursor, corruption,
  route retirement/behind-generation rejection, independent contract lanes and capture/cursor/archive
  write failures. Existing command/position/plan tests remain passing.
- **Ten disposable PostgreSQL + JetStream Memory/File cases passed**: failed result, thrown failure,
  acceptance followed by lost reply, abrupt process exit after capture before dispatch, and successful
  completion before source ACK. Each case retained source order and four unique branch receipts
  (1:A, 1:B, 2:A, 2:B). Cold actor startup resumed precisely 0, 1 or 2 unfinished branches as expected;
  duplicate source delivery added no branch writes. Original payload and origin bytes were checked.
- These use the actual router actor, scheduler, route index and flushed journal. PostgreSQL is an
  **idempotent surrogate position-command receiver**, not the production financial transaction or
  projector. The test envelope explicitly retains source headers. NATS uses explicit ACK and
  MaxAckPending=1 and remains running through abrupt API-child exit. No broker restart/power-loss,
  real financial mutation, live feed generation fencing or multi-host ownership claim is made.

### Futures routing BenchmarkDotNet

.NET 10.0.10 Release / BenchmarkDotNet 0.15.8, InProcessNoEmit, tiered compilation disabled only in
benchmark host; ten warmups, eight measured iterations, 64 invocations, no forced GC. Real
actor/scheduler with two matching position targets; substituted successful command request/reply;
after includes the actual flushed file journal. Fresh uses new source IDs; Replay repeats a completed
source. Before is the frozen original handler/actor; the common route-index implementation is shared.
The separate failure regression proves that the old behaviour is unsafe. Fixture call-history cleanup
is outside measured iterations. This measures routing overhead, not database command execution or
end-to-end market latency.

| Input | Before mean | Durable fan-out mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| Fresh | 179.6 μs | 17,634.7 μs | 24.28 KB | 99.75 KB |
| Replay | 127.9 μs | 221.9 μs | 21.05 KB | 28.47 KB |

No Gen 2 collections were observed in these component diagnostics.
No live LOH/Gen 2 improvement is claimed; aggregate bytes/op are not LOH bytes. Fresh durable routing
adds four synchronous durable replacements for two branches (capture, two confirmations, cursor),
plus terminal archival, and allocates substantially more than the original router. The roughly
17.6 ms mean is a correctness cost, not a speedup: this implementation is **not qualified for
high-rate rollout**. Batched/group durable writes, less repeat serialization and bounded history
retention need throughput qualification before cutover. Short before/replay iterations generated
BenchmarkDotNet minimum-iteration warnings; use the supplied error/stddev values and do not infer
small latency differences from these samples. No API restart or backend activation occurred.

### Previous FuturesOption routing blocker (repaired above)

`FuturesOptionRealtime` already throws on a failed position-command ServiceResult, but its actor
still uses the base failure-settlement default. The executed actual actor/scheduler regression with
no metrics returns successful delivery for a rejected required route: one failed command, zero
accepted position updates, source acknowledged. A manually forced retry after receiver recovery
succeeds, but an acknowledged broker source would not request that retry. This differs from the
repaired Futures consumed-cursor bug: the option route lookup already disables sequence consumption.
Repair failure propagation independently of metrics, then qualify partial fan-out, immutable option
observation/pricing metadata, cache/publication side effects, ordering and cold restart. Simply
assuming the handler throw causes a NAK is disproven by the regression. Stage 1 stops at this blocker;
remaining destinations, production JetStream mailbox adapter and two full-session trials remain open.

Evidence:

- [Build log](../../.artifacts/realtime-mailboxes/20261124-safety/futures-routing-build.log)
- [Trade tests](../../.artifacts/realtime-mailboxes/20261124-safety/futures-routing-full-tests.log)
- [Memory/File restart proof](../../.artifacts/realtime-mailboxes/20261124-safety/futures-routing-disposable-proof.log)
- [BenchmarkDotNet report](../../.artifacts/realtime-mailboxes/20261124-safety/BenchmarkDotNet.Artifacts/results/TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesRoutingBenchmarks-report-github.md)
- [Updated destination audit](../../.artifacts/realtime-mailboxes/20261124-safety/futures-routing-destination-audit-progress.json)

## Daily futures immutable exit-intent ownership - 2026-11-24

Daily futures now durably claim the first authorizing exit command per exact position/route generation
before dispatch. Later position and clock observations find that intent and resume it instead of
calculating another plan or replacing the exit authority. Confirmation retains the exact original
workflow/command IDs, plan event, snapshot, timestamps and binary origin metadata. A confirmed or
retired intent suppresses further handoffs from that generation. Non-Daily Futures behaviour is
unchanged. The former Daily authority-conflict counterexample is now a passing regression: the
actual downstream owner accepts the retained original command as an already-started intent.

`DailyFuturesExitDispatch` uses the existing command request/reply path rather than treating a void
send as confirmation. Only success with the exact original command ID marks the journal confirmed.
Rejected, missing/incorrect replies, exceptions and ambiguous accepted requests leave the same intent
pending; the current source fails. There is one request attempt per delivery, not a retry loop inside
the decision. Confirmation means owner start acceptance, not a filled closing order. The existing
exit workflow still owns composition, risk, execution and completion. No owner validation was weakened,
no extra close-workflow IDs are generated, and entry thresholds, trailing rules and flatten time are
unchanged. Closing plans stay frozen at their first authority; close execution remains a separate flow.

`DailyFuturesExitIntents` is an exclusive single-host binary journal with checksums, versioned bounded
frames, flushed writes and atomic durable replacement. Pending capacity is 4,096, frame limit 8 MiB,
file-backed LRU 128. Confirmed/retired records are archived in sharded history and retained for delayed
duplicates; startup scans pending records only. No automatic history pruning is enabled. The store
is registered in common API services as `AppSettings:DailyFuturesExitRecovery:Directory`, defaulting
to `%LOCALAPPDATA%/IFM/daily-futures-exit-intents`. Null storage is explicitly volatile for isolated
tests. The production context requires a store. Multiple API hosts with independent journals are
not qualified; this does not add a shared database intent table or an atomic plan/journal transaction.

Actor startup recovers pending intents before fresh messages. It reads the current position from
TradeDb only for recovery: closed/superseded positions retire the old pending intent without a send;
missing, behind or conflicting identities block recovery rather than closing an unverified exposure.
The normal hot position/clock path reads the journal/cache and performs no new recovery database
lookup. An interrupted terminal archival finishes without dispatching again. The frame stays pending
on a failed durable write/confirmation. Corrupt pending frames block startup.

Scope limits remain explicit: a legacy workflow that started before this journal cannot have its
complete original handoff reconstructed by guessing. Its changed-authority owner rejection stays
fail-closed and requires authoritative reconciliation before activation. There is no live migration
or financial repair in this step. Retention/pruning, multi-host arbitration, backup rollback and full
financial close execution remain unqualified. No live API restart, order/balance mutation, readiness
relaxation or production mailbox backend activation occurred.

Verification:

- Release benchmark/API build passed with zero warnings/errors. All **2,132 Trade unit checks passed**.
  Tests cover first-authority reuse, same-source retry, cold recovery, later position/clock observations,
  unchanged original bytes across midnight, bad owner replies, capture failure before dispatch,
  corruption, and closed/superseded/missing/behind/conflicting recovery positions. The older Daily ATR
  regression now requires one confirmed owner request instead of four conflicting fire-and-forget
  sends. Existing settlement/indicator/position/exit tests still pass.
- **20 Memory/File abrupt-process cases passed** with actual Daily position/flatten-clock creation,
  actor/scheduler, flushed intent journal and actor startup recovery. Cases cover failed sends,
  accepted-but-lost replies, crash before owner dispatch, crash after owner commit before local
  confirmation, and processing completed before source ACK. A later observation in the same
  generation cannot replace authority. Recovery advances the clock one day and retains the exact
  original command bytes; one plan receipt, one owner receipt and no pending intent/source remain.
- The downstream **actual owner Execute/state/start-event mapping** runs in these cases and is restored
  from its saved start event on duplicate requests. PostgreSQL substitutes the financial plan commit
  and owner event persistence; it is not the production command actor/transaction/projector or broker
  fill execution. Current-position recovery evidence comes from the fixture, with guard cases covered
  separately. NATS Memory/File owns ordered input with explicit ACK/MaxAckPending=1 and remains up
  during abrupt API-child exit. Broker/power loss, production source ownership and full financial
  effects are not proven by these controlled tests.

### Daily exit-intent BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit; tiered compilation disabled only in
the benchmark host; ten warmups, eight measured iterations, 64 invocations, unroll one, no forced GC.
Actual actor/scheduler and, after the repair, actual flushed file intent journal. Function calculation
and owner transport/reply are substituted. Before is the frozen previous handler with correct failure
settlement but changing Daily authority; it emits a new plan/handoff on later observations. Its
surrogate accepts that handoff for timing, although the separate real-owner counterexample showed
the authority conflict. This is not a valid alternative financial strategy or a comparison of
production order execution speeds.

Fresh operations use distinct synthetic route generations and include first claim, request/reply
confirmation, terminal write and archival. Later observations use one confirmed generation and bypass
new plan/handoff work after repair. Logs/Function surrogates reset per iteration; journal state remains.
Every timed input must settle successfully. Frozen baseline logging is adapted to guarded ordinary
logging for its inaccessible generated helper; NullLogger disables that branch. These small synthetic
payloads, short iterations and workstation disk timings do not qualify maximum latency or full-session
throughput. Actual network/financial commit latency and startup recovery are not timed here.

| Input | Observation | Before mean | Frozen intent mean | Before allocation | Frozen intent allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Position | Fresh | 68.58 Î¼s | 7,885.21 Î¼s | 13.42 KB | 64.94 KB |
| Position | Later | 66.82 Î¼s | 114.61 Î¼s | 13.4 KB | 18.33 KB |
| Clock | Fresh | 79.20 Î¼s | 7,550.40 Î¼s | 14.49 KB | 65.88 KB |
| Clock | Later | 76.25 Î¼s | 119.07 Î¼s | 14.5 KB | 19.42 KB |

No Gen 2 collections were observed in the component diagnostics.
Durable first claims and confirmations add file-write/allocation cost. Later handling also has
binary-journal lookup/copy cost despite avoiding another calculation/handoff. No speedup, live LOH,
fragmentation or two-second deadline guarantee is claimed. Batching and full-session capacity still
need qualification before backend cutover.

### Next confirmed blocker: Futures tick routing acknowledges failure and consumes its replay cursor

`FuturesRoutingSettlementAuditTests` executes the real FuturesRealtimeActor, scheduler, startup route
snapshot and ContractIdRouteIndex with a substituted position-command service. A routed tick's required
ChangeTradeLegData send returns a failed ServiceResult. The handler only logs it, so source completion
reports success. TryRoute has already advanced the contract source-sequence cursor. After the send
service is made healthy, redelivering that exact tick is classified duplicate/out-of-order and no send
is attempted. The executed test observes two successful source settlements, only one failed attempt,
zero accepted position updates and one duplicate count. No live failure was induced.

Repair per-route required-delivery settlement and recoverable cursor/fan-out ownership together;
throwing after a failed send alone cannot recover a cursor already consumed by TryRoute. Preserve
stable command IDs, successful sibling deliveries and route-generation fencing across retries and
restarts. This repair is not implemented here. **Stage 1 stops at this concrete routing blocker.**
Other pending destinations and the complete policy worksheet remain unqualified. Production stages
2-9 and two matched full-session Memory/File trials remain incomplete; the mailbox backend is disabled.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`: `daily-exit-intent-build.log`,
`daily-exit-intent-tests.log`, `daily-exit-intent-recovery-tests.log`, `daily-exit-intent-regressions.log`,
`daily-exit-intent-restart.log`, `daily-exit-intent-benchmark.log`,
`daily-exit-intent-bdn/BenchmarkDotNet.Artifacts/results/`, per-run file captures, and
`daily-exit-intent-destination-audit-progress.json`.

## Futures position and clock failure settlement - 2026-11-24

`FuturesTradePositionRealtimeActor` now explicitly propagates handler failures independently of
optional mailbox metrics. Required plan-request exceptions, failed terminals and exit-command send
failures settle the source as failed. This applies to both FuturesPositionChanged and
FuturesPositionExitClock parsing paths. The previous counterexample now asserts failed completion.
The application change is the failure-propagation override only. Thresholds, trails, flatten time,
pricing/calculation, request fingerprints, command IDs and exit ownership rules are unchanged.

Verification:

- Release benchmark/API build: zero warnings/errors. All **2,119 Trade unit checks passed**.
  New tests cover both input kinds with/without metrics across plan exceptions, failed terminals,
  committed-but-lost replies, exit-send failure and ambiguous accepted sends. They verify matching
  request fingerprints, original completed plan and exit-command bytes, ordered handoffs, no early
  acknowledgement and non-exit plans. A later request clock remains compatible with the actual
  restored Function state when parameters are unchanged, because RequestedAtUtc is excluded from
  the existing fingerprint. Original committed plan times are retained.
- **24 Memory/File abrupt-process cases passed**: both input kinds across required request throws,
  failed terminals, lost committed replies, pre-acceptance exit failure, ambiguous accepted exit,
  and processing completed before source ACK. Actual actor/scheduler and command construction;
  explicit ACK/MaxAckPending=1 retains the original source ahead of its successor. Recovery advances
  the clock one day with parameters fixed. Ordered revisions/accepted handoffs 1,2, two PostgreSQL
  surrogate plan receipts and two exact-byte exit receipts remain per case, without pending input.
- The restart/benchmark Clock inputs are synthetic **legacy position snapshots without Daily entry
  evidence**, exercising the second parsing/dispatch path and per-completed-plan exit IDs. They do
  not qualify the Daily per-generation exit identity across newer plans. A separate unit check uses
  the actual DailyFuturesFlattenService.Create source and verifies that retrying the same Daily clock
  observation after an ambiguous exit send preserves its original command bytes. The subsequent
  Daily observation conflict is the next executed blocker below.
- Function financial calculation/commit and exit-command acceptance use substituted PostgreSQL
  ledgers, not production position/order rows or real close execution. The NATS broker remains up
  during abrupt API-child exit. Parameter changes, actual generation recovery, broker/power loss,
  production financial close execution and current local/Core source ownership remain unqualified.
  No live restart, order/balance mutation, readiness relaxation or backend cutover occurred.

### Futures settlement BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit; tiered compilation disabled only in
the benchmark host; ten warmups, eight measured iterations, 4,096 invocations, unroll one, no forced GC.
Real actor/scheduler, completion and disposal with optional metrics absent; Function receipt and
exit acceptance substituted. Repeated successes use an idempotent synthetic completion. Clock inputs
use the legacy snapshot noted above. This is not fresh financial calculation/commit throughput or
real exit execution. Setup verifies the original erroneous success versus repaired failed settlement.
Observations and surrogate receipts reset per iteration. The frozen original actor uses guarded ordinary
logging for its inaccessible generated diagnostic helper; NullLogger disables that branch on both sides.
The shared handler is unchanged between versions.

| Input | Plan request | Before mean | After mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Position | Completes; exit handoff | 91.26 Î¼s | 96.98 Î¼s | 12.48 KB | 12.49 KB |
| Position | Throws | 141.73 Î¼s | 146.21 Î¼s | 10.31 KB | 12.62 KB |
| Clock | Completes; exit handoff | 85.41 Î¼s | 88.58 Î¼s | 12.47 KB | 12.47 KB |
| Clock | Throws | 117.58 Î¼s | 161.78 Î¼s | 10.3 KB | 12.6 KB |

No Gen 2 collections were observed in the component diagnostics.
Failure means compare erroneous successful settlement with explicit failed delivery and diagnostic
cost. Success differences are not a claimed throughput improvement from a one-line repair. Confidence
intervals and synthetic inputs limit precision. No maximum latency, live LOH/fragmentation, production
financial throughput or two-second deadline guarantee follows from these component measurements.

### Historical Daily exit-authority blocker: repaired for journaled intents above

`DailyFuturesExitReplayTests.Audit_later_daily_exit_plan_reuses_the_handoff_id_but_the_owner_rejects_changed_authority`
executes the actual actor/scheduler with two actual Daily flatten-clock sources constructed by
DailyFuturesFlattenService.Create for consecutive position sequences in the same route generation.
The Function completion/transport are substituted. Both supplied plans require exit and have distinct
completion IDs. The production Daily handler derives the exit-workflow and command IDs from position
and route generation, so both handoffs have **the same IDs but different serialized exit plans**.

The test then uses the actual downstream StartFuturesExitPositionWorkflow.Compute mapping, creates
its start event and restores FuturesExitPositionWorkflowCommandState from that first synthetic
commitment. The first start is valid; the later handoff is rejected with
`EXIT.WORKFLOW.ALREADY_STARTED` because its authorizing SourcePlanEventId differs. This is an
executed counterexample, not a live trade observation or an assertion that actual financial exits
completed. Same-observation retry remains stable; the problem is a later exit-required plan while
the prior close intent is still active.

Before cutover, durably retain/claim the original authorizing exit plan and handoff per Daily position
generation and continue that exact intent across ticks, clock observations, retries and restarts.
Do not mint separate close workflows for every plan or weaken the owner's immutable-authority check.
That separate financial-intent repair is not implemented in this settlement step.

**Stage 1 stops at this concrete Daily exit-intent blocker.** Other pending destinations, including
Futures/option tick routing, remain unqualified; no all-inventory completion is implied. The Futures
exit-workflow Realtime handler was inspected and currently performs no required effect; that is not
qualification of the actual financial executor. Production stages 2-9 and two matched full-session
Memory/File trials remain incomplete. The production mailbox backend remains disabled.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`: `futures-settlement-build.log`,
`futures-settlement-tests.log`, `futures-settlement-regressions.log`, `futures-settlement-restart.log`,
`futures-settlement-benchmark.log`, `futures-settlement-bdn/BenchmarkDotNet.Artifacts/results/`
and `futures-settlement-destination-audit-progress.json`.

## Vertical Spread position failure settlement - 2026-11-24

`VerticalSpreadTradePositionRealtimeActor` now explicitly propagates handler failures, independently
of optional mailbox metrics. A required Trade Plan request exception, failed terminal or exit-command
handoff exception fails delivery instead of being logged and acknowledged as success. The former
counterexample now asserts failed settlement. The application change is the failure-propagation
override only: plan calculation, parameters, fingerprints, command IDs, exit criteria and handoff
construction are unchanged.

Verification:

- Release benchmark/API build passed with zero warnings/errors. All **2,090 Trade unit checks passed**.
  Fifteen new functional checks cover both metrics modes across request exceptions, failed terminals,
  committed-but-lost Function replies, exit sends before/after acceptance, non-exit plans, no early
  ACK, matching command fingerprints and identical exit handoff bytes. The previous settlement
  counterexample is converted to the required failure regression. Another new test reproduces the
  next destination's blocker below.
- **12 Memory/File abrupt-process cases passed** using actual actor/scheduler and command construction:
  required request exception, failed terminal, committed-but-lost reply, failed exit send, ambiguous
  accepted exit send and processing completed before source ACK. Retained source is redelivered
  before its successor. Recovered revisions and accepted exit handoffs remain 1,2; exactly two
  PostgreSQL surrogate plan receipts and two exit-command receipts remain per case, with no pending
  input. Duplicate exit-command acceptance checks identical serialized bytes under the same ID.
- Parameters remain fixed in these proofs. Recovery advances the clock one day: unlike the Iron
  Condor Forward Price request, Vertical Spread's current fingerprint excludes RequestedAtUtc.
  The later request still matches the actual restored Function state and returns the original
  committed completion, preserving original plan time and deterministic exit command bytes. This
  does not qualify changing parameters/defaults during replay or freeze all request fields.
- Function calculations/financial commits and exit-command acceptance are substituted with PostgreSQL
  ledgers, not production positions/orders or real downstream close execution. Broker ordering uses
  explicit ACK/MaxAckPending=1 and the NATS broker stays running across abrupt API-child exit.
  These checks do not make the current local/Core mailbox durable or impose its failure fence.
  Production durable dispatch, real generation recovery, broker/power loss, financial close execution,
  legacy in-flight operations and parameter-change replay remain unqualified. No live restart,
  order/balance mutation or readiness relaxation occurred.

### Vertical Spread settlement BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit; tiered compilation disabled in the
benchmark host; ten warmups, eight measured iterations, 4,096 invocations and unroll one; no forced GC.
Real actor/scheduler, completion and disposal; optional metrics absent; substituted Function receipt
and exit acceptance. Repeated successful requests reuse an idempotent synthetic completion. This
is not the cost of computing/committing a fresh financial plan or executing a real exit. Setup
requires the old erroneous success and repaired failed settlement when the plan request throws.
Function receipts/request observations reset between iterations. A frozen previous actor supplies
the baseline; its inaccessible generated diagnostic helper uses guarded ordinary logging instead.
NullLogger disables that branch in both versions. The shared handler is unchanged between versions.

| Plan request | Exit required | Before mean | After mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Completes | False | 71.48 Î¼s | 74.24 Î¼s | 9.12 KB | 9.12 KB |
| Completes | True | 76.71 Î¼s | 77.51 Î¼s | 12.6 KB | 12.6 KB |
| Throws | False | 120.15 Î¼s | 178.83 Î¼s | 10.35 KB | 12.66 KB |
| Throws | True | 121.13 Î¼s | 147.12 Î¼s | 10.35 KB | 12.66 KB |

No Gen 2 collections were observed in the component diagnostics.
Failure timings compare erroneous successful settlement with explicit failed delivery and its
diagnostic costs; the configured exit flag is not reached when the request throws. Successful
timing differences are not evidence of a throughput improvement from this one-line repair.
Confidence intervals and synthetic inputs limit precision. These are component means, not maximum
latency, live LOH/fragmentation, fresh financial throughput or a two-second deadline qualification.

### Historical Futures settlement blocker: repaired above

The continued required-work audit executes `FuturesPositionSettlementAuditTests` against the actual
`FuturesTradePositionRealtimeActor` and scheduler, with optional metrics absent. Its required
`UpdateFuturesTradePlan` request throws exactly once, but source delivery completion reports
**success**. This actor also lacks explicit failure propagation; its exception hook logs and returns.
The counterexample records an undesired outcome, not successful recovery qualification. No live
failure was induced. Repair that required-work boundary and qualify its position/clock/exit paths
before it can use a durable mailbox safely.

The Vertical Spread exit-workflow Realtime handler was inspected: it currently validates context
and returns CompletedTask without a required effect. That is not downstream financial recovery
qualification. Futures Option routing and the other pending destinations remain unqualified; this
step does not claim the whole inventory has passed because a later required-work counterexample
was found. **Stage 1 stops at the concrete Futures position blocker.** Production stages 2-9 and
two matched full-session Memory/File trials remain incomplete. No backend activation/cutover occurred.

Artifacts: `.artifacts/realtime-mailboxes/20261124-safety/vertical-settlement-build.log`,
`vertical-settlement-tests.log`, `vertical-settlement-regressions.log`, `vertical-settlement-restart.log`,
`vertical-settlement-benchmark.log`, `vertical-settlement-bdn/BenchmarkDotNet.Artifacts/results/`
and `vertical-settlement-destination-audit-progress.json`.

## Iron Condor recoverable plan publication - 2026-11-24

The position context no longer relies on `cache.Apply` changing a value to retry a notification.
`IronCondorPlanPublications` captures the exact notification ID, snapshot, timestamp and binary
message-origin metadata before transport dispatch. Only successful producer completion followed by
durable completion/archival clears pending work. Failed or ambiguous sends retain the same event
identity and bytes for replay. The original failed-cache-publication counterexample is now a passing
regression: retry sends again despite unchanged cache, then completed replay sends nothing further.

Pending publication is drained before a newer publication for that trade. Per-trade async gates keep
an unrelated trade moving when one producer send is slow. Actor startup drains retained notifications
before accepting fresh messages; failure prevents successful startup. Old position/plan revisions
cannot publish newer cache contents as old observations. Existing plan-display suppression remains.
A missing cache or established trade now fails rather than returning successful plan publication.
The closing flag is recorded after confirmed publication, so a failed initial outbox write cannot
cause a retry to skip its uncaptured exit-plan notification. This changes no financial exit criteria,
order creation or pricing. Retired-generation guards and financial request fingerprints are unchanged.

The outbox is an exclusive-owner subdirectory, `publications`, under the configured persistent
Iron Condor request-capture directory. Versioned binary frames use checksums, flushed writes and
atomic durable replacement. Pending work is capped at 4,096 frames, each at 8 MiB; the file-backed
cache holds at most 128 frames. Sent frames remain in sharded disk history for duplicate recognition;
they are not all loaded at startup. Automatic history pruning is not enabled: retention/disk growth
must be qualified against the future source-retention policy. Corruption, write and archival failures
block progress. A completion write interrupted before durability can resend the same event; a durable
sent marker with interrupted archival finishes archival without resending.

**Completion means producer SendAsync completed, not subscriber processing or UI rendering.** The
existing Notify transport is Core NATS. Ambiguous sends are at least once with a stable event ID;
exactly-once subscriber effects or broker-loss durability are not established by this change.
Old uncaptured notifications that were already lost cannot be reconstructed by the new outbox.

Verification:

- Release benchmark/API build passed with zero warnings/errors. **2,074 Trade unit checks passed**.
  Coverage includes failed and ambiguous sends, immutable payload/ID/time/origin, cache-held retries,
  startup replay, ordering behind pending sends, both completion-write and archival failures, corrupt
  pending records, no send before durable capture, no source ACK before transport completion, changed
  payload conflicts, obsolete revision skips and independent progress for another trade.
- **20 Memory/File abrupt-process cases passed**: both position and monitoring inputs across pre-send
  failure, accepted-but-failed send, completed-before-source-ACK, exit just before send and exit just
  after acceptance before the sent marker. The real actor/scheduler, financial request journal,
  production publication context/cache, file outbox and actor startup recovery execute. Recovered
  notifications retain original IDs and bytes; accepted notifications and source sequences remain
  ordered; two surrogate plan receipts remain with no pending source or publication per case.
- Function calculation/commit and producer acceptance are substituted with PostgreSQL receipt and
  notification ledgers. The ledger verifies exact bytes when the same ID is sent again and deduplicates
  ambiguous sends. It is not the production financial repository or real subscriber. JetStream
  Memory/File owns the input with explicit ACK/MaxAckPending=1 while NATS remains running across
  abrupt API-child exit. Live UI delivery, broker/power loss, production financial replay, expired
  Forward Price request reconciliation and real generation authority remain unqualified. No live
  process was restarted, and no financial balances/orders or readiness validation were changed.

### Iron Condor publication BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit; tiered compilation disabled in the
benchmark host; ten warmups, eight measured iterations, 64 invocations and unroll one; no forced GC.
Both sides execute the same real actor/scheduler and flushed financial-request journal. Before uses
the frozen previous cache-driven publication context; after uses the production context/cache and
actual flushed publication outbox. Function services and transport producer are substituted.

Fresh calls use unique sources and include request-journal writes plus notification preparation,
sent-marker writes and archival on the repaired side. Position inputs have position and plan phases;
monitoring inputs have a plan phase. Replay repeats completed requests/notifications. Observation
logs and Function surrogates reset per iteration; journals/cache are retained. Every measured call
must settle successfully. The baseline's inaccessible close-intent method is an explicit unsupported
branch; its dispatcher is null and that branch is not used in these measurements. Startup and all
failure tests are separate from timing. These are synthetic small-position component means, not
fresh financial computation/commit throughput, maximum timings or a two-second deadline qualification.

| Source | Operation | Before mean | Recoverable mean | Before allocation | Recoverable allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Position | Fresh | 13,137.5 Î¼s | 29,023.8 Î¼s | 107.95 KB | 183.94 KB |
| Position | Replay | 260.7 Î¼s | 610.8 Î¼s | 24.93 KB | 46.79 KB |
| Monitoring | Fresh | 11,824.2 Î¼s | 20,371.6 Î¼s | 100.34 KB | 138.35 KB |
| Monitoring | Replay | 247.8 Î¼s | 397.7 Î¼s | 25.15 KB | 37.39 KB |

No Gen 2 collections were observed in the component diagnostics.
The incremental file writes and allocations are correctness costs, not a throughput improvement.
Confidence intervals, synthetic payload size and workstation disk behaviour limit extrapolation.
No live LOH/fragmentation improvement is claimed. Batching, retention and matched full-session
throughput still require qualification before enabling the planned mailbox backend.

### Historical Vertical Spread settlement blocker: repaired above

The continued destination audit moved past Iron Condor publication. The Iron Condor exit-workflow
Realtime handler is currently a no-op, so no required work is qualified there by inventing a failure.
The next actual required-work blocker is `VerticalSpreadTradePositionRealtimeActor`.

`VerticalSpreadSettlementAuditTests` executes the real actor and scheduler without optional mailbox
metrics. Its required `UpdateVerticalSpreadTradePlan` Function request throws an injected transport
exception. The handler reaches that required request exactly once, but delivery completion reports
**success**. The actor has no explicit failure-propagation override and its exception hook only logs.
This executed counterexample is a failure reproduction, not desired successful behaviour. No live
failure was induced. Repair required-work settlement independently of metrics, then qualify retries,
ordering and restart recovery before this destination can use a durable mailbox safely.

**Stage 1 stops at this concrete Vertical Spread blocker.** Production stages 2-9, the full destination
policy worksheet and two complete matched Memory/File sessions remain incomplete. The production
JetStream mailbox backend remains disabled; no cutover or readiness relaxation occurred.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`: `condor-publication-build.log`,
`condor-publication-tests.log`, `condor-publication-regressions.log`, `condor-publication-restart.log`,
`condor-publication-benchmark.log`, `condor-publication-bdn/BenchmarkDotNet.Artifacts/results/`,
per-run capture directories and `condor-publication-destination-audit-progress.json`.

## Iron Condor original request capture and replay - 2026-11-24

Both position and monitoring-input paths now durably capture the original Forward Price request,
UTC evaluation time, position, monitoring/pricing inputs and Trade Plan parameters before dispatch.
The exact forward response or explicit failure is then frozen into the Trade Plan request before
that request is dispatched. Plan retries bypass the Forward Price call and send the saved request.
Command IDs and Function fingerprint checks are unchanged; the earlier drift counterexamples now
require matching original requests and the actual restored Function states accept them.

`IronCondorPositionRequests` is a versioned, checksummed binary journal with exclusive ownership,
flushed file writes and atomic durable replacement. It returns deserialized copies, rejects changed
source content under the same event ID, caps pending requests at 4,096 and frames at 8 MiB, and has
a 128-record file-backed LRU. Completed captures move to sharded disk history and remain available
for redelivery; startup scans pending records rather than loading all completed history. Disk history
is not automatically pruned: retention must eventually match the source-retention policy. Corrupt
pending records block startup; completed corruption blocks lookup. Full disk/write/identity failures
block progress. A null directory is explicitly volatile for isolated tests only.

Both API host modes register a persistent singleton under `AppSettings:IronCondorPositionRecovery:Directory`,
default `%LOCALAPPDATA%/IFM/iron-condor-position-requests`. Production context construction requires
it. No live profile restart occurred. The journal resumes on original source redelivery; it does not
make the current Core/local mailbox durable or authorize retired-generation inputs. Autonomous pending
request dispatch is not enabled. Original missing-input, closing and generation guards remain.
Legacy uncaptured in-flight requests cannot have their original inputs reconstructed by this store;
their fingerprint mismatches still fail closed.

Verification:

- Release benchmark/API build: zero warnings/errors. **2,060 Trade unit checks passed**. Coverage
  includes metrics-independent failures, exact request bytes across cold replay and clock/cache/policy
  changes, frozen forward-failure evidence, failures at both durable-write boundaries without early
  Function dispatch, source-content conflicts, corruption and exclusive process ownership.
- **28 Memory/File abrupt-process cases passed**: both event kinds across request exceptions, failed
  terminals, committed-but-lost replies, failed final publication, completion before source ACK,
  exit after initial capture, and exit after plan capture before its dispatch. Recovery advances the
  clock one day and changes cache values and parameters. The first request retains original UTC time,
  inputs and parameter version; a frozen plan does not request forward pricing again. Source/command
  identities and revisions 1,2 stay ordered; two PostgreSQL surrogate receipts and no pending broker
  inputs remain per case.
- The recovery suite executes the real actor, scheduler, command construction and **actual file-backed
  capture store**. Function calculation/commit, market/cache access, generation authority and publication
  are substituted. PostgreSQL stores surrogate receipts, not production financial rows. Ordering uses
  explicit ACK/MaxAckPending=1; NATS remains running during API-child exit. Broker loss, hardware power
  loss, real generation recovery and full financial replay are not qualified. Prepared-only Forward Price
  recovery still has its original five-second Function deadline; expired-request reconciliation is not
  qualified by the surrogate. No deadline, fingerprint or readiness validation was weakened.

### Iron Condor durable-request BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, tiered compilation disabled for the
benchmark host, ten warmups, eight measured iterations, 64 invocations, unroll one, no forced GC.
Before is a frozen previous handler with metrics-independent settlement but no request journal.
After includes the actual flushed file journal on this workstation. Real actor/scheduler; substituted
Functions, cache and publications. Fresh operations use unique sources and include preparation,
plan freeze, completion write and archival rename. Replay repeats a completed capture. Observation
logs/Function surrogates reset per iteration; journal history remains. Each timed call requires
successful settlement. The baseline generated diagnostic helper is adapted to guarded ordinary logging;
NullLogger disables the branch. These are not production financial compute/commit or maximum timings.

| Source | Operation | Before mean | Durable mean | Before allocation | Durable allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Position | Fresh | 116.58 Î¼s | 11,613.38 Î¼s | 16.26 KB | 95.86 KB |
| Position | Replay | 104.03 Î¼s | 281.35 Î¼s | 15.02 KB | 24.22 KB |
| Monitoring | Fresh | 99.12 Î¼s | 11,582.90 Î¼s | 15.81 KB | 90.78 KB |
| Monitoring | Replay | 95.65 Î¼s | 266.21 Î¼s | 14.14 KB | 24.74 KB |

No Gen 2 collections were observed in the component diagnostics.
The added fresh-path cost buys request durability, not a claimed throughput improvement. Confidence
intervals and the small synthetic position/plan limit precision and extrapolation. No live LOH,
fragmentation, financial throughput or two-second deadline claim follows from these component means.

### Historical publication blocker: repaired by the outbox above

The continued audit executes `IronCondorPublicationRecoveryAuditTests` against the actual
`IronCondorTradePositionRealtimeContext`, registry and hot cache. Its substituted producer throws
on the first plan notification. The context has already applied the position and plan to cache.
Retrying the same sequence/revision causes both Apply calls to return false, so publication returns
success without resending. The counterexample observes one send attempt, the plan retained in cache,
and `HasPublishedPosition == false` after the apparently successful retry. No live failure was induced.
This is a failure reproduction, not desired successful behaviour.

Before cutover, retain pending publication independently of cache mutation, retry identical publication
identity/content until confirmed sending, and recover ambiguous send/restart outcomes. The new request
journal does not repair this separate boundary. **Stage 1 stops at this concrete publication blocker.**
Production stages 2-9 and matched full-session Memory/File trials remain incomplete; the production
JetStream mailbox backend stays disabled. No live restart, order/balance mutation or readiness
relaxation occurred.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`: `condor-replay-build.log`,
`condor-replay-regressions.log`, `condor-replay-restart.log`, `condor-replay-benchmark.log`,
`condor-replay-bdn/BenchmarkDotNet.Artifacts/results/`, per-run capture directories, and
`condor-replay-destination-audit-progress.json`.

## Iron Condor position settlement independent of metrics - 2026-11-24

`IronCondorTradePositionRealtimeActor` now explicitly propagates handler failures. A required Trade
Plan request exception, failed terminal or final realtime-publication exception fails settlement
independently of optional mailbox metrics. Diagnostics remain in place. Both position-change and
monitoring-input event handlers use this boundary. No financial calculation, exit rule, command
identity, missing-input behaviour, source timestamp rule or generation/closing guard changed.

Verification:

- Release benchmark build passed with zero warnings/errors. **2,048 Trade unit checks passed**,
  including **24 new checks**: both event kinds, metrics present/absent, exceptions, failed terminal
  replies, commit-response loss, final-publication failure, paused processing without early ACK and
  unchanged closing, superseded and retired-observation skips. The previous executed settlement
  counterexample now verifies failed completion. Two new tests reproduce the next replay blocker.
- All **20 disposable Memory/File abrupt-process cases passed under unchanged captured test inputs
  and clock**: both event kinds with request exceptions, failed terminal replies, committed-but-lost
  replies, failed final publication, and successful processing followed by exit before source ACK.
  The original source/command identities and timestamps return before their successor; revisions
  1 and 2 are published in order and exactly two PostgreSQL surrogate command receipts remain.
- These execute the real position actor/scheduler and command construction. Function calculation,
  market/cache access, current-generation checks and realtime publication are substituted. PostgreSQL
  stores test command receipts, **not production financial rows**. Tests deliberately hold the clock,
  inputs and parameters fixed when qualifying unchanged-input replay. They do not prove the current
  production position path retains those values. The separate drift counterexamples below show that
  it does not. Broker ordering uses explicit ACK/one input in flight; the local queue itself has not
  become durable or a failure fence. Broker loss, power loss, full financial repository replay,
  actual generation recovery and the production JetStream adapter remain unqualified.

### Iron Condor position BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, ten warmups, eight measured iterations,
4,096 invocations and unroll one. Tiered compilation is disabled for the final benchmark host;
no forced GC is used. The first short/tiered run is retained as `condor-settlement-benchmark.log`
but superseded by the longer final run because its initial success timing showed a large warm-up
difference unrelated to the one-line failure-propagation change.

The real actor, scheduler, completion and disposal are measured without optional mailbox metrics.
Function services, receipt deduplication, cache and publications are substituted. Inputs/captures
remain fixed. Repeated success requests receive the same idempotent surrogate Function completion;
this is not the cost of calculating and committing a fresh financial plan. The frozen actor retains
pre-fix swallowing behaviour. Its inaccessible generated diagnostic call is represented by guarded
ordinary logging; NullLogger disables that branch in both actors. Measurement state is reset between
iterations and setup verifies the expected original/repaired settlement outcomes.

| Source | Outcome | Before mean | After mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Position | Success | 118.4 Î¼s | 118.9 Î¼s | 14.8 KB | 14.8 KB |
| Position | Required request throws | 143.1 Î¼s | 167.9 Î¼s | 15.94 KB | 19.29 KB |
| Monitoring | Success | 112.4 Î¼s | 108.2 Î¼s | 13.92 KB | 13.92 KB |
| Monitoring | Required request throws | 136.3 Î¼s | 161.3 Î¼s | 15.07 KB | 18.41 KB |

No Gen 2 collections were observed in the reported component diagnostics.
Failure measurements compare the old erroneous success completion with explicit failed delivery and
its diagnostic costs. Success differences are not evidence of a throughput improvement. Raw confidence
intervals and iteration warnings limit precision. These are component means, not a maximum latency,
live LOH/fragmentation, fresh-plan throughput or two-second deadline qualification.

### Historical replay blocker: repaired by the original request journal above

The continued audit of this destination found a separate financial replay gate:

1. Replaying `IronCondorPositionChangedEvent` reads the current clock and monitoring-input cache again.
   It keeps the same deterministic Forward Price and Trade Plan command IDs.
2. Forward Price request fingerprints include `RequestedAtUtc`. The executed time-drift test advances
   the clock one second and obtains a different fingerprint under the same Forward Price command ID.
3. Trade Plan fingerprints include captured monitoring inputs and the forward result/failure. The
   executed market-input test changes PutForwardPrice from 5 to 99 and gets a different fingerprint
   under the same Trade Plan command ID.
4. Both tests restore the **actual Function state classes** from the original synthetic completion:
   `IsCompleted` is true but `Matches(replayedRequest)` is false. The shared Function actor's completed
   replay branch turns this mismatch into a conflict rather than returning the original completion.
   These are counterexamples, not desired success tests or a claim about a live trade.

`IronCondorMonitoringInputsChangedEvent` already carries its evaluation timestamp and monitoring
inputs; its corresponding test retains the same request fingerprint when the current clock/cache
change. Policy parameters and real generation/Function lifecycle recovery still need qualification.

The acknowledgement fix therefore does **not** complete general production replay safety. Before
cutover, durably freeze the original Forward Price/Trade Plan request inputs, evaluation time and
policy/version identity before dispatch, and resume the same requests and completion/publication
identity after retry/restart. Do not change command IDs or weaken fingerprint conflict checks to
accept recomputed financial decisions. That frozen-request repair is not implemented in this step.

Stage 1 stops at this concrete blocker. Production stages 2-9, matched full-session Memory/File trials
and controlled cutover remain incomplete. No live profile restart, new backend activation, order or
financial mutation was performed and no readiness checks were weakened. Artifacts under
`.artifacts/realtime-mailboxes/20261124-safety/`: `condor-settlement-build.log`,
`condor-settlement-tests.log`, `condor-settlement-regressions.log`, `condor-settlement-restart.log`,
`condor-settlement-regression-audit.log`, `condor-settlement-benchmark-final.log`,
`condor-settlement-bdn-final/BenchmarkDotNet.Artifacts/results/` and
`condor-settlement-destination-audit-progress.json`.

## TickAggregation required projection settlement - 2026-11-24

Both normalized trade and quote handlers now reject a false required-projector result. The actor
also propagates exceptions to failed settlement independently of optional metrics. Successful
projection is still awaited before success is reported. Existing synthetic option quote-midpoint
notifications remain non-trades; actual option trades still require their storage projection.
No native buffering, storage key, batch format, eligibility or trading rule changed.

The quote buffer remains owned until the admitted handler finishes and is then disposed on both
success and failure. A failed quote retry decodes a new lease from retained encoded input rather than
reusing a cleared/disposed array. Repeated disposal of the old lease must not clear a newly leased
retry. This actor change does not, by itself, turn the existing Core/local queue into a recoverable
source owner or enforce ordering behind a failed input; those remain responsibilities of the planned
transport adapter.

Verification:

- Release benchmark build: zero warnings/errors. **665 feed checks passed**, including both trade
  and quote paths, metrics present/absent, projector refusal, thrown failure and simulated loss after
  persistence. Retry attempts precede the next source, preserve values/identities and restore dataset/
  generation sidecars at the handler boundary. Those sidecars are not MessagePack body fields.
- Paused trade/quote processing cannot settle early; quote values remain valid while processing is
  pending. Failed attempts return the decoded quote lease. A retained payload reconstructs a fresh
  retry and a stale second Dispose does not corrupt the reused lease. Existing full-prefix codec,
  option-notification, actor/projector and EOD regressions pass. Two older successful-path tests now
  explicitly configure successful projector substitutes instead of relying on a default false value.
- All **16 abrupt-process cases passed**: Memory/File broker streams, trade/quote inputs, and
  projection refusal, thrown failure, committed-but-lost response, or successful processing followed
  by exit before ACK. A fresh runtime receives the original source before its successor. The isolated
  ScyllaDB tables retain exactly two trade rows or two two-item quote segments, original event/command
  IDs and received timestamps, expected trade prices, and the expected quote counts/bid/ask values.
  An expired lease may redeliver the original source; the test checks identity rather than treating
  any redelivery as an overtaking successor. No broker input remains pending after recovery.
- The actual actor, scheduler, production TradeTickQuoteDb schema and write methods run against
  isolated ScyllaDB 6.2.2 and NATS 2.12.0. The projector control/publication is deliberately substituted
  to inject outcomes around the real writes. No PostgreSQL surrogate is used for tick data. The test
  does not qualify the full production source/complete/fail publication chain, native Rust handoff,
  new JetStream adapter, broker restart, power loss or parallel consumer leasing. Transport ordering
  uses explicit ACK and `MaxAckPending=1`; NATS and Scylla remain up during abrupt API-child exit.

### TickAggregation BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups, eight measured
iterations, 4,096 invocations, unroll one and no forced GC. The benchmark includes fresh binary
decoding, the actual actor/scheduler, required projection callback, settlement and disposal. The
projector/database and publication are substituted; these are not Scylla throughput measurements.
Quotes exercise both two-item and 512-item payloads and release pooled decoder leases. Pool warm-up
occurs at actor startup outside measurement. A frozen pre-fix actor and change handlers run against
the same callback and input bytes. Setup verifies the legacy/refactored settlement contracts.

| Input | Outcome | Before mean | After mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| One trade | Success | 25.24 Î¼s | 24.65 Î¼s | 4.64 KB | 4.68 KB |
| One trade | Projection refused | 17.71 Î¼s | 182.98 Î¼s | 4.34 KB | 35.2 KB |
| Two quotes | Success | 41.73 Î¼s | 37.46 Î¼s | 4.67 KB | 4.69 KB |
| Two quotes | Projection refused | 31.76 Î¼s | 166.32 Î¼s | 4.26 KB | 35.08 KB |
| 512 quotes | Success | 313.16 Î¼s | 315.01 Î¼s | 33.22 KB | 33.22 KB |
| 512 quotes | Projection refused | 199.24 Î¼s | 323.17 Î¼s | 4.32 KB | 35.12 KB |

The 512-quote repaired-success diagnostic recorded one Gen 2 collection across 4,096 operations (0.2441 per 1,000). Other cases reported none. Its per-operation allocation stayed at 33.22 KiB; this short in-process sample does not attribute the collection to the small settlement change or establish a live GC regression.
The old refused-projection path incorrectly reports success; the repaired path reports failure and
pays the exception/diagnostic cost. Success timing changes are not conclusive speedups; compare raw
confidence intervals. These are component means, not maximum latency, live LOH/fragmentation,
full-session throughput or a two-second deadline qualification. Raw warnings and GC counts remain
in the report. The 512-quote case is a component size comparison; the broker/Scylla restart cases
use two-item segments.

### Historical blocker, repaired above: Iron Condor position handling depends on metrics

Continued Stage 1 into `IronCondorTradePositionRealtime`. `PositionDeliveryAudit` executes the real
actor/scheduler with a synthetic open-position observation and captured inputs, then injects a
required Trade Plan Function transport exception. With optional mailbox metrics absent, exactly
one required request is attempted but the input receives successful settlement. The actor's error
handler logs and returns without explicit failure propagation. This is an executed counterexample,
not evidence of a particular live trade incident. No plan or financial mutation completes in the test.

Required financial-monitoring failure settlement must be independent of telemetry. Its deterministic
command identities, frozen calculation inputs, publication and restart behaviour need qualification
before that route can pass. Iron Condor position production code is unchanged in this step.

Stage 1 stops at this concrete blocker. Production stages 2-9, the matched full-session Memory/File
trials and controlled cutover remain incomplete. No live profile restart, new backend activation or
readiness relaxation occurred. Artifacts: `.artifacts/realtime-mailboxes/20261124-safety/`
(`tick-settlement-build.log`, `tick-settlement-regressions.log`, `tick-settlement-restart.log`,
`tick-settlement-benchmark.log`, `tick-next-position-audit.log`,
`BenchmarkDotNet.Artifacts/results/*TickStorageSettlement*`, `tick-settlement-destination-audit-progress.json`).

## Option-trade settlement independent of metrics - 2026-11-24

`FuturesOptionTickDataRealtimeActor` now opts into handler-failure propagation. A thrown required
lookup or notification-send failure reaches the scheduler's failed-delivery boundary even when
optional actor metrics are absent. A false handler result also throws instead of being discarded.
Diagnostics remain in place. No strategy, tick mapping, notification payload or execution rule changed.

Existing non-option/inactive stream skips remain successful, and existing catalogue/cache-unavailable
semantics are unchanged. The actor still combines the original trade with the hot option quote/Greeks
at processing time and publishes a UI Notify event. It does not persist tick data; that storage route
is audited separately. No new notification dropping/coalescing policy was introduced. Notification
deduplication and immutable quote replay are not claimed by this failure-settlement repair.

Verification:

- Release benchmark build passed with zero warnings/errors. **650 feed checks passed**, including
  the converted failure-settlement regression, both metrics modes, lookup/send failure before
  acceptance, retry followed by the next source, exact trade price/command identity, pending lookup
  without early ACK, and unchanged intentional skips. The total includes the next-gate counterexample.
- **Eight disposable broker cases passed**: Memory/File streams, optional metrics present/absent,
  and required lookup or notification-send failure. A child receives the first source, verifies
  failed settlement and no accepted notification, confirms its successor cannot be fetched, then
  exits without source ACK or orderly actor/connection disposal. A new actor/scheduler redelivers
  the original before its successor, retaining event/command IDs, received time and tick identity.
  The two successful inputs produce two accepted notifications in order with expected trade/quote
  values and no pending broker input.
- These exercise the actual actor/scheduler/settlement with substituted catalogue, cache and Notify
  transport. Failure occurs **before notification acceptance**. Transport ordering is supplied by
  explicit ACK and `MaxAckPending=1`; the existing local queue alone does not become a durable retry
  owner or a failure fence. No database mutation is part of this option notification actor. The test
  does not qualify ambiguous Notify acceptance, duplicate suppression, production JetStream dispatch,
  source acquisition, broker restart or power loss. NATS stays running during API-child exit.

### Option-trade BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups, eight measured
iterations, 4,096 invocations, unroll one and no forced GC. Actual actor, scheduler, completion and
message disposal are timed; catalogue/cache/Notify transports are substituted. A frozen pre-fix
actor runs against the same handler and test inputs. Setup verifies the expected settlement of
both implementations before measurement. Measurement state and substitute call logs are reset
between iterations.

| Metrics | Handler outcome | Before mean | After mean | Before allocation | After allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Absent | Success | 50.01 Î¼s | 37.34 Î¼s | 7.2 KB | 7.21 KB |
| Absent | Lookup failure | 171.28 Î¼s | 193.22 Î¼s | 44.22 KB | 48.1 KB |
| Present | Success | 34.28 Î¼s | 40.78 Î¼s | 6.55 KB | 6.55 KB |
| Present | Lookup failure | 174.70 Î¼s | 179.83 Î¼s | 43.35 KB | 47.18 KB |

No Gen 2 collections were observed in the reported component diagnostics.
Without metrics the legacy failed handler incorrectly returns successful settlement; the repaired
case reports failure. With metrics, both report failure, but the repaired actor explicitly propagates
it instead of depending on the metrics side channel. Small timing changes are not a throughput claim;
failure-path measurements include diagnostic/exception costs. These are component means, not a
maximum latency, live LOH/fragmentation or two-second deadline qualification. Raw confidence intervals,
allocations and any iteration warnings remain in the report.

### Historical blocker, repaired above: TickAggregation discards required projection failure

Continued Stage 1 into `TickAggregationRealtime`. The normalized trade handler awaits its required
projector but discards the returned boolean. `TickStorageDeliverySafetyTests` runs the actual actor
and scheduler with normal metrics and a projector that returns false: the source still receives a
successful completion. There is no thrown exception for metrics to detect. The quote-change handler
contains the same discarded-result pattern by code inspection; only the trade counterexample was
executed in this step. The actor also lacks independent exception propagation when metrics are absent.

This concerns the registered routed/fallback projection path. It is **not evidence of a new live
drop or a failure in the newer Rust/native durable storage path**. The raw-storage switch can bypass
some producers, but the actor/verbs remain accepted and the fallback publisher is still present.
Before that route qualifies, required persistence/publication refusal must fail settlement and its
retry, pooled quote ownership, source identity and replay behaviour must be audited. TickAggregation
production code was not changed in this step.

Stage 1 stops at this reproduced blocker. Production stages 2-9, matched full-session Memory/File
trials and cutover remain incomplete. No live profile restart, new backend activation or readiness
relaxation occurred. Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`:
`option-settlement-build.log`, `option-settlement-regressions.log`, `option-settlement-restart.log`,
`option-settlement-benchmark.log`, `BenchmarkDotNet.Artifacts/results/*OptionTradeSettlement*` and
`option-settlement-destination-audit-progress.json`.

## Buffered EOD durable capture and publication recovery - 2026-11-24

Buffered EOD now requires an exclusive durable capture owner at startup. Failed initialization or
capture propagates failure before advancing live/no-change cache state. Successful input handling
means the exact result has been physically captured; it does not claim the database and publication
have already completed. No JSON is introduced on the new capture or progress paths.

The checksummed binary capture stores the original source and completion payloads, stable IDs,
timestamps, origin/causation sidecars and ES/VX snapshots. New ES trade, statistics and VX projection
IDs derive deterministically from the triggering source identity. A duplicate projection resumes
its existing frozen result; VX volume is not added twice. Completed receipts remain on disk for
deduplication, addressed directly by ID and sharded directory. No automatic receipt expiry is applied.

The recovery worker publishes sources in capture order, freezes allocated history sequence IDs
before database submission, applies idempotent ES/VX snapshot/history mutations, then publishes the
original completion. Grouped binary progress commits record source-publication, allocated IDs and
database-application stages. Confirmed phases are not repeated after restart; ambiguous outcomes may
repeat the same IDs. A completion is retired only after publication succeeds and the completed receipt
is durable. Source and completion contracts share a GUID, so transport deduplication must also include
the event type/verb. Publication failure cannot retire pending work. Retries run in the background,
not inside strategy decisions, and later queued results cannot overtake a failed earlier publication.

Pending capture is capped at 4,096 records and 16 MiB of encoded event/completion payloads; this is
not a total managed-heap or disk-size claim. Individual checksummed frames remain capped at 64 KiB
and use pooled buffers. The completed-receipt disk store requires operational disk capacity. New
capture can proceed while the database is blocked. Source publication is serviced on a one-second
wake or the batch threshold; history retains its minute/512-row flush policy. A blocked older batch
can delay later publication, while its captured live cache remains available. Shutdown joins running
effects and leaves failed work durable. The pending metric tracks the new journal.

Startup restores pending snapshots before fresh inputs and recovers without another tick. Legacy
JSON/binary/segment history records migrate before intake: the new receipt is durable before its old
owner is retired. Original eight-field binary records are explicitly tested. Already allocated history
IDs survive migration. Migration does not establish a raw-source replay watermark for historical
pre-receipt traffic; that broader ingress/cutover qualification remains open with the mailbox plan.

Verification:

- Release benchmark build: zero warnings and errors. **642 feed checks passed**. These include
  capture failure without cache advancement, original receipt replay, publication ordering, source/
  database/completion response loss, blocked database isolation, no duplicate VX volume, legacy
  migration, original origin metadata, exclusive ownership, corrupt progress rejection and unchanged
  progress retries requiring no extra durable write.
- **Eight abrupt-process cases passed**: Memory/File publication streams and exit immediately after
  durable capture, ambiguous source publication, committed database response loss, or ambiguous
  completion publication. Fresh instances retain source/completion IDs and order, produce exactly
  two native PostgreSQL history rows, restore latest close 102, drain pending receipts and perform
  no further publication on completed replay.
- Those cases exercise the **production buffered projector, physical binary journal and actual
  MarketDataDb schema/mutations** on disposable PostgreSQL 17.2. Publication uses a substituted
  confirmed/deduplicated NATS 2.12.0 JetStream transport. A confirmed duplicate is accepted as a
  successful retry. They do not establish production Core NATS recipient ownership, broker restart,
  power-loss recovery or a production durable mailbox adapter. NATS remains up during child exit.

### Buffered EOD BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, three warmups, ten measured iterations,
one fresh invocation per iteration and no forced GC. Setup supplies fresh storage directories and
inputs. Both methods include projector startup, all captures/publications and complete history drain;
each asserts the expected history and publication counts. Cleanup is excluded. The baseline freezes
the former projector behaviour and shares the existing frame codec. Database, sequence allocation
and publication are substituted; physical capture/progress/receipt disk operations are real.

| Completed inputs | Before mean | After mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| 1 | 53.14 ms | 138.66 ms | 51.35 KB | 77.38 KB |
| 64 | 1,392.09 ms | 2,616.50 ms | 1299.21 KB | 2289.09 KB |

The new path pays for durable publication progress and retained completion receipts. These are
correctness costs, not a claimed throughput gain. Small one-input iterations and filesystem variance
limit precision; consult confidence intervals and minimum-iteration warnings in the raw report.
These are completed-batch means, not maximum input-decision latency. No live LOH, full-session or
two-second deadline qualification is claimed. Gen 2 counts and allocations are recorded in the raw
MemoryDiagnoser output; absence of a collection in this sample is not a live-GC guarantee.

### Historical blocker, repaired above: option-trade settlement depends on optional metrics

Continued Stage 1 into `FuturesOptionTickDataRealtime`. Its required handler can throw (the test
injects an active option-contract lookup failure), but BaseEventActor consumes the exception and
this actor does not opt into failure propagation. With optional mailbox metrics absent, the actual
scheduler completes that input successfully. `OptionTradeDeliverySafetyTests` reproduces the success
ACK as a counterexample, not desired behaviour or a particular live incident. The option actor's
production implementation is unchanged in this step. Required failure settlement must be independent
of telemetry before that route can qualify; retry inputs/publication identity also need auditing.

Stage 1 stops at this blocker. Production mailbox stages 2-9, matched full-session Memory/File trials
and controlled cutover remain incomplete. No live profile restart, new backend activation or
readiness relaxation occurred. Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`:
`eod-buffered-build.log`, `eod-buffered-regressions.log`, `eod-buffered-restart.log`,
`eod-buffered-benchmark.log`, `BenchmarkDotNet.Artifacts/results/*BufferedEod*` and
`buffered-eod-destination-audit-progress.json`.

## EOD inline projection and worker completion - 2026-11-24

Required inline statistics, trade and VX quote projection failures now propagate to the delivery
boundary. EOD opts into handler-failure propagation independently of optional metrics. Worker
handoffs await actual processing rather than local queue admission; false results and exceptions
fail that input without killing the worker. Generation retirement joins running effects, cancels
queued sources and never detaches unfinished processing. Multi-producer channel usage is declared
correctly. Cache invalidation is ordered with projection work.

A shared, locked per-contract registry supplies bounded execution lanes (16 contracts, 2,048 queued
worker items per contract). A lane holds later accepted work behind a failed source until the same
source is retried successfully; no retry loop is introduced in a decision. Source identity uses
the original event ID, command ID, or a binary-payload hash for legacy unidentified inputs. Lanes
and worker queues remain process-local; restart ownership comes from retained unacknowledged input,
not from these structures. This does not establish global order across distinct source subjects.

Verification:

- Release benchmark build: zero warnings/errors. All **632 feed tests passed**, including actual
  actor/scheduler settlement checks, refusal/exception/committed-reply-loss retries, same-contract
  failure fencing across logical threads, contract isolation, ordered invalidation, saturation and
  generation retirement. Existing early-mailbox-release tests now assert completed-work semantics.
- All **16** isolated restart cases passed: Memory/File broker storage, inline/worker paths, and
  refused, thrown, committed-but-lost reply or completed-before-ACK process exits. Each fresh runtime
  receives the original input before its successor, finishes with volume 30/open 97/high 115 and
  exactly two PostgreSQL writes, then leaves no pending broker inputs.
- Restart cases use the actual EOD actor/scheduler and statistics calculation with **substituted
  query/projector persistence** into a disposable PostgreSQL snapshot table. They do not exercise
  the production MarketDataDb schema, buffered projector or notification replay. NATS remains
  running during abrupt API-child exit; broker-loss and power-loss recovery are not qualified.

### EOD BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, five warmups, eight measured
iterations, 8,192 invocations, unroll one and no forced GC. The worker reference freezes the
original admission-only implementation but includes **draining all work** before timing completes.
The new worker awaits each input's completion. Callbacks are substituted, without database/network
work; the inline comparison uses the actual statistics computation with substituted query/projector.

| Component / completed work | Before mean | After mean | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| Inline statistics projection | 20.56 us | 22.96 us | 3.74 KiB | 5.05 KiB |
| Worker, one input | 9.909 us | 4.301 us | 347 B | 400 B |
| Worker, 64 inputs | 38.908 us | 275.361 us | 378 B | 17,978 B |

The 64-input case now pays per-input completion scheduling and task allocation rather than enqueueing
the whole group before one drain barrier. This is a safety cost, not a throughput optimization.
The one-input reference also includes a separate drain barrier, so its smaller new mean is not proof
of faster real EOD processing. Confidence half-widths were 2.224/8.231 us inline, 3.255/1.416 us for
one worker input and 4.689/22.349 us for 64 inputs. Single-input worker iterations still trigger the
minimum-iteration warning. No Gen 2 collections were observed in these component measurements;
this is not live LOH, full-session throughput or two-second deadline qualification. The preliminary
64-invocation run is retained separately and superseded by `eod-settlement-bdn-final/results/`.

### Historical blocker, repaired above: buffered EOD projection can falsely report success

The continued audit reproduced a separate failure in the **production buffered projector** (enabled
by `MarketData:FuturesEodMinuteBatchWriter` in Development). If spool initialization/capture fails
and source publication also throws, it catches both failures and returns `true`. The test uses a
file in place of the spool directory and injected publication failure: publication is attempted,
no database access or durable history capture occurs, yet processing reports success. This is an
injected counterexample, not evidence that those failures occurred on the live API.

Therefore completion-aware callers can still receive a false success from this lower layer. Cache
publication precedes durable capture, so simply returning false also needs retry/no-change and
publication-identity analysis. The next gate needs durable ownership of the frozen EOD result and
recoverable required publication before acknowledging the source. This buffered-projector behaviour
was not changed in the current step.

Stage 1 stops at this reproduced blocker. Production JetStream mailbox stages 2-9, two matched full
sessions and controlled cutover remain incomplete. No live profile restart, backend activation or
readiness relaxation was performed. Artifacts: `.artifacts/realtime-mailboxes/20261124-safety/`
(`eod-settlement-regressions.log`, `eod-settlement-restart.log`, `eod-settlement-benchmark-final.log`,
`eod-settlement-bdn-final/results/`, `eod-destination-audit-progress.json`).

## ITI failed-command settlement and frozen fallback recovery - 2026-11-24

The routed Daily ITI ingress now returns failure for rejected, null or failed Generate replies and
request exceptions. The actor propagates unsuccessful handling to the delivery boundary independently
of optional metrics. Failures remain in telemetry/health evidence without counting the same ingress
failure twice. Accepted no-change commands and existing intentional eligibility/input-unavailable
skips retain their existing semantics. No ITI formula, classification, entry or exit rule changed.

The active ordered-intake/WAL path remains unchanged. The **routed fallback** now freezes the complete
Generate command before dispatch, including original command ID, ES price/time, VX, completed ATR(14),
source cursor and binary origin/causation metadata. The command actor preserves frozen ATR instead
of re-reading a newer cache value. The ITI parser restores original source headers when needed,
retaining the existing event instance when no sidecar restoration is required.

`FuturesItiIngressRecovery` owns a checksummed binary append journal. Prepare is physically flushed
before dispatch; completion is flushed only after a successful reply. Restart restores pending
commands in their original order, before fresh input. Failed earlier work cannot be overtaken:
later inputs may be captured, but their dispatch waits for the earlier command. Cancellation retains
request ownership until the actual request settles and never marks a cancelled attempt completed.
Startup recovery works without another market event, and a bounded one-second recovery task retries
pending transport work outside strategy decisions. Switching to ordered intake with pending fallback
work is refused until that work is drained or reconciled.

Both local and remote API registrations share this service with the typed context. Configuration:
`AppSettings:ItiIngressRecovery:Directory`; default `%LOCALAPPDATA%/IFM/iti-ingress`. Null storage is
explicitly memory-only in tests. The directory has one exclusive owner. Frames are capped at 64 KiB,
pending commands at 4,096, completed receipts at 8,192, source watermarks at 1,024 and retained encoded
prepare bodies at 16 MiB. This payload cap is not a claim about total managed heap size. The journal
compacts at 32 MiB; a temporary snapshot is physically flushed and atomically replaced. Native
source order uses epoch/ordinal watermarks, allowing repeated or backwards exchange timestamps.
Legacy inputs without cursors use a conservative time/window guard. Redelivery outside the retained
completed window, conflicting IDs and capacity exhaustion fail closed rather than inventing new
VX/ATR inputs or discarding required pending work. Complete corrupt frames fail startup; only an
incomplete, uncommitted trailing frame is truncated.

Verification:

- 1,008 selected analytics checks passed, including existing ITI/resident/WAL, indicator, TDI/bar,
  architecture, new recovery and actual command-actor frozen-ATR checks.
- 25 feed checks passed, including market-price exception settlement, existing projector regressions
  and two **counterexamples** reproducing the next EOD blockers below.
- Updated one stale overshoot test's expectations for Daily band-qualified Trending, which was already
  enabled before this change. Weekly/Monthly structural-only expectations and generator code remain.
- Cold recovery preserves original ID, timestamp, VX=20, ATR=8 and origin metadata despite current
  inputs changing to 99. Later captured inputs retain their own snapshots. Conflicts, compaction,
  bounded receipt-window handling, source-clock ordering, torn tails, corruption, exclusive ownership,
  cancellation and mode-change protection were verified.

All eight disposable broker cases passed: Memory/File storage with refusal, thrown request,
committed-but-lost reply, and completed processing followed by process exit before source ACK.
A child exits without disposing the actor, journal, broker or database connection. A fresh runtime
restores original frozen inputs, drains pending work before another tick and then consumes the
same original source before its successor. Source dataset/generation sidecars are retained by the
test envelope; native epoch/ordinal order and original IDs remain intact. Changed cache values of
99 do not replace the original VX=20/ATR=8. Each case leaves two unique PostgreSQL command receipts
and two real C# signal changes, with no duplicate changes after replay and no pending broker inputs.

These use synthetic inputs, the real ingress/actor/scheduler/C# computation, a physical fallback
journal, NATS 2.12.0 and PostgreSQL 17.2 **command-receipt surrogates**. They do not exercise the
entire production command-store/projector chain or implement a production JetStream actor adapter.
The single in-flight broker input enforces transport ordering; NATS remains running during child
exit. Broker restart, power loss, source acquisition before the journal and two complete sessions
are not qualified by this test.


### ITI ingress BenchmarkDotNet

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, eight warmups, eight measured
iterations, 64 invocations, unroll one and no forced GC. The reference freezes the pre-fix ingress
from Git HEAD. All paths use the same synthetic source construction and command-service fixture,
which runs the real C# ITI computation but substitutes transport/domain persistence. These timings
measure the ingress handler, not the complete actor scheduler or production database/network.
The actor/scheduler and durable broker boundaries are covered separately by correctness checks.

| Ingress operation | Mean | Managed allocation |
| --- | ---: | ---: |
| Legacy volatile success (reference) | 77.024 Î¼s | 9.18 KiB |
| Fixed volatile success (test-only context) | 94.145 Î¼s | 10.55 KiB |
| New durable fallback input | 2,820.563 Î¼s | 12.55 KiB |
| Completed-input replay | 2.936 Î¼s | 2.25 KiB |
| Legacy refusal (incorrectly returns success) | 9.167 Î¼s | 8.25 KiB |
| Fixed refusal, volatile test context | 13.621 Î¼s | 9.61 KiB |
| Retry of already prepared, still-refused input | 5.214 Î¼s | 4.71 KiB |

New durable fallback processing pays for physical prepare and completion commits; this is additional
safety work, not a general speedup. The active ordered-intake path keeps its existing batched WAL
implementation and does not use these per-input fallback commits. Completed replay performs no new
request or physical commit. Pending retry already has a prepared record and still returns failure
if the command is refused; it is not the cost of capturing a fresh failed input. Volatile contexts
are explicitly test-only comparisons, not the production recovery configuration.

No Gen 2 collections were observed by MemoryDiagnoser in this short component run. No live LOH,
fragmentation, two-second deadline or full-session throughput qualification is claimed. Some hot
iterations are short and timing variability remains in the raw report; for example the durable-new
mean has a 541.1-microsecond standard deviation and a 1,218.7-microsecond confidence half-width.
Do not treat these means as maxima or interpret the small volatile timing difference as conclusive.
Saturation, large backlog and compaction performance remain part of wider mailbox qualification.
Reports: `iti-settlement-benchmarks/results/` beneath the artifact root below.


### Historical blocker, repaired above: EOD source settlement precedes required projection

Continued the destination audit through the primary market-price and EOD actors. The primary
market-price exception path already reports failed settlement through the actual mailbox metrics;
its new regression passes, so it is **not** the next blocker.

The EOD statistics path has two reproduced failure modes, using the actual actor/scheduler and a
substituted query/projector, with no live EOD changes:

1. Inline statistics handling discards a false result from `statistics.ExecuteAsync`. A refused
   required projection therefore still produces a successful source completion.
2. Worker statistics handling acknowledges local queue admission while the required EOD query and
   projection are still unfinished. A controlled pending query proves the successful ACK occurs
   first. Releasing that query into a refused projection faults the worker and is observed at
   shutdown, after the source has already been acknowledged. The worker queue is process-local.

`EodStatisticsDeliverySafetyTests` reproduces both outcomes as counterexamples, not desirable
behaviour or evidence of a particular live EOD incident. The cumulative-trade route also uses this
worker admission boundary by source audit. Required EOD work needs actual completed projection or a
recoverable durable handoff before source ACK, with per-contract ordering and retry/publication
identity verified. EOD production code was not changed in this step.

Stage 1 remains incomplete at this concrete blocker. Production stages 2-9, matched full-session
trials and controlled cutover remain pending. The old Core/local queue itself has not become
restart-durable. No live API/profile restart, new JetStream mailbox activation or readiness relaxation
was performed. Artifacts are under `.artifacts/realtime-mailboxes/20261124-safety/`.

## Seven indicator actors: failed-delivery settlement - 2026-11-24

ADX, ATR, EMA, MACD, RSI, VWAP and VX term structure no longer discard a required handler's false
result. Each actor throws on unsuccessful required handoff and opts into propagation through
`BaseEventActor`; failed settlement therefore works independently of optional mailbox metrics.
Existing error diagnostics remain. No indicator formula, trading rule, command transport, admission
budget or readiness condition was changed. VWAP also refuses successful completion when its
required current-contract catalogue is unavailable, rather than losing an eligible checkpoint.
Valid inapplicable observations and non-price events retain their existing successful-skip semantics.

Verification:

- 751 selected analytics tests passed, including actual indicator actors/scheduler, same-actor retries,
  actor replacement, refused commands, thrown requests, committed-but-lost replies, one settlement
  and disposal per input, legitimate skips, catalogue failure, existing command/accumulator tests,
  TDI/bar recovery and architecture checks. This count includes the new ITI blocker counterexample.
- 58 scheduler/queue regression tests passed. Release API/benchmark build: zero warnings/errors.
- All 42 disposable cases passed: seven actors, three failure modes, both Memory and File JetStream.
  The actual actor and scheduler return failed settlement; the test driver leaves that broker input
  unacknowledged. A second input cannot overtake it with `MaxAckPending=1`. A child process then exits
  without disposing actors, scheduler, broker connection or database connection. A fresh runtime
  consumes the same original source first, then its successor, and confirms processing ACKs.
- The test command service uses actual PostgreSQL 17.2 synthetic receipts. The replacement service
  restores only those persisted receipts. Refusal/throw persists nothing; lost reply has committed
  the first receipt. Recovery leaves exactly receipts 1 and 2 and an empty broker backlog.

The broker proof exercises the delivery boundary with a **substituted downstream command service**,
not every indicator's production event-store/projector chain. Existing domain regression tests are
separate evidence. This is not a production JetStream mailbox adapter or a migration of the old
Core/local queue. The old local queue still has no broker retry owner; a failed settlement alone
cannot make it restart-durable. Ordering in this proof comes from one broker-owned input in flight,
not an added retry loop inside an actor or a strategy decision. NATS stays running across the child
exit. No broker restart, power-loss, live cutover or full-session guarantee is claimed.

### Indicator settlement BenchmarkDotNet

Frozen pre-fix actors are compared with repaired actors through the same real scheduler, synthetic
input construction, command substitute, settlement callback and message disposal. Successful and
refused commands are measured separately. The refused reference incorrectly acknowledges success;
the repaired path includes exception propagation and failure diagnostics. Its extra cost must not
be presented as equivalent successful work or hidden as a regression in indicator calculations.

.NET 10.0.10 Release, BenchmarkDotNet 0.15.8, InProcessNoEmit, eight warmups, eight measured
iterations, 4,096 invocations per iteration, unroll factor one and no forced GC. Mock invocation
history is cleared outside each measured iteration so the harness does not retain an ever-growing
call log. This includes the real scheduler and actor, but no real command/database latency.

Successful required handoff; means per message.

| Actor | Before (microseconds) | After (microseconds) | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| ADX | 48.50 | 41.26 | 5.53 KiB | 5.51 KiB |
| ATR | 46.33 | 47.69 | 5.49 KiB | 5.47 KiB |
| EMA | 36.74 | 41.96 | 5.86 KiB | 5.85 KiB |
| MACD | 51.43 | 56.69 | 5.52 KiB | 5.5 KiB |
| RSI | 49.12 | 48.82 | 5.5 KiB | 5.52 KiB |
| VWAP | 63.52 | 50.05 | 5.88 KiB | 5.89 KiB |
| VX term structure | 47.71 | 51.00 | 6.09 KiB | 6.09 KiB |

Refused required handoff (before incorrectly ACKs success); means per message.

| Actor | Before (microseconds) | After (microseconds) | Before allocation | After allocation |
| --- | ---: | ---: | ---: | ---: |
| ADX | 42.68 | 269.87 | 5.49 KiB | 28.1 KiB |
| ATR | 40.20 | 233.09 | 5.49 KiB | 28.1 KiB |
| EMA | 45.18 | 189.16 | 5.85 KiB | 28.41 KiB |
| MACD | 48.85 | 233.66 | 5.52 KiB | 28.19 KiB |
| RSI | 43.86 | 190.72 | 5.52 KiB | 28.09 KiB |
| VWAP | 57.00 | 177.98 | 5.9 KiB | 28.48 KiB |
| VX term structure | 53.20 | 208.91 | 6.11 KiB | 29.39 KiB |

Successful handoff allocations remain broadly unchanged. The timing intervals overlap in the
successful pairs; this repair does not establish a general speedup. Correct refusal handling costs
more time and allocation because the former path skipped failure propagation and diagnostics.
MemoryDiagnoser observed Gen 0 and Gen 1 activity, with **no Gen 2 collections observed** in these
measured component cases. No live API LOH, fragmentation or full-pipeline deadline was measured.

The full report preserves confidence intervals, standard deviations and outlier handling. Shorter
32- and 1,024-invocation preliminary runs remain in the artifacts but are excluded from these tables
because of short iterations and visible warm-up drift. The final run completed all 28 cases; it
uses a longer warm-up and batches rather than selecting favorable rows from earlier runs.

Report: `indicator-settlement-benchmarks-final/results/` under the artifact root below.


### Historical ITI blocker, subsequently repaired above: refused Generate command ACK

Continued the Stage 1 destination audit and reproduced this using the actual ITI actor, scheduler,
local queue and completion hook. A required `GenerateFuturesItiSignal` request returns
`ServiceFailed(-429)` with nothing persisted. `GenerateAsync` records the failure and returns
normally; `ExecuteAsync` returns true; the actor/scheduler then report successful delivery. Request
exceptions are also contained by that helper by source audit. Existing handler tests already
expected true after refusal/exception; the new runtime counterexample confirms the ACK consequence.

The test `Mailbox_safety_audit_rejected_ITI_command_is_still_ACKed_by_the_real_scheduler` records
this unsafe behaviour as a counterexample, not desired semantics or evidence of a live data-loss
incident. ITI production code was not changed in this step. Its required-command outcome and
recoverable ownership of the original inputs must be repaired before the safety gate can pass.

Stage 1 remains incomplete at this concrete blocker. Production stages 2-9 and the two full-session
comparison remain pending. No live API restart, new mailbox activation or weakened readiness check
was performed. See `indicator-destination-audit-progress.json`,
`indicator-settlement-recovery-proof.json`, regression logs and benchmark artifacts under
`.artifacts/realtime-mailboxes/20261124-safety/`.

## TDI result receipts and recoverable publication - 2026-11-24

Fixed the TDI replay blocker without changing the TDI formula. Each eligible RSI window now has
an immutable, durably prepared result receipt containing the original generated event, original
completion event, both event IDs, signal cross, timestamps and binary origin metadata. Identity
comes from the exact contract/timeframe/value date, latest observation time/sequence and versioned
configuration, rather than the transport publication ID. A fingerprint of the calculation inputs
rejects conflicting replays. Decimal scales and PostgreSQL microsecond precision are normalized only for identity comparison,
so database roundtrips do not create false conflicts. Original full-precision payload timestamps
remain unchanged in the receipt.

The result is prepared before the first external effect. Its source-publication, database-apply
and completion-publication stages advance durably. A completed input is not projected or published
again. An ambiguous send may be repeated with the same original event ID; exactly-once network
delivery is not claimed. Typed diagnostic failure events remain available, and failure propagates
through the actor before successful input settlement. Pending work remains owned independently of
its input delivery. Older pending work is recovered before evaluating a later window for that
contract/timeframe. A background actor recovery loop resumes pending work after startup even when
no new RSI signal arrives.

Both local and remote API registrations share a persistent `FuturesTdiResultReceipts` owner between
the typed context and projector. Configuration: `AppSettings:TdiResultRecovery:Directory`, default
`%LOCALAPPDATA%/IFM/tdi-results`. The binary SHA-256 protected records are atomically replaced and
physically flushed. Completed receipts remain on disk with a 512-entry LRU hot cache; the pending
set is capped at 4,096 and individual records at 64 KiB. The directory has an exclusive process
lease. Corrupt pending receipts fail startup rather than causing recalculation. The directory must
survive process restarts; it is not a replicated event store. Archive retention/capacity remains
part of the overall mailbox qualification work.

Legacy already-applied windows without a receipt cannot recover an event ID that was never stored.
They fail closed with `TDI.LEGACY_RECEIPT_GAP` rather than recomputing a different cross or inventing
an original identity. This repair does not rewrite historical TDI rows or manufacture legacy event
IDs. New windows continue normally; a legacy backlog needs reconciliation at cutover.

Verification:

- 235 focused analytics checks passed: 234 functional/regression checks and one counterexample
  demonstrating the **next** unsafe ACK gate. Coverage includes TDI, bar recovery and architecture.
- Seven existing base-realtime-projector regression tests passed. The shared atomic-file helper
  was extracted without changing the bar calculations or publication identities.
- Publication refusal, ambiguous source send, database refusal, committed-but-lost database reply,
  completion refusal and ambiguous completion retain the original Bullish cross and both IDs.
- Completed cold-start replay, a new transport ID for the same window, equivalent decimal scale,
  conflicting inputs, failed seed retry, failed receipt write, exclusive ownership and corrupted
  pending data were verified. Actual actor startup recovered pending work without another RSI input.
- Disposable PostgreSQL 17.2 committed the Bullish row before an injected lost response. Receipt
  recovery retained both IDs, kept the row Bullish and did not repeat the already confirmed source
  publication. This uses the real MarketDataDb insert/read and a substituted publication transport.
- Three separate child processes exited without disposal after ambiguous source publication,
  database commit and completion publication. Recovery used the frozen result and IDs in every case.
- Release API/benchmark build succeeded with zero warnings and errors.

The publication recovery uses the existing `SendAsync` acceptance contract. It does not turn Core
NATS into durable broker delivery. Confirmed destination publication, routing manifests and broker
restart guarantees still require the planned JetStream adapter; no such backend is enabled here.

### TDI BenchmarkDotNet

The reference freezes the original volatile state and realtime projector. Both reference and new
paths construct the same synthetic RSI windows; database and transport calls are substituted. The
new-window path includes physical file receipt commits. The replay path constructs the same window
under new transport IDs and returns its completed receipt without another projection/publication.
.NET 10 Release, InProcessNoEmit, three warmups, eight iterations and eight invocations, no forced GC.

| Operation | Mean | Managed allocation |
| --- | ---: | ---: |
| Frozen legacy volatile processing | 0.285 ms | 16.24 KiB |
| New window with durable receipt/publication stages | 45.329 ms | 73.72 KiB |
| Completed window replay, new transport IDs | 0.011 ms | 15.10 KiB |

Durability adds physical commit work to new windows; this is not a claim that durable first
processing is faster than the old unsafe volatile path. The completed replay performs no duplicate
projection/publication or physical receipt write. It is a distinct workload, not the same work
with a faster database. No Gen 2 collections were observed in this short component run. Live LOH,
real network latency, full-pipeline deadlines and full-session qualification were not measured.
The durable-window mean had a 7.087-ms standard deviation; do not treat its mean as a hard maximum.

### Historical blocker, subsequently repaired above: required indicator failures reported success

Continued the Stage 1 audit and reproduced the issue with the actual `ActorThreadPoolV2`, ADX actor,
local mailbox and `IActorDeliveryCompletion` hook. The synthetic Generate command returned
`ServiceFailed(-429)` with no persisted result. The closed-observation handler returned false, the
ADX actor discarded that result, and the scheduler invoked successful delivery completion.
The test `Mailbox_safety_audit_failed_ADX_command_is_acknowledged_by_the_real_scheduler` explicitly
asserts this counterexample. Passing that audit test means the exposure is confirmed, not repaired.

The same discarded-boolean pattern is present by source audit in ATR, EMA, MACD, RSI, VWAP and VX term
structure forwarding actors. Only ADX was executed as the full scheduler counterexample. Their
production forwarding code has not been changed in this work. A JetStream consumer must not delete
an input after its required downstream handoff failed. Required failure outcomes must propagate to
failed settlement, preserve the input and prevent later work overtaking it; terminal rejections
need explicit classification rather than being silently treated as success.

**Stopping point:** the Stage 1 failed-handoff safety gate, as required by the implementation plan.
The TDI blocker is repaired. The 25-destination audit and capacity worksheet remain incomplete;
production JetStream stages 2-9 and the two-full-session comparison are not complete. The live API
was not restarted, no broker cutover occurred, and readiness checks were not weakened.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`:
`tdi-tests-qualified.log`, `tdi-projector-regression.log`,
`tdi-postgres-and-crash-proof-qualified.log`, `tdi-build-complete.log`,
`tdi-receipt-verified-benchmarks/results/`, and `tdi-qualification-summary.json`.

## Batched durable commits and next recovery gate â€” 2026-11-24

Implemented an opt-in `IBatchedRealtimeActor` execution boundary and enabled it for the bar actor.
The existing scheduler takes an already available FIFO group of at most 64 messages, with a
64-KiB prefetch target (the final message can cross that target). It does not wait for a batch
window or add a local payload queue. Other actors retain their per-message execution path.
Trades and clock barriers stay in their original order and must target the same accumulator.

The bar actor applies the group under its partition gate and durably checkpoints all inputs,
open buckets and pending publications once. Confirmed bar publications still have individual
durable completion markers, so a partially accepted output prefix is not repeated after restart.
Input deliveries are settled only when the group handler finishes. This is grouped ownership,
not an atomic transaction across downstream effects. Any failed group is negatively settled;
its committed cursor and pending outputs make input redelivery safe.

Generation leases and cancellation remain per input. Actor retirement releases the scheduler
worker while the unfinished batch retains its payloads and leases, then negatively settles them
when the real handler finishes. Late success does not acknowledge a cancelled generation. The
existing source-generation fencing policy is preserved; this work does not declare that policy
sufficient for every future durable route.

Verification:

- 35 focused bar tests passed, including batch/serial OHLCV and source-lineage equivalence,
  FIFO barriers, publication failure, duplicate inputs, cancellation and restart recovery.
- 58 scheduler/queue regression tests passed, covering the opt-in batching path, ACK after
  commit, failed-group retry, source-generation fencing, completion evidence, actor retirement,
  existing FIFO/concurrency, local queue retention and unchanged per-message dispatch.
- 38 analytics architecture checks passed. The Release API/benchmark build had zero warnings and errors.
- Four abrupt process-exit scenarios passed: open state and pending publication, each with
  individual inputs and 64-input groups. No graceful shutdown/disposal was used by the child.
- Disposable JetStream Memory and File consumers both retained and redelivered all 64 inputs
  in order after failed publication and registry restart, then settled them after success.
  The command transport is substituted; these are not PostgreSQL or complete mailbox-adapter tests.
- The actual local scheduler, bar actor and file journal also completed every input and disposal
  exactly once in the before/after benchmark verification. The broker adapter is not implemented.

### BenchmarkDotNet: input checkpoint batching

Same-run ordinary-update measurements compare the existing per-message physical checkpoint with
one checkpoint for a group. They exclude broker traffic and a real downstream database command.
.NET 10 Release, InProcessNoEmit, three warmups, eight measured iterations, 16 invocations,
no forced collection. Short volatile-reference measurements are retained in the raw report but
are not used to claim an accurate speedup ratio.

| Inputs | Per-message durable commits | Grouped durable commit | Managed allocation before / after |
| --- | ---: | ---: | ---: |
| 1 | 6.144 ms | 6.331 ms | 10.49 / 15.51 KiB |
| 64 | 380.404 ms | 6.723 ms | 647.18 / 85.50 KiB |

For 64 inputs this component run is about 56.6 times faster with 86.8% fewer managed bytes.
Single-input flush latency remains; batching benefits available bursts rather than delaying
isolated messages. No Gen 2 collections were observed in this run. LOH traffic on the live API
was not profiled, and these timings do not qualify the full strategy decision deadline.

A separate full-scheduler BenchmarkDotNet comparison includes mailbox admission, the real actor,
physical journal commits, delivery completion and payload disposal. Its reference wrapper preserves
per-message dispatch without opting in to batching. Only the command transport is substituted.
The final measurement waits for explicit delivery/disposal completion, rather than the scheduler's
10-ms polling-based idle detector. It uses three warmups, eight measured iterations and eight
invocations without forced collection.

| Inputs | Per-message scheduler | Batched scheduler | Managed allocation before / after |
| --- | ---: | ---: | ---: |
| 1 | 6.587 ms | 5.939 ms | 12.23 / 17.08 KiB |
| 64 | 413.401 ms | 15.147 ms | 708.93 / 141.62 KiB |

At 64 inputs the real-scheduler comparison is about 27.3 times faster with 80.0% fewer managed
bytes. Single-input latency is effectively unchanged within measurement variation, with extra
batch bookkeeping allocations. No Gen 2 collections were observed. The actor may execute several
available groups while the producer is still enqueueing, so this measures actual scheduling rather
than assuming that all 64 inputs are available for one commit. It is not a broker-backed scheduler
benchmark, a measured live LOH reduction, or two-session qualification. The earlier scheduler run
that included idle polling is retained separately and is not used for the final comparison.

### Original TDI blocker (repaired by the receipt implementation above)

Continued the Stage 1 destination audit after fixing the bar checkpoint cost. The new executable
qualification test `Mailbox_safety_audit_redelivery_recomputes_a_different_cross_after_confirmation_or_restart`
uses the real `FuturesTdiSignalRealtimeState` with a prior negative divergence and one eligible RSI
window. It demonstrates:

1. The first evaluation emits a Bullish cross.
2. Confirming that result and evaluating the identical input again emits None.
3. The command identity is unchanged, but a new generated-event GUID is assigned.
4. A new state seeded with the already saved first result also emits None on input replay.

The audit test passes because it asserts this counterexample; it is not evidence that TDI recovery
is safe. No TDI calculation or production handler was changed. The substituted database supplies
the previously computed result; the original state/evaluation functions perform the computation.

`FuturesTdiSignalRealtimeState.TryEvaluate` has no processed-window receipt and assigns a new
GUID on every evaluation. `BaseRealtimeProjector` publishes the source, applies the database write,
and publishes completion as separate operations without an outbox. The TDI upsert allows replacement
of the same timestamp's cross/trend fields. Therefore a commit followed by a lost completion/ACK
can cause replay to produce a different payload and potentially replace the original signal. The
payload change and restart behavior were executed; the SQL overwrite consequence is inferred from
the existing upsert, not claimed as an executed PostgreSQL test.

Before migrating this destination, persist an immutable processed-window result/receipt and stable
output identity, with recoverable source/completion publication. A retry must replay the original
result, not recompute it using the result as its own prior state. Verify the commit-before-ACK and
restart cases, including preservation of the original cross. This clears the blocking requirement
without changing the TDI formula.

**Historical stopping point before the TDI repair:** Stage 1's unsafe-repeat gate. The current live API has not been restarted,
no new JetStream backend is enabled, and no two-full-session qualification is claimed. Production
stages 2â€“9 remain pending. The original 25-destination inventory is still preliminary; this audit
must not be represented as completed qualification of all destinations.

Artifacts under `.artifacts/realtime-mailboxes/20261124-safety/`:
`batched-bar-and-audit-tests.log`, `batched-scheduler-regression-final.log`,
`batched-crash-proof-final.log`, `batched-broker-proof-final.log`,
`batched-real-scheduler-proof-final.log`, `batched-checkpoint-benchmarks/results/`,
and `batched-real-scheduler-final-benchmarks/results/`.

## Bar recovery repair â€” 2026-11-24

The requested bar-handler changes are implemented. Normal trade processing now checkpoints the
open OHLCV buckets, exact trade-stream cursor, pending gap inputs/deadlines, retired epochs,
finalized intervals and pending closed observations before a successful delivery can return.
Barriers use the same transaction boundary. Confirmed publications are removed only after their
success is durably checkpointed. A failed/ambiguous handoff retains the original observation and
uses its deterministic observation ID as the command ID on retry.

The actor restores committed partitions before registering live routes. Old sessions with open
buckets or pending publications remain scheduled for barrier recovery. Source timestamps, prices,
volume, sequence and epoch are retained; no bars are reconstructed from invented quotes. The
bar actor explicitly propagates failures through the actor base, even when mailbox metrics are
absent. Other actors retain their existing exception behavior.

Both local and remote MarketData DI registrations enable the store. Its configurable directory is
`AppSettings:TradeSessionBarRecovery:Directory`, defaulting to
`%LOCALAPPDATA%/IFM/trade-session-bars`. The directory must remain on persistent storage. A writer
lease prevents two processes from owning it concurrently. Collection, frame and journal sizes
are bounded; corrupt committed data blocks recovery. Only an incomplete unacknowledged trailing
frame is truncated. There is no silent fallback to an empty accumulator.

The binary FBR1 journal appends checksummed full checkpoints, physically flushes each ownership
transition, and compacts by durable atomic replacement before exceeding 4 MiB. Cached file handles
avoid reopening the journal on each trade and are released after a session has no remaining work.
Quote-only updates do not checkpoint the trade accumulator. There is no JSON on this recovery path.

Verification:

- 31 focused bar tests passed, including exact OHLCV/source-lineage equivalence, buffered gaps,
  retired epochs, barrier recovery, partially accepted publication prefixes, disk-write failures,
  torn tails, corruption, exclusive ownership and journal compaction after 2,500 inputs.
- Separate child processes exited without shutdown/disposal after an acknowledged open input and
  after a failed closed-bar publication. A new process restored both cases and duplicate delivery
  produced no extra observation.
- Disposable NATS 2.12 Memory and File consumers both retained a failed delivery, redelivered it
  after registry restart, and acknowledged it only after the pending publication succeeded.
  These probes substitute the downstream command transport; they are not PostgreSQL integration
  tests or a completed production mailbox adapter.
- Release API/benchmark build: zero warnings and errors. The broad analytics test run had 1,354
  passes and three failures outside this change: Daily-up overshoot expectation and two option-IV
  SQL reflection lookups. The same three failures reproduced in isolation; their source files
  were not changed by this repair.

The current API was not restarted or switched to these changes. The historical evidence below
records the original failure; it no longer describes the repaired handler's test results.

### Checkpoint BenchmarkDotNet result

The final run uses the same synthetic ordinary trade-update handler with checkpointing disabled
(volatile reference) versus enabled. It does not time a real downstream database command, include
broker round trips, or represent the complete queue adapter. Updates are serial, not grouped into
one disk commit. Settings: .NET 10 Release, InProcessNoEmit, three warmups, eight iterations,
16 invocations, no forced collection. The calculation and publication paths being compared are
otherwise identical; the volatile reference includes the repaired in-memory pending-bar handling.

| Updates per invocation | Volatile reference | Durable journal | Allocated volatile / durable |
| --- | ---: | ---: | ---: |
| 1 | 35.13 Âµs | 6.119 ms | 1.53 / 10.49 KiB |
| 64 | 1.523 ms | 433.903 ms | 98.18 / 647.18 KiB |

No Gen 2 collections were observed in this component run. These are managed allocation totals,
not measured live API LOH traffic. The short volatile measurements carry BenchmarkDotNet's
minimum-iteration warnings; they should not be used for a precise speedup ratio.

The first, abandoned per-tick atomic-replacement implementation measured about 16.28 ms for one
update. Append-only checkpoint frames and retained writers reduce that cost, but physical flush
per tick is still materially slower than volatile state: approximately 148 updates/second in the
64-update case on this machine. **The correctness prerequisite is repaired, but this per-message
commit rate is not sufficient evidence for high-volume production cutover.** The next mailbox
implementation must batch durable input/checkpoint commits before acknowledging those inputs,
or reconstruct open state from an already committed input journal, and repeat the burst tests.
Do not enable the queue replacement from these component timings alone. Neither the 2-second
full-pipeline deadline nor the two-session trial has been qualified by this work.

Artifacts: `.artifacts/realtime-mailboxes/20261124-safety/` â€”
`bar-recovery-tests-verified.log`, `bar-recovery-crash-final.log`,
`bar-recovery-broker-proof-final.log`, `bar-checkpoint-final-benchmarks/results/`.
Intermediate aborted timing logs are retained for diagnosis and excluded from the final table.

## Original blocker (before repair)

Replacing the mailbox queue alone cannot make `FuturesTradeSessionBarSignal` recoverable. Its real
handler advances the in-memory trade/bar accumulator before the downstream bar publication completes.
A rejected publication can be logged while the handler returns success. Even when publication throws
and transport redelivery occurs, the already processed trade ordinal is ignored and the failed bar is
not generated again. A fresh accumulator also has no previously acknowledged open-bar contents.

This is an executable compatibility failure, not a claim that these exact synthetic failures occurred
in the live feed. No change to the current live queues or readiness checks was made during this work.

## Fault injection evidence

The shared fixture uses the actual bar handler, actual accumulator registry, CME session calendar
and two contiguous trade ordinals from the same epoch. The only substituted boundary is the
downstream publication command transport. The synthetic trading date is 2026-08-25; event times
are session start plus 1 second and 17 seconds, with a fixed calculation clock at plus 40 seconds.
The second trade should complete one fifteen-second bar containing the first trade.

| Scenario | Observed outcome |
| --- | --- |
| Healthy continuous processing | One completed bar published successfully |
| Downstream command returns a failure | Handler still returns `true`; one publication attempt, zero successful bars |
| Redeliver the triggering trade after removing that failure | No second publication attempt; zero successful bars |
| Publication throws, then the same tick is redelivered after recovery | Exception escapes initially; retry still makes no second publication attempt |
| First trade handled before accumulator replacement; only the next pending trade delivered afterward | Fresh accumulator publishes zero bars versus one with uninterrupted processing |

The final case models application-state replacement, not a destructive broker restart. It shows
that keeping only pending inputs in WorkQueue storage is insufficient: the acknowledged input that
contributed to an unfinished bar is no longer available from that mailbox's queue alone.

The transport verifier also reproduced both normal-return failure and fresh-accumulator cases using
actual disposable JetStream Memory and File consumers. Each backend delivered and acknowledged both
input messages. After the injected publication failure there were zero broker-pending messages and
zero successfully published bars. Durable input delivery therefore passed while the derived-output
ownership failed. File storage is not malfunctioning in this test: the handler reported success
before its business output had a recoverable owner.

Eight focused tests passed: the existing accumulator cases and the new safety qualification test.
The new test deliberately proves the current failure exposure; passing it does not mean the exposure
has been repaired. `--verify-realtime-mailbox-safety` also emits all three counterexample outcomes.

Relevant code:

- [Accumulator consumes the ordinal and removes completed buckets](../../TomasAI.IFM.Domain.MarketData.Analytics/FuturesTradeSessionBarSignal/Realtime/Model/FuturesTradeSessionBarAccumulator.cs)
- [Handler publishes after accumulation and returns success after reported failure](../../TomasAI.IFM.Domain.MarketData.Analytics/FuturesTradeSessionBarSignal/Realtime/FuturesMarketPriceUpdated.cs)
- [Publication constructs the command and returns the downstream result](../../TomasAI.IFM.Domain.MarketData.Analytics/FuturesTradeSessionBarSignal/Realtime/Extensions/FuturesTradeSessionBarSignalRealtimeExtensions.cs)
- [Safety fixture](../../test-support/RealtimeMailboxSafetyFixture.cs)
- [Regression test](../../TomasAI.IFM.Domain.MarketData.Analytics.UnitTests/FuturesTradeSessionBarSignal/RealtimeMailboxRedeliverySafetyTests.cs)

## Work completed and remaining scope

Captured a live baseline with all 25 registered Realtime destinations and mapped their implementation
files/handled event types into a preliminary inventory. At 10:36:25 ET the snapshot contained 772
retained Realtime mailboxes, 4,460 queued messages and 69,517 rejections on retained mailbox counters.
Those are observations, not permanent incident totals or a completed capacity/payload worksheet.
Readiness returned HTTP 200 at baseline collection.

Added isolated Core NATS/current local queue versus JetStream Memory/File transport probes. Each
probe verifies payload/checksum equivalence. They do not implement production actor scheduling,
durable source publication, branch manifests, handler checkpointing or a recoverable mailbox registry.
The initial qualification changed only tests, harnesses and documentation. The subsequent repair
also changes the bar recovery/handler path and its DI registrations, as documented above.

## Benchmark qualification scope

The before probe publishes over Core NATS, receives the payload, admits it into the current
`ActorThreadQueueV2` under enforced admission, drains it and executes a small identical checksum
handler. The two candidate probes confirm JetStream publication, pull the same number of payloads,
execute that checksum and receive explicit processing acknowledgement confirmation.

Parameters are synthetic 512-byte and 8,192-byte payloads, with 1 or 64 messages per invocation.
Each broker destination has its own explicit-ack consumer. Streams use WorkQueue retention,
`DiscardNew`, per-subject count 512, maximum payload 256 KiB, maximum stream bytes 64 MiB,
one replica and `MaxAckPending=128`. This is an isolated transport-cost comparison, not a completed
actor-backend benchmark or a two-second strategy-pipeline qualification.

BenchmarkDotNet uses .NET 10 Release, the in-process no-emit toolchain, three warmups, eight measured
iterations and eight invocations per iteration. No forced-GC setting is introduced. The disposable
NATS server is pinned to 2.12.0; only synthetic data and isolated fixture resources are used. File
results use the broker's normal sync policy, not a per-message power-loss guarantee. Unrelated live
development workloads share the machine, so timing variability must remain visible in the reports.

This benchmark includes confirmed ACK and a publish-then-fetch sequence; an eventual streaming
adapter may have a different latency/throughput profile. The bounded-batch probes pipeline at most
64 publications with separately owned payloads, execute the checksum handler in delivery order and
confirm all processing ACKs before returning. They also verify delivery order and checksum equality.
They do not implement production failure settlement, a source outbox or business checkpoint costs.
Those must be included before production comparison.

## Completed before and after transport probes

The primary table is from one completed 12-case BenchmarkDotNet run, `benchmarks-comparison`.
Times are means per invocation, not per individual message in the 64-message cases.

| Messages | Payload each | Before Core NATS plus local mailbox | After Memory batch probe | After File batch probe |
| --- | --- | --- | --- | --- |
| 1 | 512 B | 1.882 ms | 5.487 ms | 6.182 ms |
| 1 | 8 KiB | 2.138 ms | 6.776 ms | 8.126 ms |
| 64 | 512 B | 2.898 ms | 10.354 ms | 11.217 ms |
| 64 | 8 KiB | 9.402 ms | 20.488 ms | 22.481 ms |

| Messages | Payload each | Before allocated | Memory batch allocated | File batch allocated |
| --- | --- | --- | --- | --- |
| 1 | 512 B | 1.62 KiB | 12.52 KiB | 12.50 KiB |
| 1 | 8 KiB | 9.77 KiB | 27.57 KiB | 35.55 KiB |
| 64 | 512 B | 43.13 KiB | 496.64 KiB | 483.80 KiB |
| 64 | 8 KiB | 532.00 KiB | 1,458.22 KiB | 1,454.88 KiB |

No Gen 2 collections were observed by MemoryDiagnoser in these measured cases. This is not a live
LOH or Gen 2 qualification. Timing error and standard-deviation columns remain in the generated
reports; the machine also runs the development workload.

Confirmed broker publication and processing acknowledgements add latency and allocation compared
with the current Core/local path. In this run Memory's mean was lower than File's mean, with timing
intervals overlapping in several cases. These measurements do not justify calling file storage
prohibitively slow or predicting a production throughput ceiling.

A separate completed 12-case serial-confirmation run, `benchmarks-final`, measured 64 x 512 B at
2.566 ms before, 204.907 ms Memory and 207.636 ms File. For 64 x 8 KiB it measured 11.774 ms before,
223.335 ms Memory and 231.574 ms File. This demonstrates the cost of serial network round trips;
bounded pipelining is required before considering the actor adapter. The two runs are separate
samples and are not a controlled full-system speedup comparison.

Two intermediate runs are excluded: the first stopped on a disposable Docker startup failure; a
later mixed run stopped on a missing confirmed ACK reply during BenchmarkDotNet's diagnostic pass.
Their logs remain available. No retries or silent success were added to conceal either failure.
The isolated transport implementation still needs the production recovery gates in the plan.

These are component probe results, not deployed after results. The production actor backend remains
unimplemented at the Stage 1 blocker, and the two-full-session comparison has not started.

Artifacts: `.artifacts/realtime-mailboxes/20261124-safety/` contains the registration snapshot,
inventory, safety proof, test/build logs, transport checksum proof and BenchmarkDotNet output.

## Required repair before proceeding

1. Retain completed-but-unpublished bars until downstream publication has a recoverable, confirmed
   owner. A failure must not erase the bar or leave only an already-consumed input ordinal.
2. Make failure outcomes observable by the mailbox delivery boundary so a failed handoff cannot
   become a successful ACK merely because its handler returned normally.
3. Checkpoint open-bar state/source cursors before acknowledging contributing inputs, or implement
   verified restart reconstruction from an authentic retained input range. Verify continuation and
   every affected timeframe rather than fabricating partial valid-looking bars.
4. Re-run these counterexamples as successful recovery tests, then continue the remaining destination
   idempotency/source-ownership audit. Do not assume this first blocker is the only remaining one.

The trading thresholds, indicator formulas and entry/exit rules need not change to repair this data
ownership problem. The repair does require bar-processing recovery/publication work beyond swapping
the queue storage adapter. Until it is qualified, leave the new production mailbox backend disabled.
