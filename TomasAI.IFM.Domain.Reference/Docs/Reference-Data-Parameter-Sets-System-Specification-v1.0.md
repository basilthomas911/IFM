# Reference Data Parameter Sets: System Specification

Version: 1.0  
Date: 2026-09-10  
Status: Proposed implementation specification; no implementation implied  
Source: [System Design v1.0](Reference-Data-Parameter-Sets-System-Design-v1.0.md)  
First supported component: Regime Discovery

## 1. Authority, terminology and scope

MUST/MUST NOT are release requirements. SHOULD describes a recommendation requiring a recorded reason for deviation. MAY describes optional capability. This specification refines the source design. Requirements explicitly labelled Integration Gate cannot be represented as implemented until supporting repository evidence is recorded. Do not invent domain fallback rules to close a gate.

Application Area is a grouping such as Strategy Workflow. Component is a configurable consumer implementation such as Regime Discovery. A Parameter Set is a stable named identity. A Version is immutable saved content. An Assignment chooses an exact version for a consumer role. A Demand is an enabled signal requirement expanded to a resolved runtime instrument. Readiness describes present runtime evidence; it is not configuration validity.

PS-SCOPE-001: Implement the generic Reference Data shell, shared parameter-set contracts/lifecycle, Regime Discovery editor, explicit signal requirements, assignment integration, startup contribution and monitoring.

PS-SCOPE-002: Other components MAY be listed read-only but MUST NOT expose nonfunctional editing or imply supported execution. Registering another component MUST NOT require adding a strategy-specific branch to the common navigation or persistence model.

PS-SCOPE-003: No runtime setting, database record or source code is modified by this specification document. Implementing it is a separate task.

PS-SCOPE-004: No new unbounded recovery loop, fabricated market value, automatic publication, implicit deployment reassignment or unbounded historical acquisition is permitted.

## 2. System invariants

| ID | Required invariant |
| --- | --- |
| PS-INV-001 | One authoritative payload for each exact set/version; signals are part of that payload |
| PS-INV-002 | Saved payloads are immutable, including drafts |
| PS-INV-003 | Publication and assignment are distinct operations |
| PS-INV-004 | Running work holds exact version/hash references |
| PS-INV-005 | Each consumer role has one assignment authority |
| PS-INV-006 | Disabled signal rows generate neither requested evidence nor startup ownership |
| PS-INV-007 | Another consumer can retain a shared producer independently |
| PS-INV-008 | Missing optional evidence cannot be silently converted into a numeric value |
| PS-INV-009 | Missing inputs block only affected consumer evaluation, not API host availability |
| PS-INV-010 | Duplicate commands with the same identity/input have one committed effect |
| PS-INV-011 | Unknown schema, producer or runtime generation cannot be reported as supported/ready |
| PS-INV-012 | Each mutation is authorized and validated server-side |

## 3. Reference Data user interface

### 3.1 Navigation

PS-UI-001: Add Parameter Sets to Reference Data. Use three regions: Application Areas, Components, Detail. Set/version selectors belong in Detail. Initial area code is `strategy-workflow`; initial editable component code is `strategy-workflow.regime-discovery`.

PS-UI-002: Area selection filters components. Component selection clears obsolete set/version detail before loading. A current consumer context MAY select its assigned set; otherwise multiple sets require explicit selection. A zero-result screen offers New Set.

PS-UI-003: Preserve selection by stable IDs, not displayed names or row indices. Cancel previous requests on navigation and discard any response whose selection generation is no longer current.

### 3.2 Detail and action states

Required tabs: Overview, Parameters, Market Signals, Applicability, Usage & Startup, Versions, Validation, Audit.

Overview displays name, description, component, schema, version, lifecycle, creator and timestamps. Usage & Startup shows exact active and next-startup versions separately. Versions supports exact comparison. Audit presents chronological identity/action/reference changes without making user interpretation depend on raw JSON.

| State | Enabled actions | Exit behaviour |
| --- | --- | --- |
| Empty | Refresh, New Set when component supported | No stale editor |
| Loading | Cancel/navigation | Results checked against selection generation |
| Viewing draft | Edit as New Draft, Validate, Publish, Compare, Usage | Original draft remains immutable |
| Viewing published | Edit as New Draft, Assign, Retire, Compare, Usage | No in-place save |
| Viewing retired | Inspect, Compare, Usage, Copy to Draft if schema supported | Cannot assign for new work |
| Editing | Save Draft, Validate, Discard | Dirty navigation offers Save/Discard/Cancel |
| Validating | Cancel; preserve working copy | Report tied to candidate hash |
| Saving | No competing mutation | Preserve edits on failure |
| Conflict | Compare/reload/rebase as new working copy | Never overwrite winner silently |
| Unsupported schema | Inspect/export/usage | No destructive round-trip |

