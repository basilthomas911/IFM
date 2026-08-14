# Event Sourcing and Projection Split-Brain Controls

## Purpose

This document is the production-safety record for consistency between the PostgreSQL event log, EventProjector
execution state, durable replay, NATS publication, and domain read-model stores. A split-brain condition exists when
two of those components disagree about the latest successfully applied version of an actor stream, or when concurrent
owners can apply incompatible effects.

The controls described here are implemented. The current projector set has no known unresolved split-brain defect.
The cross-store section records a permanent design constraint and the mandatory contract for future projector target
operations; it must not be mistaken for a distributed-transaction guarantee.

## Required invariants

1. Every persisted event has one globally unique `EventId` and one contiguous `StreamVersion` within its event stream.
2. Only one execution owner may transition a projector event at a time; an expired owner must be unable to commit.
3. A projector may not apply stream version N while an unresolved earlier version exists for the same projector and
   stream.
4. Once version N is applied, a late or retried version less than N must never overwrite the read model.
5. Projector state changes and projector publication outbox entries must commit atomically in PostgreSQL.
6. A retried target mutation must use the same business identity and payload as its first attempt.
7. Start/stop lifecycle events and the Application projector are explicitly non-durable; their loss on process failure
   is intentional and must not be mistaken for durable completion.

## Issues encountered and disposition

