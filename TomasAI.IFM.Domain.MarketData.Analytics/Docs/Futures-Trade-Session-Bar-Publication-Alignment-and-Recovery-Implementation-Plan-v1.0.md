# Futures Trade-Session Bar Publication Alignment and Recovery Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Status | Implemented and qualified in tests; live-feed smoke remains for release |
| Date | 2026-09-15 |
| Scope | `FuturesTradeSessionBarSignal` Command publication, its Realtime handoff, bar calculation/validation, and focused qualification |
| Authority | `Documents/system/Actor-Implementation-Conventions.md`, section 13.1; `Regime-Discovery-Market-Signal-Interface-MDSI-5-Trade-Session-Bar-Signal-v1.1.md` |
| Existing durable path | Realtime accumulator → Publish command → PostgreSQL `FuturesTradeSessionBarPublishedEvent` → Scylla projection → PublishedComplete → downstream Closed Realtime event |

## 1. Problem and completion criteria

The live API throws `System.InvalidOperationException: A valid completed futures trade-session bar is required.` when the Publish command rejects a bar. A 2026-09-15 CLR trace captured four such throws and four exception-counter increments in the same interval. The Command actor replaces the bar validator's individual errors with this generic message, and the Realtime publication helper turns the failed service result into an exception. Read-only command-audit payload inspection identified the exact failure: four ES 15-second bars had valid OHLCV/lineage but true Databento event timestamps 45-339 ms ahead of the host's true `CalculatedAtUtc`. The strict cross-clock ordering rule rejected them. Both timestamps remain distinct in the corrected model.

The Command actor has `_parseMap`, `_validationMap`, and `_receiveMap`, each supporting only `PublishFuturesTradeSessionBarCommand`. It also has an extra `GetCommandValidationErrors` override that evaluates the validation map before `OnValidateAsync` evaluates it again on accepted commands. Its one receive handler is correctly located directly in `Command`, but named `FuturesTradeSessionBarPublication` rather than `PublishFuturesTradeSessionBar`.

Completion requires all of the following:

1. The Command actor follows the three-map, one-message-per-extension-handler convention without a duplicate validation path.
2. The loaded latest Published snapshot proves only whether the incoming bar repeats its last applied interval; valid distinct intervals publish regardless of arrival order or overlap.
3. Validation reports the actual rule, bar identity, and time/lineage fields on rejection.
4. The proven malformed-bar cause is corrected without fabricating timestamps or publishing an invalid bar.
5. Expected rejection or absence of a usable bar is classified and observable without a Realtime `InvalidOperationException`; genuine infrastructure failures remain reported.
6. No uncommitted bar reaches the Scylla projection or downstream bar-derived signals, and a healthy subsequent bar can recover the signal path.
7. No per-tick diagnostic allocation or database poll is introduced.

Do not deploy an intermediate actor-only change while the live invalid-bar path still runs. Align the actor first in the implementation branch, then finish the root correction and qualification before a live run.

## 2. Gate A — establish baseline and contract invariants

1. Record the existing map command sets, current Command validation response, Published event count, and first-chance exception rate over a short live or isolated sample. Do not start an integration host while the IFM API is connected to the live feed.
2. Confirm the Publish command's common header keys 0–5 and `Bar` at key 6. Preserve MessagePack keys and the PostgreSQL event-log and Scylla schemas.
3. Confirm the authoritative stream is one market-series/timeframe stream, the repository loads only the latest `FuturesTradeSessionBarPublishedEvent`, and the Event actor emits a downstream Closed event only after successful projection.
4. Confirm the development and production command-audit mode is `WindowedMessagePack`. Its command-ID reservation compares the full serialized payload SHA-256 on duplicate IDs. In particular, `CalculatedAtUtc` is in the serialized bar.

**Exit:** baseline and schema/identity assumptions are recorded; no app or test-host overlap.

## 3. Gate B — align the Command actor and dedicated extension

### B1. Mechanical actor maps

1. Keep the actor as one sealed concrete `BaseEventSourceCommandActor<TActor>` with the existing typed context, repository, and projector lifecycle.
2. Keep exactly one `_parseMap` entry, keyed by `PublishFuturesTradeSessionBarCommand.Verb`. Use `StringComparer.Ordinal`; the delegate only calls `AsCommand<PublishFuturesTradeSessionBarCommand>()`. `ParseMappedCommand` owns routing and null-result rejection.
3. Keep exactly one `_validationMap` and one `_receiveMap` entry, keyed by `typeof(PublishFuturesTradeSessionBarCommand)`. The receive delegate only casts and calls the dedicated handler's `Execute` method.
4. Remove the actor-specific `GetCommandValidationErrors` override. `OnValidateAsync` alone invokes `ValidateMappedCommand(command, _validationMap)` in accordance with the documented Command convention. Do not change the shared base actor in this gate.
5. Preserve load/save through `FuturesTradeSessionBarSignalStateRepository` and preserve projector ordering. `ReceiveAsync` only checks common arguments, casts loaded state, resolves the exact handler, and returns its result.

