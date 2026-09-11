+
# Reference Data Parameter Sets: Complete Implementation Plan

Version: 1.5  
Date: 2026-09-10  
Status: Active implementation plan; partial foundation exists; Parameter Sets management is the next delivery gate; RSI and Market Outlook follow  
First supported component: Regime Discovery

Sources: [Design](Reference-Data-Parameter-Sets-System-Design-v1.0.md), [Specification](Reference-Data-Parameter-Sets-System-Specification-v1.0.md), [Actor conventions](../../Documents/system/Actor-Implementation-Conventions.md), [Message conventions](../../Documents/system/Actor-Message-Types-and-Delivery-Conventions.md).


**Single implementation checklist:** This document covers every coding layer. Sections 1–17 define architecture, phased delivery and test strategy. Sections 18–25 contain the concrete UI, schema, contract, message, integration and verification work packages. Use both parts as one plan; the design/specification are background references, not additional implementation checklists. No application implementation has been performed by writing this plan.

| Coding area | Concrete work packages | Phase |
| --- | --- | --- |
| Shared models, types, enums, validators | Section 18 | 1 |
| SQL schemas, migrations, persistence and projections | Section 19 | 2 |
| Messages, actors, maps, extension handlers | Section 20 | 3 |
| API clients, composition roots and registration | Section 21 | 3 |
| Reference Data UI and Regime grid | Section 22 | 5 |
| Signal requirements, calculations, startup and monitoring | Section 23 | 4,6,7 |
| Unit/integration/verification suites and acceptance mapping | Section 24 | Every phase |
| Cutover, rollback and completion checklist | Section 25 | 8 |
## Current execution order and precedence (version 1.4)

The latest user instruction supersedes the earlier RSI-first ordering. Complete Parameter Sets management end to end first: storage, standard actor messages, editing, validation, publication and assignment configuration. Do not change Regime Discovery pipeline processing during this phase. Keep its current dependency rules; expose configurable supported reductions without inventing scoring changes.

After parameterization, implement the RSI intraday/Daily seeding pilot and update which market-data signals populate Market Outlook. The user explicitly deferred Market Outlook selection/update; do not request or implement it during the current Parameter Sets phase. Observe RSI for approximately one trading day and obtain the user's explicit acceptance before verifying/adopting the seeding pattern in other signal families. Return to Regime Discovery runtime integration/validation after those steps.

Section 26 remains the RSI pilot implementation specification, but is deferred until the Parameter Sets management gate. Missing historical coverage permits empty startup and is not a startup blocker. Unsupported calculations and invalid identities remain distinct errors. The future AI supervisor remains out of scope.

### Latest approved amendments (version 1.4)

Section 27 supersedes required/optional membership in earlier sections for new parameter versions. Keep schemas 1 and 2 readable without rewriting their bytes. Schema 3 has explicit included/excluded membership: every included signal and every configured interval is required. Calculation dependencies remain enforced. The user approved all previously mapped Weekly and Monthly intervals, including the former optional intervals. Daily uses 15 seconds, 1 minute and 5 minutes. Market Outlook remains deferred.

The user approved an explicit single-user Development policy for this release. Enforce it on server command/query handlers; it must not enable production or staging. Production permission mapping remains a separate release gate, not a blocker for implementing the approved development release.

## 1. Binding implementation decisions

1. Use standard Command, Query and Event actor conventions for this feature. Create no Function or Realtime actors or messages for parameter management, preview, initialization orchestration or monitoring.
2. Replace the specification's ValidateParameterCandidateFunction with ValidateParameterCandidateQuery and PreviewSignalStartupPlanFunction with PreviewSignalStartupPlanQuery. Both are bounded, read-only queries; neither persists state, starts producers or acquires history.
3. Actor classes own maps, framework dispatch and lifecycle only. Every mapped message invokes its message-specific extension handler. Domain validation, calculations, dependency expansion and decision rules belong in Model folders.
4. Mutations use the established event-sourced Command actor/state/repository/projector pattern. Do not invent a competing mutable state engine alongside it.
5. Query handling uses parse/receive/exception maps and typed responses. Event handling uses parse/receive maps, typed context and explicit typed logger. All asynchronous handlers are awaited.
6. Implement the 69-signal draft, dependency-aware reductions, startup ownership union and readiness together through phased gates. A working grid alone is not completion.
7. Create unit, integration and verification test suites and run them at the phase gates. Planned tests are not evidence of execution.
8. Historical seeding is optional. Start with a verified contiguous seed or an empty array; never fabricate zero-price history, erase valid restored state, or require all producers to be warm before application startup.
9. An eligible ITI trigger starts a visible workflow. Regime Discovery owns readiness checks and returns a terminal WaitingForInputs outcome when required data is unusable. Do not move this check before workflow admission or leave an execution waiting indefinitely.
10. Complete Parameter Sets management first, then implement RSI per Section 26, complete a recorded trading-day observation and obtain user acceptance before expanding signal by signal. Future AI supervision, reinforcement learning and advanced backtesting are outside this delivery.

This plan supersedes Function/Realtime suggestions for the parameter-set feature in preceding documents. Existing analytics consumers outside this feature continue through their established public APIs; this is not a rewrite of all existing analytics actor types.

## 2. Verified examples and application boundaries

The current `RegimeDiscoveryConfigurationCommandActor` in Domain.Reference/Configuration/Strategy uses BaseEventSourceCommandActor, _parseMap, _validationMap and _receiveMap and invokes command extensions. Use its framework pattern, not its fixed three-message scope.

The current RegimeDiscoveryConfigurationQueryActor provides ParseMappedQuery, ResolveMappedQueryHandler and CreateQueryExceptionMap. Its inline receive-map query bodies must not be copied into new actors: delegate those operations to extension handlers.

Existing Reference integration tests include ConfigurationStrategyCatalogActorTests. Existing UI tests provide architecture/service-boundary coverage. Add tests to these test families where appropriate; isolate new verification fixtures from production data.

## 3. Actor inventory and ownership

| Actor | Role and route identity | Responsibility |
| --- | --- | --- |
| ParameterSetCommandActor | Command / ParameterSet / SetId | Create, save draft, rename, publish, retire |
| ParameterAssignmentCommandActor | Command / ParameterAssignment / authoritative assignment identity | Assign/disable generic bindings; delegate existing deployment bindings through their established owner |
| ParameterSetQueryActor | Query / ParameterSet | Catalog/schema/list/detail/usage/compare/operation result and candidate validation |
| ParameterSetEventActor | Event / ParameterSet | Consume committed lifecycle facts for nonauthoritative projections/observer notifications |
| SignalStartupCommandActor | Command / SignalStartup / StartupRunId | Apply frozen plan and process explicit step outcomes through bounded state transitions |
| SignalStartupQueryActor | Query / SignalStartup | Pure plan preview, saved plan/run status |
| SignalReadinessQueryActor | Query / SignalReadiness | Current signal and per-consumer readiness |
| SignalReadinessEventActor | Event / SignalReadiness | Consume applicable durable producer lifecycle facts and update readiness metadata |

Actor names are proposed registry names; phase 0 checks subject collisions. Readiness queries obtain current observations through existing read APIs; do not introduce a durable event for every tick. Where an existing producer exposes no durable lifecycle fact, query its status rather than adding a Realtime actor.

Notify remains best-effort observer output from mapped handlers. No new Notify actor type, transport flag or dual publication is needed. Shared helpers are injected services, not actors merely to increase actor count.

### 3.1 State and projection authority

ParameterSetCommandState contains aggregate revision, set metadata, version/lifecycle references and any framework-required operation identity state. ParameterAssignmentCommandState contains exact assignment revision/reference and application policy. SignalStartupCommandState contains frozen plan hash/generation, steps, attempts, deadlines and terminal outcome.

Do not load every full historical payload into the command state when the existing storage pattern supports exact immutable payload reads. Models decide transitions; repositories apply established optimistic event-stream concurrency.

The event stream and ConfigurationDb cannot independently be authoritative for the same mutation. Phase 0 must trace existing state repository/projector transactions and document their atomic boundary. Use the established event-source authority; generic tables provide the required immutable exact-read configuration storage/projection. If a transaction cannot atomically support a specification invariant, resolve the storage design before proceeding. No success response based on an uncommitted projection, and no new unbounded outbox worker.

## 4. Folder and file plan

Proposed Domain.Reference tree:

