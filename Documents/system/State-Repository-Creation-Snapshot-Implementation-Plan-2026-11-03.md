# State repository creation snapshot implementation plan

Date: 2026-11-03  
Status: Complete. The 72-repository implementation, paired timings and required broader qualification pass. See the [implementation results](State-Repository-Creation-Snapshot-Implementation-Results-2026-11-03.md) and [qualification follow-up](State-Repository-Qualification-Blockers-Results-2026-11-03.md) for verification and the explicitly skipped live/manual gates.

Make the event that creates each aggregate stream its default snapshot anchor. Restore that event and every state-affecting event after it in stream order. Preserve aggregate identity, committed stream version, lifecycle, duplicate-command evidence, transactional completion and existing business decisions.

The [repository audit](../../.artifacts/state-repository-snapshot-audit-20261103.json) identifies 72 concrete repositories: 22 full-history loaders, one RSI event-window loader, one empty option-tick loader, one partially snapshot-based ADX loader, 39 standard snapshot loaders, one custom workflow snapshot loader, three latest-event loaders and four operation-completion loaders. The missing/partial categories contain 25 repositories; the seven alternative loaders bring the primary migration review to 32. Review the remaining 40 repositories against the creation-event rule as well, because using a snapshot helper alone does not establish that its anchor is the stream creation event.

## Creation event rules

1. The creation event is the first committed domain fact establishing the stream's aggregate identity. Emit it once, atomically with the first accepted state transition. A rejected first command creates no partial aggregate. Competing first commands cannot create two baselines.
2. A record does not have to be named Created to qualify. An existing Requested, Started, Stored or Completed event can qualify when it actually creates that stream. A recurring start, generated signal, changed fact or completed calculation is not automatically a creation anchor.
3. Creation establishes initial state; subsequent facts reconstruct current state. Do not load creation alone, discard terminal facts, lose receipts, reset an indicator accumulator, resurrect released state or replace full state with an incomplete latest Changed event.
4. A creation-based load is the default. Keep any later complete-checkpoint optimization explicit, separately declared and verified against creation-plus-tail replay. Do not silently substitute it for the default or introduce periodic snapshot scheduling in this change.
5. Creation is not necessarily a performance shortcut: a long-lived stream can still have a long tail. Record lookup cost separately from tail replay cost, memory and end-to-end load time. Correctness and measured regression checks govern any optimization.
6. Keep public namespaces, existing event names, numeric MessagePack keys, stream IDs and database/provider-independent parameter conventions stable. New creation types require explicit serialization, replay, projection and durable-delivery registrations. Do not rename historical Changed events into Created events.

## Resolved implementation convention

The [resolved 72-repository catalog](State-Repository-Creation-Snapshot-Catalog-2026-11-03.json) supersedes the proposed names below. Creation is the **first committed instance at StreamVersion 1**, independently of its event type's name. A later event of that same type never replaces creation. The existing first source fact supplies the initial state, and the ordered tail reconstructs the current state. Consequently no new Created wire contracts, event-row rewrites, stream renumbering or provider-specific parameters are required.

A read-only inspection of development source streams found first facts named Changed, Started, Generated, Requested, Stored, Reset and even StreamingStopped. Every nonempty inspected command stream had version 1. For example, four option-tick streams begin with StreamingStopped; adding a later Created event would misrepresent their history. New streams continue using the existing atomically committed first accepted domain fact as their creation instance. Concurrency and duplicate admission remain governed by expected stream version and the existing atomic command receipt transaction.

The resolved policies cover 53 ordinary creation-plus-tail repositories, six backup concern repositories with the preserved legacy prefix, one authoritative workflow creation-plus-tail loader, five explicitly complete checkpoint optimizations (ITI, Trade Signal, TDI, Iron Condor Plan and Forward Price), three explicitly stateless creation/head loaders (tick, option tick and bar data), and four canonical immutable Portfolio operation-completion creation adapters. The metadata optimization is constrained by a marker contract and a test that fails if retained business fields are introduced. Incomplete analytics checkpoints fall back to complete creation replay.

All source reads capture the head and facts in one PostgreSQL MVCC statement. Normal replay rejects missing creation, middle gaps and missing tail; explicit checkpoint/metadata policies allow only their declared skip. Source order is StreamVersion order. The loader drains and validates rows even if its consumer stops reading early. Backup legacy versions are never inherited as the owned stream's append version. Portfolio operations retain their original single canonical operation store and transaction ownership.