PS-UI-004: Saving creates a new draft version. A successful save selects that exact version and marks the working copy clean. A timed-out response stays unresolved until operation-status lookup confirms the outcome or the user chooses an explicit bounded retry using the same identity.

PS-UI-005: Publication MUST show the exact set/version and explain that it does not activate the set. Initial assignment action reads Assign for Next Startup. After assignment, display Restart required while preserving the active version indicator.

PS-UI-006: Retirement MUST list current usage. Retire cannot proceed while enabled assignments still select the version for new work. Existing historical running references remain valid.

PS-UI-007: Reuse existing Reference styles, keyboard navigation and accessible labels. Every numeric field has units; errors identify paths/rows. Colour is supplemental. Normal users need not edit JSON.

### 3.3 Signal grid

Required columns: Enabled, Metric, Timeframe, Requirement, Maximum Age, Calculation, Source Binding, Prepare at Startup, Monitor, Validation. Maximum age is edited as a duration and persisted in seconds. Requiredness is independent of Enabled; disabled rows retain their edited values but generate no demand.

PS-UI-008: Support row filtering, search, bulk enable/disable and required/optional changes. Display total/enabled/required/optional counts, plus separate runtime readiness counts. Bulk changes MUST run the same validation as individual edits.

PS-UI-009: Selecting a signal shows consuming calculation sections and producer dependencies. Changes MUST NOT silently alter calculation sections or weights. Offer an explicit proposed adjustment or report the incompatible dependency.

PS-UI-010: Current-input checks require a concrete consumer/instrument. Show check timestamp, runtime generation and parameter reference. These checks do not modify configuration or gate publication on temporary market availability.

## 4. Catalog, schemas and editor providers

PS-CAT-001: Stable area/component codes are unique and independent of display names. Unknown component descriptors are read-only with an unsupported diagnostic.

PS-CAT-002: Component registration supplies schema versions, editor descriptor/provider, validator, codec, scope rules, runtime application policy and optional signal-demand contributor. Runtime adapters remain domain-owned.

PS-CAT-003: Schema/editor metadata MUST NOT execute code, load arbitrary assemblies or accept unrestricted expressions. Conditional visibility uses a bounded declarative grammar. Reference fields resolve through registered reference-query sources.

PS-CAT-004: Structural schema validation precedes typed/domain validation. A schema version identifies immutable schema and canonicalization rules. Unsupported versions remain inspectable. Migration creates a new version and preserves the old one.

PS-CAT-005: Unknown compatible fields must be preserved. Reject duplicate JSON object keys, nonfinite numbers, malformed UUIDs/dates and unrecognised required enum values. Canonicalization must not erase array ordering or meaningful precision.

## 5. Payload and public contracts

The model/enum dictionary in Appendix B and table dictionary in Appendix C are normative, subject to explicit Integration Gates. New transport contracts use existing actor envelopes and serialization conventions. Append new MessagePack keys; never renumber existing enums/fields.

PS-CON-001: All external mutation identities are nonempty UUIDs. Positive versions/revisions reject zero. A ParameterVersionRef contains SetId, Version, ComponentId and hash; callers cannot substitute a name for an exact reference.

PS-CON-002: Collection/query limits: 512 signal rows, 1 MiB UTF-8 payload, 100 items per query page, 128-character codes, 200-character names and 4,000-character descriptions. Nesting depth maximum is 32. Reject oversize requests before deserialization allocates unbounded structures.

PS-CON-003: Codes use lowercase ASCII letters, digits, dots and hyphens; begin with a letter. Payload keys retain declared case. Set code uniqueness is component-local; component codes are system-wide unique. Display names need not be unique.

PS-CON-004: Date/time fields use explicit UTC instants. Durations are positive integer seconds unless a contract explicitly permits zero. UI localization must not change serialized decimals, enum names or dates.

PS-CON-005: Canonical hashing uses a registered codec per schema: sorted object property names, invariant scalar formatting, preserved array order, and no lifecycle metadata. Store codec version with the schema. A legacy reference retains its original hash/codec semantics.

## 6. Regime Discovery configuration and validation

### 6.1 New payload fields

Add SignalRequirements and EvidencePolicy using a new supported schema version; preserve existing parameter sections. Each signal row contains:

