# Realtime Actor JetStream Mailbox Implementation Plan

**Version:** 1.0\
**Created:** 2026-11-24\
**Status:** Superseded for execution by v2.0; historical audit and prerequisite evidence retained
**Design:** [Realtime Actor JetStream Mailbox Design](Realtime-Actor-JetStream-Mailbox-Design-v0.1.md)

## Superseded for execution — 2026-11-25

Use the [v2.0 complete parallel implementation plan](Realtime-Actor-JetStream-Mailbox-Implementation-Plan-v2.0.md)
and its [24-package execution checklist](Realtime-Actor-JetStream-Mailbox-Execution-Plan-v2.0.json).
All W/P/D/INF/VAL packages and adjacent account fixes A1/A2 are explicitly included. This v1.0
file retains historical progress and original stage definitions; it is not the current execution queue.

## Consolidated remaining-work inventory — 2026-11-25

The sequential blocker audit is replaced by the [complete remaining-work inventory](Realtime-Actor-JetStream-Mailbox-Remaining-Blockers-Audit-2026-11-25.md)
and [machine-readable 24-package backlog](Realtime-Actor-JetStream-Mailbox-Remaining-Work-2026-11-25.json).
It covers all 25 current destinations, 33 workflow boundaries and all source/producer families,
with 12 targeted counterexample checks. These checks confirm current defects/conditional gaps;
no fixes were implemented in this audit. The 22 core packages include 8 workflow, 1 ingress,
3 destination/compatibility, 6 infrastructure and 4 evidence/cutover packages. Two adjacent
BrokerAccount safety items are separate from the 25 Realtime destinations. Plan the whole dependency
backlog with parallel lanes; do not continue treating each unfrozen command as a new isolated task.

## Objective and scope

Replace the large in-process queues for `ActorType.Realtime` with independently consumed JetStream
mailboxes keyed by stable `ActorThreadId`. A full or slow destination must not block an unrelated
mailbox through a shared dispatch stripe or the old shared Realtime admission budget.

Keep higher-level handlers, message contracts, strategy rules, and Command/Query/Function/domain
Event delivery unchanged. Use JetStream Memory as the development trial default and support File
through the same adapter. Memory storage is not restart-persistent if the NATS server is lost.
Existing raw-source journals and domain-event durability must remain intact.

The implementation remains disabled until the disposable correctness and performance gates pass.
This plan authorizes no claim that existing handlers are automatically idempotent or that memory
storage satisfies an unconditional no-loss guarantee.

## Stages and exit gates

| Stage | Deliverable | Exit gate |
| --- | --- | --- |
| 1 | Destination and publisher inventory, baseline capture, measured capacity worksheet | All 25 registered Realtime destinations and their delivery paths accounted for |
| 2 | Realtime-only options, stable identity, envelope and mailbox registry | Identity/codec compatibility, bounded resource lifecycle and startup validation pass |
| 3 | Confirmed publication and destination fan-out | Every required branch is stored or retained at a recoverable source; partial publication is recoverable |
| 4 | Broker-backed scheduling and completion | One handler per mailbox, independent progress, bounded in-flight memory and correct acknowledgements |
| 5 | Failure, restart and saturation handling | Recovery, full-queue ownership, ordering, self-send and generation fencing tests pass |
| 6 | Integration and non-Realtime regression qualification | Existing domain delivery and business outcomes remain equivalent |
| 7 | BenchmarkDotNet and matched burst qualification | Before/after memory, file and local-queue results recorded with explicit limits |
| 8 | Two complete matched trading-session trials | Both storage modes cover the same two full sessions with integrity and isolation evidence |
| 9 | Report, controlled cutover and rollback qualification | Backend choice and its failure guarantees recorded; pending work survives supported transitions |

**2026-11-24 progress:** The captured baseline and preliminary 25-destination inventory remain.
Bar open-state/publication recovery and bounded input checkpoint batching are implemented. Thirty-five
bar tests and 58 scheduler/queue regression tests passed, as did individual/group abrupt-process
recovery and disposable Memory/File broker retry checks. The 64-input handler benchmark improved
from 380.404 ms and 647.18 KiB to 6.723 ms and 85.50 KiB; the full-scheduler comparison is recorded
separately: 413.401 ms to 15.147 ms for 64 inputs, with allocation falling from 708.93 to
141.62 KiB. Thirty-eight analytics architecture checks also passed.

The TDI replay issue is now repaired with immutable binary result receipts, original generated and
completion IDs, staged recoverable publication, cold-restart replay and actor startup recovery.
PostgreSQL committed-response-loss and abrupt process-exit verification passed. The 235 focused
analytics checks comprise 234 functional/regression passes plus a counterexample for the next gate;
seven base-projector regression tests also passed. BenchmarkDotNet measured 45.329 ms for a new
durable window and 0.011 ms for a completed replay (database/transport substituted; full results linked).

Repaired required-delivery settlement in ADX, ATR, EMA, MACD, RSI, VWAP and VX term structure.
False handler results and thrown failures now escape to the delivery boundary independently of
optional metrics. Valid non-applicable inputs still complete normally. Missing required VWAP
contract context now fails rather than silently losing an eligible checkpoint.