## Initial proposed event catalog (superseded by resolved convention)

Names marked proposed are implementation names, not existing contracts. Phase 1 confirms stream ownership and payload coverage before any repository is converted. Reuse an existing event only after proving it is the first accepted fact for that identity. Where a recurring event currently starts a stream, introduce a distinct one-time creation contract for new streams.

| Repository or owner | Default creation anchor and treatment |
| --- | --- |
| RegimeDiscoveryConfigurationStateRepository | Existing RegimeDiscoveryParameterSetCreatedEvent; replay publication and retirement after creation. Change loading only; retain typed configuration behavior and its separation from ParameterSets. |
| TradeOrderStateRepository | Proposed TradeOrderCreatedEvent with initial TradeOrderDefinition and required execution metadata. Preserve TradeOrderChangedEvent for later transitions and historical replay. |
| BrokerOrderStateRepository | Proposed BrokerOrderCreatedEvent with initial owned order state and identifiers. |
| OrderExecutionStateRepository | Proposed OrderExecutionCreatedEvent with initial execution state and attempt identity. |
| FuturesTradeStateRepository | Proposed FuturesTradeCreatedEvent with initial trade state. |
| FuturesOptionTradeStateRepository | Proposed OptionTradeCreatedEvent, consistent with the existing OptionTradeChangedEvent family. |
| FuturesPositionStateRepository | Proposed FuturesPositionCreatedEvent. Verify actual source-stream ownership before reusing any existing position-opened boundary event. |
| VerticalSpreadPositionStateRepository | Proposed VerticalSpreadPositionCreatedEvent. Preserve position identity and quantity/exposure history. |
| FuturesExitPositionWorkflowStateRepository | Existing ExitPositionWorkflowStartedEvent if it uniquely creates the decision stream; otherwise proposed shared ExitPositionWorkflowCreatedEvent. |
| VerticalSpreadExitPositionWorkflowStateRepository | Same shared exit-workflow creation contract, with the correct strategy kind and owner. |
| IronCondorExitPositionWorkflowStateRepository | Same shared exit-workflow creation contract; restore all later completion/failure/decision facts. |
| MarketConditionAssessmentStateRepository | Existing MarketConditionAssessmentCompletedEvent when it is the sole creation fact of the invocation stream. Otherwise add a distinct creation contract without changing invocation ownership. |
| OrderCompositionFunctionStateRepository | Existing OrderCompositionFunctionCompletedEvent under the same invocation-stream proof. |
| RegimeDiscoveryFunctionStateRepository | Existing RegimeDiscoveryPipelineCompletedEvent under the same invocation-stream proof. |
| RiskManagementFunctionStateRepository | Existing RiskManagementFunctionCompletedEvent under the same invocation-stream proof. |
| TradeSelectionFunctionStateRepository | Existing TradeSelectionFunctionCompletedEvent under the same invocation-stream proof. |
| DatabaseBackupSetStateRepository | Proposed DatabaseBackupSetCreatedEvent with a complete concern-owned initial baseline. |
| DatabaseBackupStateRepository | Proposed DatabaseBackupCreatedEvent with its operation baseline. |
| DatabaseBackupPolicyStateRepository | Proposed DatabaseBackupPolicyCreatedEvent. |
| DatabaseRecoveryStateRepository | Proposed DatabaseRecoveryCreatedEvent. |
| DatabaseBackupRetentionStateRepository | Proposed DatabaseBackupRetentionCreatedEvent. |
| DatabaseBackupServiceStateRepository | Proposed DatabaseBackupServiceCreatedEvent. Service capability/reconciliation events remain subsequent facts. |
| FuturesRsiSignalStateRepository | Proposed FuturesRsiSignalCreatedEvent and FuturesRsiDailySignalCreatedEvent unless an existing first event can be proven unambiguous. Include initial configuration, restoration inputs and applicable Wilder checkpoint/warm history. Started/Stopped remain lifecycle facts. |
| FuturesAdxSignalStateRepository | Verify the intraday Started anchor is truly creation; add proposed FuturesAdxSignalCreatedEvent and FuturesAdxDailySignalCreatedEvent where it is recurring or absent. Cover both daily and intraday paths. |
| FuturesOptionTickDataStateRepository | Proposed FuturesOptionTickDataCreatedEvent. Initialize the state actually required by the reducer and restore identity/version instead of returning an unbound empty state. Do not add an unsolicited raw tick cache or subscription side effect. |
| IronCondorTradePlanFunctionStateRepository | Proposed IronCondorTradePlanCreatedEvent for a long-lived plan stream, unless the existing Updated event is proven one-time for the actual stream identity. Preserve request preparation, fingerprint checks and disposable calculation behavior. |
| IronCondorForwardPriceFunctionStateRepository | Proposed IronCondorForwardPriceSessionCreatedEvent for a long-lived session; reuse Calculated only if it creates a distinct invocation stream. Preserve current-command matching and fingerprints. |
| MarketDataFeedRecoveryRecordRepository | Existing MarketDataFeedRecoveryRecordStoredEvent if one record identity owns one canonical stored completion. |
| CapacityReservationFunctionStateRepository | Existing CapacityReservationCompletedEvent as the immutable operation creation/completion snapshot in its canonical operation store. |
| CapacityConsumptionFunctionStateRepository | Existing CapacityConsumptionCompletedEvent under the same operation ownership. |
| PortfolioOrderCompositionFunctionStateRepository | Existing PortfolioOrderCompositionCompletedEvent under the same operation ownership. |
| PortfolioCloseOrderCompositionFunctionStateRepository | Existing PortfolioCloseOrderCompositionCompletedEvent under the same operation ownership. |

