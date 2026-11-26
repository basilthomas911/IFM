# Realtime actor durable mailbox — implementation plan v2.0

**Date:** 2026-11-25, America/Toronto\
**Status:** Planned; this task creates the plan and does not implement fixes or activate services.\
**Scope:** All **24 audited work packages**: 22 core plus A1/A2 account safety.\
**Supersedes:** [v1.0 sequential execution plan](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v1.0.md), retaining its historical evidence and original nine exit gates.\
**Inputs:** [Complete audit](Realtime-Actor-JetStream-Mailbox-Remaining-Blockers-Audit-2026-11-25.md), [24-item inventory](Realtime-Actor-JetStream-Mailbox-Remaining-Work-2026-11-25.json).\
**Execution checklist:** [Machine-readable plan](Realtime-Actor-JetStream-Mailbox-Execution-Plan-v2.0.json).

## Outcome

Deliver a Realtime-only independently consumed JetStream mailbox per logical ActorThreadId, with
recoverable required publication, safe retry/restart behavior, bounded resources, and verified
business outcomes. Complete every audited workflow/producer/destination/account package together.
Preserve ordinary display coalescing, existing market-data/raw-storage and domain-event durability,
and trading eligibility/expiry rules. Report code completion, qualification and activation separately.

All 25 current destinations and all 33 workflow boundary rows are acceptance checklists. An issue
already listed in this plan is work to execute, not a reason to stop and ask for another isolated fix.

## Non-negotiable behavior

- Decision operators use prepared, validated, immutable cached inputs. No database/provider input
  fetches, configuration loading, warm-up acquisition or retry loops inside strategy decisions.
  Required inputs are validated when created/published; preserve existing identity/freshness guards
  without adding repeated full validation to every operator.
- Keep the two-second stage deadline. Measure ReadyAt -> preparation -> calculation -> durable
  terminal ownership, and owner acknowledgement separately. Bound/cancel delivery waits against
  the remaining budget; uncertain acceptance becomes owned recovery work, never a fabricated
  calculation failure or a restarted two-second timer. If durable capture is unavailable/full,
  retain source ownership, fail closed and surface readiness; do not claim availability can be
  guaranteed through a storage outage. Queue age is separate and original 30-second freshness remains.
- Durable source capture, command commits and authoritative outcome reconciliation are outside
  pure calculation. Recovery may read owner receipts; it must not fetch new decision inputs or
  rerun eligibility from an old signal. Record these recovery IO costs explicitly.
- Never weaken MatchesResult/MatchesApproval, reference-identity, financial receipt, source-age,
  route-generation or deadline guards to make a replay succeed. Never restamp old market input fresh.
- Cold-session unaccepted nonfinancial decisions still end as interrupted/expired No Trade. Already
  approved financial EntryCommit recovery is reconcile-only: no fresh eligibility calculation or
  new old-session financial effects. Unknown money outcomes remain unknown until resolved by authority.
- Required raw/history, financial, order/exit and lifecycle/control work is preserved. MarketOutlook,
  ordinary Notify and explicitly latest-state wakeups may coalesce with proven catch-up. Final
  close/feed-off must leave the open UI screen readable and authoritative closed state available.
- Memory is the requested development default and recovers API restart with NATS alive. Broker
  restart/OS-power loss requires File or independently proven persistent source reconstruction;
  explicitly test and state the boundary. File sync/replication policy must match the guarantee.
- Non-Realtime transport behavior is unchanged. A1/A2 intentionally fix account-domain convergence
  and evidence ordering on its existing Event/Command path; they do not migrate it into Realtime.

## Agent model and file ownership

The available maximum is **four active agents: root plus three subagents**. Reuse agents by lane
and phase; do not split one shared workflow actor file among several writers.