751 selected analytics checks and 58 scheduler/queue checks passed. All 42 disposable broker cases
(seven actors, three failure modes, Memory/File) passed with abrupt child-process exit, retained
source identity/order and PostgreSQL-backed synthetic command receipts. The command service is
substituted; these are actor-boundary/recovery-driver proofs, not a production JetStream adapter
or a new qualification of every domain store. Ordering uses one broker-owned input in flight per
test mailbox; the old Core/local queue has not gained restart durability.

All 28 BenchmarkDotNet cases completed with longer warm-up. Successful handoffs measured
36.74-63.52 microseconds before and 41.26-56.69 after (scheduler plus substituted command service),
with broadly unchanged allocations. Proper failure settlement measured 177.98-269.87 microseconds
after, with additional diagnostic/exception allocation. Full per-actor success/refusal tables and
measurement limits are in the results. No Gen 2 collections were observed in these component cases;
this is not a live LOH or two-second deadline qualification.

The ITI failed-command settlement blocker is now repaired. Routed fallback commands are durably
prepared with original ES/VX/ATR/cursor/origin values, completed only after success, replayed in order
and recovered at startup without a new tick. Frozen ATR is preserved by the command actor. The active
ordered-intake WAL path remains unchanged. Bounded receipts, compaction, corrupt/torn data, source
ordering, cancellation and mode-transition protection are qualified in the component tests. The
latest analytics run passed 1,008 checks; 25 feed checks include the next blocker reproductions.

Eight disposable Memory/File abrupt-process recovery cases passed using original frozen inputs
and PostgreSQL command-receipt surrogates. Seven BenchmarkDotNet ingress cases completed: volatile
success 77.024 to 94.145 microseconds; new durable fallback 2.821 ms including physical commits;
completed replay 2.936 microseconds. The ordered WAL path remains unchanged. No Gen 2 collection was
observed in this short component run; full scheduler/network, live LOH and session gates remain open.

EOD inline projection and worker completion are now repaired: required failures propagate,
callers await actual completion, later contract work is fenced behind a failed source and generation
retirement joins running effects. All 632 feed tests and 16 disposable Memory/File abrupt-process
cases passed. The PostgreSQL proof stores surrogate snapshots through substituted persistence;
production buffered projection and notification replay are not qualified. BenchmarkDotNet measured
inline 20.56 to 22.96 us, worker one input 9.909 to 4.301 us, and worker 64 inputs 38.908 to 275.361 us.
The reference drains all work; the new path pays per-input completion costs. See results for limits.

Buffered EOD now captures frozen source/completion results before cache advancement or successful
handoff, resumes ordered publication and idempotent persistence, durably freezes history IDs, and
retains completed receipts for deduplication. Phase progress is group-committed. Startup recovers
pending work and migrates legacy history, including eight-field binary records. 642 feed checks
and eight Memory/File abrupt-process proofs passed using actual MarketDataDb schema/mutations;
confirmed publication is a test transport substitute. BenchmarkDotNet complete-drain comparisons
are in the results with their filesystem/latency limits.

Option-trade required failures now reach settlement independently of optional metrics; false handler
results are rejected too. All 650 feed checks and eight Memory/File abrupt-process cases passed with
metrics present/absent and lookup/send failures before acceptance. The actor/scheduler are real;
catalogue/cache and Notify transport are substituted. Broker ordering uses one source in flight;
ambiguous notification acceptance and production durable dispatch remain unqualified. BenchmarkDotNet
success/failure results for both metrics modes, allocations and limits are recorded in the results.

TickAggregation now rejects false required trade/quote projection results and propagates handler
exceptions independently of metrics. All 665 feed checks passed, including fresh quote retry leases,
ownership while work is pending, retained values and normal/absent metrics. Sixteen Memory/File
abrupt-process cases passed against actual TradeTickQuoteDb schema/write methods on isolated ScyllaDB;
the projector control/publication is substituted. Confirmed row identities, received times, prices,
ordered replay and no duplicate stored rows. Source/complete publication and parallel leasing remain
open. BenchmarkDotNet includes trades, two quotes and 512 quotes; results and limits are linked.

Iron Condor position required failures now escape independently of metrics. All 2,048 Trade unit
checks passed, including 24 new checks for both handlers, retries and intentional skips. Twenty
Memory/File abrupt-process cases passed under unchanged captures/clock using PostgreSQL surrogate
command receipts; the production financial repository, calculations, cache/generation and publication
are substituted. BenchmarkDotNet results and precise recovery limits are recorded in the results.

Original request capture/replay is now repaired: Forward Price time/inputs and Trade Plan parameters
are durably captured; the exact forward response/failure is frozen before plan dispatch. All 2,060
Trade unit checks passed. Twenty-eight Memory/File abrupt-process cases passed using the actual file
journal, changed clocks/cache/parameters and PostgreSQL surrogate Function receipts. Full financial
persistence, expired original Forward Price deadlines, legacy uncaptured requests and real generation
recovery remain unqualified. BenchmarkDotNet records actual file-journal cost against the previous handler.

