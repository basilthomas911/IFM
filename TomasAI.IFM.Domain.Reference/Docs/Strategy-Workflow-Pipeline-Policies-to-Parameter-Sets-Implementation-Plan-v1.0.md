# Strategy Workflow Pipeline Policies to Parameter Sets Implementation Plan

Version: 1.0\
Date: 2026-09-14\
Status: Approved direction; implementation pending\
Owner: Domain.Reference Parameter Sets, with operator-owned typed models\

Related documents:

- [Reference Data Parameter Sets System Design](Reference-Data-Parameter-Sets-System-Design-v1.0.md)
- [Reference Data Parameter Sets System Specification](Reference-Data-Parameter-Sets-System-Specification-v1.0.md)
- [Reference Data Parameter Sets Actor Implementation Plan](Reference-Data-Parameter-Sets-Actor-Implementation-Plan-v1.0.md)
- [Parameter Component Onboarding](../ParameterSets/Docs/Component-Onboarding.md)
- [Actor Implementation Conventions](../../Documents/system/Actor-Implementation-Conventions.md)

## 1. Objective

Convert every Intrinsic Time Strategy Workflow pipeline operator from its standalone policy/configuration mechanism to the generic Reference Data parameter-set system.

The five required operator components are:

1. Regime Discovery;
2. Market Condition;
3. Trade Selection;
4. Order Composition; and
5. Risk Management.

The completed system has one configuration lifecycle, one assignment model, one audit model and one Reference Data UI. Each operator retains its domain-specific typed payload, validation and calculation behavior.

Standalone policy persistence, lifecycle services, effective-version queries, deployment-time policy stores and startup policy provisioners are removed after compatible migration and runtime cutover. Policy business meaning remains represented by typed parameter-set payloads and operator calculation models.

## 2. Binding implementation decisions

1. `reference_configuration.parameter_set`, `parameter_set_version`, `parameter_assignment`, `parameter_assignment_revision`, `parameter_schema_version`, `parameter_operation`, `parameter_set_audit` and `parameter_legacy_reference` become the sole configuration authority for the five operators.
2. The generic Parameter Set Command, Query and Event actors remain the only actors that manage parameter lifecycle. Do not create operator-specific parameter management actors.
3. Every pipeline operator is a registered component under the `strategy-workflow` area.
4. Every component has an immutable, versioned, typed payload named `XxxParameterSet`.
5. Configuration helper classes may implement validation, canonicalization and calculation, but they cannot own a separate policy lifecycle or storage path.
6. Assignment scope is workflow definition plus Daily, Weekly or Monthly horizon. `Role` distinguishes multiple bindings owned by the same component when necessary.
7. Assignment changes use `NextStartup`. The API startup process loads and validates an immutable parameter snapshot once. Workflow and pipeline processing do not poll PostgreSQL and do not resolve a mutable latest version.
8. A workflow records exact parameter-set ID, version, schema version and payload hash for every invoked stage.
9. Missing or invalid assignments degrade the affected workflow visibly. They do not prevent the API host from starting and do not cause an unbounded retry loop.
10. Existing immutable definitions and payloads are never overwritten. Migration creates generic immutable versions and lineage records.
11. No dual writes are permitted. Before cutover, legacy tables are read-only migration sources. After cutover, normal runtime code reads only the generic startup snapshot.
12. The final cleanup removes legacy policy tables after their data, hashes, statuses, references and historical compatibility requirements have been verified.
13. Regime Discovery is the reference implementation. Its existing generic component is aligned and retained rather than rebuilt.
14. `MarketConditionAssessmentParameterSet` is the active source model for the Market Condition operator. The older `MarketConditionParameterSet` is treated as legacy unless inventory identifies a still-active behavior that is absent from the assessment model.
15. Order Composition receives one operator parameter set per workflow definition and horizon. Its payload contains common construction constraints plus strategy-specific groups for Futures, Vertical Spreads and Iron Condors. Trade Selection outputs a stable variant key; it does not carry a reference to a second policy lifecycle.
16. The strategy catalog continues to define products, strategies, variants and deployments. Converted catalog entries reference exact generic `ParameterVersionRef` values; `StrategyCatalogKind.ParameterSet` no longer owns operator policy content.
17. The existing workflow activation parameter-set table is replaced by the generic assignment bundle where its purpose is only to associate operator versions with a workflow definition and horizon.

