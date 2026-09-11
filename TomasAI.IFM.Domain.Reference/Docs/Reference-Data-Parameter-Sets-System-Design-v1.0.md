# Reference Data Parameter Sets — System-Wide Detailed Design

**Version:** 1.0  
**Date:** 2026-09-10  
**Status:** Proposed; not implemented  
**First implementation:** Regime Discovery

## 1. Objective

Add a system-wide Parameter Sets entry to Reference Data. Users select an application area, a configurable component, a named parameter set and a version, then use a typed detail editor. Regime Discovery is the first supported implementation and the reference pattern for future components.

Populate its initial draft with the exact 69 metric/timeframe pairs from the supplied error. Users can disable signals and change requiredness, freshness and supported calculation selections. The assigned version also determines startup preparation and monitoring expectations.

Reducing inputs must preserve meaningful calculation behaviour. Never silently fabricate zero values or a successful regime. Missing observations do not prevent the API host from starting; they may leave the affected consumer waiting. This design requires no additional continuously polling recovery service.

This document is a design artifact only. Runtime code, database records and configuration are unchanged.

## 2. Verified existing infrastructure

| Location | Verified role |
| --- | --- |
| `TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs` | Reference categories and hosted controls |
| `TomasAI.IFM.UI.Net.Views/Reference/StrategyCatalogReferenceView.cs` | List/detail UI and publication interactions |
| `TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationParameterSet.cs` | Strategy-specific kinds, versions, hashes and lifecycle |
| `TomasAI.IFM.Application.Storage/ConfigurationDb/Schema/ConfigurationSchemaSql.cs` | PostgreSQL JSONB configuration tables |
| `TomasAI.IFM.Domain.Strategy.Contracts.Shared/Reference/StrategyCatalog/StrategyCatalogContracts.cs` | Exact pipeline references containing kind, ID, version and hash |
| Domain.Trade `RegimeDiscovery/Model/RegimeDiscoverySnapshotRequestFactory.cs` | Hardcoded metric lists combined with configured frames |
| Domain.MarketData.Analytics `RegimeDiscovery/RegimeDiscoveryMarketSignalSnapshotProvider.cs` | Process-local observations and readiness checks |
| Application.MarketData `Historical/HistoricalAnalyticsWarmupOptions.cs` | Daily historical warmup settings |

Existing Regime configuration is stored in `reference_configuration.regime_discovery_parameter_set.payload_json`. New intraday preparation requires producer support, not simply extra UI fields. Configuration persistence does not make market observations durable.

The supplied failure does not contain its frozen parameter payload. The seed uses checked-in Daily defaults, not claimed settings recovered from that failing run.

Follow [Actor Message Types and Delivery Conventions](Actor-Message-Types-and-Delivery-Conventions.md) and [Actor Implementation Conventions](Actor-Implementation-Conventions.md). Commands/Queries/Functions use Core NATS; durable Events use JetStream; Notify supplies observer hints. Do not introduce transport flags or actor types.

## 3. UI navigation and behaviour

### 3.1 Hierarchy

Reference Data → Parameter Sets → Application Area → Component → Parameter Set → Version.

Master pane: Application Areas. Child pane: Components, labelled Pipeline Operators within Strategy Workflow. Detail header: parameter-set selector, version selector, status, description and applicability. Avoid a fourth permanent pane by placing set/version selection in Detail.

Initial area: `strategy-workflow` / Strategy Workflow. Initial editable component: `strategy-workflow.regime-discovery` / Regime Discovery. Workflow Settings, Market Condition, Market Condition Assessment, Trade Selection, Order Composition and Risk Management may appear as read-only catalog entries explicitly marked Editor not available. Their listing does not imply execution order or implemented support.

Stable codes identify components. Names are presentation data. Registering a database row alone cannot create a runtime implementation.

### 3.2 Detail view

Tabs: Overview, Parameters, Market Signals, Applicability, Usage & Startup, Versions, Validation, Audit.

Actions: New Set, Edit as New Draft, Save Draft, Validate, Compare, Publish, Retire, Usage, Refresh, Assign for Next Startup, View Active Version, Cancel Pending Assignment.

Published versions are read-only. Opening one for editing creates a working copy. Multiple named sets require explicit selection unless navigation supplies a specific consumer context. Empty lists offer Create parameter set. Unsupported schema versions allow inspection/export, not destructive editing.

UI states: Empty, Loading, Viewing, Editing, Validating, Saving, Conflict, Failed. Cancel obsolete reads and use selection-generation checks to reject late responses. Leaving dirty edits offers Save Draft, Discard or Cancel. Failures preserve edits. Disable conflicting commands while a mutation is pending.