Iron Condor publication now uses a persistent immutable notification outbox, independent of cache
mutation. Failed/ambiguous sends reuse the original event identity, timestamp, payload and origin.
Startup drains pending notifications; per-trade gates preserve ordering without blocking another
trade. All 2,074 Trade unit checks and 20 scoped Memory/File abrupt-process cases passed. The latter
use the actual context/cache/outbox/startup path with PostgreSQL surrogate Function/acceptance ledgers;
Core Notify subscriber delivery and full financial replay are not qualified. File-write/allocation
costs, retention limits and before/after BenchmarkDotNet results are recorded in the results.

Vertical Spread required failures now propagate independently of metrics, including the exit-command
handoff. All 2,090 Trade unit checks passed. Twelve Memory/File abrupt-process cases passed with
the actual actor/scheduler, fixed parameters and a retry clock advanced one day, plus PostgreSQL
surrogate Function and exit-command acceptance receipts. Deterministic exit IDs/bytes and ordering
survive failed/ambiguous handoffs. Actual financial execution, parameter changes and production source
ownership remain unqualified. Component before/after BenchmarkDotNet results are recorded in the results.

Futures position and clock required errors now fail settlement independently of metrics. All 2,119
Trade unit checks and 24 scoped Memory/File abrupt-process cases passed, using legacy snapshots for
both parsing paths and PostgreSQL surrogate plan/exit acceptance ledgers. Parameters stay fixed;
later request clocks keep fingerprints and original committed handoff bytes. Actual Daily flatten
same-observation retry is separately verified. BenchmarkDotNet results/limits are recorded in results.

Daily exits now durably claim the first authorizing command per position generation and use a
matching owner request/reply before confirmation. Later observations reuse it without another
calculation/handoff. Startup reconciles pending intents with current position identity; closed/newer
positions retire without dispatch, gaps/conflicts block. All 2,132 Trade checks and 20 Memory/File
abrupt-process cases passed with actual owner Execute/state/mapping and PostgreSQL surrogate plan/
owner persistence. Legacy uncaptured workflows, multi-host arbitration, real financial close execution
and production source ownership remain unqualified. Binary-file costs and scope are in the results.

Futures tick routing now captures immutable fan-out commands before sends, confirms each branch,
advances its durable source cursor only after all branches settle and restores unfinished branches
at startup. Failed/mismatched replies and durable-write failures fail source settlement. Successful
siblings are not resent; authoritative closed/newer generations retire old branches, and behind or
conflicting route evidence blocks. Source epoch headers, original command IDs, timestamps and binary
origins are retained. Per-contract asynchronous lanes preserve ordering without a shared send lane.
The route index serializes synchronous shared dictionary access.

All 2,147 Trade checks passed (including the next-blocker counterexample), as did ten disposable
PostgreSQL-surrogate/Memory/File abrupt-process cases. BenchmarkDotNet measured fresh two-target
routing at 179.6 us before versus 17,634.7 us with durable receipts; completed replay at 127.9 us
versus 221.9 us. Allocations rose from 24.28 to 99.75 KiB fresh and 21.05 to 28.47 KiB replay.
The added synchronous flush/serialization cost requires batching and throughput qualification before
rollout. Completed history pruning, multi-host ownership and end-to-end native epoch admission remain
unqualified. Production financial receiver persistence is substituted in these broker proofs.

FuturesOption tick routing now propagates required failures independently of metrics and retains
original option observations, exact Iron Condor/Vertical Spread commands and snapshot notifications
in a separate binary journal. Matching acknowledgements mark position branches; completed snapshot
sends mark publication branches. Retry/restart resumes unfinished branches with original IDs, prices,
UTC/origin, full risk/mark payload and route generation. Provider sequences remain arrival-ordered,
including resets/repeats/zero; quote and pricing-completion identities remain distinct. Startup
recovers before fresh ticks; behind/conflicting route evidence blocks, and authoritative retirement
settles old branches without new targets. Routing health reflects the whole retained source.

All 2,164 Trade checks (including the next-blocker counterexample), six focused integration checks
and 32 disposable Memory/File abrupt-process cases passed. PostgreSQL substitutes command and
notification acceptance; production financial mutations and actual UI subscribers are not claimed.
The full BenchmarkDotNet quote/pricing and fresh/replay table, allocation costs and sample warnings
are recorded in results. Synchronous flushes/repeated serialization materially increase fresh-input
cost; batching, throughput and bounded history still need qualification. No high-rate rollout,
native-generation admission, multi-host operation or live activation is qualified.

BrokerOrderRealtime now propagates broker quote-processing exceptions independently of metrics and
releases decoded futures-quote leases after the awaited broker call on success/failure. Original
book fields, latest-tail selection and option filters are unchanged. No financial actor receipt is
introduced while broker settlement is unqualified. All 2,175 Trade checks passed, including a new
actual-emulator blocker reproduction. Twelve Memory/File abrupt-child cases passed with PostgreSQL
idempotent quote-acceptance substitutes; a real committed-full-fill/file-reconstruction retry check
also passed. Quote buffer lifetime and awaited successor ordering are verified. BenchmarkDotNet
successful/failing futures/option timings, allocation and GC counts are in the results; these exclude
real matching, persistence and transport decode.