| Lane | Main ownership | Packages | File boundaries |
| --- | --- | --- | --- |
| Root — protocol, infrastructure and integration | Shared contracts/receipt semantics, canonical codec, registry, Realtime producer/consumer/scheduler, ACK/generation, capacity/metrics, integration and rollout | INF1–INF6, VAL1–VAL4; shared W1/W6/W7/W8 contract | Framework/Shared.Contracts/EventModelActor; NATS transport; global Actor runtime DI; plan/evidence manifests. Root alone integrates shared registration/fixture interfaces. |
| Agent A — workflow and financial handoffs | All workflow stages and their owner adapters in one lane | W1–W8 | IntrinsicTime workflow actors/helpers/runtime/owners, related Fund/Portfolio adapters and workflow-only tests. Own the existing WorkflowDispatchDeliveryFixture after root publishes its required interface patch. |
| Agent B — producer/native ingress | Source capture, admission progress, native/managed publication and frozen routing obligations | P1 and producer side of INF2/INF4/INF5 | Rust dataset/messaging delivery, managed ingress/receiver/publishers, raw source ownership and producer-specific tests. Do not edit root-owned NatsActorProducer/consumer/registration files. |
| Agent C — destinations and accounts | Lifecycle/control state, metrics-free settlement, versioned position input, account convergence/evidence ordering | D1–D3, A1/A2 | Feed/Trade destination handlers and dedicated fixtures; BrokerAccount bridge/owner and emulator account wakeups. Do not edit root framework/base actors or Agent A workflow hot files. |

Use disjoint files and isolated broker/database resources. Shared interface changes go through root;
if two lanes require the same file, root assigns one owner and integrates the other's patch after
that owner finishes. No destructive reset/cleanup of the existing dirty workspace. Separate build
artifacts per independent project; avoid simultaneous builds writing the same project/artifact tree.
The coordinator serializes BenchmarkDotNet runs and heavyweight competing workloads.

## Stages and parallel execution

| Stage | Root | Agent A | Agent B | Agent C | Exit gate |
| --- | --- | --- | --- | --- | --- |
| S0 — baseline | Preserve source/config checksums, acceptance sheet and current-backend trace | Catalogue all 33 boundary variants and baseline workflow benchmarks | Catalogue every source/recipient and baseline ingress/capture | Baseline destination/account cases and supported modes | Frozen matched before workloads, 12 audit scenarios mapped, numeric SLO/capacity measurement plan |
| S1 — shared protocol | Publish/test versioned intent, output receipt, capture and owner disposition contracts | Review real owner trust/session rules and migrate strategy | Review native/source capture ACK and manifest semantics | Review control/display and version/account order semantics; independent D2/A2 work can proceed after baseline | Interfaces and recovery policy frozen; no circular ownership or blind approval deserialization |
| S2 — parallel components | INF1 registry/envelope/backend skeleton, start INF3 | Apply W1–W8 across all stage variants; not just MC completion | P1 staged admission, native source persistence/reconstruction ports | D2/A2, then D1/D3 and A1 after evidence-order fence | Component behavior tests pass with live backend unchanged |
| S3 — integrate full path | Complete INF2–INF4/INF6 and durable producer fan-out | Actual workflow owner receipt/recovery wiring | Connect all required native/managed/domain source families | Lifecycle/account/versioned input integration and actual receivers | Every enabled required source/verb owns pending work; no double Core/JS path |
| S4 — faults and bounds | VAL1, INF5 saturation/retention and all regression coordinator | Workflow/financial fault cases | Native/capture/watermark fan-out fault cases | Destination/close/account/upgrade fault cases | Actual owners/schemas and money invariants pass; bounded retention/exhaustion; all audit cases converted |
| S5 — matched performance | VAL2 runs in a serialized benchmark window | Supply workflow workloads and explanations | Supply source/burst/cardinality workloads | Supply destination/account resource checks | Before/local vs Memory vs File meet predeclared limits with complete cost accounting |
| S6 — matched sessions | VAL3 source/session manifest and reconciliation | Workflow/financial reconciliation | Source/recipient identity and backlog reconciliation | EOD/rollover, close/UI/account reconciliation | Two full matching sessions per backend, isolated effects and declared failure scopes |
| S7 — development cutover | VAL4 epoch/drain/migration, profile verification and rollback | Validate pending workflow settlement | Validate source cursor continuity | Validate UI/closed-trade/feed/account readiness | Controlled development activation/rollback demonstrated; final completion report |

The table shows shared protocol dependency, not a requirement for every lane to idle. Independent
baseline/test preparation and D2/A2 work can overlap S1. Root may start backend skeletons while
workflow/producer changes proceed against the frozen contract. Full live delivery does not switch
until the downstream prerequisites and qualification gates pass.

## Shared identity, outcome and recovery protocol — S1

1. **Accepted intent identity:** stage execution key + canonical accepted input digest. RD/MC include
   the accepted workflow view, original trigger and frozen configuration; exclude new outer
   notification IDs/receive times/authorized redispatch metadata. TS/OC use the exact persisted
   dispatch. Another authorized delivery aliases the same intent; changed accepted content fails closed.