Reuse existing Reference Data controls, dark theme and typography. Include keyboard navigation, accessible labels, units, field-level errors and status text independent of colour. No technical JSON is required in normal editing.

### 3.3 Market Signals grid

Columns: Enabled, Metric, Timeframe, Requirement, Maximum Age, Calculation, Source Binding, Prepare at Startup, Monitor, Validation.

Support bulk enable/disable and required/optional changes. Disabled rows stay visible and saved. Filters: All, Enabled, Required, Optional, Invalid, Unsupported. Show configured counts separately from runtime missing counts.

A dependency panel identifies calculations using the selected signal. Change preview explains affected calculations and producer dependencies. Weights stay in their calculation sections unless the model supports a per-signal weight.

Check current inputs requires an instrument/consumer context and returns timestamped, generation-specific diagnostics. It neither modifies the draft nor blocks publication because markets are offline.

## 4. Generic architecture and extension contracts

| Layer | Responsibility |
| --- | --- |
| Shared UI | Navigation, lifecycle, typed fields, comparison, audit |
| Component descriptor | Schemas, editor, validator, scope and application policy |
| Application service | Authorization, validation, versions, lifecycle and assignments |
| Store | Transactions, constraints, immutable payloads and idempotent outcomes |
| Domain adapter | Typed payload, dependency rules, runtime resolution |
| Startup planner | Union active consumer demands and build bounded execution plan |
| Existing producer actors | Initialize/warm supported signals |
| Monitor | Current availability and per-consumer readiness |

Proposed namespaces: `TomasAI.IFM.Domain.Reference.Shared.ParameterSets` for contracts; `TomasAI.IFM.Application.Storage.ConfigurationDb.ParameterSets` for storage; existing UI Reference namespaces for views/view models. Respect existing physical contracts assembly/type-forwarding patterns. Regime payloads remain domain-owned.

Interfaces:

- `IParameterComponentDescriptor`: code, display metadata, supported schema refs, scope descriptors, application policies.
- `IParameterPayloadValidator`: bounded pure validation returning typed issues.
- `IParameterPayloadCodec<T>`: version-aware serialization, canonicalization and typed materialization.
- `IParameterEditorProvider`: generic or specialised editor selected by registered key.
- `IParameterConsumerAdapter`: existing assignment authority and exact resolution.
- `ISignalRequirementContributor`: resolved payload to signal demands.
- `ISignalProducerDescriptor`: supported outputs, source compatibility, dependencies, warmup and readiness.

Use registered identifiers, never scripts or arbitrary assembly/type names from database metadata.

## 5. Models and identity types

All types below are proposed unless noted as existing. Follow repository serialization conventions; allocate stable MessagePack field keys and never reorder existing contracts.

| Model | Fields / meaning |
| --- | --- |
| `ParameterAreaId`, `ParameterComponentId`, `ParameterSetId` | Distinct UUID wrappers; reject empty |
| `ParameterComponentCode` | Stable bounded code |
| `ParameterSchemaRef` | ComponentId, positive SchemaVersion, SchemaHash |
| `ParameterVersionRef` | SetId, positive Version, ComponentId, PayloadSha256 |
| `ParameterAreaSummary` | Id, Code, Name, Description, SortOrder, Enabled |
| `ParameterComponentSummary` | Id, AreaId, Code, Name, Description, EditorAvailability, SupportedSchemas |
| `ParameterSetSummary` | Id, ComponentId, Name, Description, Revision, latest version, assignment summaries |
| `ParameterSetVersion` | Ref, SchemaRef, Status, PayloadJson, Description, CreatedAtUtc, CreatedBy, PublishedAtUtc?, RetiredAtUtc? |
| `ParameterScope` | ScopeSchemaVersion, validated typed dimensions, canonical ScopeHash |
| `ParameterAssignment` | Id, ConsumerKindCode, ConsumerId, Role, Scope, VersionRef, Revision, ApplicationPolicy |
| `ParameterValidationIssue` | Code, Severity, JSON path, RowId?, Message, DependencyRefs[] |
| `ParameterValidationReport` | CandidateHash, SchemaRef, ValidatorVersion, Issues[], ValidatedAtUtc |
| `ParameterOperationResult<T>` | OperationId, Outcome, Value?, Issues[], CurrentRevision?, CommittedRef? |
| `ParameterVersionComparison` | BaseRef, candidate ref/hash, changes with path/old/new/impact |
| `ParameterUsage` | Consumer, assignment authority, exact version, active/pending state |