```text
ParameterSets/
  Command/
    Actor/ParameterSetCommandActor.cs
    Actor/IParameterSetCommandContext.cs
    Actor/ParameterSetCommandContext.cs
    Extensions/CreateParameterSet.cs
    Extensions/SaveParameterDraft.cs
    Extensions/RenameParameterSet.cs
    Extensions/PublishParameterVersion.cs
    Extensions/RetireParameterVersion.cs
    Model/ParameterSetLifecycleModel.cs
    Model/ParameterVersionAllocationModel.cs
    Model/ParameterPublicationModel.cs
    State/ParameterSetCommandState.cs
    State/ParameterSetStateRepository.cs
    EventProjector/ParameterSetEventProjector.cs
  Query/
    Actor/ParameterSetQueryActor.cs
    Actor/IParameterSetQueryContext.cs
    Actor/ParameterSetQueryContext.cs
    Extensions/ListParameterAreas.cs
    Extensions/ListParameterComponents.cs
    Extensions/GetParameterSchema.cs
    Extensions/ListParameterSets.cs
    Extensions/ListParameterVersions.cs
    Extensions/GetParameterVersion.cs
    Extensions/GetParameterUsage.cs
    Extensions/CompareParameterVersions.cs
    Extensions/GetParameterOperation.cs
    Extensions/ValidateParameterCandidate.cs
    Model/ParameterComparisonModel.cs
    Model/ParameterCandidateValidationModel.cs
  Event/
    Actor/ParameterSetEventActor.cs
    Extensions/ParameterSetCreated.cs
    Extensions/ParameterDraftSaved.cs
    Extensions/ParameterVersionPublished.cs
    Extensions/ParameterVersionRetired.cs
    Extensions/ParameterAssignmentChanged.cs
  Model/
    ParameterComponentRegistry.cs
    ParameterSchemaModel.cs
    ParameterCanonicalPayloadModel.cs
    ParameterScopeModel.cs
ParameterAssignments/
  Command/Actor, Extensions, Model, State, EventProjector
  Query/Actor, Extensions
```

Add corresponding contracts under Reference.Shared.ParameterSets: Identity, Commands, Queries, Events, Models, Validation and Enums. Follow physical shared-contract/type-forwarding conventions rather than duplicating public types in two assemblies.

Place generic startup planning under the existing application startup/market-data orchestration ownership, using SignalStartup/Command/{Actor,Extensions,Model,State} and SignalStartup/Query/{Actor,Extensions,Model}. Keep Regime-specific rule calculations in Domain.Trade's existing RegimeDiscovery/Model. Keep storage implementation under Application.Storage/ConfigurationDb/ParameterSets; UI under existing Reference view/model/service namespaces.

The assignment tree uses the same file-per-message convention; phase tasks below enumerate its operations. Do not place SQL in actors, UI or calculation models.

## 5. Map and extension rules

### 5.1 Command actor

- _parseMap: exact contract Verb to AsCommand<T> deserializer.
- _validationMap: exact concrete type to command/envelope validation and bounded pure payload validation.
- _receiveMap: exact type to extension ExecuteAsync(context, state, explicit dependencies, typed logger, cancellation where supported).
- ParseMappedCommand, ValidateMappedCommand and ResolveMappedCommandHandler perform standard dispatch.
- No business switch, query, hash calculation or lifecycle algorithm in receive-map lambdas.
- Reject null/malformed identity, unsupported type and invalid envelope using standard framework behaviour. Do not silently acknowledge an unmapped supported message.

### 5.2 Query actor

- _parseMap keys are declared verbs, never CLR names.
- _receiveMap resolves exact types and forwards to Query/Extensions/<VerbWithoutQuery>.cs.
- _exceptionMap uses existing query exception mapping and typed failures.
- Extensions pass cancellation to storage, compute via models and reply once through the context.
- Candidate validation and preview may calculate bounded results but never write or activate anything.

### 5.3 Event actor

- Use ParseMappedEvent and ResolveMappedEventHandler with exact maps.
- One extension family per event name without Event suffix.
- Pass IEventActorContext and typed logger explicitly, plus declared dependencies.
- Await processing before completion/acknowledgement. Handle duplicate event IDs and stale revisions idempotently.
- Events describe committed facts, not commands in disguise. Extension handlers must not initiate perpetual retries.

### 5.4 Model rules

Model methods accept explicit immutable input and return typed decisions/issues/plans. Use an injected or supplied clock instant, not hidden wall-clock reads in deterministic calculations. No service locator, transport, SQL, UI references or Task.Run in Model code. Domain actions requiring IO are orchestrated by extensions using repositories/APIs; models select the action.

Typical decisions: draft allocation, legal lifecycle transition, assignment eligibility, canonical equality, calculation dependency compatibility, enabled-signal expansion, demand union, readiness and bounded startup next-step selection.

## 6. Phase 0: convention and authority baseline

Tasks:

1. Read applicable AGENTS and authoritative actor conventions before implementation.
2. Record current working changes and avoid overwriting unrelated edits.
3. Inspect existing configuration command state/repository/projector, event transactions and query reply semantics.
4. Register proposed subject names, error codes and public type locations; resolve collisions.
5. Add a specification erratum replacing the two Function contracts with Query contracts and prohibiting new Function/Realtime actors for this feature.
6. Resolve RD-G1, ST-G1, MSG-G1, SEC-G1 and MIG-G1 into an evidence checklist with owners by subsystem.

Deliverable: convention/authority decision record linked from the implementation evidence file. Exit: transactional authority and message boundaries are understood; no competing write authority or guessed evaluator fallback remains in the coding plan.

## 7. Phase 1: contracts, registry and pure models

Tasks:

- Implement generic identity/reference types, enums, schema/field descriptors, bounded query pages and typed result/issues from the specification.
- Add Command/Query/Event contracts; replace Function names with ValidateParameterCandidateQuery and PreviewSignalStartupPlanQuery.
- Implement component registry and Regime descriptor; unknown components/schema versions remain read-only.
- Implement canonical payload, schema, scope, comparison and lifecycle models.
- Add serialization/version compatibility adapters and validation limits.

Unit tests: identity/enum validation; canonical hash culture/date/decimal stability; duplicate JSON keys; array-order preservation; payload size/nesting limits; unknown compatible fields; unsupported schemas; legal/illegal lifecycle transitions; exact comparison paths.

Verification tests: public contracts reference allowed shared types; no Function or Realtime subject for new contracts; numeric compatibility fixtures; descriptor IDs are unique; model assemblies have no UI or transport dependencies.

Exit: contracts compile and deterministic model tests pass before database/UI work.

## 8. Phase 2: storage, state repositories and migrations

Tasks:

- Add generic schema/catalog/set/version/assignment/operation/audit/legacy mapping tables and indexes from the specification.
- Implement immutable payload and lifecycle constraints and exact composite relationships.
- Integrate state repositories and event projectors with the established atomic event/configuration boundary.
- Implement query repositories with keyset pagination and bounded projections.
- Implement idempotent command results, expected revision checks and collision-aware legacy mapping.
- Add additive migration scripts, preflight inventory, rollback instructions and no-destructive-default test fixture.

Integration tests against isolated real PostgreSQL: version allocation races; optimistic conflict; same operation replay and mismatched input; lifecycle constraints through direct SQL; assignment-versus-retirement race; failed transaction rollback of audit/receipt/projection; exact hash reads; pagination ties; migration rerun and legacy UUID collisions.

Failure injection: before append, between enlisted writes, before commit and lost response after commit. Prove no partial success or duplicate version. Do not depend solely on mocked repositories.

Exit: database invariants and state reconstruction tests pass; migration preserves legacy references.

## 9. Phase 3: standard actors and extension handlers

Implement ParameterSet command handlers: CreateParameterSet, SaveParameterDraft, RenameParameterSet, PublishParameterVersion, RetireParameterVersion. Implement assignment handlers AssignParameterVersion and DisableParameterAssignment through the correct authority adapter.

Implement ParameterSet query handlers from the specification, plus ValidateParameterCandidate. Add GetParameterAssignment in the appropriate query actor. Each handler uses models for decisions and explicit repository/API dependencies for IO.

Implement committed Event handlers for catalog/read-model refresh and Notify hints only where needed. Register contexts, state repositories, projectors, validators and API clients in existing composition roots. Ensure start/stop ownership is exactly once.

Unit tests per handler: valid result; validation issue; authorization denial; stale revision; repository failure; cancellation; duplicate operation; explicit logger/context propagation where observable. Test model outputs independently from orchestration mocks.

Actor integration tests: send through actual actor API, verify typed replies and persisted exact versions; malformed/unknown verbs; parse/validate/receive parity; cancellation; durable duplicate delivery; no extra notifications before commit; lost reply resolved by GetParameterOperationQuery.

Exit: no inline domain logic in maps, no direct UI-to-store access, complete mapped message coverage.

## 10. Phase 4: Regime schema, 69-row seed and calculation dependencies

Tasks:

1. Add new-schema SignalRequirements and EvidencePolicy with stable row identities.
2. Create idempotent draft seed from the specification's exact 69 pairs, 35 required and 34 optional under documented seed defaults.
3. Store rows once in versioned payload; keep the original legacy payload/hash immutable.
4. Replace new-schema hardcoded request expansion with RegimeSignalRequirementModel.
5. Create RegimeCalculationDependencyModel mapping enabled calculation sections to mandatory groups and tested alternatives.
6. Implement RegimeEvidencePolicyModel and explicit WaitingForInputs/InsufficientEvidence outcomes; no zero substitution or accidental successful result.
7. Preserve existing requirement age, schema, source and calculation checks. Monitor=false never bypasses requiredness.

Models to add/extend in existing RegimeDiscovery/Model: RegimeSignalRequirementModel, RegimeCalculationDependencyModel, RegimeEvidencePolicyModel and a seed factory in the appropriate Reference adapter ownership.

Unit tests: exact 69-pair fixture; seed defaults and stable IDs; disabled-row exclusion; duplicate identities/output combinations; required versus optional missing data; dependencies requiring explicit section change; unsupported enabled producer; insufficient evidence; unchanged legacy default expansion.