2. **Frozen publication:** before first handoff, durably own original request, typed outcome/command,
   result bytes/fingerprints, parameter evidence and origin/causation. Cover completion, refusal,
   timeout, preparation acceptance, reservation receipt and financial advancement through one
   versioned API/registry, with bounded batched persistence. Separate calculation catches from send.
3. **Outcome state machine:** Captured/Pending; OutcomeUnknown; AuthoritativelyAccepted;
   AuthoritativelyInterruptedOrExpiredWithoutEffects; Confirmed; SafelyRetired. A generic success
   transport return does not replace the owner acceptance/disposition required for each command.
4. **Current session:** preserve the original trusted runtime-produced approval object through first
   acceptance where owner guards require reference identity. A copied/deserialized approval must
   remain rejected. Do not solve this by replacing ReferenceEquals with unverified object content.
5. **Cold recovery:** first reconcile original command ID/hash with actual owner audit/receipt. If
   accepted, confirm that exact outcome. If unaccepted and expired/old-session, durably record the
   authoritative interrupted/expired No Trade disposition and retire the pending output. If the
   financial outcome may exist, reconcile its existing operation/receipt under owner authority;
   absence must be authoritative before concluding no effects. Do not create a second commit.
6. **Compatibility:** retain v1/v2 existing journal readers and exact old terminal replay. Migrate
   source-hash-only records to canonical intent identity only using verified original evidence;
   never reinterpret a legacy source hash as an intent hash. Unknown legacy origin stays unknown.
7. **Transport capture:** source-to-destination capture receipt means broker publish ACK or flushed
   recoverable outbox ownership, not Core flush, enqueue or business handler completion. Preserve
   per-recipient completion and original manifest across ambiguous acceptance/partial fan-out.
8. **Wakeups:** repeated clock/latest-state wakeups may share a logical identity with newer receive
   time. Define their merge/reconstruction contract separately from immutable market facts. Never
   broaden that exception to ticks or refresh an old signal's business timestamp.

## Complete package checklist

Every row below inherits the full acceptance criterion and scope from the machine-readable audit.
No package is closed merely because one representative handler/test passes.

| Package | Owner | Required implementation and verification | Component ready |
| --- | --- | --- | --- |
| W1 | A + root contract | One frozen-output protocol across MC completion, prepared/legacy Risk failure/preparation, entry-commit outcome and financial-advance commands; exact owner outcome/bytes/provenance | S2 |
| W2 | A | Calculation-only catches; uncertain accepted approval remains owned completion/reconciliation, never contradictory failure | S2 |
| W3 | A | Freeze original Portfolio operation/request/hash/evidence before evaluation; real store retry/reservation invariants | S2 |
| W4 | A | Preserve canonical first reservation receipt; Committed/IdempotentReplay presentation cannot change the workflow command | S2 |
| W5 | A | Durable timeout/late-reservation reconciliation and stopped cleanup; required failed read never false-success; survive death during callback | S2 |
| W6 | A + root policy | Unaccepted composition preparation is persisted exactly or durably interrupted/retired; no changed recapture under old authority | S2 |
| W7 | A + root policy | Owner proof/session-compatible acceptance/reconciliation, forged/copied approval rejection, expired/interrupted retirement without stale trades | S2 |
| W8 | A | Pending and sent journal aliases allow legitimate redispatch; canonical accepted-content tampering still rejected | S2 |
| P1 | B | Validate/own pending admission/commit watermark in stages; mirror/latest progress cannot fence a pending required replay; successor cannot overtake | S3 |
| D1 | C | Capture lifecycle/control intent before route/cache mutation; confirmed stages and idempotent cleanup; reconcile independent IronCondor final snapshots/read repair and last feed-off | S3 |
| D2 | C | Failure propagation independent of optional metrics in raw-durable and legacy supported modes; no duplicate storage route | S2 |
| D3 | C | Freeze accepted versioned plan parameters/request/hash/times; exact old receipt or explicit incompatible unaccepted-work retirement; never discard financial exit authority | S3 |
| A2 | C | Authoritative monotonic generation + revision/time evidence; older/equal-time conflicting snapshot cannot reopen risk; preserve manual/qualification holds | S2 |
| A1 | C | Latest account replay/resync ownership, supervised startup/reader, last-failure convergence without next callback; bounded wakeups cannot stall ledger/order/quote work | S3, after A2 |
| INF1 | Root | Realtime-only options, stable ActorThreadId key, binary envelope and recoverable async registry; all verbs share one ordered consumer | S2 |
| INF2 | Root + B | Confirmed source/recipient manifest/outbox for every required source family; uncertainty/partial fan-out recovery; reject unsupported enabled profiles | S3 |
| INF3 | Root | Bounded async pulls/bytes, single owner/mailbox, ACK and failure ordering/fairness; no shared Realtime admission/stripe blockage or cycle deadlock | S3 |
| INF4 | Root | Idle eviction retains broker work, restart discovery, generation/rollover and late-write/ACK fencing; obsolete-vs-reconstructable evidence explicit | S3 |
| INF5 | Root | Numeric burst/outage budgets, DiscardNew/no required TTL, refused-work source ownership, batched durable commits, safe receipt pruning and disk/broker exhaustion | S4 |
| INF6 | Root | Queue bytes/messages/age, in-flight/ACK/refusal/outbox/cursor and source-gap metrics; truthful affected readiness and cumulative incidents across eviction | S3 |
| VAL1 | Root + A/B/C | Actual workflow/Fund/Portfolio/financial/Function/projector owners on real disposable schemas; complete fault and non-Realtime regression matrix | S4 |
| VAL2 | Root | Matched full-backend BenchmarkDotNet, raw latency distributions and complete API+NATS cost accounting; fixed acceptance thresholds | S5 |
| VAL3 | Root + A/B/C | Two complete authoritative captured sessions for each mode; full source/recipient/business reconciliation; no live financial effects | S6 |
| VAL4 | Root | Explicit epoch, drain/migrate pending work, exclude double delivery, activate development profile and verify reverse rollback with preserved source identity | S7 |