Renaming a set changes metadata revision, not its ID or payload hash. Scope is validated by the component, never treated as an arbitrary database predicate.

### 5.1 Enums

Numeric values below apply only to proposed new enums. Do not alter existing enum values.

| Enum | Values |
| --- | --- |
| `ParameterVersionStatus : byte` | Draft=0, Published=1, Retired=2 |
| `ParameterEditorAvailability : byte` | Unknown=0, Supported=1, ReadOnly=2, Unsupported=3 |
| `ParameterApplicationPolicy : byte` | Unknown=0, NextOperation=1, NextStartup=2, ExplicitReload=3 |
| `ParameterIssueSeverity : byte` | Unknown=0, Information=1, Warning=2, Error=3 |
| `ParameterOperationOutcome : byte` | Unknown=0, Completed=1, AlreadyCompleted=2, ValidationFailed=3, Conflict=4, NotFound=5, Forbidden=6, Unsupported=7 |
| `SignalRequirementMode : byte` | Unknown=0, Optional=1, Required=2 |
| `SignalStartupPreparation : byte` | None=0, Initialize=1, InitializeAndWarm=2 |
| `SignalRuntimeReadiness : byte` | Unknown=0, Disabled=1, Pending=2, Warming=3, Available=4, Missing=5, Stale=6, Invalid=7, Unsupported=8, Failed=9 |
| `ParameterConsumerReadiness : byte` | Unknown=0, Disabled=1, WaitingForInputs=2, Ready=3, Degraded=4, ConfigurationInvalid=5 |
| `RegimeEvidenceOutcome : byte` | Unknown=0, Sufficient=1, ReducedEvidence=2, InsufficientEvidence=3 |

Reuse existing `RegimeDiscoverySignalMetric`, `TimeFrameType` and market-series/calculation identities. Areas, component kinds and consumer kinds are registry codes rather than an expanding strategy enum.

## 6. PostgreSQL storage schemas

Proposed schema: `reference_configuration`. UUID identities; `timestamptz` dates; positive integer versions and bigint revisions; JSONB payloads; validated lowercase SHA-256 hashes.

| Table | Columns and constraints |
| --- | --- |
| `parameter_area` | area_id UUID PK, code TEXT UNIQUE, name, description, sort_order INT, enabled BOOL, revision BIGINT, timestamps |
| `parameter_component` | component_id UUID PK, area_id FK, code UNIQUE, name, description, descriptor_key, enabled, revision |
| `parameter_schema` | (component_id,schema_version) PK, schema_json JSONB, editor_json JSONB, schema_sha256, validator_key/version, created_at_utc; immutable |
| `parameter_set` | set_id UUID PK, component_id FK, code, name, description, revision, next_version INT, creator/timestamps; UNIQUE(component_id,code), UNIQUE(set_id,component_id) |
| `parameter_set_version` | (set_id,version) PK, component_id, schema_version, status SMALLINT, payload_json JSONB, payload_sha256, description, created_at_utc/by, published_at_utc?, retired_at_utc?; composite FKs to set/component and schema |
| `parameter_assignment` | assignment_id UUID PK, consumer_kind_code, consumer_id, role, component_id, scope_json JSONB, scope_sha256, revision; UNIQUE(consumer_kind_code,consumer_id,role,scope_sha256) |
| `parameter_assignment_revision` | (assignment_id,revision) PK, exact set/version/hash, enabled BOOL, application_policy, effective_from_utc, created_at_utc/by; immutable, FK to version |
| `parameter_operation` | operation_id UUID PK, request_sha256, operation_type, result_json JSONB, committed_at_utc |
| `parameter_set_audit` | audit_id UUID PK, operation_id, entity_type/id, principal, action, before/after refs, timestamp, reason; append-only |
| `parameter_legacy_reference` | (legacy_kind,legacy_set_id,legacy_version) PK, generic component/set/version, legacy hash |

Use NOT NULL on mandatory fields and CHECK constraints for statuses, positive numbers, hash format, JSON object shape and lifecycle timestamps. Payload, schema and identity fields of saved versions are immutable. Draft→Published→Retired is the only version lifecycle path. Preserve retired versions for historical resolution.

Indexes: sets (component_id,name,set_id), versions (set_id,version DESC), assignments by consumer/component, audit (entity_id,timestamp), operations by ID. Use keyset pagination and stable tie-breakers. Add payload expression indexes only for verified query needs.

Initial limits: 1 MiB payload, 512 signal rows, 100 rows maximum per query page, 128-character codes, 200-character names, 4,000-character descriptions. Validate nesting/collection sizes and enforce server-side; never truncate.