| Field | Type | Validation |
| --- | --- | --- |
| RequirementId | UUID | Nonempty; unique in payload; stable across edited copies |
| Metric | Existing RegimeDiscoverySignalMetric | Defined enum member |
| TimeFrame | Existing TimeFrameType | Supported by selected producer when enabled |
| Enabled | bool | Explicit, never inferred from weight |
| RequirementMode | SignalRequirementMode | Required or Optional, not Unknown |
| MaximumAgeSeconds | int | Positive; component upper bound if applicable |
| CalculationConfigurationId | string | Registered versioned calculation |
| SourceBindingKey | string | Registered role resolving compatible source/instrument |
| StartupPreparation | SignalStartupPreparation | Supported mode or validation issue |
| Monitor | bool | Controls presentation/alerts; cannot bypass required-input gating |

PS-RD-001: Store rows only in the immutable payload. No separately editable signal table. A reporting view may project JSON.

PS-RD-002: New-schema snapshot requests MUST be generated only from enabled configured rows. No hidden insertion of former hardcoded rows. Migration of old schemas may use the legacy factory under an explicit legacy adapter.

PS-RD-003: Duplicate active output definitions or contradictory definitions for the same metric/timeframe must fail validation. Disabled historical rows may remain but row identities must remain unique.

### 6.2 Validation stages

1. Envelope, size, schema and primitive checks.
2. Unique identities/rows and supported metric/timeframe/calculation/source checks.
3. Enabled calculation dependency checks and scope consistency.
4. Evidence-policy validity and mandatory anchors.
5. Startup feasibility/capability validation without querying temporary live availability.
6. Exact candidate hash plus typed issue report.

Errors block publication. Warnings do not, but must remain visible. Drafts MAY preserve semantically incomplete content for editing if structurally valid; publication always reruns all stages. This distinction allows the 69-row draft to exist before all producers are supported.

### 6.3 Reduced-evidence rules

PS-RD-004: A required missing/invalid/stale observation yields WaitingForInputs for that consumer. Optional absence invokes a supported omission/alternative; it never becomes zero.

PS-RD-005: If enabled sections cannot produce a meaningful classification, return InsufficientEvidence as a typed non-success evaluation outcome. Do not emit a normal regime decision carrying invented values.

PS-RD-006: MinimumContributingFrames, MinimumEvidenceCoverage, section omission and alternative-input rules require a tested domain definition. Disabled dependencies either require an explicit section change or fail validation. Do not dynamically reweight unless that calculation explicitly defines the behaviour.

PS-RD-007: These outcomes must not spawn recursive retries. A subsequent ordinary signal trigger may evaluate current readiness. Use state-change notifications and aggregate counts rather than one exception stack per missing row per tick.

Integration Gate RD-G1: Before implementing configurable reductions, map every currently enabled calculation to mandatory groups, optional inputs and valid alternatives. Tests must establish at least one meaningful reduced configuration. No generic guessed threshold can satisfy this gate.

## 7. Persistence and transactional operations

### 7.1 Database constraints

PS-DB-001: Implement the Appendix C tables with explicit mandatory NOT NULL fields, FK relationships, positive version checks, status checks and lifecycle timestamp checks. Schema/set-version payload fields are immutable through application and database protections.

PS-DB-002: Composite relationships enforce that a version belongs to its set/component and supported schema; assignments reference a compatible component/version. Stored reference hashes are validated against target payloads in the mutation transaction.

PS-DB-003: Do not hard-delete saved versions, operation receipts or audit history in normal UI flows. Retention/archival is separately specified and cannot break historical exact-reference resolution or duplicate-command safety.

### 7.2 Mutation algorithms

Create Set: authorize; validate identity/code/schema and structurally valid draft; begin transaction; check operation receipt; insert set; allocate version 1; insert payload/audit/result; commit.

Save Draft: authorize; canonicalize; begin transaction; check operation identity/hash; lock set; compare ExpectedRevision; allocate next_version; insert immutable draft; advance revision; insert audit/result; commit. Never update the base payload.

Publish: authorize; load exact draft; validate all domain stages; transactionally recheck hash/state; set Published and server publication time; append audit/result; commit. A candidate validation report is advisory until server revalidation.

Assign: authorize the consumer role; resolve authoritative binding adapter; validate exact Published version/component/hash and scope; lock assignment/authority; compare ExpectedRevision; append new assignment revision; audit/result; commit. Return exact next-startup assignment.