Component-ready S2/S3/S4 gates above do not close a package. Implementation packages close only
after their relevant S4 actual-owner/recovery/regression evidence and S5 measured resource gates
pass. The execution checklist records component readiness separately from final qualification.

## Producer/destination coverage that INF2 must implement

- Native raw trades/quote batches: preserve existing raw WAL/storage receipts and faster individual-row storage batching. Do not replace rows with opaque arrays.
- Official statistics, replay-complete/VWAP/control frames and the last EOD values: persist original assembled output or prove exact original-time reconstruction; raw Trade/Quote capture alone is insufficient.
- Option trade/pricing evidence: retain original source and storage acceptance, then confirm required actor recipients through native/API/generation restart.
- Option quote/greeks observations: explicitly separate required exit/order/position consumers from permitted latest/display coalescing.
- Domain projectors/notifications: retire parent work only after every required destination is captured or durably owned; recover partial fan-out independently.
- Clocks, bar barriers, monitoring-input callbacks: deterministic wakeup/reconstruction semantics, including repeated clock ID with changed ReceivedOn.
- EOD coalescer, source receipt cursors and recovery scanners: last-message failure recovery without another market event.
- Emulator order facts: keep repaired financial journal and ordered publication cursor; verify actual downstream effects, not only byte receipt tables.
- Account A1/A2 stays on existing account Event/Command transport; coalescing wakeups is allowed, coalescing fills/cash facts is not.
- All enabled alternate/compatibility/nonrecoverable broker profiles must either pass their declared guarantee or be explicitly unsupported at startup; never silently fall back through an unprotected path.

Refresh the audit's 25 actor/event maps if implementation adds an accepted verb. Keep the 33 workflow
boundary matrix current. MarketOutlook display coalescing and the three intentional exit-start no-op
notification actors do not need fabricated business fixes.

## Unified verification matrix — VAL1