IntrinsicTimeStrategyWorkflowStateRepository already has a custom snapshot loader. Review it with the other 40 existing snapshot loaders: WorkflowStrategyStateUpdatedEvent is recurring, so it must not be described as stream creation without proof. If its current stream has no distinct creation contract, propose WorkflowStrategyCreatedEvent carrying the initial authoritative workflow snapshot. Preserve existing rejection of unsupported legacy workflow histories until a tested compatibility mapping exists.

For the other existing snapshot loaders, publish the same owner/stream/creation-event inventory using the audit's snapshotArguments. Retain already-correct bindings, including ParameterSetCreatedEvent, ParameterAssignmentCreatedEvent and ParameterStartupCreatedEvent. Flag recurring Started/Generated/Changed anchors for correction rather than assuming all 40 bindings meet the new rule.

## Implementation sequence

| Phase | Work | Exit evidence |
| --- | --- | --- |
| 1 Baseline and event definitions | Refresh the 72-class inventory. Record actual stream identity, first-event producer, complete state requirements, creation contract, historical aliases and every command overload. Capture focused test results and load timings. Finalize all creation event definitions before changing loaders. | A complete per-repository catalog, with existing/proposed types clearly distinguished and no unidentified loading branch. |
| 2 Shared persistence convention | Add an explicit creation-anchor policy or typed creation-loader API in BaseEventSourceActorRepository. Use existing snapshot primitives where they meet the policy. Put new SQL and alias resolution behind LoadEvent/MapReduce persistence APIs. Capture a committed stream head and replay only through that head. Restore Id and CommittedStreamVersion consistently. | Framework/storage tests prove creation-plus-tail equivalence, indexed lookup, cancellation and consistent stream versions under concurrent append. |
| 3 Reference and trade aggregates | Convert typed Regime Discovery configuration; order, broker-order, execution and trade repositories; futures/vertical-spread positions; and all three exit workflows. Add creation emitters/reducers/projectors atomically with their repository changes. | Each aggregate restarts into the same state and makes the same next-command decision as full replay. |
| 4 Functions and financial operations | Convert the five strategy Function repositories. Declare creation snapshots for the three latest-event loaders and four transactional operation loaders. Preserve request-local preparation and exact duplicate/fingerprint handling. | Completed invocations return their original result without recalculation, republishing or duplicate financial writes. |
| 5 Database backup concerns | Parameterize the shared DatabaseOperationStateRepository by the concern's creation policy. Preserve each of the six owned streams, legacy-prefix replay ordering, processed service-event fingerprints, outbox behavior and transactional projection. | Tests cover all six repositories, historical shared-stream compatibility and restart during every operation lifecycle. |
| 6 Analytics and feed state | Fix RSI daily/intraday, ADX daily/intraday and option-tick loading. Separate lifecycle restoration from retained signal windows. Preserve RSI checkpoints, historical seeding and current output values. Verify all 40 existing snapshot loaders against the catalog. | Restart outputs and lifecycle state match uninterrupted processing; bounded histories remain correct and creation is emitted once. |
| 7 Historical compatibility and qualification | Exercise old/new/mixed histories, missing creation, aliases, repeated commands and dense streams against real PostgreSQL. Run affected suites and contract verification; record before/after timings. Update the inventory with actual policies and evidence. | No unexplained loading paths, no unresolved affected-test failures, documented compatibility behavior and measured performance results. |