### B2. Deterministic input validation

1. Move `ValidatePublish` out of the actor into a `List<ValidationError>` adapter under `Command/Validation`. The map visibly validates `CommandId`, `EntityId`, then the `Bar` payload and repeated routing/identity values.
2. A null/default `Bar` produces errors, not a null-reference exception. Append every error returned by `FuturesTradeSessionBarReadModelValidationRules`; do not replace them with one generic sentence.
3. Require this command's bar to be `IsComplete == true`, `IsValid == true`, have no `ValidationIssues`, and have the required positive trade evidence. The shared read model may represent invalid observations; the Publish command is narrower.
4. Independently validate the observation identity and EntityId, then cross-check series, timeframe, routed subject, and any selected command/observation-ID relationship only when those independent identities are valid. Preserve stable error codes and field names.
5. Review every serialized bar property against the shared rules and make temporal/lineage errors explicit enough to distinguish first/last event order, calculation clock skew, OHLC inconsistency, source sequence, stream epoch, and interval bounds.

### B3. Dedicated command handler

1. Rename `Command/FuturesTradeSessionBarPublication.cs` and its class to `Command/PublishFuturesTradeSessionBar.cs` / `PublishFuturesTradeSessionBar`. Keep one public extension handler method, `Execute`; supporting event construction remains private in the same class.
2. Follow `AddOrderToFund`: show state-dependent guards in deterministic order, return `UpdateFailed` for a rejected rule, then construct `FuturesTradeSessionBarPublishedEvent` and apply it with `state.Update(domainEvent, command)` through the normal success helper.
3. Keep payload-field validation out of `Execute`; the handler receives a validated command and loaded state. Keep storage, projection, and downstream messaging out of the handler.

**Exit:** parse/validation/receive command-type sets are equal; the extension is the only Publish receive handler; existing event persistence and projection behavior still pass focused tests.

## 4. Gate C — state-backed publication rules and duplicate identity

The state retains `LastAppliedBarId` and `LastAppliedBar` from the latest appended Published event. This is the last *applied* event, not necessarily the newest market interval. The repository loads only that latest event, avoiding a stream replay or per-tick lookup. It cannot prove whether every earlier interval was published.

The handler checks, in this order:

1. No last bar: accept the first valid completed bar. Absence of previous publisher state is not a failure.
2. Same start and end as the last applied bar in this series/timeframe stream: acknowledge idempotently without a second Published event, regardless of recalculated content or observation identity. Keep the first committed bar authoritative; do not overwrite it.
3. Any other structurally valid interval: construct and apply the Published event. This includes older, out-of-order, and overlapping intervals. Do not infer a duplicate from an end time earlier than the last appended event.
4. A failed `state.Update` returns a failure and creates no durable success path. Bar ingress validation still rejects genuinely invalid structure, lineage, OHLCV, or trade evidence.

The command audit is a separate, earlier boundary. An exact transport redelivery reuses its `CommandId` and payload and is acknowledged before `Execute`. A different full MessagePack payload under that same attempt ID is an audit conflict. A recalculation uses a new attempt ID, even when its deterministic `ObservationId` is unchanged.

**Retry identity decision:** keep `ObservationId` as the deterministic domain identity and `CommandId` as the stable identity of one serialized submission attempt. Transport redelivery reuses that attempt ID and bytes; a newly reconstructed bar after restart uses a new attempt ID. The latest Published snapshot enforces idempotency only for its proven interval. Guaranteed all-history duplicate suppression would require a separate durable per-interval index kept transactionally consistent with the event log; it is not inferred from the latest-event snapshot. No database schema or MessagePack key migration is required.

If a command reservation exists but no Published event committed, a new submission attempt may safely proceed. If an attempt was committed and its interval is the loaded last applied interval, the handler acknowledges a new attempt without a second event. An unknown older interval is published. Do not add a polling recovery service.

**Exit:** first, later, proven repeat, older distinct, overlapping distinct, restart-recreation, and duplicate-audit cases have explicit results. Only a proven repeat creates no extra Published event.

## 5. Gate D — expose and correct the real invalid-bar cause