### 6.1 Transaction boundaries

Save Draft locks the set row, checks ExpectedRevision, allocates next_version, inserts immutable payload, advances revision, records audit and command receipt, then commits once. Same OperationId and request hash returns the original result; a different hash fails.

Publish revalidates the exact version with supported schema/domain rules, checks Draft state and commits lifecycle/audit/receipt atomically. Assignment validates component, publication eligibility and exact hash in the transaction. Assignment revision must reference a set belonging to its component; enforce a composite FK or equivalent transaction constraint.

Canonical payload serialization sorts object properties, preserves array order and uses invariant numeric/date formatting. Lifecycle metadata is excluded from payload hashing. Canonicalization version belongs to schema metadata. Legacy hashes are preserved, not silently recomputed.

### 6.2 Existing assignment ownership

Strategy deployments currently carry exact pipeline references. Keep them authoritative during adaptation. Generic assignment queries combine adapter-backed existing bindings with new generic bindings without dual authority for a consumer role. Do not write two independently editable assignment records for the same role.

### 6.3 Runtime data

Parameter tables store intent, not price observations. Retain startup plan identity/hash with the existing startup-run record or adjacent `signal_startup_plan(run_id,plan_id,assignment_fingerprint,plan_json,created_at_utc)` table. Readiness is a current-state projection keyed by runtime generation and demand key, rebuilt by queries rather than a new replay service.

## 7. Schema metadata and lifecycle

A component schema is immutable and versioned. Use JSON Schema-compatible structural validation plus editor metadata. Domain rules remain authoritative for cross-field dependencies.

Field descriptor: Path, Label, Description, FieldKind, Required, Unit, Minimum?, Maximum?, DecimalPlaces?, EnumChoices?, ReferenceSourceKey?, VisibleWhen?, ReadOnlyWhen?, Group, DisplayOrder. Field kinds: Text, Integer, Decimal, Boolean, Enum, Duration, Timestamp, Reference, Object, Collection. Conditional expressions use a bounded declarative grammar, never executable code.

Unknown compatible fields must survive round-tripping. Unsupported schema versions are read-only. Explicit schema migration creates a new draft with a comparison. Defaults are applied visibly when creating drafts; runtime must not silently insert enabled signal rows missing from the payload.

| Action | Behaviour |
| --- | --- |
| New | Create named set and editable working copy |
| Save Draft | Append immutable version |
| Edit saved version | Create new working copy based on exact version |
| Validate | Pure structural/domain checks; does not activate |
| Publish | Make exact version eligible for assignment |
| Assign | Select exact published version for consumer |
| Retire | Prevent new selection, preserve historical references |
| Compare | Display changed fields and affected dependencies |

Publishing is separate from activation. Running workflows retain captured versions/hashes. Initial Regime Discovery application policy is NextStartup. Display Active version and Assigned for next startup separately. ExplicitReload is reserved for components that implement it.

Scope is component-specific: e.g. strategy deployment/horizon/instrument, or future provider/dataset/environment. Prefer exact assignments. Any effective-time resolver must return exactly one compatible version. Ambiguous/missing selection is a typed configuration error. Avoid layered inheritance in the first implementation.

## 8. Regime Discovery payload and evaluation

Extend the existing typed payload with a new schema version while retaining its existing trend, volatility, structure and calculation sections:

- `SignalRequirements : RegimeSignalRequirementDefinition[]`
- `EvidencePolicy : RegimeEvidencePolicy`

`RegimeSignalRequirementDefinition`: RequirementId UUID, Metric (existing enum), TimeFrame (existing enum), Enabled BOOL, RequirementMode, MaximumAgeSeconds positive integer, CalculationConfigurationId string, SourceBindingKey registered string, StartupPreparation, Monitor BOOL.

Rows are stored once inside versioned JSON. Do not create a separately editable signal table that can diverge from the version hash. A read-only reporting view may expand JSON rows.

`RegimeEvidencePolicy`: enabled calculation sections, per-section supported missing-input policies, MinimumContributingFrames, MinimumEvidenceCoverage and domain-defined mandatory anchors. Exact defaults must be derived and tested against the existing evaluator; this document does not invent thresholds that change its meaning.

Each calculation descriptor identifies mandatory input groups, supported alternatives and permitted omission. Supported missing-input policies are RequireInputs or OmitSection. Alternatives appear only if implemented and tested. Reweight remaining evidence only where explicitly defined by the evaluator. Never substitute zero or reuse an observation beyond its permitted age.