| ID | Issue | Consequence | Disposition |
| --- | --- | --- | --- |
| SB-01 | Global `EventId` was being used as though it were a per-stream version. | Independent streams could not be reasoned about or checkpointed correctly. | **Fixed.** `event_log.StreamVersion`, `event_stream_id.CurrentVersion`, execution-state `StreamVersion`, and model mappings were added. |
| SB-02 | Append used a read/maximum pattern that did not serialize same-stream writers safely under PostgreSQL statement snapshots. | Concurrent writers could calculate the same next stream version. | **Fixed.** Append atomically increments `event_stream_id.CurrentVersion` with `UPDATE ... RETURNING`; `(EventStreamId, StreamVersion)` is unique. |
| SB-03 | First use of an event stream or event name used check-then-insert behavior. | Concurrent first writers could race on identity creation. | **Fixed.** Both identities use `INSERT ... ON CONFLICT ... DO UPDATE RETURNING`. |
| SB-04 | A late replay or operator retry had no durable knowledge that a newer stream version had already updated the target. | An old projection could overwrite current state. | **Fixed.** `event_projector_stream_checkpoint` records the last applied stream version per projector/stream. State creation and pre-apply claim reconciliation mark covered work `Superseded`; post-apply publication stages always resume to terminal. |
| SB-05 | Same-stream ordering used global event ordering rather than the new stream version. | Ordering intent was implicit and fragile. | **Fixed.** predecessor checks use `StreamVersion`; independent streams remain concurrent. |
| SB-06 | Projector ownership could outlive a worker while another worker took over. | A stale worker could race a replacement. | **Fixed.** execution token, revision, stage, and lease predicates fence transition, release, renewal, and terminalization. |
| SB-07 | Publication and projector-state transitions could commit separately. | A crash could publish without state, or state could advance without a recoverable publication. | **Fixed for PostgreSQL publication effects.** Processing/completion/failure publication uses the transactional outbox with deterministic message IDs and leased dispatch. |
| SB-08 | Source-only events had to synthesize a terminal message or could finish without advancing the applied checkpoint. | Notification-only workflows could remain replayable after successful application. | **Fixed.** source-only descriptors terminalize directly and atomically advance the stream checkpoint after `ApplyProjection`. |
| SB-09 | `SpreadDistributionJobSubmittedEvent` could be published before its job row existed. | A fast consumer could observe the event and fail to find its input. | **Fixed.** `PublishProcessingAfterApply` applies the row before source publication. |
| SB-10 | `FuturesTradeSignal` allocated a new sequence during each projector attempt, while storage allocated another sequence again. | A crash between ScyllaDB mutation and PostgreSQL checkpoint could create duplicate signals on replay. | **Fixed.** the persisted `EventId` is the stable signal sequence for this projection, and storage honors a supplied positive sequence. |
| SB-11 | Hosted services could resolve Simple Injector while generic registrations were still being added. | The container could lock mid-registration; startup then failed nondeterministically when reliability workers started. | **Fixed.** all registrations now complete before host construction can start background services. SystemAdmin service contracts share one singleton registration. |
| SB-12 | Reliability switches were disabled in application and integration configuration. | The implemented fences, bounded recovery, outbox, and backlog metrics were bypassed. | **Fixed.** all four switches are enabled in API Server and actor integration settings. |
| SB-13 | Start/stop events could enter replay even though they represent process lifecycle rather than durable business state. | Restart could replay stale lifecycle intent. | **Fixed by contract.** start/stop descriptors use `UseDurableReplay=false`; Application is entirely non-durable. |
| SB-14 | A legacy untyped `TradePositionAddedEvent` has no typed actor publication contract. | It cannot safely publish a source or terminal actor event. | **Contained, not redesigned.** it is a durable local-only projection, so it still receives checkpoint/fencing but emits no actor messages. |
| SB-15 | Old DatabaseBackup messages exist with three-token subjects such as `Event.Backup{id}.Execute`, while actor ingress requires four tokens. | An invalid durable delivery could be rejected and redelivered indefinitely, obscuring current failures. | **Fixed without weakening validation or deleting retained stream data.** both JetStream actor-consumer paths terminally acknowledge malformed actor subjects with reason `invalid-actor-subject`, log the exact subject, increment `ifm.nats.messages.malformed_subject_terminated`, and continue. This settles poison delivery for the durable consumer; normal stream retention policy still governs the stored message. |
| SB-16 | Broad integration hosts shared one static Simple Injector container, while several suites also ran classes concurrently against persistent infrastructure. | A later host could replace the resolver seen by an older background worker, lock a half-registered container, and make full-suite results order dependent. | **Fixed.** every test host owns and resolves its own container, and the persistent Reference, Feed, and Analytics assemblies serialize their test classes. Consecutive dirty-state suites pass Reference 14/14, Feed 40/40, and Analytics 25/25. |
| SB-17 | Spread-distribution projection allocated fresh put and call row IDs during every apply attempt. | A retry after target success but before checkpoint could append two more distribution rows. | **Fixed.** projector retries use two stable negative IDs derived from the persisted event ID; ordinary non-projector callers retain positive sequence allocation. Reapplying one descriptor is unit-tested. |
| SB-18 | Futures tick, futures option tick, futures ITI signal, and option-trade spread writes ignored supplied positive IDs and allocated new IDs during apply. | Replaying the same event could create a second logical observation and change query ordering. | **Fixed.** target stores honor supplied positive identities, and projectors preserve the source identity or use the persisted event ID when legacy payloads contain zero. Repeat-apply tests cover all four projector paths; real target-store tests verify representative option-tick and trade-spread identities. |
| SB-19 | The ITI projector queried the read model after insert and mutated the in-memory source event. | The completion payload could disagree with the immutable event-log payload, the already-published source message, and the inserted read model. | **Fixed.** projectors no longer mutate committed domain events. Any hold-state derivation must occur in the command workflow before the event is created and persisted. |
| SB-20 | `FuturesRsiDailySignalsGeneratedEvent` was emitted by the RSI command workflow but absent from the RSI projector descriptor table. | The durable event could enter the event log without applying its daily-signal read-model mutation. | **Fixed.** the source-only durable descriptor is registered and included in the exact descriptor-table contract; the focused workflow and complete Analytics integration suite pass. |

## Cross-store atomicity boundary

A ScyllaDB/domain-store mutation and its PostgreSQL projector checkpoint cannot be committed in one local database
transaction. The implemented sequence is fenced PostgreSQL claim, idempotent target mutation, then fenced PostgreSQL
checkpoint/state transition. If the process stops after the target write and before the checkpoint, the target
operation is repeated. This physical boundary is a design constraint, not a claim of distributed atomicity.

The current projector set closes this boundary by deterministic/natural-key idempotency, per-stream ordering, and
checkpoint suppression of older work. The audit corrected all sequence-allocation violations found in current
projector paths: `FuturesTradeSignal`, `FuturesItiSignal`, futures ticks, futures option ticks, option-trade spread data,
and spread distributions. PostgreSQL DatabaseBackup projection uses a target receipt in the same transaction as its
target change. Actual Scylla target tests repeat representative supplied-ID writes and prove the same logical row and
identity remain. Descriptor repeat-apply tests cover every corrected append-style projector.