Retire: authorize; lock lifecycle/assignment authority in consistent order; ensure no enabled new-work assignments; check Published; change state/timestamp; audit/result; commit.

Disable Assignment: authorize; compare expected revision; append disabled revision and receipt. Runtime application follows NextStartup for Regime Discovery.

PS-DB-004: Serialize assignment-versus-retirement eligibility on the same version lock/transaction boundary, preventing a version from being assigned concurrently with retirement.

PS-DB-005: Same OperationId/same canonical request hash returns stored outcome without repeating side effects. Same ID/different hash returns OperationIdentityMismatch. ExpectedRevision failures expose current revision without overwriting.

### 7.3 Lifecycle matrix

| Operation | Draft | Published | Retired |
| --- | --- | --- | --- |
| Read/compare | Allowed | Allowed | Allowed |
| Edit original payload | Forbidden | Forbidden | Forbidden |
| Copy to new draft | Supported schema only | Supported schema only | Supported schema only |
| Publish | Allowed after validation | Same original operation replay only | Forbidden |
| Assign for new work | Forbidden | Allowed after compatibility checks | Forbidden |
| Retire | Forbidden | Allowed when not assigned for new work | Same original operation replay only |

Publication timestamps are server facts. Effective assignment times cannot make a draft eligible. Historical running consumers retain exact references after retirement.

## 8. Application assignment and runtime resolution

PS-ASG-001: Select configurations through exact assignments. Strategy deployments retain their current authoritative pipeline binding during adaptation; the generic UI MUST use that adapter instead of creating a competing generic binding.

PS-ASG-002: Assignment identity is ConsumerKindCode + ConsumerId + Role + canonical ScopeHash. Scope is typed and validated by the component. No arbitrary JSON predicate, wildcard precedence or implicit inheritance is introduced in this release.

PS-ASG-003: Existing effective-time resolution may be adapted, but ambiguous/missing matches must return typed failures. Resolution captures a timestamp and exact version/hash. No runtime lookup of mutable latest is permitted inside an already-started operation.

PS-ASG-004: Regime assignments apply at NextStartup. Publication does not apply them. Running versus pending references are queryable separately. Startup captures a fingerprint; changes committed afterward apply on the following startup.

PS-ASG-005: Cancellation of a pending assignment appends a new revision selecting the prior eligible assignment or disabling it; it does not delete history. If the prior version is retired, cancellation must offer a valid published replacement rather than silently reactivate it.

## 9. Startup and dependency planning

### 9.1 Plan construction

PS-ST-001: Snapshot enabled assignments and existing consumer contributions at startup. Verify exact hashes/schema support. Unassigned sets and drafts MUST NOT contribute demands.

PS-ST-002: Expand enabled outputs to producer, feed, contracts, bars, calculation and historical-data dependencies through registered descriptors. Detect cycles and unsupported paths before execution. Source binding resolves ES observations separately from VX pairs and spot VIX.

PS-ST-003: Deduplicate only compatible demands. Different sources or calculation versions cannot be merged merely because metric/timeframe match. Retain per-consumer requiredness, freshness and ownership.

PS-ST-004: Determine warmup from the producer's minimum input history, including slope/baseline prerequisites, not just the visible indicator period. Warmup must remain within existing cost/byte/time limits and acquisition authorization. Unsupported warmup is an explicit validation/plan failure.

PS-ST-005: Producer ownership is the union across consumers. Disabling Regime's EMA requirement cannot stop an EMA producer owned by another feature. Existing non-parameterised consumers need adapters before the planner can narrow shared startup activity.

### 9.2 Runtime types

| Type | Mandatory fields |
| --- | --- |
| SignalDemandKey | Resolved MarketSeriesIdentity, output identity, TimeFrame, CalculationConfigurationId, SourceBindingKey |
| SignalDemand | Key, RequiredBy[], OptionalFor[], MaximumAgeByConsumer, Preparation, MonitorConsumers[], ProducerDependencies[] |
| SignalStartupPlan | PlanId, StartupRunId, RuntimeGeneration, AssignmentFingerprint, ResolvedVersionRefs[], Demands[], Steps[], Budgets, Issues[] |
| SignalStartupStep | StepId, ProducerKey, DependencyStepIds[], Action, DemandKeys[], DeadlineUtc, AttemptLimit, ResourceBudgetRef |
| SignalReadinessSnapshot | DemandKey, RuntimeGeneration, ProducerState, Readiness, IsWarm, LastMarketDataAtUtc?, ObservedAtUtc, SourceSequence?, ErrorCode?, ConsumerEvaluations[] |
| ConsumerSignalEvaluation | ConsumerRef, VersionRef, RequirementId, Mode, MaximumAgeSeconds, Availability, BlocksEvaluation, Reason |