## Shared loading and compatibility behavior

The canonical load resolves the stream and captured committed head, selects its creation baseline, applies that event, then applies the required subsequent facts in StreamVersion order through the captured head. EventId, timestamp and per-event-type version are not substitutes for stream order. Validate owner identity and version continuity. Retry a consistent load when the implementation cannot retain a stable view; do not return state that includes events beyond the version used for the next append.

For historical streams whose first source event is named Changed, Generated or Completed, validate that first committed fact as the historical creation baseline using an explicit owner-specific mapping. A helper that selects the latest occurrence of that recurring event type is insufficient. Add an indexed first-event/creation lookup through the persistence layer when required. Preserve compatible event-name aliases and numeric wire layouts.

Do not rewrite historical event rows, renumber stream versions, silently delete data or append a late Created event and pretend it was the stream's first event. Existing full-replay fallback can preserve service continuity while a historical format is being qualified, but it remains an explicitly tracked compatibility path rather than completed creation-based migration. If a history lacks sufficient facts to establish its baseline, record a concrete migration blocker and retain existing behavior until a deterministic reconstruction is available.

Backup legacy-prefix replay needs its own compatibility proof: preserve the prior shared-stream facts required by each concern, then apply the current owned stream in its original order. A later concern event cannot erase the inherited baseline, deduplication fingerprints or receipts. Any persisted import baseline must genuinely create a new owned stream and must be committed atomically with its first transition, following the established per-run Startup pattern; changing existing stream identities requires a separately documented migration decision.

The four Portfolio Function repositories read canonical immutable operation completions from the Portfolio store. Use a typed snapshot-loading adapter around that source, preserving operation identity and input-hash checks, rather than creating a second event authority. If source-event persistence must change, enlist it in the existing business/event transaction. Keep current financial-state updates, receipt persistence and completion publication semantics.

For reusable complete-calculation streams, retain any latest-full-checkpoint fast path only as an explicit verified optimization over the creation baseline. Prove it restores every field required by the next request, including request matching, processed identities and version information. Retained signal-window loading must also restore creation/configuration/lifecycle facts outside the numeric sample window. Never require fresh feed data to reconstruct state or establish subscription readiness.

## Required test coverage

| Test family | Required cases and assertions |
| --- | --- |
| Repository convention | A test catalog covers all 72 concrete repositories and all overloads/branches. Verify declared creation-event bindings, explicit adapters/optimizations and no unexplained calls to full-history or empty-state loading. A text search alone is not acceptance evidence. |
| Replay equivalence | Empty stream; creation only; creation plus many updates; published/retired, cancelled/failed/closed/terminal state; restart immediately before the next command. Compare all authoritative fields, retained collections, receipts, fingerprints and next-command decisions with full replay. |
| Creation exactly once | Concurrent first commands; rejected initial command; accepted command retry; retry after lost acknowledgement; restart after commit. Exactly one creation fact, correct expected version, and no partial source/projection transaction. |
| Serialization and registration | New creation events roundtrip with complete payloads. Existing MessagePack keys, constructors, aliases and historical bytes remain readable. Verify event type registry, state factory, replay dispatch, projector descriptors and durable projection/outbox registration. |
| Persistence consistency | Capture-head versus concurrent append; correct committed version; stream-owner mismatch; revision gap; duplicate or malformed creation; mixed event families; unrelated streams; missing/empty lookup; cancellation during lookup and replay. No wrong-owner state or writes based on an incorrect head. |
| Historical streams | Changed-first histories, historical event-name aliases, streams without the new event type, old serialized payloads and mixed old/new contracts. Compare state and duplicate-command behavior before/after. Preserve existing explicit rejection of unsupported histories. |
| Reference configuration | Create, publish, retire, restart, duplicate operations and effective-date selection. ParameterSets ownership and its three creation anchors remain correct. |
| Orders and trades | Creation through lifecycle completion, amendments, broker identifiers, execution attempts, fills, partial fills, retries, corrections and closed state. No order submission or broker action during replay. |
| Positions and exit workflows | Open/update/correct/close; quantity and exposure; plan linkage; started/completed/failed decisions; restart after an exit was committed. Preserve long/short behavior, thresholds and exactly-once downstream requests. |
| Strategy Functions | Complete every one of the five stages, restart, replay the original command, reject a changed fingerprint, and verify atomic completion/projection. No pipeline stage runs again merely because state was reloaded. |
| Portfolio operations | All four operation snapshot adapters; exact request versus changed hash; missing operation versus unavailable database; rollback after injected failure between business mutation and completion persistence; lost acknowledgement. No duplicate reservations, consumption or orders. |
| Backup operations | All six concern repositories; interleaved historical concerns; current and legacy stream heads; capability/reconciliation events; cancellation, retention and recovery; service-event deduplication; outbox restart. Loads must never launch a real backup, restore, cleanup or scheduled task. |
| RSI and ADX | Daily and supported intraday identities; cold/warm startup; checkpoint/seed restoration; repeated start/stop/reset; sparse history; window rollover; duplicates; historical seed followed by live data. Match indicator values and warm-up readiness to uninterrupted processing. |
| Option ticks | Empty versus created stream; correct identity/head; repeated start/stop and insert commands; restart after accepted writes. Preserve present business semantics without adding subscription/recovery side effects or raw-history caches. |
| Latest-event Functions | Iron Condor plan, forward-price and recovery-record restoration; creation followed by multiple complete updates; exact request matching versus stale completion; failure after commit. Compare explicit latest-checkpoint behavior with canonical creation-plus-tail state. |
| PostgreSQL query plans | Extend SnapshotLookupPlanTests and EventSourceActorSnapshotRangeTests for first-event/creation lookup, alias sets, tail upper bounds and owner isolation. Use dense histories and EXPLAIN ANALYZE to detect unindexed anchor searches. |