Integration tests: save/edit/publish/read typed Regime payload; assign exact version; snapshot capture uses enabled requirements only; live cache lookup matches source/contract/timeframe/calculation; failure path returns typed readiness.

Verification: at least one genuinely reduced configuration must produce a meaningful valid result with qualified remaining inputs, and insufficient evidence must not produce a classification. No test may pass by globally mocking every signal as available.

Exit: reducing the list is behaviourally supported, not merely an editable flag.

## 11. Phase 5: Reference Data UI

Tasks:

- Add Parameter Sets category and three-region area/component/detail view.
- Implement ParameterSetsReferenceViewModel and a service boundary using only typed actor-facing APIs.
- Add set/version selectors, immutable draft workflow, publish/retire, comparison, usage and next-startup assignment.
- Implement Regime signal grid with all 69 rows, bulk editing, dependency panel, validation and explicit readiness checks.
- Implement dirty navigation, cancellation/generation guards, conflicts and operation-status resolution after timeout.
- Add accessible labels, keyboard navigation, duration units, status text and existing theme integration.

Unit tests in UI.Net.Presentation.UnitTests: selected area filters child list; stale response ignored; dirty edit preserved on failure; correct action availability; immutable version copy; bulk edit validates; required/optional/runtime counts; active versus pending version display; unsupported schema read-only.

UI integration/system tests: Reference navigation → Regime seed → edit reduced set → save → validate → publish → assign for next startup. Verify published version remains unchanged, old assignment still active until restart, and no raw persistence access in UI.

Manual verification evidence: screenshots of navigation, 69-row list, dependency validation, version comparison, pending assignment and missing-versus-optional readiness. Inspect keyboard-only editing and resize behaviour. Screenshots supplement automated behaviour tests, not replace them.

Exit: ordinary edits require no JSON manipulation and all edits reach standard actor extensions.

## 12. Phase 6: startup planning through Command and Query actors

Tasks:

- Implement PreviewSignalStartupPlanQuery extension: read frozen assignments/descriptors, call SignalStartupPlanModel, return plan/fingerprint/issues; no IO side effects beyond reads.
- Implement SignalDemandExpansionModel, SignalDemandUnionModel and SignalStartupTransitionModel under startup Model folders.
- Implement ApplySignalStartupPlanCommand extension and explicit step-outcome commands through the existing startup scheduler/orchestrator.
- Persist plan/run/generation references and bounded state. Each outcome validates plan/step identity and revision; reject stale/different-generation completion.
- Contribute existing non-parameterised consumer demands before allowing narrower activation.
- Invoke existing public producer APIs for preparation. Do not add Function/Realtime actors for plan construction or execution.
- Enforce one new orchestration attempt per step initially, bounded producer retries/deadlines, cost budgets and explicit failed terminal state.

Additional mapped commands: RecordSignalStartupStepCompletedCommand, RecordSignalStartupStepFailedCommand and ExpireSignalStartupStepCommand. Completion/failure follows legitimate producer results; timeout is supplied by the existing scheduler, not a new polling service. Idempotent duplicates must not advance a step twice. If existing startup contracts already express these transitions, reuse them instead of duplicating names.

Unit tests: deterministic topological order; cycle detection; demand union compatible/incompatible sources; per-consumer ages; strongest preparation; minimum historical dependency; disabled inputs; stale plan fingerprint; generation mismatch; duplicate completion; deadline and budget transitions.

Integration tests: assignment snapshot → preview → apply → existing producer API; no draft contribution; shared producer ownership; partial failure does not block unrelated steps; step completion after timeout cannot resurrect the wrong run; startup API remains available.

Verification: restart with the edited assignment and record actual requested producer plan/readiness for the reduced profile. Verify ES identity and VX pair resolution against real configured source capabilities; provider acquisition remains within explicit test budgets.

Exit: plan application is finite, explicit and traceable to exact versions.

## 13. Phase 7: readiness queries and event handlers

Tasks:

- Implement GetSignalReadinessQuery and current-state projection adapters.
- Implement SignalReadinessEventActor only for applicable existing durable lifecycle facts; map each to extension handlers.
- Put availability/freshness/per-consumer evaluation in SignalReadinessModel and ConsumerReadinessModel.
- Read current observations through existing analytics read interfaces; do not add a tick-consuming Realtime actor.
- Publish coalesced Notify hints, reject stale revisions/generations, display Unknown after disconnect and query current state on refresh.
- Required readiness checks remain active even if alert monitoring is disabled.

Unit tests: age boundary/future skew; warm/valid flags; optional versus blocking; two consumers with different ages; disabled and unsupported cases; same status alert coalescing; stale revision suppression.

Integration tests: producer status/event → handler → query → UI; duplicate events; missed Notify repaired by query; new generation invalidates old readiness; no producer-start command emitted by a missing-state check.

Verification: prolonged unavailable source produces visible bounded failure/readiness and no new recovery loop. Assert command-start counts remain stable after exhaustion, rather than merely asserting logs are quiet.

Exit: readiness reflects actual current data and cannot recursively restart work.

## 14. Phase 8: migration, rollback and release

Tasks:

1. Run preflight inventory in read-only mode and report unsupported legacy schemas/ambiguous assignments.
2. Apply additive schema and seed migration to isolated fixture, then approved environment through normal release procedure.
3. Preserve exact old payloads/hashes and existing authority adapters.
4. Publish/select an explicitly validated reduced configuration; do not activate the 69-row seed automatically.
5. Rebuild/restart using established deployment workflow; verify active assignment fingerprint.
6. Exercise rollback to prior compatible assignment with old resolvers retained.
7. Produce component-onboarding guide using Regime implementation as example.

Release gates: all specification AT-001 through AT-030 mapped; architecture verification green; transaction failure injection green; 69-row fixture matches; meaningful reduced evaluation observed; startup shared ownership proven; no new Function/Realtime actors or recovery polling service; no unresolved critical integration gates.

## 15. Test suite and fixture plan

### 15.1 Unit suites

| Existing project/family | Proposed test classes |
| --- | --- |
| Domain.Reference.UnitTests | ParameterIdentityTests, ParameterCanonicalPayloadTests, ParameterSchemaTests, ParameterLifecycleModelTests, ParameterAssignmentModelTests, ParameterComparisonModelTests, ParameterCommandHandlerTests, ParameterQueryHandlerTests |
| Domain.Trade.UnitTests | RegimeSignalRequirementModelTests, RegimeCalculationDependencyModelTests, RegimeEvidencePolicyModelTests, RegimeReducedConfigurationTests |
| Application.MarketData.UnitTests | SignalDemandExpansionTests, SignalDemandUnionTests, SignalStartupTransitionTests, SignalReadinessModelTests |
| UI.Net.Presentation.UnitTests | ParameterSetsReferenceViewModelTests, RegimeSignalGridTests, ParameterVersionActionTests, ParameterAssignmentDisplayTests |

Use actual repository project filenames discovered at execution time; some project filenames differ from directory names. Do not create redundant test projects when existing ones provide the correct runtime/dependencies.

### 15.2 Integration suites

| Project/family | Proposed coverage |
| --- | --- |
| Domain.Reference.IntegrationTests | ParameterSchemaMigrationTests, ParameterSetPersistenceTests, ParameterSetConcurrencyTests, ParameterLifecycleActorTests, ParameterAssignmentActorTests, ParameterEventDeliveryTests, ParameterLegacyMigrationTests |
| Domain.Trade.IntegratedTests | RegimeConfiguredSnapshotTests, RegimeReducedEvaluationIntegrationTests |
| Application.Api.IntegrationTests / startup tests | ParameterSetCompositionTests, SignalStartupPlanActorTests, SharedSignalOwnershipTests |
| UI.Net.SystemTests | ParameterSetsReferenceWorkflowTests, RegimeSignalEditorWorkflowTests |

Use isolated PostgreSQL database/schema with deterministic setup/cleanup and actual message transport fixtures where actor delivery is under test. Never run migration/destructive fixtures against development or production configuration tables. Use deterministic clock and controlled source fixtures for freshness/deadline cases. Live provider smoke tests are explicitly separated and bounded.

### 15.3 Architecture/verification suites

Place architecture checks in existing verification/architecture families, creating a focused Reference verification project only if no appropriate project exists.

- ParameterActorMapParityTests: every declared supported command has parse/validation/receive coverage; query has parse/receive/exception coverage; Event has parse/receive coverage.
- ParameterActorBoundaryTests: maps delegate to extensions; models have no SQL/transport/UI dependencies; no service-locator fallback.
- ParameterActorTypeTests: this feature introduces no Function/Realtime contracts, registrations or actors.
- ParameterHandlerAwaitingTests: dispatch awaits completion; exceptions/cancellation propagate through standard handlers.
- ParameterStorageAuthorityTests: exact references resolve through a single authority adapter and no UI references storage projects.
- RegimeSeedVerificationTests: 69 unique ordered pairs identical to specification fixture; 35/34 documented default split.
- ParameterCompatibilityVerificationTests: stable serialization/enum fixtures and preserved legacy hashes.
- ParameterRequirementsTraceabilityTests: all specification acceptance IDs linked to concrete tests/evidence; no orphan mandatory requirement.

Use semantic/syntax inspection where enforcing source architecture is appropriate; do not use fragile whole-file text snapshots. Behavioural tests must exercise actual actors and stores in addition to source checks.