Validation rejects duplicate metric/timeframe/calculation/source rows, unsupported enabled producers, invalid ages, missing mandatory dependencies and enabled evaluation with no meaningful evidence. Disabled future rows can carry an unsupported warning; they contribute no demand.

Required data missing → WaitingForInputs before evaluation. Optional data missing → use supported remaining evidence. Insufficient remaining evidence → InsufficientEvidence, not a fabricated classification. Neither case needs an exception loop. Notify/log state transitions and show current blockers.

## 9. Startup planning and shared producer ownership

1. Resolve assignments for enabled consumers at a captured startup instant.
2. Freeze exact version references and verify hashes/schema compatibility.
3. Translate enabled requirements into demands through domain adapters.
4. Expand output dependencies: feed, contracts, bars, calculations and history.
5. Detect unsupported production, cycles and incompatible requirements.
6. Union compatible demands, preserving consumer ownership.
7. Execute a bounded plan through existing startup/actor mechanisms.
8. Expose readiness while allowing the API to serve requests.

`SignalDemandKey`: resolved MarketSeriesIdentity, output metric, TimeFrame, CalculationConfigurationId, SourceBindingKey. Contract rollover creates a new resolved generation; do not accidentally reuse another contract's observations.

`SignalDemand`: Key, RequiredBy[], OptionalFor[], MaximumAgeByConsumer, StartupPreparation, MonitorConsumers[], ProducerDependencies[]. Preparation uses the strongest supported requested mode; history covers every consumer's needs. Freshness is evaluated per consumer. Required for a consumer is not required for host startup.

`SignalStartupPlan`: PlanId, StartupRunId, RuntimeGeneration, AssignmentFingerprint, resolved version refs, demands, ordered steps, budgets and validation issues.

`SignalStartupStep`: StepId, ProducerKey, DependencyStepIds, Action, DemandKeys, DeadlineUtc, AttemptLimit, resource/cost budget references. Use existing history-acquisition authorization and budgets. Publishing configuration does not authorize unbounded provider charges.

Regime Discovery initial policy is NextStartup. Saving/publishing does not mutate the active producer graph. None preparation is valid for an explicitly supported externally/shared-owned signal. A required signal otherwise needs a supported initialization path.

Equivalent producer work is started once. Removing one consumer releases only its ownership; other consumers keep their producer. Existing non-parameterised consumers must contribute current demands through adapters before the new planner is allowed to narrow shared producer activation.

## 10. Monitoring and readiness

`SignalReadinessSnapshot`: DemandKey, RuntimeGeneration, ProducerState, Readiness, IsWarm, LastMarketDataAtUtc?, ObservedAtUtc, SourceSequence?, ErrorCode?, ConsumerEvaluations[].

`ConsumerSignalEvaluation`: ConsumerRef, ParameterVersionRef, RequirementId, Mode, AcceptedMaximumAgeSeconds, Availability, BlocksEvaluation, Reason.

Display instrument, signal, timeframe, source, age, warmup, ownership count, consumers and configuration versions. Disconnect changes readiness to Unknown, not Ready. Coalesce repeated alerts and log transitions/aggregate counts. Scheduled freshness checks may update monitoring but must not launch recovery workflows.

A startup step exhausts its bounded attempts/deadline and remains visibly Failed until an explicit restart/reapply or legitimate producer update. Missing does not trigger an endless startup retry. Process-local readiness is generation-specific and cannot survive restart as proof of readiness.

## 11. Commands, queries, events and notifications

Use existing actor envelopes and validated subjects. Do not add parallel transport selection. Command metadata supplements existing conventions with OperationId, correlation/causation identifiers, expected revision/state and deadline. Principal/CreatedBy comes from authenticated server context, not trusted UI input.

### 11.1 Queries — ActorType.Query / Core NATS

| Message | Request | Response |
| --- | --- | --- |
| `ListParameterAreasQuery` | Filter, cursor, limit | Page<ParameterAreaSummary> |
| `ListParameterComponentsQuery` | AreaId, support filter, page | Page<ParameterComponentSummary> |
| `GetParameterSchemaQuery` | SchemaRef | Schema and editor descriptors |
| `ListParameterSetsQuery` | ComponentId, name/status filters, page | Page<ParameterSetSummary> |
| `ListParameterVersionsQuery` | SetId, page | Version summary page |
| `GetParameterVersionQuery` | Exact Ref | ParameterSetVersion |
| `GetParameterUsageQuery` | SetId or exact Ref, page | Usage page |
| `CompareParameterVersionsQuery` | Exact references | Field comparison |
| `GetParameterOperationQuery` | OperationId | Stored result or NotFound |
| `GetSignalReadinessQuery` | Consumer/context or demand keys, generation | Timestamped readiness |
| `GetParameterAssignmentQuery` | Consumer/role/scope | Pending assignment and applied runtime reference |