## 3. Verified current state

The runtime workflow has five stages in `StrategyWorkflowStage`: RegimeDiscovery, MarketCondition, TradeSelection, OrderComposition and RiskManagement.

The generic component registry currently exposes only:

- `strategy-workflow.regime-discovery`; and
- `market-data-analytics.futures-iti-signal`.

Future ITI Signal belongs to Market Data Analytics and is not part of this conversion.

Current operator configuration is fragmented:

| Operator | Current typed content | Current authority/problem |
| --- | --- | --- |
| Regime Discovery | `RegimeDiscoveryParameterSet` | Generic component exists; legacy configuration compatibility remains |
| Market Condition | `MarketConditionAssessmentParameterSet`, older `MarketConditionParameterSet` | Dedicated tables and conditional development provisioning |
| Trade Selection | `TradeSelectionParameterSet` and `TradeSelectionPolicy` helpers | Dedicated table and development default provisioning |
| Order Composition | `SelectionConstructionPolicy`, composition rules stored through strategy-catalog parameter definitions | Two configuration mechanisms and deployment references |
| Risk Management | `RiskParameterSet` and helpers | Dedicated table and development default provisioning |
| Workflow activation | `TradeSelectionActivation` | Dedicated table duplicates generic assignment responsibility |

The current generic assignment contract already contains consumer kind, consumer ID, role, component code, canonical scope JSON and scope hash. It is sufficient for all five operator assignments.

## 4. Target component catalog

Register these components using stable codes and display names:

| Area | Component code | Display name | Editor code | Assignment role |
| --- | --- | --- | --- | --- |
| Strategy Workflow | `strategy-workflow.regime-discovery` | Regime Discovery | `regime-discovery` | `regime-discovery` |
| Strategy Workflow | `strategy-workflow.market-condition` | Market Condition | `market-condition` | `market-condition` |
| Strategy Workflow | `strategy-workflow.trade-selection` | Trade Selection | `trade-selection` | `trade-selection` |
| Strategy Workflow | `strategy-workflow.order-composition` | Order Composition | `order-composition` | `order-composition` |
| Strategy Workflow | `strategy-workflow.risk-management` | Risk Management | `risk-management` | `risk-management` |

Component codes, schema versions and schema hashes are immutable. Registry startup uses insert-if-missing followed by exact equality verification. An existing code with a different name, editor, schema or hash fails startup migration with a classified error; it is never silently updated.

## 5. Typed parameter-set payloads

All payloads contain the following common metadata:

- `ParameterSetId`;
- `Version`;
- `SchemaVersion`;
- `WorkflowDefinitionId` where required for payload validation;
- `TargetHorizon`;
- `InstrumentRoot` where the operator is instrument-specific; and
- component-specific parameter groups.

Lifecycle status, timestamps, creator, description and payload hash remain in the generic envelope and are not duplicated inside calculation fields.

### 5.1 Regime Discovery

Retain `RegimeDiscoveryParameterSet` as the baseline implementation.

Required work:

- confirm current schema preserves signal, observation and calculation groups;
- retain the normalized signal and observation editors;
- remove remaining normal-runtime reads from `regime_discovery_parameter_set`;
- migrate every retained legacy version with exact lineage;
- generalize `WorkflowParameterScopeModel` so it creates and validates all operator roles rather than embedding the Regime role; and
- preserve current supported-reduction and mandatory-dependency rules.

### 5.2 Market Condition

Create the generic `MarketConditionParameterSet` from the active Market Condition Assessment contract without changing behavior.

Parameter groups include:

- profile and horizon;
- calendar/session rules;
- source/evidence bindings;
- regime interpretation thresholds;
- event-risk rules;
- confidence and insufficiency rules; and
- freshness/maximum-age rules.