### 15.4 Failure matrix

| Failure point | Required observation |
| --- | --- |
| Invalid envelope/type/verb | Standard rejection; no handler mutation |
| Authorization failure | No state change; typed forbidden |
| Model validation error | Field/row issue returned; publish blocked |
| State revision conflict | Winner preserved; caller can compare |
| Transaction rollback | No partial payload/event/receipt success |
| Commit then reply loss | Same operation resolves original committed version |
| Duplicate durable event | Projection/notification state not duplicated |
| Unsupported schema | Read-only inspection; no lossy save |
| Missing required input | Consumer waiting, host serving |
| Missing optional input | Supported reduced outcome or explicit insufficient evidence |
| Startup dependency failure | Only dependents blocked; bounded terminal failure |
| New contract/runtime generation | Old readiness rejected |
| Shared producer demand removed | Remaining owner retains producer |
| Notify loss | Query restores state |

## 16. Test execution and evidence

During implementation, discover solution/project files and use existing repository test runner conventions. Build changed projects and their impacted dependencies. Run pure tests first, then isolated persistence/actor integration, then UI/system and live capability verification. Run required repository checks after the final change; repeat only when further edits or failures justify it.

Suggested command shape (substitute verified project paths):

```text
dotnet test <Reference.UnitTests.csproj> --filter FullyQualifiedName~Parameter
dotnet test <Trade.UnitTests.csproj> --filter FullyQualifiedName~Regime
dotnet test <MarketData.UnitTests.csproj> --filter FullyQualifiedName~Signal
dotnet test <Reference.IntegrationTests.csproj> --filter FullyQualifiedName~Parameter
dotnet test <Ui.Presentation.UnitTests.csproj> --filter FullyQualifiedName~Parameter
dotnet test <VerificationProject.csproj> --filter FullyQualifiedName~Parameter
```

These are command templates, not commands already run. Tag new tests with consistent ParameterSets/RegimeConfiguration traits and use the repository's existing isolated-fixture settings. Integration runs must report fixture endpoint identity without secrets.

Create `Documents/system/Reference-Data-Parameter-Sets-Implementation-Evidence.md` during implementation containing: commit/worktree context, phase completion, actual commands, passed/failed/skipped counts, unresolved failures, test artifact paths, seed verification, actor-map verification, migration/rollback evidence and bounded startup results. Skipped live tests are explicit limitations, not passes.

## 17. Phase dependency and completion checklist

| Phase | Depends on | Completion evidence |
| --- | --- | --- |
| 0 conventions/authority | Source design/specification | Authority and contract erratum |
| 1 models/contracts | 0 | Pure and architecture tests |
| 2 storage/state | 0,1 | Isolation, atomicity, migration tests |
| 3 actors/extensions | 1,2 | Actual request/reply and mapping verification |
| 4 Regime behaviour/seed | 1,2,3 | 69-row equivalence and valid reduction |
| 5 UI | 3,4 | Actor-bound editing and UI tests |
| 6 startup | 3,4, producer capability gate | Union/budgets/real preparation verification |
| 7 monitoring | 4,6 | Query/event freshness and nonrecursive behaviour |
| 8 release | All previous | Full acceptance matrix, rollback and evidence |

Definition of done: Parameter Sets is usable in Reference Data; Regime's 69-row draft is editable; valid reductions affect requests/evaluation; exact assigned versions govern next startup; producer ownership is shared correctly; monitoring reports actual readiness; actor/extension/Model conventions hold; all required unit/integration/verification tests are implemented and passing or release is explicitly blocked. No production code or tests are created merely by writing this plan.

## 18. Shared contract and Model coding work packages

### C01 — identity and stable references

Create public types in the registered Reference parameter-set contracts assembly:

- ParameterAreaId, ParameterComponentId, ParameterSetId, ParameterAssignmentId: nonempty UUID wrappers.
- ParameterSchemaRef: ComponentId, SchemaVersion, SchemaHash.
- ParameterVersionRef: ComponentId, SetId, Version, PayloadSha256.
- ParameterConsumerRef: ConsumerKindCode, ConsumerId, Role.
- ParameterScope: ScopeSchemaVersion, typed dimensions, canonical ScopeHash.

Implement equality, serialization, validators and compatibility fixtures. Codes/names are not substitutes for IDs. Reuse existing market-series identities and timeframe/metric enums.

### C02 — transport models

Create ParameterAreaSummary, ParameterComponentSummary, ParameterSetSummary, ParameterSetVersion, ParameterAssignment, ParameterUsage, ParameterValidationIssue, ParameterValidationReport, ParameterVersionComparison, ParameterOperationResult<T>, ParameterPage<T>, ParameterSchemaDescriptor and ParameterFieldDescriptor.

Required common version fields: exact Ref, SchemaRef, lifecycle Status, PayloadJson, Description, CreatedAtUtc/By, PublishedAtUtc?, RetiredAtUtc?. Operation responses carry OperationId, outcome, committed reference/revision, validation issues and projection visibility where relevant. Page cursors are tied to filters and sort order.

Create SignalDemandKey, SignalDemand, SignalStartupPlan, SignalStartupStep, SignalReadinessSnapshot and ConsumerSignalEvaluation with the fields defined in section 12 and the specification. Avoid a second copy of these shapes in UI models; UI wrappers reference the public contracts.

### C03 — enums

Implement the specification's proposed ParameterVersionStatus, ParameterEditorAvailability, ParameterApplicationPolicy, ParameterIssueSeverity, ParameterOperationOutcome, SignalRequirementMode, SignalStartupPreparation, SignalRuntimeReadiness, ParameterConsumerReadiness and RegimeEvidenceOutcome. Preserve specified numeric values and reject Unknown for activated configuration.

Keep component/area/consumer kinds as registry codes, not new strategy-only enums. Keep UI editing state separate from persisted lifecycle state.

### C04 — schema, codec and component registry

Implement IParameterComponentDescriptor, IParameterPayloadCodec<T>, IParameterPayloadValidator, IParameterEditorProvider, IParameterConsumerAdapter, ISignalRequirementContributor and ISignalProducerDescriptor.

Create RegimeDiscoveryParameterComponentDescriptor with stable component code, supported legacy/new schemas, scope rules, typed editor key, domain validator and NextStartup policy. The registry exposes unsupported components read-only and does not load arbitrary types from stored metadata.

Implement structural schema validation, field metadata, canonical hash rules, default creation, unknown-field preservation and explicit schema upgrade to a new draft. Add size/depth/row/name limits exactly as specified. Draft saving accepts structurally valid incomplete content; Publish requires full semantic validity.

### C05 — pure Model classes

Create the following classes in Model folders, not actors or UI:

| Model | Input | Output |
| --- | --- | --- |
| ParameterSetLifecycleModel | Current state + operation | Legal transition or issues |
| ParameterVersionAllocationModel | Current metadata revision/version | Proposed next immutable version |
| ParameterPublicationModel | Exact payload + schema + capabilities | Eligibility report |
| ParameterAssignmentModel | Consumer/scope/version/current assignment | Assignment decision |
| ParameterCanonicalPayloadModel | Payload + codec version | Canonical bytes/hash |
| ParameterComparisonModel | Exact version payloads | Typed field differences |
| ParameterScopeModel | Descriptor + supplied scope | Normalized scope/hash/issues |
| RegimeSignalRequirementModel | New-schema Regime payload | Enabled requests only |
| RegimeCalculationDependencyModel | Enabled sections + inputs | Dependency/alternative issues |
| RegimeEvidencePolicyModel | Qualified observations + policy | Sufficient/reduced/insufficient outcome |
| SignalDemandExpansionModel | Resolved assignments + descriptors | Dependency graph |
| SignalDemandUnionModel | Demands from all consumers | Shared producer demands with ownership |
| SignalStartupTransitionModel | Plan state + completion/failure/timeout | Bounded next transition |
| SignalReadinessModel | Observation metadata + supplied time | Precise availability |
| ConsumerReadinessModel | Requirements + availability | Blocking/optional evidence summary |

Acceptance: deterministic tests pass without database, transport or UI dependencies; models never resolve services globally.

## 19. Database, schema and persistence coding work packages

### DB01 — schema migrations

Create `ParameterSetSchemaSql.cs`, `ParameterSetSchemaDb.cs` and versioned additive migrations under Application.Storage/ConfigurationDb/ParameterSets/Schema. Register migrations in the existing ConfigurationDb schema bootstrap; do not add an independent startup database worker.

Implement tables with the following fields. Required identity/code/revision/payload fields are NOT NULL; only explicitly optional lifecycle timestamps are nullable.