`ValidateParameterCandidateFunction` and `PreviewSignalStartupPlanFunction` are bounded Function requests. They return typed validation/preview results, publish no Events and do not activate configuration.

### 11.2 Commands — ActorType.Command / Core NATS

| Message | Body | Committed result |
| --- | --- | --- |
| `CreateParameterSetCommand` | ComponentId, code/name/description, initial schema/payload | Set and draft refs |
| `SaveParameterDraftCommand` | SetId, ExpectedRevision, BaseVersionRef?, schema/payload, description | New immutable version |
| `RenameParameterSetCommand` | SetId, ExpectedRevision, metadata | Metadata revision |
| `PublishParameterVersionCommand` | Exact Ref, expected Draft | Published ref |
| `RetireParameterVersionCommand` | Exact Ref, expected Published, reason | Retired ref |
| `AssignParameterVersionCommand` | Consumer, role, scope, exact Ref, ExpectedRevision, application policy | Assignment revision |
| `DisableParameterAssignmentCommand` | AssignmentId, ExpectedRevision, reason | Disabled revision |
| `ApplySignalStartupPlanCommand` | PlanId/hash, StartupRunId, expected fingerprint | Plan acceptance; completion through existing startup workflow |

Reject retirement while a version remains assigned for new work; first replace/disable assignments. Preserve historical running references. Plan application rejects stale assignment fingerprints. A preview is not authority to silently choose different versions.

### 11.3 Durable facts — ActorType.Event / JetStream

`ParameterSetCreatedEvent`, `ParameterDraftSavedEvent`, `ParameterVersionPublishedEvent`, `ParameterVersionRetiredEvent`, `ParameterAssignmentChangedEvent` carry EventId, OperationId, aggregate ID/revision, exact affected refs, actor identity and timestamp. Consumers are idempotent by EventId/revision.

Use established transactional event infrastructure. A committed database write plus a best-effort publish is insufficient. Implementation must verify how ConfigurationDb enlists in the existing atomic event append/dispatch path. If it cannot, resolve that integration before enabling event-dependent behaviour. Do not introduce an unbounded outbox polling service as a shortcut. Command receipts and current-state queries remain authoritative for UI results.

### 11.4 Observer messages — ActorType.Notify / Core NATS

`ParameterCatalogChangedNotification`, `ParameterAssignmentAppliedNotification`, `SignalReadinessChangedNotification`: identity, revision/generation, status and timestamp. These are refresh hints; missed notifications are resolved by current-state queries. Do not duplicate durable Events onto a second transport.

### 11.5 Failure reasons

Stable proposed reason codes: `PARAM.NOT_FOUND`, `PARAM.SCHEMA_UNSUPPORTED`, `PARAM.VALIDATION_FAILED`, `PARAM.REVISION_CONFLICT`, `PARAM.HASH_MISMATCH`, `PARAM.OPERATION_IDENTITY_MISMATCH`, `PARAM.VERSION_IMMUTABLE`, `PARAM.LIFECYCLE_INVALID`, `PARAM.VERSION_IN_USE`, `PARAM.ASSIGNMENT_AMBIGUOUS`, `PARAM.COMPONENT_MISMATCH`, `SIGNAL.PRODUCER_UNSUPPORTED`, `SIGNAL.DEPENDENCY_MISSING`, `SIGNAL.PLAN_STALE`, `SIGNAL.STARTUP_BUDGET_EXCEEDED`.

Allocate numeric error codes through the existing registry during implementation. Availability is readiness/outcome data. Malformed configuration and infrastructure failure remain distinct typed errors with correlation IDs.

## 12. Initial 69-signal seed

Appendix A preserves the exact metric/timeframe pairs and issue order from the user's supplied failure. Create a named Daily-horizon draft; never overwrite a published set or silently assign the seed.

- Enabled=true for all rows so the complete original request list is editable.
- Required/Optional defaults use the checked-in Daily profile, not claimed runtime settings.
- 15m and 1h trend metrics required; 5m and 4h trend metrics optional.
- Additional target evidence on 1h required. VIX, TDI, raw Bollinger width, realized-volatility percentile and prior composite optional.
- Daily VX ratio required; maximum age 345600 seconds (96h).
- Ages: 5m=900, 15m=2700, 1h=10800, 4h=43200 seconds.
- Calculation ID `<Metric>.v1`, as in the current factory; verify supported producer resolution before publication.
- Proposed StartupPreparation=InitializeAndWarm and Monitor=true. Unsupported production/warmup paths are flagged, not assumed implemented.
- Source binding: ES series for contract metrics, VX contract pair for ratio, separate spot-VIX source for VIX. Never relabel front VX as spot VIX.
- Generate RequirementId once at seed creation and persist it.