The old `MarketConditionParameterSet` and current assessment payload must be compared field by field. Any active field in the old payload that is not represented in the assessment contract is either migrated into the new schema or explicitly classified as unused with test evidence. There will be one active Market Condition payload after cutover.

### 5.3 Trade Selection

Create the generic `TradeSelectionParameterSet` using the current typed selection contract.

Parameter groups include:

- candidate eligibility;
- strategy-family eligibility;
- scoring and ranking;
- confidence and minimum-evidence limits;
- tie breaking;
- selected variant keys;
- NoTrade thresholds; and
- validity/expiry limits.

Remove parameter-set IDs for downstream composition policies from the selection decision. The result carries the selected strategy/variant identity. The frozen Order Composition parameter set resolves the matching strategy rules.

### 5.4 Order Composition

Create a single `OrderCompositionParameterSet` contract for each workflow definition and horizon.

Parameter groups include:

- common construction constraints;
- quantity and maximum-leg rules;
- expiration and contract-selection rules;
- limit-price and pricing tolerances;
- Futures rules;
- Vertical Spread rules;
- Iron Condor rules;
- builder/version compatibility; and
- validation and expiry rules.

The strategy-specific section is keyed by stable strategy and variant codes. It incorporates the currently separate `SelectionConstructionPolicy` and composition-rule parameter definitions without merging executable pricing logic into configuration.

The strategy catalog retains variant metadata and capabilities. Its deployment definition references the exact generic Order Composition version assigned to the workflow/horizon or verifies compatibility with that assignment. It does not store another mutable or independently published policy payload.

### 5.5 Risk Management

Create `RiskManagementParameterSet` from the current `RiskParameterSet` behavior.

Parameter groups include:

- currency and supported instrument root;
- maximum risk per trade;
- maximum aggregate risk;
- maximum margin;
- resizing limits and rounding;
- stale-input limits;
- acceptance/rejection thresholds;
- portfolio handoff limits; and
- execution validity windows.

Portfolio and Fund identity are runtime financial-authority inputs. They are not embedded as fixed identities in the workflow-level parameter set. Portfolio remains the final authority that atomically accepts the composed opportunity and creates Fund-specific trade orders.

## 6. Shared contracts and schema work

Add or update contracts under `TomasAI.IFM.Domain.Reference.Shared/ParameterSets` and the appropriate operator-owned Shared folders.

Required contracts:

- component-code constants;
- typed `XxxParameterSet` payloads;
- generic `ParameterVersionRef` usage in workflow bindings;
- `StrategyWorkflowParameterBundle` containing all five exact resolved versions;
- typed per-stage binding records with ID/version/schema/hash and typed payload;
- startup resolution result and per-component issue records;
- migration inventory/result records; and
- compatibility readers for historical messages that still contain old policy types.

MessagePack changes append keys only. Existing key numbers and enum numeric values remain unchanged. A renamed public type requires either a compatible adapter/type forwarder or retention of a read-only legacy DTO until persisted messages no longer require it.

Each schema has:

- bounded JSON structure;
- a canonical codec version;
- an immutable schema hash;
- structural validation;
- typed semantic validation;
- cross-field validation;
- workflow/horizon compatibility validation; and
- conversion from supported legacy schemas.

## 7. Assignment model

Generalize workflow assignment construction around:

```text
ConsumerKindCode = strategy-workflow
ConsumerId       = IntrinsicTimeStrategyWorkflow definition ID
Role             = operator role
ComponentCode    = operator component code
ScopeJson        = { TargetHorizon: Daily|Weekly|Monthly }
ScopeSha256      = canonical scope hash
```

There is exactly one enabled assignment revision for each workflow definition, horizon and operator role.

Assignment validation must prove:

- the version is Published;
- the component matches the role;
- the payload hash matches the version reference;
- workflow definition and horizon are compatible;
- referenced strategy/variant codes exist where applicable;
- no second enabled authority exists for the same scope; and
- retirement and assignment serialize through the established shared writer lease and transaction rules.