| Table | Required column set |
| --- | --- |
| parameter_area | area_id UUID PK; code TEXT UNIQUE; name; description; sort_order INT; enabled BOOL; revision BIGINT; created_at_utc; updated_at_utc |
| parameter_component | component_id UUID PK; area_id FK; code UNIQUE; name; description; descriptor_key; enabled; revision; timestamps |
| parameter_schema | component_id + schema_version PK; schema_json JSONB; editor_json JSONB; schema_sha256; validator_key; validator_version; codec_version; created_at_utc |
| parameter_set | set_id UUID PK; component_id FK; code; name; description; revision; next_version INT; created_at_utc/by; updated_at_utc |
| parameter_set_version | set_id + version PK; component_id; schema_version; status SMALLINT; payload_json JSONB; payload_sha256; description; created_at_utc/by; published_at_utc?; retired_at_utc? |
| parameter_assignment | assignment_id UUID PK; consumer_kind_code; consumer_id; role; component_id; scope_json JSONB; scope_sha256; revision |
| parameter_assignment_revision | assignment_id + revision PK; component_id; set_id; version; payload_sha256; enabled; application_policy; effective_from_utc; created_at_utc/by |
| parameter_operation | operation_id UUID PK; request_sha256; operation_type; exact committed references/result_json JSONB; committed_at_utc |
| parameter_set_audit | audit_id UUID PK; operation_id; entity_type; entity_id; actor_identity; action; before/after refs; recorded_at_utc; reason |
| parameter_legacy_reference | legacy_kind + legacy_set_id + legacy_version PK; component_id; generic_set_id; generic_version; legacy_hash |

If startup-run storage has no appropriate payload field, add signal_startup_plan with StartupRunId/PlanId, assignment fingerprint, plan JSON and timestamp. Reuse existing run storage where it already meets this requirement.

Constraints: unique component/set code; composite set-component/version-schema relationships; compatible assignment component/version; status and lifecycle checks; positive revisions; valid hash format; object payload shape. Prevent saved payload/schema edits and deletes through database guards.

Indexes: component/name/id set listing; set/version descending; consumer/role/scope assignment lookup; audit entity/time; legacy exact references. Keyset cursor tests must cover tied names. Do not invent generic payload indexes without a demonstrated query.

### DB02 — query stores and enlisted persistence

Implement IParameterSetReadStore and ParameterSetReadStore for bounded catalog, schema, set/version, comparison payload, usage and receipt reads. Create named SQL in ParameterSetSql.cs and typed row mappings rather than inline SQL in actor maps.

Implement mutation adapters/state repositories using the existing event-source command infrastructure. Lock/revision checks protect append-only version allocation and assignment-retirement races. Provide exact-version eligibility reads that cannot rely on stale projection status.

Create ParameterSetEventProjector and assignment projector using the repository's established projection pattern. Projection application is idempotent by event identity/revision. Replaying historical facts cannot create a second version or overwrite a newer lifecycle state.

### DB03 — commit and projection visibility contract

Verified current configuration code saves an event-source state and then calls DomainEventsProjectionAsync for ConfigurationDb projection. Do not assume those separate calls prove one database transaction.

Implementation must trace that existing boundary and select one of these supported outcomes before coding success semantics:

- If event append and immutable configuration writes can enlist atomically, use that established transaction and prove it with failure injection.
- Otherwise, event-source state remains authoritative. A committed mutation result distinguishes CommitRecorded from ProjectionReady through explicit result metadata. Read-after-write operations use the authoritative exact state until projection catches up. Publish/assignment eligibility checks use authoritative state, never a lagging list projection.

Do not acknowledge a successful committed mutation and later pretend it never happened because a projection is delayed. Do not introduce a second independently mutable authority in parameter_operation; transactionally integrate or derive receipts from the same command authority. Resolve this choice in the phase-0 decision record and align schema/result contract tests before implementation.

### DB04 — migrations and seed

Implement migration inventory/report, legacy exact mapping and the idempotent draft seed. Preserve legacy hashes, IDs and bytes; disambiguate cross-kind UUID collisions. Seed uses the exact specification fixture and is never published or assigned automatically.

Acceptance: direct database constraint tests, event/state reconstruction, atomicity or explicit projection-lag tests, duplicate commands/events, migration rerun and rollback all pass against isolated PostgreSQL.

## 20. Complete message-to-actor-to-handler coding matrix

Every entry requires a public contract, validator, API client operation, actor parse map, exact receive mapping and extension handler. Commands additionally require validation-map coverage; queries require typed exception/reply mapping. Event handlers require explicit context/logger and awaited completion.

### M01 — parameter commands

| Message | Actor | Extension | Model/store responsibility |
| --- | --- | --- | --- |
| CreateParameterSetCommand | ParameterSetCommandActor | CreateParameterSet.cs | Structural validation, initial draft allocation |
| SaveParameterDraftCommand | Same | SaveParameterDraft.cs | Expected revision, append new version |
| RenameParameterSetCommand | Same | RenameParameterSet.cs | Metadata revision only |
| PublishParameterVersionCommand | Same | PublishParameterVersion.cs | Full validation, immutable publication |
| RetireParameterVersionCommand | Same | RetireParameterVersion.cs | Usage/eligibility and lifecycle |
| AssignParameterVersionCommand | ParameterAssignmentCommandActor or authoritative deployment adapter | AssignParameterVersion.cs | Exact role/scope/version eligibility |
| DisableParameterAssignmentCommand | Same authority | DisableParameterAssignment.cs | Append disabled assignment revision |

Each command carries OperationId, validated subject identity, expected revision/state as applicable and authenticated context. UI timeouts resolve by operation query, not a new identity with the same apparent action.

### M02 — parameter queries

| Message | Handler file | Result |
| --- | --- | --- |
| ListParameterAreasQuery | ListParameterAreas.cs | Area page |
| ListParameterComponentsQuery | ListParameterComponents.cs | Component page |
| GetParameterSchemaQuery | GetParameterSchema.cs | Immutable schema/editor metadata |
| ListParameterSetsQuery | ListParameterSets.cs | Set summary page |
| ListParameterVersionsQuery | ListParameterVersions.cs | Version page |
| GetParameterVersionQuery | GetParameterVersion.cs | Exact version payload |
| GetParameterUsageQuery | GetParameterUsage.cs | Current/historical consumer refs |
| CompareParameterVersionsQuery | CompareParameterVersions.cs | Typed comparison |
| GetParameterOperationQuery | GetParameterOperation.cs | Authoritative committed outcome |
| GetParameterAssignmentQuery | GetParameterAssignment.cs | Active/pending exact assignment |
| ValidateParameterCandidateQuery | ValidateParameterCandidate.cs | Candidate-hash validation report |

Map these to ParameterSetQueryActor, except an assignment-specific query actor may own GetParameterAssignment when required by the existing binding adapter. Do not duplicate routing ownership. Candidate validation uses unsaved payload plus schema/ref and has no write side effects.

### M03 — lifecycle facts

ParameterSetCreatedEvent → ParameterSetCreated.cs; ParameterDraftSavedEvent → ParameterDraftSaved.cs; ParameterVersionPublishedEvent → ParameterVersionPublished.cs; ParameterVersionRetiredEvent → ParameterVersionRetired.cs; ParameterAssignmentChangedEvent → ParameterAssignmentChanged.cs.

Add a committed metadata-change fact for RenameParameterSetCommand, named ParameterSetRenamedEvent → ParameterSetRenamed.cs, so event-sourced metadata reconstruction is complete. Register it in state application, projector and observer maps. This closes an omission in the earlier specification catalog; record it in the contract erratum.

Use ParameterSetEventActor for observer/domain reactions as required; authoritative projection remains under established projector ownership. Never update the same projection through two independent uncoordinated consumers.

### M04 — startup and monitoring messages

| Message | Actor | Extension |
| --- | --- | --- |
| PreviewSignalStartupPlanQuery | SignalStartupQueryActor | PreviewSignalStartupPlan.cs |
| GetSignalStartupPlanQuery | Same | GetSignalStartupPlan.cs |
| GetSignalStartupStatusQuery | Same | GetSignalStartupStatus.cs |
| ApplySignalStartupPlanCommand | SignalStartupCommandActor | ApplySignalStartupPlan.cs |
| RecordSignalStartupStepCompletedCommand | Same or reused existing startup owner | RecordSignalStartupStepCompleted.cs |
| RecordSignalStartupStepFailedCommand | Same owner | RecordSignalStartupStepFailed.cs |
| ExpireSignalStartupStepCommand | Same owner, existing scheduler delivery | ExpireSignalStartupStep.cs |
| GetSignalReadinessQuery | SignalReadinessQueryActor | GetSignalReadiness.cs |

Persist startup state transitions as existing startup events where available. Otherwise add SignalStartupPlanAppliedEvent, SignalStartupStepCompletedEvent, SignalStartupStepFailedEvent and SignalStartupStepExpiredEvent, with state-apply/projector tests. Do not persist a step transition without its reconstructable committed fact.

SignalReadinessEventActor maps supported existing producer lifecycle facts to event extensions. Inventory actual producer contracts in phase 0; do not invent subscriptions to nonexistent events. Latest observations are queried through existing read APIs.

Notify outputs: ParameterCatalogChangedNotification, ParameterAssignmentAppliedNotification, SignalReadinessChangedNotification. Include entity revision/runtime generation and timestamp; lost hints are repaired by queries.

### M05 — actor support files and registration

For each new actor create typed context interface/implementation, parse/receive maps and lifecycle registration. Command actors include state, repository, state event application, validators and projector as needed. Query actors include exception map and typed reply helpers. Do not introduce a custom actor base solely for this feature.

Acceptance: map parity and transport classification verification covers every row above, including newly added Rename/startup facts. No Function or Realtime request appears in these feature APIs.

## 21. API clients, dependency injection and composition

### API01 — shared API boundaries