Plan ordering is deterministic for a fixed assignment snapshot and descriptor registry. Persist the plan/hash against startup run identity; avoid persisting price ticks in parameter tables.

### 9.3 Execution and bounded failure

PS-ST-006: Execute dependency-ordered steps through existing startup/actor APIs. Every step has a deadline and attempt limit. Initial new orchestration defaults are one attempt per step; established producer-internal retry policy must also be bounded by the step deadline. The new layer must not multiply retries across levels.

PS-ST-007: Failed dependency steps block their dependents, not unrelated producers. Deadline/cost exhaustion yields Failed with a reason and exhausted budget. The API host remains available; consumer readiness explains missing dependencies.

PS-ST-008: Monitoring MUST NOT start another recovery workflow. Explicit restart/reapply or legitimate producer state changes can change Failed/Warming status. Repeated missing observations alone cannot restart exhausted work.

PS-ST-009: Contract rollover and process restart create a new generation. A cache observation must match source/series/calculation identity; prior-generation readiness cannot prove availability.

Integration Gate ST-G1: Verify production capabilities for each enabled seed input, especially intraday initialization, spot VIX and front/second VX. A registered enum value is not evidence of an implemented producer.

## 10. Monitoring and data quality

PS-MON-001: Required inputs are always checked for consumer readiness even if Monitor=false. Monitor controls presentation/alerts, not evaluation correctness. Disabled rows display Disabled when viewed and produce no startup ownership.

PS-MON-002: Track latest observation timestamp/sequence, producer initialization, warmness, validity, freshness and supported schema/calculation version. Retain precise availability reasons rather than flattening all failures to Missing.

PS-MON-003: Evaluate age per consumer against its configured maximum. Shared inputs may be Available for one consumer and Stale for another. Future timestamps follow the existing configured skew tolerance.

PS-MON-004: Emit changes with runtime generation and monotonic projection revision. Clients reject older generations/revisions. On disconnect, show Unknown/Disconnected until a current-state query succeeds.

PS-MON-005: Suppress identical repeated alerts. Display blocking requirements separately from optional unavailable evidence. Monitoring lists exact parameter versions, owner count and preparation status.

## 11. Messaging and service contracts

The complete message catalog is included in Appendix D. Commands/Queries/Functions follow existing Core NATS conventions; Event facts use JetStream; observer hints use Notify. Do not introduce a Durable flag or dual publication.

PS-MSG-001: Every mutation uses a validated subject, operation identity, correlation/causation context, deadline and expected revision/state as applicable. Authenticated identity is server-derived. Do not trust CreatedBy or permission claims in the payload.

PS-MSG-002: Queries return typed envelopes, page cursor and bounded results. Invalid/mismatched cursors fail clearly. A cursor contains filters/sort context sufficient to reject reuse for a different query.

PS-MSG-003: ValidateParameterCandidateFunction and PreviewSignalStartupPlanFunction are pure bounded operations. They return CandidateHash/AssignmentFingerprint and typed issues. They do not publish durable Events or activate configuration.

PS-MSG-004: ApplySignalStartupPlanCommand must recheck the plan's assignment fingerprint and producer registry compatibility. Stale preview returns SIGNAL.PLAN_STALE, not an implicitly substituted plan.

PS-MSG-005: Command response timeout means outcome unknown, not failure proof. Query operation receipt. Reusing the original OperationId is allowed for the identical request only. No automatic endless status polling.

PS-MSG-006: Durable facts must be recorded through the established atomic transaction/event infrastructure. A database commit followed by best-effort publish is not an accepted guarantee. Notify is only a hint; current queries and operation receipts are authoritative.

Integration Gate MSG-G1: Establish ConfigurationDb enlistment with the existing event append/dispatch infrastructure before enabling event-dependent consumers. Do not add a new unbounded outbox service to work around uncertainty.

### 11.1 Message routing and aggregate ownership

Parameter-set mutations route by SetId (creation includes intended SetId). Assignment changes route by authoritative consumer/assignment identity. Schema/catalog queries route through the Reference query API. Handler dispatch and validators use existing actor parse/receive-map conventions.

A common command result contains Outcome, OperationId, committed reference/revision when successful, and typed Issues when unsuccessful. Replayed results return the same committed identity. Notifications carry entity revision/runtime generation so consumers can discard stale refreshes.