1. Use the detailed validation result to capture the first reproducible failing bar: contract, series, timeframe, value date, interval, observation ID, source sequence/ordinal, first/last market timestamps, receive timestamp, `CalculatedAtUtc`, stream epoch, and exact rule errors. Emit one structured error or degraded-observation record per rejected bar; never log every accepted tick.
2. Reproduce the confirmed cross-clock rule using a market event 339 ms ahead of the host finalization clock. A separate edge test checks source sequence progress with a regressing exchange event timestamp.
3. If first/last event ordering fails, compute event-time minimum and maximum independently of receipt-order Open/Close. Retain Open/Close according to accepted trade order and preserve deterministic observation lineage.
4. Databento market time and host finalization time are independent clocks. Validate that each is a real UTC timestamp without requiring the host time to follow market time. Retain the actual values and continue normal interval closure. No delayed timer or fictional timestamp is required for the observed lead.
5. If another rule fails, correct that field's generation or source admission and add its exact regression case. Do not weaken the validator merely to pass malformed OHLCV or lineage data.
6. Validate a completed bar before sending the Publish command so routine input anomalies produce an explicit unavailable/degraded bar outcome without a first-chance Command validation exception. The Command validation remains the authoritative safety boundary for malformed ingress.

**Exit:** the exact formerly failing rule has a reproducing test that passes after correction; healthy live bars pass Command validation; malformed bars cannot be published.

## 6. Gate E — Realtime failure handling and observability

1. Change `PublishFuturesTradeSessionBarAsync` so an expected validation rejection is returned as a classified failed result rather than converted to `InvalidOperationException`. Its two callers handle that result explicitly and record one structured failure/degraded status with bar identifiers and rule details.
2. Distinguish validation rejection, command-audit payload conflict, command transport failure, event-log failure, and projection failure. A proven interval repeat is an acknowledged no-op, not a failure. Genuine exceptions are logged once at the owning actor boundary with exception type and correlation; no success log is added to every tick or poll.
3. A failed Publish command never emits the downstream Closed event. A later healthy bar is allowed to proceed without a timer loop, background replay, or database poll. Preserve existing projection-fail terminal behavior for committed bars whose Scylla projection fails.
4. Review the accumulator's closed-bucket removal before command publication. Define the loss/degraded outcome of a rejected interval explicitly, and use only bounded, event-driven retry for transient submission failures when retaining the exact immutable bar is justified. Do not retry malformed bars indefinitely.

**Exit:** expected malformed-bar handling produces no Realtime `InvalidOperationException`; true failures remain visible and no unpersisted bar reaches derived signals.

## 7. Gate F — tests and release verification

### Focused unit and BDD coverage

- Three-map parity, unsupported verb/type failure, parse-null handling, one handler class/one public `Execute`, and validation invoked once on new commands.
- Aggregate per-field validation, null Bar, incomplete/invalid Bar, mismatched EntityId/subject, deterministic observation identity, and no state load or event for rejected ingress.
- First publish, later interval, value-date transition, roll/new epoch, proven same-interval repeat with changed content, older distinct interval, overlapping distinct interval, `state.Update` failure, and exact Published event construction.
- Source sequence progressing while exchange timestamp regresses; source timestamp ahead of server clock; boundary tick at interval end; UTC/session boundary; fixed-clock and live-clock model equivalence.
- Command-audit exact redelivery, in-flight duplicate, cache hit/miss, different-payload conflict, reserved-but-uncommitted attempt, committed attempt, and restart-created submission attempt. Include both audit implementations if the legacy one remains selectable.
- No downstream Closed event on command failure or projection failure; one Closed event after committed event and successful projection.

### Integration, verification, and performance

1. Run isolated PostgreSQL/Scylla/NATS integration tests with the IFM live API stopped or a separately isolated backend/namespace; never run a test host against the same feed/runtime while the live API is running.
2. Verify event-log → projector → Scylla → PublishedComplete → Closed ordering, plus recovery on the next healthy interval after a rejected one.
3. Build Analytics, API Server, and actor integration host; run focused and broader affected unit/BDD/verification suites once changes are stable.
4. Benchmark bar-close/validation and Realtime publication allocation and latency against baseline. The fix must not add work to each trade tick beyond the existing accumulator path.
5. In an authorized live/dev run, observe at least several 15-second closes and 1-minute/5-minute boundaries, then a session barrier. Correlate Published event counts, projected bars, downstream Closed events, classified failures, and first-chance exception counters. A healthy feed should show no recurring `A valid completed futures trade-session bar is required` throws.