Add typed parameter operations to IReferenceQueryApi/IReferenceCommandApi through focused partials, or inject dedicated IParameterSetQueryApi/IParameterSetCommandApi through the existing catalog if interface growth warrants separation. Select one approach in phase 0 and keep the UI boundary consistent. Do not expose SQL/repositories to UI.

Implement corresponding ReferenceQueryApi.ParameterSets.cs and ReferenceCommandApi.ParameterSets.cs in Application.Api.Nats.Client when using partials. Every operation constructs the exact validated subject and uses existing request/reply/error conventions. Client methods forward cancellation, expected revisions and original operation identity.

Add startup/readiness APIs to the owning existing application service boundary; UI preview/monitoring uses typed queries. No HTTP endpoint is required unless the current public API architecture needs one; if added, adapt the same contracts rather than duplicate business logic.

### API02 — composition roots

Update server Startup actor/context/service registration, actor assembly discovery as required, validators, state repositories, projectors, query stores and component descriptors. Update UI service catalog and ReferenceDataService boundary to expose parameter editing through the chosen API interfaces.

Register schema migrations through existing ConfigurationDb initialization. Register Regime's demand contributor with existing startup orchestration. Start/stop actor projectors through their established owner; no duplicate registrations or extra polling hosted service.

Add composition tests that resolve every actor context, editor/provider and API service in isolation and verify no missing dependencies or conflicting singleton ownership. Verify actual message routing through the API client, not only direct handler invocation.

## 22. Complete UI coding work packages

### UI01 — Reference navigation and reusable shell

Modify `TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs` and its existing layout/selector binding as needed to add Parameter Sets. Add `ParameterSetsReferenceView.cs` implementing the same command/lifetime contracts as existing Reference controls.

Add `ParameterSetsReferenceViewModel.cs` under UI.Net.ViewModels/Reference and `IParameterSetsUiService.cs`/`ParameterSetsUiService.cs` under UI.Net.Services/Reference. The service wraps typed application APIs only.

Implement master area list, child component list, detail host and search. Preserve selection by IDs; cancel obsolete reads; clear stale children/detail; display empty, unsupported and unavailable states. Use existing theme and control disposal patterns.

### UI02 — set/version header and lifecycle controls

Add `ParameterSetDetailView.cs` and `ParameterSetDetailViewModel.cs`. Implement named-set/version selectors and Overview, Parameters, Applicability, Usage & Startup, Versions, Validation and Audit tabs. Regime adds Market Signals through a registered editor provider.

Actions and their exact service calls:

| UI action | Contract |
| --- | --- |
| New Set | CreateParameterSetCommand |
| Edit saved version | Local working copy; no mutation yet |
| Save Draft | SaveParameterDraftCommand |
| Validate | ValidateParameterCandidateQuery |
| Compare | CompareParameterVersionsQuery, or pure local comparison for unsaved working copy |
| Publish | PublishParameterVersionCommand |
| Retire | GetParameterUsageQuery then RetireParameterVersionCommand |
| Assign for Next Startup | AssignParameterVersionCommand |
| Disable assignment | DisableParameterAssignmentCommand |
| Refresh after timeout | GetParameterOperationQuery with original identity |
| Preview startup | PreviewSignalStartupPlanQuery |
| Check current inputs | GetSignalReadinessQuery with explicit context |

Add dirty-navigation Save/Discard/Cancel dialog, revision conflict comparison/rebase, publication and assignment review, typed error panel and unknown-outcome state. Active versus next-startup references must remain distinct.

### UI03 — generic editor and Regime signal editor

Add `ParameterSchemaEditor.cs` for supported scalar/reference/object/collection controls and `ParameterEditorRegistry.cs` selecting registered editor providers. Add `RegimeDiscoveryParameterEditor.cs`, `RegimeSignalRequirementsGrid.cs`, `RegimeSignalRequirementRowViewModel.cs` and `RegimeDependencySummaryView.cs`.

Bind all 69 seed rows without default filtering that hides optional rows. Implement editable Enabled, Requirement, duration, supported calculation/source, preparation and monitoring fields; metric/timeframe edits use supported choices and dependency validation. Preserve row identity across version edits. New rows receive new identities; deletion is an explicit working-copy operation shown in comparison.

Show dependency errors by row/path and affected calculation section. Bulk edits use one working-copy transaction/undo operation and one resulting validation pass. No UI edit silently changes another calculation's weight or section state.

Create summary counts, filtered-grid counts, disabled/unsupported badges and contextual help explaining required versus optional. Expose incomplete draft issues while allowing structural draft saves; publish remains fully validated.

### UI04 — versions, usage, audit and readiness

Add `ParameterVersionComparisonView.cs`, `ParameterUsageView.cs`, `ParameterAuditView.cs` and `ParameterSignalReadinessView.cs` or reusable equivalents consistent with repository controls.

Implement paginated history/usage reads; do not preload every full version. Show exact hashes/IDs in expandable technical details. Readiness displays resolved instrument/timeframe, required/optional, age, warmness, source, generation, owners and blocking reason.

Use Notify as refresh hint and explicit current-state query. Ensure closing/navigating disposes subscriptions and cancels pending requests. Unknown/disconnected states cannot show Ready. Retain keyboard navigation and accessible text across all views.

### UI05 — UI verification deliverables

Implement view-model tests for every state transition and system tests for full editor lifecycle. Capture evidence for 69-row display, valid reduction, rejected invalid dependency, immutable version comparison, assignment pending restart and runtime readiness. Test resize, focus order, Enter/Escape actions and cancellation without losing unsaved values.

## 23. Regime, startup and monitoring code integration

### RD01 — exact seed artifact

Create a checked-in fixture/seed source with the specification's 69 ordered pairs and documented 35 Required/34 Optional defaults. Avoid copying the prose table manually into several production files. A single typed seed factory generates the initial draft; verification compares it with a canonical test fixture.

Do not assume the failing runtime used the seed's default ages/requiredness. Legacy migration expands the actual saved payload. Seed is editable and unassigned; unsupported enabled calculations are visible validation errors; unavailable historical coverage is a visible cold-start reason, not a publication error.

### RD02 — evaluator and request factory

Modify RegimeDiscoverySnapshotRequestFactory through explicit schema-version dispatch: old schema uses compatibility expansion; new schema delegates to configured requirement model. Remove implicit injection for new-schema payloads.

Add/extend typed evidence outcomes and map them through pipeline failure/readiness presentation. Distinguish WaitingForInputs from a completed valid decision. Validate reduced profiles against the real calculation dependency graph, and implement supported section omission/alternatives before making those choices editable.

### ST01 — preparation execution

Connect assigned Regime requirements to existing startup activities. Implement dependency graph and exact plan persistence, snapshot fingerprint validation, bounded step transitions and scheduler deadlines. Source/calculation descriptors provide actual minimum warmup dependencies. Respect provider costs and current ownership; do not equate an enum value with a working producer.

Existing consumers must contribute their requirements before stopping any shared producer based on the new parameter list. Applying the edited configuration happens at next startup; mere publication does not change the running graph.

### MON01 — readiness projection

Implement current-read adapters, event lifecycle handlers, pure per-consumer readiness models and Notify coalescing. Monitoring must never submit another preparation command when a signal is merely missing. Verify that missing-input duration increases without increasing exhausted startup-attempt counts.

## 24. Exhaustive testing work checklist

For each work package, add tests in the nearest existing project family. New tests are part of implementation, not optional follow-up work.

| Work package | Unit tests | Integration tests | Verification evidence |
| --- | --- | --- | --- |
| C01/C02 identities/models | Invalid IDs, bounds, equality, serialization | API request/reply round-trip | Public contract compatibility |
| C03 enums | Unknown/value validation | Legacy decode | Numeric values unchanged |
| C04 schema/codec | Canonical hash, unknown fields, limits | Save/read schema payload | Culture/version fixtures |
| C05 domain models | Legal decisions and all rejection branches | Decisions used by actual handlers | No IO/transport/UI model dependencies |
| DB01 schema | Migration descriptor validation | Real FK/check/immutability/index tests | Repeated migration and rollback |
| DB02 state/store | Mapping and cancellation | Concurrent version allocation and exact reads | Single authority and deterministic replay |
| DB03 visibility | Committed/pending/ready result semantics | Event/projection fault injection | No false success, no lost committed mutation |
| DB04 seed/migration | 69 pairs and stable identities | Seed twice, legacy collision/hash | Before/after inventory comparison |
| M01 commands | Each extension's success/rejections | Real actor API and database effects | Parse/validation/receive parity |
| M02 queries | Pagination/validation/reply paths | Real query replies and cancellation | No side effects in validation/preview |
| M03 events | Duplicate/stale facts | Durable redelivery/projector behaviour | Full reconstruction including Rename |
| M04 startup | Step transitions/deadlines | Real producer boundary and scheduler | No Function/Realtime feature contracts |
| API01/API02 | Routing/envelopes | Composition root and transport | No UI storage dependency |
| UI01/UI02 | Selection/edit/action state | Full save/publish/assign workflow | Accessible navigation/screenshots |
| UI03/UI04 | Bulk edits, dependencies, counts | Query-backed detail and readiness | No misleading active/pending status |
| RD01/RD02 | Missing optional/required/evidence policies | Actual configured snapshot/evaluation | Meaningful reduced configuration |
| ST01 | Demand union and budgets | Shared ownership/restart/cost bounds | Finite preparation with correct fingerprint |
| MON01 | Age/generation/alert coalescing | Event/query/Notify loss paths | Missing does not restart exhausted work |