Emulator financial checkpoint recovery is now repaired. Mutations share a root commit boundary;
failed/uncertain saves hide unconfirmed financial state and freeze the exact candidate. The next
mutation resolves/re-saves that candidate once; original IDs, cash, fees and timestamps remain.
Persisted books and quote/match receipts prevent duplicate partial fills. The real file store
streams, flushes and atomically replaces checkpoints with an exclusive writer lease. All 63 broker
and 2,176 Trade checks passed, including the next-blocker reproduction. Eighteen real-emulator
Memory/File abrupt-child cases passed for 1/2/4 legs and before-save/after-save/completed-no-ACK.
BenchmarkDotNet shows one-leg fills 1.216 -> 3.603 ms and four-leg fills 1.568 -> 4.007 ms, with
27–36% lower fill allocation. No-match quotes now incur approximately 3.6 ms durable writes;
history growth, batching/retention and high-rate capacity remain unqualified.

Broker order publication recovery is now repaired for the registered emulator. The retained
financial observation journal and a durable ordered publication cursor replace the transient
order callback queue. A coalescing hint wakes its sole reader. The bridge confirms publication
before acknowledging, retries the same fact ahead of successors, and resumes from the cursor
after reconstruction. Original event/command/observation IDs and evidence remain; uncertain sends
are at least once. The actual Frozen emulator decorator forwards the recovery capability/ACK.
Cursor-save uncertainty uses the same repaired financial commit boundary. Legacy heads replay
retained facts without claiming prior publication. Account callbacks remain a separate scope.

All 65 broker and 2,184 Trade checks passed. Twenty real-emulator/bridge abrupt-process cases
passed over Memory/File confirmed JetStream publication, covering 1/4 legs and five publication/
cursor boundaries. BenchmarkDotNet drains one order in 0.179 -> 14.518 ms (one leg) and
0.250 -> 38.754 ms (four legs). These costs include four/ten flushed cursor checkpoints, with a
substituted synchronous publication receiver. Cursor batching/append storage, history retention
and production rate qualification remain open; no live GC improvement is inferred.

Workflow actor failure settlement is now repaired: PropagateHandlerFailures is enabled, so
required read/send exceptions and failed typed command replies fail source settlement without
relying on mailbox metrics. No decision retry loop or database read was added. Successful,
superseded and terminal notifications preserve behavior. All 2,193 Trade checks passed, including
the new initialization-handoff counterexample; ten PostgreSQL/Memory/File abrupt-child cases
verify ordered retained-source replay and byte-identical stale-trigger terminal requests against
an idempotent receiver substitute. These are not full production workflow/financial recovery tests.
BenchmarkDotNet successful mean is 82.16 -> 77.07 us with unchanged 9.19 KiB allocation (overlapping
confidence intervals); failure cases pay additional exception/delivery handling cost. Zero Gen 2
collections in these component diagnostics; no live API latency/GC improvement is inferred.

Regime initialization terminal recovery is now repaired. Initialization outcome handling and
its command send sit outside the calculation catch. A production-registered exclusive binary
journal freezes the original failure command/input hash before dispatch, marks confirmed delivery,
suppresses completed replay and resumes pending work on actor startup. Source replay recovers the
saved command before reinitialization/current-view fencing; later clock/configuration changes do
not replace it. No decision retry loop or database read was added. Bounds/checksums, receipt-write
failure, confirmed pending-marker cleanup and exclusive writers fail closed without fabricated
completion. Sent history has no automatic pruning; null storage is volatile isolated-test scope.

All 2,201 Trade checks and ten PostgreSQL/Memory/File abrupt-child cases passed, covering actual
file-journal reconstruction, next-day actor startup, identical terminal command bytes/IDs,
accepted-but-lost replies and death before the local completion marker. Receiver workflow effects
are substituted; this does not qualify production financial/projector recovery. BenchmarkDotNet
new/complete replay persistence timings and allocation/GC diagnostics are in the results. No live
API/GC or two-second full-pipeline claim follows from these component probes.

Order Composition preparation-failure recovery is now repaired. A production-registered binary
journal captures the original failure command/input hash before dispatch, confirms/archives only
after owner success, suppresses completed replay and resumes pending commands at actor startup.
Source replay checks saved evidence before current-view fencing/preparation, retaining the original
timestamp/content after cache/clock changes. Real typed command rejection, failed receipt writes,
confirmed pending markers, corrupt input and exclusive ownership are verified. No decision retry
loop, additional database read or financial reservation was added.