### 11.2 Stable reason codes and HTTP adapters

Preserve Appendix D reason codes end-to-end. Numeric codes are allocated using the repository registry; this specification assigns no arbitrary numeric range. Any HTTP adapter maps validation to 400/422 according to existing API convention, forbidden to 403, not found to 404 and revision/lifecycle conflict to 409, while retaining typed reasons. NATS callers receive the same semantic envelope without relying on HTTP status.

## 12. Security, auditing and operational limits

PS-SEC-001: Map existing permissions to read, draft-author, publish, assign and retire capabilities. Do not create guessed production role names. UI hiding does not replace server checks.

PS-SEC-002: Secrets, credentials and provider tokens are not parameter payload values. Use registered secret references only where supported; redact sensitive resolved values from comparison, audit and logs.

PS-SEC-003: Audit successful lifecycle/assignment mutations transactionally with actor identity, operation, timestamp and exact before/after references. Authorization failures are recorded through existing security logging without storing full rejected payloads.

PS-SEC-004: Scope/catalog reference validation prevents a user from referencing inaccessible consumers. Payload/export permissions must follow existing application policy.

PS-OPS-001: Query lists use pagination. UI fetches selected details on demand rather than loading all versions/payloads at startup. Functions have cancellation/deadline support and cannot acquire historical data as a side effect of preview.

PS-OPS-002: Log operation/plan identities, counts, durations and typed reasons. Avoid full parameter payloads in routine logs. Metrics should use bounded component/status labels, not a label per UUID or contract.

## 13. Seeding, migration and rollback

### 13.1 Seed

PS-SEED-001: Appendix A is the exact initial 69-pair list from the supplied failure. All rows enabled. Required/age settings are proposed checked-in Daily defaults: 35 required and 34 optional, not recovered settings from that runtime.

PS-SEED-002: Seed creates a named draft, never overwrites published records or activates an assignment. Code is unique within Regime component, e.g. `regime-discovery-daily-signals-v1`. Use a fixed seed operation identity in migration metadata so repeated execution does not create duplicates.

PS-SEED-003: Each row receives a persistent RequirementId on first creation. Replaying seed creation returns the same IDs. Editing preserves IDs. New copied parameter sets may generate new row IDs; version copies within a set preserve them.

PS-SEED-004: Calculation IDs default to `<Metric>.v1`. Source bindings distinguish ES, VX pair and spot VIX. Proposed preparation is InitializeAndWarm and Monitor=true; unsupported routes remain explicit draft issues. Semantically unsupported enabled rows must be disabled or supported before publication.

### 13.2 Migration

PS-MIG-001: Inventory legacy set/version/hash/schema and all references before migration. Expose legacy versions read-only through adapters. Migrate exact frozen payload expansion rather than substituting seed defaults.

PS-MIG-002: Preserve old serialized payload/hash interpretation. Cross-table UUID collisions are disambiguated by legacy kind in the mapping. New-schema drafts receive new explicit version references.

PS-MIG-003: One store and assignment authority per migrated component. Cutover must switch compatible request-generation and startup contribution together. Unmigrated consumers retain adapters and existing runtime behaviour.

PS-MIG-004: Rollback uses a compatible prior assignment and restart. Do not rewrite published history. Keep historical resolvers until retained workflow references remain readable.

### 13.3 Release sequence

1. Generic contracts, schema and adapters; read-only exact queries.
2. Reference editor and idempotent draft seed.
3. Dependency-aware evaluator and explicit request generation.
4. Assignment/startup contribution and producer readiness.
5. Migration verification, operational acceptance and extension guide.

The UI alone is not feature completion. Reduction of inputs cannot ship as working until valid reduced-evidence evaluation is tested. Startup control cannot ship as working until demands reach verified producers.

## 14. Acceptance specification