Add tests for all specification AT-001 through AT-030 and trace their concrete class/method names in the evidence document. Also cover additions in this consolidated plan: Rename event replay, full startup-state reconstruction and explicit projection-lag handling.

No broad test run against a developer's live financial/configuration database. Integration fixtures own isolated databases and message subjects. Live market-data capability checks use separate explicit settings/budgets and do not mutate operational assignments.

## 25. Unified execution checklist and completion

Use this as the final coding checklist; each item must have implementation files, tests and recorded results:

- [ ] Phase-0 authority/actor/message erratum completed; subject/error registry names verified.
- [ ] Generic identities/models/enums/validators/codecs/descriptors implemented.
- [ ] SQL tables/constraints/indexes/migrations and exact-read stores implemented.
- [ ] Command states/repositories/event application/projectors implemented with proved visibility semantics.
- [ ] Every M01–M04 contract implemented and mapped to its extension handler.
- [ ] Model folders own domain decisions/calculations; maps contain dispatch only.
- [ ] NATS API clients, server composition and UI service catalog connected.
- [ ] Reference Data Parameter Sets navigation and every detail/editor tab implemented.
- [ ] Initial 69-row draft and bulk editing/dependency validation implemented.
- [ ] New-schema Regime request generation and tested reduced-evidence evaluation implemented.
- [ ] Exact next-startup assignment, shared signal planning/preparation and budgets implemented.
- [ ] Current readiness queries/event handlers/monitoring UI implemented without recovery loops.
- [ ] Legacy migration, deterministic seed rerun and rollback verified.
- [ ] Unit, integration, architecture and verification tests implemented and passing.
- [ ] Actual commands/results, UI evidence and remaining limitations recorded.

The implementation is complete only when a user can navigate Reference Data, edit a valid reduced Regime signal set, save/publish/assign it, restart and observe that exact configuration driving preparation, evaluation and monitoring, with all persistence/message/actor conventions and test gates satisfied.


## 26. RSI-first optional historical initialization and staged rollout

### 26.1 Agreed behavior and scope

Historical input accelerates warmup; it is not required to start a signal. The startup command accepts an immutable historical bar array, including an empty array. It restores applicable durable state, loops over accepted historical bars in chronological order through the normal calculation/state-event path, and then continues normal live processing. An empty array executes zero iterations. It does not insert a zero observation or reset a restored checkpoint.

If the requested historical window cannot be obtained as a valid contiguous sequence, use an empty seed and record the reason. Expected session closures and holidays do not constitute missing trading intervals. Do not silently combine disconnected segments. Do not run an automatic acquisition/recovery loop because history is absent. Separate Started/Warming/Ready from startup execution errors and current freshness.

First implementation is RSI only, including RSI13/TDI and RSI14/Regime identities, and the existing Daily route. No other signal receives the new startup behavior before the observation gate. Shared history preparation may be reusable, but adopting it in other signals is deferred.

### 26.2 Timeframe coverage and data preparation

Create an explicit RSI capability matrix covering every TimeFrameType value. Ordinary intraday targets are TenSeconds, FifteenSeconds, OneMinute, FiveMinutes, TenMinutes, FifteenMinutes, ThirtyMinutes, OneHour and FourHours. Daily uses EOD data. Weekly, Monthly and Quarterly require completed calendar-period aggregation of EOD sessions if supported as RSI routes; include matching live update behavior. None is invalid. WeekMonthBridge must use an established domain definition if one exists; otherwise record an explicit capability/domain decision, not an invented duration. Do not silently narrow the user's all-time-periods requirement to only the currently registered six intraday intervals.

Verify and extend live timeframe production as necessary before claiming historical/live parity. The current trade-session accumulator does not implement every enum member. Historical and live paths must share session alignment, exclusive interval ends and calendar rules.

For intraday periods, read stored trade ticks using bounded contract/session/time-range queries and produce OHLCV observations. A close is the last eligible trade inside [start,end), never the nearest trade after end. Resolve equal timestamps deterministically. Current GetLastFuturesTickDataByTickTime uses exact equality; add proper bounded range access rather than assuming that lookup finds a preceding tick. Reuse a prepared batch across compatible consumers rather than scanning history independently per indicator.

For Daily, obtain X completed, valid trading-session EOD observations and use Close for RSI. Do not substitute settlement without an explicit source policy. Weekly/monthly/quarterly closes, where supported, come from the last completed session of each completed period. Retain full OHLCV/provenance for future consumers.

Separate CalculationPeriodLength, SeedBarCount and TimeFrame. RSI14's current Wilder path produces its first RSI after 15 closes and warm RSI plus slope after 16 closes. The requested seed count must respect consumer needs; RSI13/TDI also needs its output-history window. More history may improve agreement with continuously running recursive calculations; minimum warmness alone does not establish such agreement.

Use previous-session observations at market open with their original dates. A prior close may anchor the next change; it must not be replicated as an entire synthetic history. Validate contract/continuation mappings and rollover boundaries. Missing coverage, invalid source data or preparation timeout selects the empty-history fallback with diagnostics; malformed signal configuration still fails explicitly.

### 26.3 Contracts, actors and model work

Extend StartFuturesRsiSignalCommand compatibly: preserve existing MessagePack keys and append a versioned initialization payload. Missing payload from old callers means empty seeding. Payload includes operation/startup generation identity, calculation configuration/version, requested seed count, prepared immutable bars, batch hash/provenance, historical cutoff and bounded execution deadline. Do not duplicate conflicting period/timeframe identities outside EntityId. Add a corresponding typed start contract for the existing Daily entity route if needed; do not change historical actor stream identities to force both routes together.

Map Start through _parseMap, _validationMap and _receiveMap in the existing RSI Command actor to a message-specific awaited extension. Add models under FuturesRsiSignal/Command/Model for seed validation, replay and checkpoint/handoff decisions. No SQL or calculation loops in actor maps. No new Function or Realtime actor for initialization; preserve the existing live event ingress.

Preparation IO belongs behind typed storage/application APIs, outside pure calculation models. The signal receives a validated immutable input batch or empty array. Do not add a service that repeatedly re-fetches history until warm.

The intraday bar path uses FuturesRsiWilderAccumulator and FuturesRsiWilderSignalFactory. The current Daily generator uses the existing collection-based RSI path and its generated events do not carry the same checkpoint. Make this difference explicit: initialization must use the same algorithm/state semantics as subsequent live updates for each route. Do not seed with Wilder state and then continue with a different Daily algorithm. Preserve existing Daily behavior for this pilot; any intentional algorithm migration requires a distinct configuration/version and comparison evidence.

Append generated signal events and resulting accumulator state through the established state repository/projector. Persist a reconstructable initialization result containing operation ID, seed identity, accepted/skipped count, seed disposition, cutoff and warmness. If adding an initialization fact, update the repository's filtered/limited event loading so it restores that fact and required output history; otherwise a restart could forget completion and replay the seed. Verify identity/hash mismatch rejection for duplicate startup commands.

Because intraday entity identity includes ValueDate, seed previous-session observations into the target actor through explicit initialization handling while preserving source dates. Do not route each historical bar to a different historical actor and assume the current actor was seeded. Do not relabel historical observation timestamps as current.

### 26.4 Live handoff, interruption and operator outcomes

Establish a cutoff and buffer or catch up live bars during seeding. All later live bars enter the same accumulator exactly once. Do not attach too late and lose observations, or seed an already-advanced checkpoint backwards. Existing valid persisted state is reused; incompatible state is reported, never silently mixed with a different source or configuration.

Distinguish signal start completion from indicator readiness. Suggested startup dispositions: Seeded, StartedEmpty, Restored, Failed. Empty reasons include NotRequested, MissingHistory, NonContiguousHistory and PreparationTimedOut. Expose sample count, required count, last accepted interval, freshness, continuity status and generation. Use existing compatible status types when available instead of creating duplicate enums.

During production interruption, stale or broken-continuity state must not be reported ready merely because one fresh tick arrived. Determine recovery from valid checkpoint/continuity evidence or new accumulation, with bounded transitions. Recovery concerns the signal/service; old terminated trading workflows are not resurrected.

An eligible ITI signal still starts the workflow. Regime Discovery checks required inputs, returns structured WaitingForInputs and terminates that execution when necessary. Severity and continuation are independent: informational warmup may stop an execution without making the application unhealthy. Display later stages as NotReached with the blocking operator. A subsequent ITI trigger creates a fresh execution. Retain exception, timeout, invalid-result and critical infrastructure outcomes distinctly. Keep the application and unaffected services available during degraded operation.

### 26.5 Test and implementation gates

| Gate | Required work and evidence |
| --- | --- |
| RSI-P0 inventory | Enumerate timeframe routes, source queries, current algorithms, stream identities, event loading and consumer windows. Record real capability gaps without treating missing history as a failure to start. |
| RSI-P1 contracts/models | Key compatibility, identity validation, seeded/empty behavior, deterministic replay and startup outcomes implemented through standard maps/extensions/models. |
| RSI-P2 persistence/handoff | Durable event/checkpoint/result replay, command idempotency, generation isolation and gap-free live handoff verified. |
| RSI-P3 automated verification | Unit, isolated storage/actor integration and UI/workflow behavior tests pass for every claimed timeframe and both RSI configurations. |
| RSI-P4 observation | Run the RSI-only implementation for approximately one complete trading day with useful data and inspect the evidence below. Do not mark complete just because 24 hours elapsed. |
| RSI-P5 expansion | Review RSI-P4 evidence and unresolved defects, update the reusable pattern, then adopt it one signal family at a time with its own dependency and parity tests. |