| Boundary/fault | Required evidence |
| --- | --- |
| Before capture/send; capacity refusal | Original source still owned, no false ACK, no successor overtaking |
| Captured/owner audit not committed | Exact command/result retained, no premature success marker |
| Owner committed, reply lost | Same ID/hash/bytes reconciled; no contradictory failure or duplicate money |
| Death before local confirmation/source ACK | Cold original outcome or authoritative interrupted retirement; source cursor consistent |
| Typed refusal; unknown financial outcome | Correct durable disposition, preserve unknown until authoritative reconciliation |
| RD/MC redispatch aliases, pending and sent | Same accepted intent settles; changed accepted content rejected |
| TS/OC local proof; copied/forged approvals | Valid current proof accepted, copies/forgeries rejected; old session cannot reenter |
| EntryCommit approved/rejected/unknown | Atomic real financial transaction exactly once, receipt absence resolved under authority, no old-session new commit |
| Reservation timeout before late commit | Durable reconciliation until final disposition; process/read failures cannot leak required cleanup |
| Generation/contract/attachment rollover | No old writes/late ACK, no route resurrection; required data recovered or proven obsolete |
| Idle queue eviction; actor/broker/API/native restart | Same logical consumer/cursor, preserved pending identities; Memory loss boundary explicit |
| Partial fan-out, self-send and cycles | Independent recipients progress, no double settlement or deadlock |
| Account last failure/reader dead/>4,096 wakeups | Last snapshot converges without next callback; financial progress not blocked |
| Account stale/equal-time resync race | Old evidence cannot reopen a newer closed gate or remove holds |
| Legacy/parameter/version/codec migration | Exact compatible replay; deterministic retirement for unaccepted incompatible work; no lost exit/reservation |
| Actual final close/UI reconnect | Balances/fills/closed position/final TradePlan correct, routes cleaned, feed disabled last, existing screen stays open |
| Pool lifetime and non-Realtime regression | Payload released once after correct ownership transfer; other categories' delivery/ordering unchanged |

Convert the 12 audit scenarios to desired-behavior tests; retain the rejected same-version parameter
hypothesis as documentation. Each implemented package must attach its test/owner/schema/fault evidence
to its checklist. Run relevant full projects/architecture/runtime regressions after component checks;
central integration resolves cross-project failures before proceeding.

## Before/after measurement contract — VAL2

Capture the before workload before code changes or preserve an exact executable baseline with source
and configuration hashes. Existing journal-disabled probes may remain component baselines but cannot
stand in for complete old-vs-new behavior. Declare which durability guarantee each comparison provides.

- Workloads: every workflow terminal/request family and account/destination fix, idle/steady/burst/saturation,
  small/representative/maximum payloads, one hot mailbox and thousands of hot/cold identities, complete fan-out,
  pending replay, restart and retained history growth. Use identical source tapes/settings and owner behavior.
- Report before/local/Memory/File mean and error intervals for BenchmarkDotNet plus separately captured
  p50/p95/p99/max publication ACK, queue, preparation, calculation, durable capture and owner/end-to-end times.
- Report throughput/drain, CPU, GC pause, allocations and measured LOH/Gen2, API **and NATS** RAM, file IO,
  storage growth/consumer metadata, mutex contention and bounded waiting tasks. Aggregate bytes/op is not LOH.
- Include batching/sync/outbox costs on both sides. Use bounded group commits where needed, only ACK after
  durable batch ownership; record tail latency and the batching time bound. Do not hide journal fsync costs.
- Declare numeric payload/fetch/stream/consumer/outage/headroom budgets from S0 observations before evaluating
  candidates. No automatic TTL/DiscardOld for required work; retention pruning needs a proven receipt horizon.
- The hard decision deadline stays two seconds; all measured stage samples under the qualified workload must
  meet it or end safely under that deadline. Show pending transport recovery separately; an outage is not a
  successful low-latency decision. A slow backend or unbounded growth fails the gate; do not drop work to pass.
- Serialize benchmark runs, prohibit competing builds/replays, record host/runtime/job/configuration and
  deterministic dataset hashes. No claim of whole-process GC improvement from a short microbenchmark alone.

## Session qualification and controlled development rollout — VAL3/VAL4

Use two complete calendar-qualified exchange sessions per backend with identical source/destination
manifests and initial owner/database/config snapshots. Prefer complete recorded captures if they contain
all required source families; otherwise acquire authoritative full-session captures during execution.
Include overnight, cash open/close, EOD and rollover. A shortened holiday session must be identified and
cannot quietly replace the required comparable normal-session evidence.

Run isolated broker namespaces/databases and only one authoritative financial path. Shadows/replays
must not submit real orders, modify live balances or publish to live subjects. Memory and File cover
the same two sessions, not one unmatched day each. Reconcile original source and per-recipient IDs,
outputs, workflow/financial revisions, positions, final plans, source cursors and pending/refused ownership.

Only after the gates pass: pause affected publication, account for backlog, drain/migrate pending work,
switch an explicit transport epoch, exclude Core/JS double execution and resume. Verify the VS Code API
+ Rust eGui + Rust MarketData development profile, existing workflows, indicators, raw history, EOD,
closed-trade screen/read repair and /health/ready without relaxed checks. Exercise reverse transition
with pending work and keep the old backend available. Production deployment is outside this plan.