All 2,209 Trade checks and ten actual file-journal/PostgreSQL/Memory/File abrupt-child cases passed.
BenchmarkDotNet measures new capture at 0.133 -> 12.095 ms and completed replay at 0.123 -> 0.206 ms;
the latter suppresses owner sends. These are synthetic preparation-failure component measurements,
not live/full-pipeline deadline or LOH qualification. Composition Function-result, timeout and
preparation-acceptance handoffs remain outside this branch's recovery qualification.

Market Condition initialization-failure recovery is now repaired. Its production-registered binary
journal freezes the original failure command/source hash before dispatch, confirms/archives after
owner success, suppresses completed replay and resumes pending work at actor startup. Replay finds
saved evidence before stage processing or initialization; later clock/cache changes do not replace
the command. Typed rejection and original owner validation remain active. No decision retry loop,
database input read, financial reservation or strategy change was added.

All 2,217 Trade checks and ten actual file-journal/PostgreSQL/Memory/File abrupt-child cases passed.
They verify next-day cold startup, unchanged terminal bytes/IDs and interrupted capture/completion
boundaries with an idempotent owner substitute. BenchmarkDotNet new/complete replay write costs,
allocation and GC diagnostics are in the results. These are synthetic initialization-failure
component measurements, not live/full-pipeline deadline or LOH qualification. Other assessment
results and financial/projector outcomes remain outside this branch's qualification.

Legacy reservation-rejection recovery is now repaired. A production-registered binary journal
freezes the original failure terminal/source hash before dispatch, confirms and archives after
owner success, suppresses completed replay and resumes pending records at actor startup. Source
replay finds saved evidence before active-view fencing, expiry or another reservation call. Owner
and financial guards are unchanged; no decision retry loop, database input read or new reservation
was added. Successful/unknown reservation outcomes, timeout and late callbacks remain unqualified.

All 2,225 Trade checks and ten actual file-journal/PostgreSQL/Memory/File abrupt-child cases passed.
They cover clock-advanced cold startup, exact terminal bytes/IDs, no repeated reservation API calls,
typed terminal rejection and interrupted capture/completion. Portfolio/terminal effects are
substituted; these are not actual financial posting proofs. BenchmarkDotNet new/complete replay
write cost and allocation/GC diagnostics are in the results; no live/full-pipeline deadline or
LOH qualification follows from these component measurements.

Composition Function failure and timeout terminal recovery is now repaired. A production-registered
request-keyed binary journal freezes the original typed command, exact request hash and provenance
before dispatch, confirms/archives after owner success, suppresses completed replay and resumes
pending records at startup. Matching active dispatches recover before current-view fencing and
Function evaluation; later terminal notifications retain their stopped-workflow handling. Owner
guards, strategy/deadline rules and financial policy are unchanged. No database input read or
decision retry loop was added. Successful completion terminals remain outside this scope.

All 2,233 Trade checks and ten actual failure-journal/PostgreSQL/Memory/File abrupt-child cases
passed. Real fixture requests, timeout cold reconstruction, original trace/run/generation, changed
input and old-hash parity are covered. Streaming pooled MessagePack/SHA-256 removes the new
contiguous representative request hashing allocation while preserving stored hashes. Final and
archived unpooled BenchmarkDotNet timings/allocations are in the results for small/representative
new and completed-replay cases. No live GC or full-pipeline latency qualification is inferred.

Regular Trade Selection Function-failure recovery is now repaired. A production-registered binary
journal freezes original failure/time-out-labelled commands by request ID/hash with provenance,
confirms/archives after owner success, suppresses completed replay and resumes pending records at
startup. Matching dispatches recover before current-view fencing/evaluation; later terminal and
legacy reservation notifications retain their normal handling. Unknown Function-commit/reply
guards, owner validation, deadlines and financial policy are unchanged. No decision retry loop,
database input read or financial reservation was added.

All 2,241 Trade checks and ten actual selector-journal/PostgreSQL/Memory/File abrupt-child cases
passed. Real-request hash/trace parity, timeout-labelled failures, changed/corrupt input, write
failure and interrupted marker/archive recovery are covered. Selector and composition now share
the tested pooled MessagePack/SHA-256 hasher. BenchmarkDotNet new/complete replay costs and
allocation/GC diagnostics are in the results for small/representative payloads; no live GC or
full-pipeline latency/capacity qualification follows from these component probes.

Market Condition assessment Function-failure replay is now repaired. A production-registered
binary journal freezes the original FailMarketConditionCommand under the deterministic Function
request identity and original snapshot hash. It preserves failure IDs, timestamps, categories,
revisions and provenance; confirms/archives successful delivery; recovers pending captures at
startup and suppresses completed replay. Legacy prepared inputs retain their original source hash
and recover before configuration access. Pooled MessagePack hashing matches contiguous hashing.
Owner guards, deadlines, failure mapping and strategy rules are unchanged; no new database input
reads or in-decision retry loops were added. Successful assessment results remain outside scope.

All 2,250 Trade checks and ten actual assessment-journal/PostgreSQL/Memory/File abrupt-child cases
passed. Timeout, provenance, real-view retry, legacy preparation, corruption, capture failures and
confirmed-marker recovery are covered. BenchmarkDotNet small/representative new/complete-replay
costs and allocations are recorded in the results; durable IO cost is not a full-pipeline or live
GC guarantee. Journal retention/batching and full-session throughput remain unqualified.