Unit tests must cover empty startup and preservation of restored state; exact full seed versus incremental live processing from the same starting state; RSI14 warm boundary and slope; RSI13 downstream window; insufficient/gapped history fallback; session-open use of prior observations; holiday/DST and session boundaries; no future tick selection; Daily and longer-period completion; duplicate/out-of-order bars; changed seed hash on repeated operation; interruption freshness/continuity; cancellation and deadlines. Verify synthetic flat prices are not inserted.

Integration tests must cover real isolated tick/EOD reads, persistence failure rollback, restore after seed, duplicate Start delivery, seed/live interleaving, current-value-date state seeded from prior sessions, projection delay and downstream notification behavior. UI/workflow tests must show StartedEmpty/Warming/Ready, diagnostic severity, visible ITI-admitted workflow termination at Regime Discovery and later successful progression without restarting the application. Failure storms must not increase history-fetch attempts without an explicit bounded recovery action.

### 26.6 Trading-day observation and rollback

Record build/configuration identifiers, seed source/count/hash/cutoff, covered timeframes, startup disposition, warm transition, signal timestamps and workflow IDs. Observe both seeded and empty starts, market-open behavior, live accumulation and at least one controlled gap/restart using an isolated or non-ordering test setup. Do not deliberately interrupt production trading or place orders merely to complete this gate.

Use recorded deterministic replay to verify long-period windows that cannot mature within one day; mark actual live coverage separately. Sparse development data is a valid degraded-operation case, but is not proof of continuous production behavior. Extend the observation window when necessary to obtain meaningful evidence; report incomplete coverage honestly.

Check no duplicate application, no lost bars at handoff, no false warmness, no startup blocking, no retry amplification, RSI13/TDI compatibility and no unexplained calculation divergence. Show visible Regime stop reasons followed by new workflow progression when required inputs become available. Other missing signal families can still stop Regime; record the precise remaining dependency rather than claiming RSI fixes all 69 inputs.

Rollback disables historical seeding and restores empty-start behavior while preserving compatible durable state. Version contracts/events additively so previously committed history stays readable. Do not automatically erase accumulators or change trading configuration for rollback.

Exit RSI-P4 only after the observation evidence is reviewed and material defects are resolved. Broader signal rollout is deliberately held at this gate; it is not a requirement to implement all signal families before observing RSI. The overall ParameterSets completion checklist remains open until its separate storage, assignment, UI and runtime gates also pass.


## 27. Explicit horizon sets and historical initialization amendments

### 27.1 Approved interval membership

| ITI/workflow horizon | Included observation intervals |
| --- | --- |
| Daily | 15 seconds, 1 minute, 5 minutes |
| Weekly | 15 minutes, 1 hour, 4 hours, Daily bars |
| Monthly | 1 hour, 4 hours, Daily bars |

The Daily/Weekly/Monthly workflow horizon is not the indicator bar interval. The existing Daily VX front/second-ratio dependency is explicitly included in every set. Every configured interval contributes its required calculation inputs; no optional interval remains. Keep weights editable. New Daily drafts use equal relative weights and maximum ages of three intervals; Weekly/Monthly preserve existing weights and age limits. These draft defaults must be reviewed before publication.

### 27.2 Schema, editor and migration

- Schema 3 retains serialized IsRequired members for wire compatibility, but requires them to be true. Enabled is the sole membership switch. No included signal can be optional.
- Seed nonessential catalogue entries as excluded. The legacy 69-row schema-2 seed remains reproducible; catalogue size follows the selected interval set (Daily/Monthly 55 rows, Weekly 69 rows).
- New draft creation selects a horizon. Interval editing adds/removes intervals and edits relative weights/freshness, then regenerates dependent metric rows. Preserve matching row identities and edits; surface newly included dependencies for review.
- Upgrading an older version creates a working copy and ultimately a new version. Do not mutate saved payloads, hashes or assignments. Rebuild structure evidence at the longest included interval.
- Invalid/incomplete schema-3 drafts must remain reopenable for correction. Publication still requires complete dependency validation.
- Cover schema-2 compatibility, all three mappings, rejected optional semantics, interval removal, regenerated structure dependencies, immutable upgrade, persistence and editor behavior with tests.

### 27.3 Historical RSI pilot

Use existing Databento historical acquisition contracts for older intraday data and retained ticks/live replay for the recent boundary. Verify actual dataset availability and contiguous session-aligned coverage; the advertised 24-hour boundary is not a promise that local historical and live windows meet. Build 15-second bars from trades or finer aggregates, never from minute bars. Construct longer bars using the same calendar/alignment as live processing. Daily intervals use completed EOD observations.

The one-day development goal is a measured readiness target, not a guaranteed delay or a mandatory wait. History may make Monthly workflows ready shortly after startup. RSI alone cannot qualify other signal families. Keep the existing approximately one-trading-day observation and explicit user acceptance gate before expanding the pattern.

### 27.4 Development access

User decision: explicit single-user development policy. API-host configuration ParameterSets:SingleUserDevelopmentEnabled enables read/author/publish/assign/retire only when the host environment is Development. Enforce capabilities in command/query extension handlers. The current trusted development host identity is not an authenticated remote-user permission system. Production authorization integration remains deferred and disabled by this policy.


### 27.5 Runtime snapshots and persistent assignments (2026-09-10)

Registry-backed authoring/schema inspection, lossless read-only handling, startup preview, durable process-boot generation application, current-assignment retirement protection, startup producer demand union, and frozen Regime assignment resolution are implemented. See `../ParameterSets/Docs/Implementation-Evidence.md` for precise scope and test evidence.

User-confirmed lifetime: parameter assignments persist until explicitly changed or disabled; changes are expected to be rare. ApplicationShutdown and manual generation release are not dependencies or blockers. Published payloads remain immutable and retained after retirement. Running workflows retain their captured version; retirement checks current assignments, not historical startup records. The recent-startup inspection cache is bounded to 100 records without limiting application starts; durable history remains in event storage and ConfigurationDb. Changes retain the approved NextStartup activation policy.

Remaining gates include live actor/host acceptance, migration/rollback compatibility, readiness/monitoring completion, producer outcome/failure-injection evidence, and the RSI historical seeding pilot plus observation/acceptance. These remain open; the implementation is not declared complete.

### 27.6 Durable startup evidence, monitoring and migration (2026-09-10)

The implementation includes standard actor report commands/queries, immutable PostgreSQL report history, bounded host preparation outcomes, current monitored-signal availability, Reference Data evidence/monitoring dialogs, read-only legacy inventory with exact draft migration and kind-qualified lineage, and successful workflow assignment provenance. Exact migration preserves original calculation settings and legacy optionality; schema-3 conversion remains a separate reviewed edit. Do not substitute current seed values for legacy frozen values.

Real NATS actor-host acceptance is being qualified on disposable PostgreSQL/Redis/NATS infrastructure. See the implementation evidence for current passing tests and remaining gates. RSI acquisition/replay/handoff and its user observation/acceptance gate remain last; Market Outlook remains deferred.


### 27.7 RSI pilot implementation and current acceptance boundary (2026-09-10)

The additional Parameter Sets RSI startup route now supports optional retained-tick/EOD seeding for all seven configured bar intervals, including Daily. Initialization reuses the Wilder accumulator, applies either the complete validated sequence or zero observations, preserves restored checkpoints, records seed disposition and resumes the existing live route. Historical Databento intraday acquisition is not part of this implemented increment. Existing RSI13/TDI startup is unchanged.

Real NATS Parameter Sets lifecycle acceptance, the isolated nearest-tick Scylla query, RSI replay/handoff model checks, source fallback tests and current UI rendering passed. Read `../ParameterSets/Docs/Implementation-Evidence.md` for exact results. These close the named checks only; sections 24?26 remain the acceptance authority.

Do not mark the overall implementation complete: useful trading-day observation/user acceptance, full retained-reference migration inventory and exhaustive transport/fault-injection qualification remain open. The registered-schema nullable-reference compatibility correction is complete in schema 4. Application shutdown is not a gate. Market Outlook and other indicator rollout remain deferred.

### 27.8 Compatible schema-4 migration (2026-09-10)

Regime Discovery schema 4 supersedes schema 3 for new drafts and editor working copies while retaining schema 3 membership, interval and dependency behavior. The version-4 schema generator honors nullable-reference metadata. Structural validation rejects null for non-nullable domain objects and recognizes intentionally nullable compatibility fields. Domain validation continues to require explicit signal rows for schema 4.

Schema definitions 1-3 remain generated by the original algorithm; their JSON and hashes are verified unchanged. ConfigurationDb adds only the new (component_code,4) immutable row. Editing a schema 1-3 payload creates a reviewable schema-4 working copy, and Save appends the next immutable parameter-set version. No published payload, assignment, frozen startup snapshot or old schema row is rewritten. Publication and NextStartup assignment remain explicit.