The assignment UI displays Active at Startup and Assigned for Next Startup separately. Saving or publishing a parameter set never assigns it automatically.

## 8. Startup snapshot and runtime resolution

At startup, `ParameterStartupReader` loads enabled assignment revisions and exact immutable versions in bounded queries. It validates and deserializes each payload once and creates an immutable lookup keyed by workflow definition, horizon and role.

The lookup returns `StrategyWorkflowParameterBundle` in O(1) without PostgreSQL access. The bundle contains all exact references and typed payloads required by the five stages.

Workflow admission behavior:

1. Accept the eligible completed ITI signal and create visible workflow state.
2. Read the applicable immutable bundle from the startup snapshot.
3. Record the startup generation and all available exact references.
4. Start Regime Discovery with its frozen binding.
5. Pass each later operator only its own frozen binding plus prior stage results.
6. If an assignment is missing or invalid, fail the affected stage with component, workflow, horizon, assignment ID, parameter ID/version/hash and validation details.
7. Never query for a newer parameter version while the workflow is running.

Startup reports incomplete bundles as degraded configuration. The API host remains available. No timer repeatedly queries for corrected assignments; corrections apply through a later deliberate application startup.

## 9. Actor and handler changes

Parameter management continues through:

- `ParameterSetCommandActor`;
- `ParameterAssignmentCommandActor`;
- `ParameterSetQueryActor`;
- `ParameterSetEventActor`; and
- existing startup/readiness actors where applicable.

Each mapped command, query or event has one dedicated extension handler named after the message without its type suffix and stored in the corresponding Command, Query or Event folder. Actors contain framework lifecycle and maps only. Domain validation and conversions belong in Model folders.

Do not create Function or Realtime actors for configuration management.

Existing pipeline Function/Realtime actors remain responsible for execution. Update their start/execute messages to consume typed frozen parameter bindings. Remove calls to legacy effective-policy resolvers.

Retire after cutover:

- Regime-specific configuration command/query actors used only for legacy lifecycle;
- operator-specific policy repositories and effective resolvers;
- development policy provisioners;
- policy publication services duplicated by generic actors; and
- background refresh/polling code for operator configuration.

## 10. PostgreSQL conversion

### 10.1 Generic authority

Extend `ParameterRegistrySchemaSql` with the four missing Strategy Workflow components and immutable schema versions. Keep SQL constants and migrations under `Application.Storage/ConfigurationDb/ParameterSets`.

Add indexes required for bounded startup loading and UI queries:

- component/name/set identity;
- assignment consumer/role/scope;
- exact set/version;
- lifecycle status; and
- legacy-kind/set/version lineage.

### 10.2 Legacy inventory

Inventory all rows and references from:

- `regime_discovery_parameter_set`;
- `market_condition_parameter_set`;
- `market_condition_assessment_parameter_set`;
- `trade_selection_parameter_set`;
- `order_composition_parameter_set`;
- `risk_management_parameter_set`;
- `intrinsic_time_strategy_workflow_parameter_set`; and
- strategy-catalog definitions whose kind is `ParameterSet` and whose content belongs to these operators.

For every source row capture:

- legacy kind;
- source ID/version/schema;
- original payload bytes or canonical JSON as applicable;
- original hash and codec semantics;
- lifecycle status and timestamps;
- creator/description;
- active deployment references; and
- target generic ID/version/component/schema/hash.

### 10.3 Compatible migration

Migration is additive and repeatable:

1. Validate source rows before writing targets.
2. Preserve a source ID when it cannot collide; otherwise derive a deterministic kind-qualified target ID.
3. Convert the payload through the registered component adapter.
4. Insert a generic set and immutable version.
5. Preserve Draft, Published or Retired meaning and timestamps.
6. Insert `parameter_legacy_reference` with source and target identities and hashes.
7. Convert active workflow/deployment bindings into generic assignment revisions.
8. Re-read and validate the target through normal generic query code.
9. Record an immutable migration result and mismatch details.
10. On rerun, accept exact prior results and fail any differing collision.