**2026-11-25 progress:** Regime Discovery Function-failure recovery is repaired. Its production
binary journal freezes the original failure under the deterministic Function request identity and
original source hash, with IDs/timestamps, parameter evidence and origin. It confirms/archives owner
success, recovers at startup, suppresses completed replay and recovers before stale-trigger/input
preparation so delayed retries do not invent a different failure. Existing freshness/owner/deadline
and strategy rules are unchanged; no new database input read or decision retry loop was added.

All 2,258 Trade checks and ten actual Regime-journal/PostgreSQL/Memory/File abrupt-child cases
passed. Real initialization validation/capture precede failure mapping; timeout, stale retry,
provenance, exact hash, write/corruption, marker recovery and no repeated input preparation are
covered. BenchmarkDotNet measures small/representative new failure and completed replay costs,
allocations and GC with controlled inputs/owner. No live or full-session guarantee is inferred.

Regime Discovery successful-completion recovery is now repaired. The same legacy-named journal
stores failures/completions under one request/source identity, with v2 kind-tagged frames and v1
failure frame compatibility. Original result bytes/fingerprints, IDs, times, parameter evidence and
origin survive retry/startup; confirmation suppresses repeat delivery. Current owner/deadline and
result guards remain unchanged; no decision database input reads/retry loops were introduced.

All 2,267 Trade checks and ten actual completion-journal/PostgreSQL/Memory/File abrupt-child cases
passed, including the real local decision executor/runtime/model and completion mapping. Nine
completion repair cases, old pending/confirmed frame compatibility and all failure repair tests
passed. Before/after BenchmarkDotNet small/representative new and completed replay costs, memory
and raw GC diagnostics are recorded; no live/full-session performance guarantee is inferred.

The next confirmed blocker is Market Condition successful-completion timestamp replay. An actual
local runtime reuses identical assessment bytes and result/source/command IDs after a lost reply,
but the completion mapper stamps a fresh CompletedAtUtc. An immutable receiver rejects the changed
bytes under the same command ID. Freeze the original successful assessment terminal, then qualify
retries/restart with proof/deadline/owner guards intact. Stage 1 stops here; other terminal/callback
paths, retention/batching, production adapter and full-session trials remain open. No live restart,
readiness relaxation, financial repair or backend cutover occurred.

Stages 2 through 9 are **Not started** as production JetStream mailbox implementation. The batch
scheduler is a prerequisite used by the existing local backend, not a completed broker-backed
queue. No live cutover, readiness relaxation or two-full-session qualification has occurred.

See [qualification results](Realtime-Actor-JetStream-Mailbox-Results.md) for the reproduction and
the explicitly limited before/after transport probe. Do not mark later stages complete from earlier
component tests or from a partial trading day.

## Stage 1 Destination inventory and baseline

1. Record the 25 current actor registrations, entity-key shapes, accepted verbs and routed recipients.
   Distinguish message publication from database persistence and UI publication. Include mixed
   destinations and the three exit-start notification handlers that currently do no additional work.
2. Inventory all producers: Rust MarketData, C# actors, in-process routes, background services,
   external consumers and any request/reply use of Realtime subjects.
3. Record existing snapshot coalescing separately. Do not introduce new display/drop policies.
   Any necessary change to an existing exception requires an explicit compatibility decision.
4. Trace source ownership through publication, storage acknowledgement, fan-out and consumption.
   Verify whether a source survives an API or broker restart. Do not assume raw tick history alone
   reconstructs all destination messages or their original routing manifest.
5. Audit each handler's externally observable effects under redelivery. Identify durable commit IDs,
   financial/workflow deduplication, failure settlement, self-sends and cyclic sends.
6. Capture the local-mailbox baseline: message/payload distributions, arrival bursts, handler/drain
   rates, mailbox cardinality, CPU, allocation, LOH, Gen 2 and queue-age/latency percentiles.
7. Produce numeric per-subject count, payload, stream byte, consumer in-flight and fetch budgets
   from measured bursts and outage coverage, including broker/OS headroom. Record shared-stream
   storage interference; independent consumer cursors are not independent physical storage quotas.

**Blocker gate:** unresolved required-source ownership, unsafe repeated business effects, or a
lossy hop before publication prevents claiming preserved delivery for that route. Record the exact
destination and necessary infrastructure or business exception rather than silently weakening it.

## Stage 2 Configuration and mailbox identity

Add dedicated Realtime mailbox options without changing existing options for other actor categories.
The initial switch leaves the current live backend active until qualification. The new trial profile
selects Memory by default; File is an explicit alternative with distinct stream resources.

Define a versioned binary envelope carrying the original subject and payload, complete destination
`ActorThreadId`, stable source/destination delivery identity, original origin and source timing,
dataset/source generation, and routing revision/manifest. Preserve legacy unknown metadata as unknown.
Do not introduce JSON roundtrips or change business MessagePack keys.