**Release gate:** all affected tests pass, the live invalid-bar cause is identified and corrected, accepted bars are durable before downstream delivery, malformed or unavailable bars are visible without an exception loop, and the command audit/observation identity semantics are documented and verified.

## 8. Files expected to change during implementation

- `FuturesTradeSessionBarSignal/Command/Actor/FuturesTradeSessionBarSignalCommandActor.cs` — map alignment and single validation path.
- `FuturesTradeSessionBarSignal/Command/Validation/` — command-specific List adapter and full error preservation.
- `FuturesTradeSessionBarSignal/Command/PublishFuturesTradeSessionBar.cs` — renamed single-message extension and ordered business guards.
- `FuturesTradeSessionBarSignal/Command/State/FuturesTradeSessionBarSignalCommandState.cs` — latest Published bar snapshot fields.
- `FuturesTradeSessionBarSignal/Realtime/Model/FuturesTradeSessionBarAccumulator.cs` — only the confirmed calculation/source-time defect.
- `FuturesTradeSessionBarSignal/Realtime/Extensions/FuturesTradeSessionBarSignalRealtimeExtensions.cs` and its two caller handlers — classified publication result and one failure log.
- Shared Publish command/read-model validation and focused Analytics unit, BDD, integration, and verification tests — only when required by the proven rule or attempt-ID decision.
- MDSI-5 design/qualification note — command attempt ID versus deterministic observation ID, accepted guards, and exception behavior.

The existing Command/Realtime/Event actor topology, PostgreSQL event-log schema, Scylla bar schema, and successful projection-before-downstream ordering remain intact.

## 9. Implementation and verification record (2026-09-15)

Read-only inspection of audited Publish payloads isolated the bad rule. One ES
15-second bar had `LastMarketEventUtc=14:46:14.9610686Z` and the genuine host
`CalculatedAtUtc=14:46:14.6218828Z`; its sole bar-model validation error was
the strict ordering of these independently sourced clocks. Three other failed
ES 15-second bars in the sample had the same sole rule. The implemented
validator requires real UTC timestamps but does not compare host and exchange
clock order. The accumulator also takes the minimum/maximum of accepted market
event times while retaining Open/Close by trade order.

The actor has one parse, validation, and receive entry. Its dedicated `Publish`
handler acknowledges only a proven repeat of the latest applied interval;
valid distinct intervals publish even when older or overlapping. `CommandId` now
identifies a serialized submission attempt; deterministic `ObservationId`
remains the market bar identity. The existing full-payload command audit handles
exact redelivery. Pre-validation returns detailed rejection without sending an
invalid bar, and genuine Command exceptions carry a classified result and one
actor-owned structured exception log. The two Realtime handlers log routine
failed results once with timestamps, lineage, and rule detail. An interval
removed from the accumulator after closure is not retried indefinitely after
a failed command; a later healthy interval can proceed, and the failure is
explicitly logged.

Qualification: 1,052 analytics unit tests, 471 analytics BDD tests, 16
command-audit tests, and the isolated `LiveTrade_ProducesDurableProjectedSessionBar`
integration test passed. That fixture uses a unique non-ES synthetic contract
because all ES-prefixed contracts deliberately share one continuation
publication stream; this isolates its integration observations from other
scenarios. The integration host exited. The bar-close benchmark
measured 5.651 µs/2.82 KB for shared bar-model validation and 8.846 µs/4.57 KB
for full ingress validation on a valid ES 15-second bar. This adds work only
at bar closure. The running IFM API had stopped before integration testing;
a new-binary live-feed smoke with exception counters remains the release
observation gate.

The bar-generation policy was then revised to treat only a durably proven
repeat of the last applied interval as a no-op. Valid older and overlapping
distinct intervals create Published events; a same-interval recalculation with
changed lineage is acknowledged without overwriting the first committed bar.
The latest-event snapshot deliberately cannot claim that every historical
interval was published. The targeted unit test uses bars that pass Command
ingress validation for the older and overlapping cases. After this revision,
1,052 analytics unit tests, 471 analytics BDD tests, and the isolated
`LiveTrade_ProducesDurableProjectedSessionBar` integration test passed. The API
Server build passed with zero warnings/errors; the integration test host exited.

Remaining release observation: run the rebuilt live API through several real
15-second closes and inspect classified failures and CLR exception counters.
Downstream bar-derived analytics and the Regime Discovery rolling-bar cache
currently own their own bar ordering/calculation behavior; this publication
change does not claim that those consumers have been requalified for every
historically late or overlapping interval. The event log and Scylla bar table
retain such accepted observations for targeted downstream policy work.