## Execution, stopping and completion rules

- Execute the entire listed backlog in dependency order, using parallel lanes. A known audited defect,
  supported legacy mode or failed test within these packages is expected work, not another user handoff.
- Root handles lane conflicts, failing checks and integration. Stop only for a genuinely new unresolvable
  external dependency, an uncovered scope/authority decision, irrecoverable required source evidence or
  an exit-gate failure that cannot be safely addressed within the plan. Name the exact dependency and
  continue independent packages while it is blocked. Do not weaken guarantees or mark it complete.
- If two-session wall-clock evidence remains outstanding, report **implementation complete; session
  qualification pending**, keeping activation disabled. This is not completion of the full plan.
- A package is complete only with its desired behavior, real-owner or explicit authoritative-retirement
  evidence, compatible upgrade/recovery, relevant regression checks, measured resource cost and documentation.
- The whole plan is complete only when all 24 packages and S0–S7 gates pass, both session/backend comparisons
  exist, and controlled development cutover/rollback verification is recorded. Account A1/A2 is included.
- Final report: per-package completion/evidence, before/after timings and allocation/LOH/Gen2, actual owner
  financial invariants, capacity/retention limits, supported loss boundaries, session reconciliation and rollback.

No application code was changed or services restarted to create this plan. The earlier audit/results
remain historical evidence; this document does not relabel planned work as implemented.


## Execution update - 2026-11-25

Execution is underway with root and three parallel agents. The immutable pre-change source manifest/archive and executable baseline are under `.artifacts/realtime-mailboxes/20261125-execution-v2/baseline`. Application activation remains disabled.

Workflow W1-W8 and destination/account D1-D3/A1/A2 have implementation and component evidence; actual PostgreSQL workflow/Fund receipts and all three financial round-trip/exit families have been exercised. Package qualification is not inferred from those component tests. Current counters and remaining gates are recorded in the execution JSON and lane result documents.

The realtime backend now has frozen fan-out, bounded flushed source capture, per-recipient durable handler-success proofs before broker ACK, exact restart deduplication, and Memory/File transport tests. Unfinished original bodies remain locally owned until all required handler proofs exist. This permits classified missing-stream reconstruction with protocol-v2 ownership evidence; broker loss is not treated as successful delivery. Missing/corrupt original ownership remains failed closed. Required broker duplicates with completed handler proof are ACKed without executing the handler again.

Measured native IO exposed a throughput limit: 4,096 entropy-bearing 512-byte sources took a median 32,331 ms with per-source flushes (127/s), 5,480 ms in groups of eight (747/s), and 2,048 ms in groups of 64 (2,000/s). These are native timed disk measurements, not BenchmarkDotNet or whole-path throughput. Bounded ordered source batches and opted-in actor batches are now integrated. Managed ITI preparation/release for 64 commands measured 929.08 ms serial versus 19.41 ms grouped in BenchmarkDotNet.

The latest NATS suite passed 167/167, the consolidated Trade RealtimeDelivery suite 274/274, focused workflow/real-owner/byte-compatibility checks 27/27, and actual financial-owner round-trip/exit cases 6/6. Two complete 60-case serialized transport BenchmarkDotNet sweeps and scoped LOH/Gen 2 probes were recorded. Safe journal and transport-codec copy reductions preserve existing wire bytes/hashes. A measured healthy-backlog scheduling defect was fixed without removing failed-head backoff or ordering.

The full plan is not complete. The final Nov 25 latency follow-up passes three first workflow traversals, strict blocked-provider termination, operational warm/burst workloads. Bulk cold creation of 3,000 consumers is explicit test-only stress, outside the two-second decision gate; its raw File maximum 5.77 seconds versus 17.98 before is retained unchanged. The full harness includes the stress phase when explicitly invoked; ordinary application startup does not run it. All four matched runs reconcile original input/owner hashes and zero duplicate effects. Full NATS correctness/retention passes 249 cases including 20,000 receipts; workflow/runtime passes 322 and startup/readiness passes five. See `Realtime-Mailbox-Latency-Remediation-2026-11-25.md` for exact scopes and final results. Fully eligible options, complete sessions, matching owner snapshots, production-owner replay/resource reconciliation and controlled rollout remain open. Nov 30/Dec 1 jobs remain PreparedNotArmed; existing live source-gap/readiness and snapshot-floor blockers were not repaired by this unactivated build. Activation remains disabled; no live profile was restarted.