Encode the full mailbox identity canonically into one broker subject token. Validate identity/key
agreement and use a deterministic digest or encoding, never process-random `GetHashCode`. Include
all verbs for the same actor/entity under its one ordered consumer. Define producer/source sequencing
and concurrent-publication serialization explicitly; timestamps alone do not establish ordering.

Prepare streams and independent filtered durable pull consumers in background lifecycle work.
Maintain a recoverable registry and cached hot-path lookups. Allocate no large local payload queue
per mailbox, and do not create broker resources or query application databases on every delivery.
Bound consumer metadata, in-flight buffers, registry growth and idle-runtime objects independently.

Use WorkQueue retention with non-overlapping destination filters, explicit acknowledgements,
`DiscardNew`, `DiscardNewPerSubject`, configured `MaxMsgsPerSubject`/`MaxMsgSize`/`MaxBytes`, bounded
`MaxAckPending` and bounded fetches. Required pending work must not expire through age/TTL eviction.
Validate the pinned NATS 2.12 client/server capabilities on disposable infrastructure.

## Stage 3 Publication and routing

Implement a Realtime-only publication adapter behind existing send interfaces. Preserve current
business subjects and routing semantics through the internal envelope. Do not turn Realtime into
`ActorType.Event` or reuse the high-level event-replay consumer as its mailbox implementation.

Freeze the complete destination manifest before considering fan-out accepted. Confirm each broker
publication or transfer remaining publication ownership to a recoverable transport outbox. Keep
source/destination IDs stable after ambiguous acknowledgements or partial fan-out. Each destination
gets its own acknowledgement state; completion of A must not settle B.

Avoid the current Core NATS reader as an unprotected first hop for required messages. Verify C#
publication interception and Rust transport integration independently. If Rust needs publication
acknowledgement/source-recovery changes, keep them in transport infrastructure and report the scope;
do not pretend a downstream queue alone closes that earlier loss window.

When a target is full, retain the failed publication at its source/outbox. Do not dispose it as an
optional drop, acknowledge it upstream or build an unlimited collection of waiting tasks. Resolve
self-send and cyclic-send deadlocks before using a waiting publication path inside actor handlers.
Measure and declare any outgoing outbox persistence cost in both backend comparisons.

Prove that the old Core route and new broker route cannot both execute a migrated delivery. Add an
explicit cutover epoch/manifest so retries across a switch retain identity without double delivery.

## Stage 4 Actor scheduling and acknowledgements

Introduce an internal asynchronous mailbox-delivery source for Realtime only. Preserve the current
local queue and synchronous fast path for other actor categories. Adapt `ActorThreadQueues` and
`ActorThreadV2`/scheduler integration; preserve public actor handler interfaces.

Fetch work only under bounded delivery capacity. Empty broker fetches and full destination waits must
not consume the actor execution worker pool. Fairly schedule runnable mailbox identities, with one
processing owner per `ActorThreadId`, and bounded message/byte prefetch. Unprocessed backlog remains
in JetStream, rather than another large C# or dispatch-stripe queue.

Reuse `IActorDeliveryCompletion` where valid. Complete the broker delivery after successful handling
or a confirmed durable handoff, not after a memory enqueue. Verify how handled exceptions, explicit
business outcomes, stale signals, superseded inputs and cancellation are classified. Preserve existing
business freshness rules without inventing a fresh timestamp during redelivery.

Retain processing order on failure: later messages for that mailbox cannot overtake an unfinished
earlier delivery. A failed destination must not pause other consumers. Transport redelivery/recovery
is outside strategy decision execution; do not add retries inside decision handlers.

## Stage 5 Recovery and bounded storage

Cover these cases on disposable NATS and PostgreSQL:

- API stop/restart before handling, during handling, after commit and before ACK, and after ACK.
- Actor replacement/retirement while a handler or publication is unfinished; fence late writes/ACKs.
- Connection interruption, publish timeout with ambiguous acceptance, partial fan-out and unavailable
  destinations, preserving original routing identities and timestamp/source metadata.
- Per-mailbox count limit, actor-stream byte limit and broker account/server exhaustion. Check
  accepted-versus-stored accounting and ownership of refused new publications.
- Self-send and cross-mailbox delivery cycles with tiny queue limits. No worker/stripe deadlock.
- Idle local-object eviction while broker work exists. Discover/resume the same durable consumer;
  never delete pending work as idle cleanup or silently recreate a cursor at the end of a stream.
- Broker restart in Memory mode: demonstrate the expected loss boundary on disposable resources.
  If another durable source is claimed to cover it, prove reconstruction for each required branch.
- Broker restart in File mode: verify recovered pending identities and cursor state. Document sync,
  replica and storage-volume settings; distinguish broker restart from OS/power failure.

Keep existing raw tick journals, storage consumers and domain-event streams unchanged. Do not restart
or destroy the active broker for destructive fault tests.

## Stage 6 Regression qualification

Extend the existing actor queue/scheduler, retention, admission, protected-delivery, owned-payload and
NATS routing tests. Add real broker integration tests for destination filters, independent cursors,
metadata roundtrips, restart discovery and non-overlapping work-queue subscriptions.