Existing deployment migration expands its exact frozen legacy payload, not these generic defaults. All 69 requested pairs are not evidence that all 69 production paths currently work.

## 13. Migration and compatibility

1. Inventory sets, hashes, schemas and existing deployment bindings.
2. Add generic catalog/editor/adapters without changing runtime resolution.
3. Expose legacy Regime versions read-only through exact-reference adapters.
4. Create new-schema drafts with explicit requirements and compare against old expansion.
5. Preserve old ID/version/hash references through mappings; account for cross-table UUID collisions.
6. Publish validated new versions and explicitly assign for next startup.
7. Maintain one authoritative store per component; avoid indefinite dual writes.
8. Switch new-schema request generation and startup contribution together.

Legacy hashes/bytes remain unchanged. Schema migrations append versions. Retain old resolvers while historical references need them. Rollback selects a compatible prior assignment and restarts. The existing Strategy Catalog parameter schema/set concepts must be integrated through adapters, never duplicated as a competing editable authority.

## 14. Validation and acceptance gates

### UI and serialization

Selection cancellation, empty/multiple sets, published read-only, unsupported schemas, dirty navigation, conflict preservation, accessibility and grid bulk changes. Verify stable typed round-trip/hash across culture/timezone; preserve compatible unknown fields and enforce payload limits.

### Persistence

Concurrent draft version allocation; stale-revision rejection; duplicate/mismatched OperationId behaviour; database immutability; valid lifecycle transitions; atomic audit/receipts; no orphan rows after failure; legacy hash resolution.

### Regime Discovery

Exactly 69 unique pairs; checked-in Daily default equivalence; disabled rows absent from requests and demands; optional absence handled only by supported model paths; incompatible reductions fail validation; required absence gives WaitingForInputs; insufficient remaining evidence gives InsufficientEvidence.

### Startup and monitoring

Shared producers start once and survive another consumer disabling them. Per-consumer freshness/requiredness is retained. Rollover/restart changes generation. Drafts and unassigned sets contribute no work. Budgets/deadlines terminate attempts. Missing notifications are repaired by queries. API remains available while Regime waits.

### Delivery phases

A. Generic contracts, storage/adapters and exact-version queries.  
B. Reference UI and Regime editor with the 69-row seed.  
C. Dependency-aware evaluation and configurable request generation.  
D. Startup assignment/contribution, supported producer preparation and monitoring.  
E. Migration, operational acceptance and a component-onboarding guide.

The grid alone is not completion. Phase C is required before reducing inputs safely changes evaluation; phase D is required before the list controls startup.

## 15. Integration decisions to verify before implementation

Verify exact evaluator alternatives/minimum-evidence rules, available intraday warmup paths, spot-VIX/VX source capability, ConfigurationDb atomic event integration, authoritative deployment selection and existing permissions. These are concrete implementation checks, not permission to invent fallback evidence or unsupported production capability.

## Appendix A. Exact initial metric/timeframe list

Pairs and order come from the supplied error. Requirement and age are proposed seed defaults from the checked-in Daily profile, not recovered runtime settings. Enabled=true for every row.