There is intentionally no universal receipt API pretending to transact across PostgreSQL and ScyllaDB. Every future
target operation must still supply a reviewed idempotency proof or an actual target-store receipt before it can be
accepted. A new external side effect is therefore a release-gated contract addition, not an unresolved defect in the
current projector set.

For every future nontrivial cross-store projection, choose one of these contracts:

- store the deterministic projector effect identity in the target store and conditionally apply it once;
- use a target-store conditional write whose natural key and full payload are stable across retries; or
- rebuild the read model from an explicitly versioned projection generation rather than mutating it in place.

External broker calls, file writes, and any operation that allocates an identity during apply must use a target receipt;
they must not declare `NaturalKeyMutation` merely because retry exists.

## Projector rollout completed before Milestone A

All currently in-scope command actors own and start an EventProjector. Repositories delegate committed events to that
projector instead of maintaining a parallel denormalization implementation.

| Domain | Projectors |
| --- | --- |
| Application | `ApplicationEventProjector` (all descriptors non-durable) |
| Fund | `FundEventProjector`, `FundTransactionEventProjector` |
| MarketData Analytics | ADX, ATR, ITI, MACD, RSI, TDI, and FuturesTradeSignal projectors |
| MarketData Feed | MarketDataFeed, FuturesBarData, FuturesClosingPrice, FuturesEodData, FuturesOptionTickData, FuturesTickData, and TickAggregation projectors |
| Securities | FuturesContract and FuturesOptionContract projectors |
| OptionPricer | SpreadDistribution and SpreadDistributionJob projectors |
| Reference | LookupType projector |
| Trade | OptionTrade projector |
| SystemAdmin | existing DatabaseBackup projector |

`EconomicCalendarEventProjector` and `YieldCurveRateEventProjector` remain intentionally deferred until the FMP import
implementation is complete. Legacy TradePlan actors are excluded because they will be replaced by the trade-monitor
workflow. `ItiStrategyWorkflowCommandActor` remains a placeholder and has no projector implementation.

## Verification evidence

The focused real-PostgreSQL suite covers:

- 16 concurrent writers to one stream, proving unique contiguous stream versions;
- independent per-stream versions with unique global event IDs;
- stale state created after a newer checkpoint;
- a pre-existing skipped/retried old state reconciled at claim;
- same-stream predecessor blocking and independent-stream progress;
- post-apply publication resume after lease takeover while later stream versions remain blocked;
- lease takeover, stale-owner fencing, compare-and-set transitions, retry, skip, outbox, and recovery;
- direct source-only terminalization and atomic checkpoint advancement; and
- publish-after-apply checkpoint advancement in the same transaction as its outbox write; and
- schema/mapping/SQL contract checks.

Current result: 28 focused storage integration tests pass. Shared event-model tests (130) pass. Domain descriptor suites
verify one descriptor per source type, enforce non-durable lifecycle conventions, and repeat replay-sensitive
descriptors with the same persisted identity. The current domain and shared unit total is 1,671 passing tests. Three
focused real target-store fail-stop equivalents repeat an already-successful mutation before its checkpoint and retain
one logical effect. The NATS messaging suite passes 71/71, including malformed-subject rejection and terminal
acknowledgement coverage. Consecutive
full dirty-state actor suites pass Analytics 25/25, Feed 40/40, and Reference 14/14; focused actor results also include
Fund 30/30, OptionPricer 7/7, and Securities 7/7.

## Release gates

The known implementation defects in the current projector set are closed. Production rollout still requires all of
the following operational and extension gates:

1. every added or changed cross-store projection has a reviewed idempotency proof or target receipt;
2. environment operators monitor malformed-subject termination and investigate producers still emitting legacy
   subjects; destructive stream purging is not required by the consumer fix;
3. full domain suites pass from both clean and previously populated infrastructure before release;
4. new target-mutation categories include a fail-stop test after target apply and before checkpoint;
5. projection generation/version migration and rebuild procedures are defined; and
6. operational alerts cover blocked streams, failed outbox rows, stale leases, checkpoint lag, poison messages, and
   malformed-subject terminations.