| Test ID | Given / When | Required outcome | Requirements |
| --- | --- | --- | --- |
| AT-001 | Two rapid component selections; first query returns last | Only final selection displayed | PS-UI-002/003 |
| AT-002 | Published version opened | Read-only; edit creates working copy | PS-UI-004, PS-INV-002 |
| AT-003 | Save times out after commit | Receipt lookup selects original version; no duplicate | PS-DB-005, PS-MSG-005 |
| AT-004 | Two edits share ExpectedRevision | One succeeds; other conflict preserves input | PS-DB-005 |
| AT-005 | Same OperationId with altered payload | OperationIdentityMismatch; no mutation | PS-DB-005 |
| AT-006 | Publish while live data unavailable but configuration valid | Publication succeeds; runtime readiness unchanged | PS-UI-010 |
| AT-007 | Publish unsupported enabled producer | Validation failure identifies row and capability | PS-RD-003, ST-G1 |
| AT-008 | Assign draft or retired version | Reject; assignment unchanged | PS-ASG-001, lifecycle matrix |
| AT-009 | Assignment races retirement | No enabled assignment references a newly retired target | PS-DB-004 |
| AT-010 | Publish new version while application running | Active version unchanged | PS-ASG-004 |
| AT-011 | Initial seed generated twice | One seed draft; same 69 pairs/IDs | PS-SEED-001/002/003 |
| AT-012 | Disable optional row and publish valid config | No request/demand from that row | PS-INV-006, PS-RD-002 |
| AT-013 | Disable input required by active calculation | Validation error or explicit supported section change | PS-RD-006 |
| AT-014 | Required observation missing | WaitingForInputs; API available; no exception storm | PS-RD-004/007 |
| AT-015 | Optional observation missing but sufficient evidence remains | Valid reduced-evidence result with omissions reported | PS-RD-005/006 |
| AT-016 | Too little optional evidence remains | InsufficientEvidence, no normal regime decision | PS-RD-005 |
| AT-017 | Two consumers share EMA, one disables it | Remaining owner retains producer | PS-ST-003/005 |
| AT-018 | Same metric but different calculation/source | Separate demands; no incorrect merge | PS-ST-003 |
| AT-019 | Warmup exceeds limit | Bounded Failed; no recursive retry | PS-ST-004/006/008 |
| AT-020 | Different maximum ages on shared signal | Per-consumer readiness differs correctly | PS-MON-003 |
| AT-021 | Restart or rollover | Old generation cannot prove Ready | PS-ST-009 |
| AT-022 | Notify missed | Current query restores accurate UI | PS-MSG-006 |
| AT-023 | Unknown schema opened | Inspect only; no lossy save | PS-CAT-004/005 |
| AT-024 | Legacy workflow resolves old reference after migration | Same payload/hash semantics | PS-MIG-002/004 |
| AT-025 | Unsupported other component selected | Read-only unsupported state | PS-SCOPE-002 |
| AT-026 | User lacks assignment permission but sends direct command | Server denies; no state change | PS-SEC-001 |
| AT-027 | Payload exceeds limit or duplicate JSON keys | Reject before unbounded processing | PS-CON-002, PS-CAT-005 |
| AT-028 | Preview becomes stale before apply | SIGNAL.PLAN_STALE; no replacement plan silently applied | PS-MSG-004 |
| AT-029 | Monitor=false on required signal | Evaluation still checks readiness | PS-MON-001 |
| AT-030 | Equivalent typed payload serialized under different locale | Same canonical hash for same schema | PS-CON-005 |

Unit tests cover pure validation, hashing, dependencies and plan union. Database integration tests enforce races, immutability and transaction rollback. UI tests cover navigation/conflict states. Runtime integration tests cover actual producer/cache/request boundaries. Do not use synthetic all-ready observations as sole proof of production readiness.

## 15. Implementation gates and completion evidence

| Gate | Evidence required |
| --- | --- |
| RD-G1 | Calculation dependency matrix and tested reduced profile with meaningful outcome |
| ST-G1 | Producer/warmup capability matrix for enabled signals and live contract resolution |
| MSG-G1 | Atomic event/persistence integration verified against existing infrastructure |
| SEC-G1 | Permission mapping to real application capabilities |
| MIG-G1 | Inventory and exact-reference migration/rollback verification |

No unsupported fallback is authorized by these gates. The feature may be delivered in draft-authoring mode before runtime gates pass, but must be labelled accordingly.

Release evidence must include requirements-to-tests mapping, migration report, seed comparison, concurrency results, actual startup readiness results for the chosen reduced configuration and documentation of remaining unsupported inputs. Record test commands and results; do not claim the specification's acceptance cases have already run.

## Appendix A. Initial 69-signal fixture

The table below is copied exactly from Design v1.0. All rows are enabled in the initial draft; 35 are required and 34 optional under the documented seed defaults. Preparation/source defaults are specified in section 13.1.
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


## Appendix B. Model and enum dictionary

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

### B.1 Enums

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

## Appendix C. Storage dictionary

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

### C.1 Transaction boundaries

Save Draft locks the set row, checks ExpectedRevision, allocates next_version, inserts immutable payload, advances revision, records audit and command receipt, then commits once. Same OperationId and request hash returns the original result; a different hash fails.