Use the existing Reference, Trade, Portfolio, SystemAdmin, MarketData.Analytics and MarketData.Feed unit/integration projects. Extend Trade and Portfolio verification suites for lifecycle/financial invariants, the Feed recovery integration suite for record replay, and Application.Storage.IntegrationTests for production PostgreSQL query behavior. Reuse the current snapshot and scheduling tests rather than duplicating equivalent assertions.

## Timing and regression gates

Before repository changes, record cold and warm loading for empty, short, moderate and dense histories. Include representative order/position, function, backup, RSI/ADX and option-feed streams; use synthetic dense histories of 100,000 and approximately 1.2 million events where representative. Run the same datasets, build profile and machine conditions afterwards.

Report p50/p95 load time, rows read, anchor-lookup plan/buffers, replayed events, allocation/peak memory, time to accept the next command and resulting state equality. Separate creation lookup from replay-tail cost. Establish per-family acceptable limits from the baseline before implementation, and investigate regressions rather than masking them with a later event anchor. Do not set an invented universal speedup target.

Use isolated PostgreSQL/NATS fixtures and isolated .NET output on E: when the debugger holds development binaries. Preserve running development services. No paid data acquisition, production data rewrite, backup execution or deployment is part of this plan.

## Completion criteria

- Every one of the 72 repositories has a verified creation-event policy; all 25 missing/partial paths and seven alternative loaders have an implemented or explicitly justified conforming path.
- New streams create their baseline exactly once. Historical streams have tested deterministic compatibility, with no hidden full-replay fallback counted as migration completion.
- Replay restores state, identity, versions, lifecycle and duplicate protection without business side effects or state loss.
- All affected unit, integration, BDD and verification tests pass. Record any pre-existing failure distinctly and resolve any affected regression before completion.
- Identify any opt-in test fixtures required by the changed behavior and run them explicitly. Record remaining unrelated skips separately; skipped tests are not passing evidence.
- Regenerate and verify cross-language fixtures if public event contracts change. Run the full Rust verification script when those schema/fixture changes affect the native client, preserving the 20px spacing and previous-month date defaults.
- Before/after results, query-plan evidence, the final repository/anchor catalog and any explicit checkpoint optimizations are recorded alongside implementation evidence. Deployment and commit/push remain separate actions.

## Summary of planned delivery

Define and register creation events first; implement consistent shared loading and legacy compatibility; migrate reference/trade, function/financial, backup and analytics/feed families in testable batches; then qualify all 72 policies with restart equivalence, real-store concurrency/query-plan tests and before/after timings. No application code is changed by creating this plan.