One transaction covers each migrated aggregate and lineage record. No source row is modified. Migration does not publish an invalid version or guess missing required values.

### 10.4 Final storage cleanup

After runtime and historical verification:

1. block legacy writes explicitly;
2. prove no production composition root resolves a legacy repository;
3. export a final source-to-target manifest;
4. verify all persisted workflow references remain readable;
5. remove legacy repository interfaces and SQL;
6. remove legacy tables in a versioned migration; and
7. retain required provenance in generic immutable versions, audit rows and legacy-reference rows.

Table removal is the final gate, never the first migration action.

## 11. Reference Data UI

The existing Parameter Sets view remains registry-driven.

Under the `Strategy Workflow` master entry, show the five pipeline operators in execution order. Selecting an operator shows its parameter sets and versions using the established master/detail layout.

Common behavior:

- view mode is read-only;
- Add creates a new set and version 1 draft;
- Change creates a new immutable draft version;
- Remove retires a Published version or disables/removes the applicable assignment; it never deletes history silently;
- version increases only after a successful save;
- Publish and Assign remain separate explicit actions;
- validation errors identify group, property or row;
- assignment selects workflow definition and horizon;
- Active and Next Startup versions are both visible;
- unsupported legacy schemas remain inspectable and copyable through an explicit upgrade preview; and
- navigation preserves selection by stable IDs.

Editor layouts:

| Operator | Left-side groups | Right-side detail |
| --- | --- | --- |
| Regime Discovery | Signals, Observations, calculation groups | Existing signal/observation grids and grouped property editor |
| Market Condition | Profile, Sources, Calendar, Regime, Event Risk, Freshness | Typed property grid and bounded source list |
| Trade Selection | Eligibility, Ranking, Confidence, NoTrade, Variants, Expiry | Typed property grid plus ordered scoring/variant grids |
| Order Composition | Common, Pricing, Futures, Vertical Spreads, Iron Condor, Validation | Strategy-specific rule/property grids |
| Risk Management | Product, Capital, Margin, Loss, Resizing, Freshness, Handoff | Typed numeric/enum property grid with units |

Enums use dropdowns. Booleans use checkboxes. Integer, decimal, duration and money values use validated typed editors with units. Users never need to edit raw JSON for supported schemas.

## 12. Application and service cleanup

Update dependency injection so normal runtime resolves only:

- generic parameter-set APIs;
- generic assignment APIs;
- immutable startup snapshot access; and
- operator-owned typed validators/deserializers.

Remove registrations for legacy policy stores and provisioners after cutover. Development defaults are created as normal generic Draft/Published versions and assignments through an explicit idempotent migration/bootstrap operation. They do not bypass lifecycle validation.

Update all commands, events, state models, read models, UI details and structured logs that currently expose a generic or empty parameter identity. Each stage reports its own exact parameter reference.

## 13. Implementation phases and gates

### Phase 0: inventory and baseline

Tasks:

- enumerate every policy type, serializer, validator, table, repository, resolver, provisioner and deployment reference;
- identify persisted events/messages containing policy types;
- capture row counts, hashes and active references from an isolated database snapshot;
- run current workflow happy-path tests for Daily, Weekly and Monthly; and
- publish the inventory as implementation evidence.

Gate PSW-G0: every existing policy field and active reference has a target disposition. Unknown active fields are a blocker; unused fields require evidence.

### Phase 1: shared components and schemas

Tasks:

- add the four missing component descriptors;
- generalize workflow assignment scope roles;
- define typed parameter-set payloads and bindings;
- register immutable schema versions/hashes;
- implement legacy-to-generic adapters; and
- preserve MessagePack compatibility.

Gate PSW-G1: all component model and schema tests pass; schema collision tests prove immutable registration.

### Phase 2: operator model conversion

Implement and test each operator vertically in this order:

1. Market Condition;
2. Trade Selection;
3. Order Composition;
4. Risk Management; and
5. Regime Discovery alignment.

For each operator:

- preserve current default behavior exactly;
- move persistence-neutral validation/calculation to Model classes;
- implement descriptor, canonicalization and editor metadata;
- implement typed payload conversion;
- test every field and edge rule; and
- prohibit legacy lifecycle access from the new model.

Gate PSW-G2: serialized defaults and representative legacy versions produce behaviorally equivalent decisions.

### Phase 3: generic persistence and migration

Tasks:

- add component/schema/index migration;
- implement legacy inventory and conversion;
- migrate versions and lineage;
- migrate workflow/horizon assignments;
- convert strategy-catalog references; and
- implement idempotent rerun and mismatch reports.

Gate PSW-G3: real PostgreSQL tests prove counts, hashes, statuses, assignments, immutability, rollback and rerun behavior.

### Phase 4: Reference Data UI

Tasks:

- register the four new editors;
- implement grouped typed controls;
- implement common Add/Change/Remove/Publish/Assign flow;
- show validation, version comparison, usage and audit;
- show Active versus Next Startup; and
- add accessible dark-theme layouts and resize behavior.

Gate PSW-G4: UI unit/system tests and manual review prove all five operators can be managed without raw JSON.

### Phase 5: startup bundle and workflow integration

Tasks:

- load and validate typed bundles at startup;
- expose immutable O(1) lookup;
- freeze the bundle into workflow state;
- update all five stage starts and results;
- remove runtime calls to legacy resolvers;
- add complete structured error details; and
- report incomplete bundles as degraded configuration.

Gate PSW-G5: Daily, Weekly and Monthly workflows use exact generic versions end to end, with no policy-table query after startup.

### Phase 6: authority cutover

Tasks:

- enable generic authority in Development;
- run shadow comparison using migration verification only, not dual writes;
- verify workflow decisions and parameter identities;
- make generic authority unconditional;
- remove fallback-to-legacy behavior; and
- verify restart applies pending assignments.

Gate PSW-G6: no normal runtime path reads or writes a legacy policy table.

### Phase 7: code and schema removal

Tasks:

- delete legacy policy repositories/resolvers/provisioners;
- delete obsolete configuration actors/messages;
- remove obsolete policy lifecycle helpers and DI registrations;
- replace or retain only required historical compatibility DTOs;
- remove converted strategy-catalog policy bodies;
- execute the final table-removal migration; and
- update documentation and diagrams.

Gate PSW-G7: static searches, architecture tests and database inspection prove a single authority remains.

### Phase 8: full qualification

Run all unit, BDD, actor, integration, UI, migration, workflow, performance and verification suites. Record command lines, counts, durations and failures in `ParameterSets/Docs/Implementation-Evidence.md`.

Gate PSW-G8: all acceptance criteria in Section 18 are satisfied with no unresolved domain blocker.

## 14. Test plan

### 14.1 Unit tests

For every component:

- valid defaults for all three horizons;
- every scalar boundary;
- undefined enums;
- duplicate group/row/variant keys;
- missing mandatory values;
- incompatible workflow or horizon;
- canonical hash stability;
- culture-independent decimal and date handling;
- schema upgrade preservation;
- unsupported-schema read-only behavior;
- legacy conversion field parity; and
- behavioral equivalence of current and converted defaults.

Assignment tests:

- deterministic identity for workflow/horizon/role;
- role/component mismatch;
- Draft or Retired target rejection;
- hash mismatch;
- stale revision;
- assignment-versus-retirement race; and
- NextStartup activation semantics.

### 14.2 Actor tests

- parse/validation/receive map parity;
- one dedicated extension handler per mapped message;
- create, save, publish, assign, retire and disable happy paths;
- malformed/unknown message failures;
- duplicate operation idempotency;
- same operation ID with different request rejection;
- storage exception classification; and
- complete structured error details.

### 14.3 PostgreSQL integration tests

- clean schema creation and migration rerun;
- immutable schema hash collision;
- immutable payload enforcement;
- lifecycle transitions;
- all legacy status mappings;
- deterministic ID collision handling;
- transaction rollback at each write boundary;
- exact lineage and audit preservation;
- assignment bundle completeness;
- strategy-catalog reference conversion;
- no orphaned references; and
- final legacy-table removal only after zero-reference verification.