Publish revalidates the exact version with supported schema/domain rules, checks Draft state and commits lifecycle/audit/receipt atomically. Assignment validates component, publication eligibility and exact hash in the transaction. Assignment revision must reference a set belonging to its component; enforce a composite FK or equivalent transaction constraint.

Canonical payload serialization sorts object properties, preserves array order and uses invariant numeric/date formatting. Lifecycle metadata is excluded from payload hashing. Canonicalization version belongs to schema metadata. Legacy hashes are preserved, not silently recomputed.

### C.2 Existing assignment ownership

Strategy deployments currently carry exact pipeline references. Keep them authoritative during adaptation. Generic assignment queries combine adapter-backed existing bindings with new generic bindings without dual authority for a consumer role. Do not write two independently editable assignment records for the same role.

### C.3 Runtime data

Parameter tables store intent, not price observations. Retain startup plan identity/hash with the existing startup-run record or adjacent `signal_startup_plan(run_id,plan_id,assignment_fingerprint,plan_json,created_at_utc)` table. Readiness is a current-state projection keyed by runtime generation and demand key, rebuilt by queries rather than a new replay service.

## Appendix D. Message catalog

Use existing actor envelopes and validated subjects. Do not add parallel transport selection. Command metadata supplements existing conventions with OperationId, correlation/causation identifiers, expected revision/state and deadline. Principal/CreatedBy comes from authenticated server context, not trusted UI input.

### D.1 Queries — ActorType.Query / Core NATS

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

### D.2 Commands — ActorType.Command / Core NATS

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

### D.3 Durable facts — ActorType.Event / JetStream

`ParameterSetCreatedEvent`, `ParameterDraftSavedEvent`, `ParameterVersionPublishedEvent`, `ParameterVersionRetiredEvent`, `ParameterAssignmentChangedEvent` carry EventId, OperationId, aggregate ID/revision, exact affected refs, actor identity and timestamp. Consumers are idempotent by EventId/revision.

Use established transactional event infrastructure. A committed database write plus a best-effort publish is insufficient. Implementation must verify how ConfigurationDb enlists in the existing atomic event append/dispatch path. If it cannot, resolve that integration before enabling event-dependent behaviour. Do not introduce an unbounded outbox polling service as a shortcut. Command receipts and current-state queries remain authoritative for UI results.

### D.4 Observer messages — ActorType.Notify / Core NATS

`ParameterCatalogChangedNotification`, `ParameterAssignmentAppliedNotification`, `SignalReadinessChangedNotification`: identity, revision/generation, status and timestamp. These are refresh hints; missed notifications are resolved by current-state queries. Do not duplicate durable Events onto a second transport.

### D.5 Failure reasons

Stable proposed reason codes: `PARAM.NOT_FOUND`, `PARAM.SCHEMA_UNSUPPORTED`, `PARAM.VALIDATION_FAILED`, `PARAM.REVISION_CONFLICT`, `PARAM.HASH_MISMATCH`, `PARAM.OPERATION_IDENTITY_MISMATCH`, `PARAM.VERSION_IMMUTABLE`, `PARAM.LIFECYCLE_INVALID`, `PARAM.VERSION_IN_USE`, `PARAM.ASSIGNMENT_AMBIGUOUS`, `PARAM.COMPONENT_MISMATCH`, `SIGNAL.PRODUCER_UNSUPPORTED`, `SIGNAL.DEPENDENCY_MISSING`, `SIGNAL.PLAN_STALE`, `SIGNAL.STARTUP_BUDGET_EXCEEDED`.

Allocate numeric error codes through the existing registry during implementation. Availability is readiness/outcome data. Malformed configuration and infrastructure failure remain distinct typed errors with correlation IDs.

## Schema-4 compatibility requirements

PS-SCHEMA-004: Regime schema 4 SHALL preserve schema 3 membership and dependency semantics and SHALL describe nullable-reference fields accurately.

PS-SCHEMA-005: Registry initialization SHALL add schema 4 without updating or deleting schema definitions 1-3. Their schema JSON and SHA-256 hashes SHALL remain unchanged.

PS-MIG-004: Editing a supported schema 1-3 Regime payload SHALL produce a schema-4 working copy. Saving SHALL allocate the next immutable parameter-set version and SHALL preserve the source version.

PS-MIG-005: Migration SHALL preserve parameter-set identity, target horizon, supported requirement identities and editable values unless interval rebuilding is explicitly requested. Publishing and assignment SHALL remain explicit operations.