Verify Command/Query/Function/Event paths, replies, ordering, domain-event fan-out/replay and admission
remain unchanged. Compare financial results, workflow counts/identities, indicator/bar results,
position/TradePlan history and emulator effects for identical source inputs. Verify correct disposal
of pooled payloads on success, refusal, exception, retirement and shutdown.

Expose queued broker messages/bytes, oldest age, fetched/in-flight work, completion/redelivery counts,
publication refusals and retained source publications through existing runtime/Actor Health access.
Keep cumulative incident counters independent of idle-mailbox eviction. Do not call an empty local
buffer healthy if its broker consumer is stalled or required source evidence has a gap. Preserve
readiness checks and scope faults to the affected workload where the current readiness contract allows.

## Stage 7 Performance qualification

Run BenchmarkDotNet against frozen current local-queue behaviour and the Memory/File adapters with
identical payloads, ordering, retention, replica and acknowledgement policies. Include:

- an idle destination, steady arrivals, short bursts and sustained saturation;
- representative small/large payloads and recorded source/destination fan-out;
- one mailbox, hot/cold mailbox mixtures and thousands of stable logical destinations;
- one-message correctness delivery and small bounded prefetch/batch alternatives;
- one full mailbox while unrelated mailboxes continue, plus one full actor stream;
- processor/worker fairness, idle consumer metadata cost, and file sync/outbox settings.

Report publication ACK, queue wait, handler and end-to-end p50/p95/p99/max latency; throughput; backlog
drain time; CPU; allocations/LOH/Gen 2/GC pause; API and NATS working set; broker storage; IO and
consumer resource counts. Do not report API heap savings while excluding increased NATS RAM.
Separate warm steady execution from stream/consumer creation and cold restart overhead.

Keep the existing two-second pipeline decision requirement as a regression gate. Measure queue age
separately; durable preservation does not make an old signal fresh or permit delayed entries.
Record the latency and burst-drain acceptance thresholds from the baseline/capacity worksheet before
evaluating candidates. Do not select thresholds after seeing which backend wins.

## Stage 8 Two complete trading sessions

Both Memory and File must cover the same two complete, authoritative-calendar exchange sessions,
including overnight trading, the cash open/close, EOD and rollover. Record shortened/holiday sessions
separately. This is two days of evidence for each backend, not an unmatched memory day followed by a
different file day.

Capture exact source envelopes, destination manifests and arrival timing. Use isolated broker
namespaces and databases with only one authoritative execution path; shadow runs must not submit
live orders, change authoritative balances or publish into live subjects. Run matched sequential
replays too if parallel shadows compete for CPU, RAM or storage. Declare that contention in results.

At each session boundary reconcile publication and destination identities, successful terminal
outcomes, pending work, refusal/outbox ownership and duplicate business effects. Record burst
recovery, per-mailbox order, memory plateaus, scheduled EOD behaviour and restart outcomes. Never
substitute a short microbenchmark or an incomplete live session for the two-day evidence.

Write machine-readable run manifests, source checksums, exact configuration, BenchmarkDotNet reports,
integrity/recovery outcomes and per-session metrics under `.artifacts/realtime-mailboxes/<run>/`.
Summarise qualified results in `Documents/system/Realtime-Actor-JetStream-Mailbox-Results.md` only
when measurements exist. No two-day measurements exist at plan creation.

## Stage 9 Cutover and rollback

Select the backend using measured latency/resource costs and explicit failure requirements. Memory
remains the requested development trial default; broker-restart persistence requires File or a
separately proven persistent source. Do not describe the Memory trial as unconditional durability.

For activation or rollback, pause affected publication, account for recoverable outgoing work,
drain or explicitly migrate old pending deliveries, switch the transport epoch and verify identity
continuity before resuming. Do not purge old queues or delete consumers with pending work. Storage
type changes use separate resources; do not edit a stream in place and assume pending data migrated.

Restart the VS Code API + Rust eGui + Rust MarketData profile after functional qualification and
confirm readiness, existing UI workflows, indicators, open-position EOD and raw-storage recovery.
Retain the old backend as a tested rollback path until the session comparison is complete. A feature
switch must never bypass an unresolved pending backlog.

## Completion and blockers

The plan is complete only after all nine exit gates, before/after benchmarks, both full-session
comparisons and the cutover/rollback evidence exist. Report implementation complete separately from
two-day qualification complete if market-session time remains outstanding.

Stop the affected stage and report a concrete blocker if required delivery lacks a recoverable
publisher, a handler can duplicate financial/business effects, ordering or self-send safety cannot
be preserved, capacity cannot be bounded, non-Realtime behaviour changes, or the required source
capture/session evidence is unavailable. Performance regression or broker consumer scale beyond the
measured budget also prevents rollout; do not conceal it by dropping work or weakening readiness.

The plan began as documentation only. Implemented prerequisites and qualification progress are
recorded above and in the results; the running profile and JetStream cutover remain unchanged.