### 14.4 Workflow integration and BDD tests

Happy paths:

- Daily workflow uses all five Daily assignments;
- Weekly workflow uses all five Weekly assignments;
- Monthly workflow uses all five Monthly assignments;
- NoTrade completes without Order Composition or Risk Management execution when current workflow rules require that termination;
- selected trade flows through Order Composition and Risk Management; and
- every executed stage records exact parameter identity/version/hash.

Edge paths:

- missing Regime assignment;
- missing later-stage assignment after visible workflow admission;
- invalid or stale parameter payload;
- retired version retained by historical workflow but unavailable for new startup assignment;
- selected variant absent from Order Composition rules;
- incompatible Risk horizon/root/currency;
- assignment changed while a workflow is running;
- application restart activates the new version;
- migration rerun after partial transaction failure; and
- runtime remains available in degraded configuration without polling or recursive recovery.

### 14.5 UI tests

- all five child components appear in execution order;
- master/detail titles and dark theme;
- read-only viewing;
- Add/Change/Remove action states;
- version increments only on successful save;
- field/group navigation;
- enum and numeric editor behavior;
- validation path focus;
- publish versus assign separation;
- Active/Next Startup display;
- dirty navigation guard;
- resize/layout behavior; and
- unsupported legacy version inspection.

### 14.6 Performance and allocation verification

Use BenchmarkDotNet and running-process counters to prove:

- startup snapshot load remains bounded;
- workflow bundle lookup performs no database IO;
- bundle lookup and typed binding access avoid per-stage JSON deserialization;
- no configuration polling timer exists;
- workflow admission allocations do not materially regress from baseline; and
- configuration changes do not increase Ring 2 allocation rates.

## 15. Observability and errors

Structured logs and stage results include:

- workflow ID and definition;
- horizon;
- pipeline stage/component;
- assignment ID and revision;
- parameter-set ID/version/schema/hash;
- startup generation;
- validation/error code;
- full inner exception chain where an actual exception occurs; and
- degraded, waiting, failed or completed outcome.

Normal missing configuration is a typed failure result, not an exception-driven retry. Actual deserialization, hash, storage or invariant failures are exceptions at their owning boundary and are logged once with full details.

## 16. Rollback

Before final legacy removal, rollback is a deployment rollback:

- generic migration never modifies source rows;
- the source-to-target manifest proves exact provenance;
- the previous application version can read unchanged legacy tables; and
- no dual-write reconciliation is required.

After final removal, rollback restores the database backup taken at the removal gate or deploys forward using the generic authority. Never recreate old policy rows from inferred current values.

Parameter rollback within the new system assigns a previously Published compatible generic version for NextStartup. It never edits or republishes an immutable old version.

## 17. Out of scope

- changing the business algorithms or default decisions;
- adding new strategy families or variants;
- changing Portfolio financial authority;
- changing broker execution;
- live assignment hot swapping;
- background database polling;
- AI-generated policy changes; and
- deleting historical events required for replay or audit.

## 18. Completion criteria

Implementation is complete only when:

1. all five Strategy Workflow components appear in Reference Data;
2. each has a typed editor and immutable schemas;
3. each has Published Daily, Weekly and Monthly versions where the workflow supports those horizons;
4. each workflow definition/horizon has exactly one enabled assignment for every required operator role;
5. startup produces a validated immutable typed bundle;
6. workflows use only that bundle and record exact references;
7. no pipeline operator reads a legacy policy table;
8. no standalone policy lifecycle service remains registered;
9. Order Composition policy content no longer lives in strategy-catalog parameter definitions;
10. all legacy rows have verified generic lineage;
11. final legacy tables are removed through a guarded migration;
12. historical compatible messages remain readable;
13. all happy-path and edge-case tests pass;
14. UI behavior is verified for every operator;
15. performance tests show no per-workflow configuration database IO or polling; and
16. the implementation evidence document contains the executed results for every gate.