| Row | Metric | Timeframe | Requirement | Maximum age (seconds) |
| ---: | --- | --- | --- | ---: |
| 1 | VixLevel | Daily | Optional | 345600 |
| 2 | VxFrontSecondRatio | Daily | Required | 345600 |
| 3 | Ema20 | FiveMinutes | Optional | 900 |
| 4 | Ema50 | FiveMinutes | Optional | 900 |
| 5 | Ema200 | FiveMinutes | Optional | 900 |
| 6 | Ema20Slope | FiveMinutes | Optional | 900 |
| 7 | Ema50Slope | FiveMinutes | Optional | 900 |
| 8 | Ema200Slope | FiveMinutes | Optional | 900 |
| 9 | Rsi14 | FiveMinutes | Optional | 900 |
| 10 | Rsi14Slope | FiveMinutes | Optional | 900 |
| 11 | Adx14 | FiveMinutes | Optional | 900 |
| 12 | PlusDi14 | FiveMinutes | Optional | 900 |
| 13 | MinusDi14 | FiveMinutes | Optional | 900 |
| 14 | MacdHistogram | FiveMinutes | Optional | 900 |
| 15 | Atr14 | FiveMinutes | Optional | 900 |
| 16 | Tdi | FiveMinutes | Optional | 900 |
| 17 | Ema20 | FifteenMinutes | Required | 2700 |
| 18 | Ema50 | FifteenMinutes | Required | 2700 |
| 19 | Ema200 | FifteenMinutes | Required | 2700 |
| 20 | Ema20Slope | FifteenMinutes | Required | 2700 |
| 21 | Ema50Slope | FifteenMinutes | Required | 2700 |
| 22 | Ema200Slope | FifteenMinutes | Required | 2700 |
| 23 | Rsi14 | FifteenMinutes | Required | 2700 |
| 24 | Rsi14Slope | FifteenMinutes | Required | 2700 |
| 25 | Adx14 | FifteenMinutes | Required | 2700 |
| 26 | PlusDi14 | FifteenMinutes | Required | 2700 |
| 27 | MinusDi14 | FifteenMinutes | Required | 2700 |
| 28 | MacdHistogram | FifteenMinutes | Required | 2700 |
| 29 | Atr14 | FifteenMinutes | Required | 2700 |
| 30 | Tdi | FifteenMinutes | Optional | 2700 |
| 31 | Ema20 | OneHour | Required | 10800 |
| 32 | Ema50 | OneHour | Required | 10800 |
| 33 | Ema200 | OneHour | Required | 10800 |
| 34 | Ema20Slope | OneHour | Required | 10800 |
| 35 | Ema50Slope | OneHour | Required | 10800 |
| 36 | Ema200Slope | OneHour | Required | 10800 |
| 37 | Rsi14 | OneHour | Required | 10800 |
| 38 | Rsi14Slope | OneHour | Required | 10800 |
| 39 | Adx14 | OneHour | Required | 10800 |
| 40 | PlusDi14 | OneHour | Required | 10800 |
| 41 | MinusDi14 | OneHour | Required | 10800 |
| 42 | MacdHistogram | OneHour | Required | 10800 |
| 43 | Atr14 | OneHour | Required | 10800 |
| 44 | AtrBaselineRatio | OneHour | Required | 10800 |
| 45 | RealizedVolatilityPercentile | OneHour | Optional | 10800 |
| 46 | PriorVolatilityComposite | OneHour | Optional | 10800 |
| 47 | BollingerWidth | OneHour | Optional | 10800 |
| 48 | BollingerWidthRatio | OneHour | Required | 10800 |
| 49 | BollingerPosition | OneHour | Required | 10800 |
| 50 | Ema20Interaction | OneHour | Required | 10800 |
| 51 | AtrNormalizedRange | OneHour | Required | 10800 |
| 52 | RollingHigh20 | OneHour | Required | 10800 |
| 53 | RollingLow20 | OneHour | Required | 10800 |
| 54 | BreakoutDistanceAtr | OneHour | Required | 10800 |
| 55 | Tdi | OneHour | Optional | 10800 |
| 56 | Ema20 | FourHours | Optional | 43200 |
| 57 | Ema50 | FourHours | Optional | 43200 |
| 58 | Ema200 | FourHours | Optional | 43200 |
| 59 | Ema20Slope | FourHours | Optional | 43200 |
| 60 | Ema50Slope | FourHours | Optional | 43200 |
| 61 | Ema200Slope | FourHours | Optional | 43200 |
| 62 | Rsi14 | FourHours | Optional | 43200 |
| 63 | Rsi14Slope | FourHours | Optional | 43200 |
| 64 | Adx14 | FourHours | Optional | 43200 |
| 65 | PlusDi14 | FourHours | Optional | 43200 |
| 66 | MinusDi14 | FourHours | Optional | 43200 |
| 67 | MacdHistogram | FourHours | Optional | 43200 |
| 68 | Atr14 | FourHours | Optional | 43200 |
| 69 | Tdi | FourHours | Optional | 43200 |

## Compatible schema evolution amendment: Regime schema 4

Regime schema 4 is the current authoring schema. It preserves schema 3 membership semantics and corrects JSON-schema nullability from C# nullable-reference metadata. Versions 1-3 remain immutable and readable with their original schema JSON and hashes. Registry initialization inserts version 4 as a new row and never updates a registered definition.

Opening an older supported version for editing creates a schema-4 working copy. Saving appends a new immutable parameter-set version while the source remains available for comparison and rollback. Publishing and assigning the migrated version are separate explicit operations.