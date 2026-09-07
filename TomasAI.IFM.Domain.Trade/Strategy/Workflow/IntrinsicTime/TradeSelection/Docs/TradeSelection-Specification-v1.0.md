# TradeSelection Detailed Specification v1.0

> **Strategy catalog direction (2026-09-06):** TradeSelection implementation is on hold at the user's request. Reusable strategy definitions, structures, variants and deployments will be owned by PostgreSQL ConfigurationDb; Portfolio owns Fund authorization. The catalog decision supersedes the earlier fixed three-variant scope and selector-only template catalog. Sections below retain the previous baseline where not explicitly updated; their proposed schemas, wire layouts and TS gates must be realigned before implementation. This document update does not resume any gate. See [ConfigurationDb strategy catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

| Item | Value |
| --- | --- |
| Document version | 1.0 |
| Revised | 2026-09-06 |
| Status | On hold; prior implementation baseline requires catalog alignment; numerical defaults remain engineering examples |
| Authority | [TradeSelection high-level design, revision 0.7](TradeSelection-High-Level-Design-v0.1.md) |
| Implementation plan | [Implementation Plan v1.0](TradeSelection-Implementation-Plan-v1.0.md) |
| Stage | `StrategyWorkflowStage.TradeSelection` |
| Runtime boundary | Command actor, durable event projection/publication, workflow-owned continuation |
| Initial scope | ES; one assigned template for the triggering Daily, Weekly or Monthly horizon |
| Implementation status | Existing contracts/helpers remain; prior SHALL requirements are suspended where catalog alignment is pending |

This specification fixes the input contract, parameter schema, initial compatibility rules, result, actor behavior and integration requirements. All numerical defaults below are explicit test configuration, not calibrated claims of trading performance. Completing this document does not create database rows, publish profiles, enable workflows or change code. The catalog, legacy mapping, variant rules and candidate policy must now be specified before implementation can resume.

## 1. Authorities and boundaries

- [MarketCondition specification](../../MarketCondition/Docs/MarketCondition-Specification-v2.0.md) defines the assessment-only upstream boundary.
- [Portfolio/Fund specification](../../../../../../TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-Fund-Specification-v1.0.md) defines Fund authority, composition identity reservation and financial ownership.
- [Reference catalog implementation](../../../../../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Symbol-Catalog-Implementation.md) defines exact family and product identities.
- [Trade Strategy Builder design](../../OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md) defines one-unit construction and downstream final sizing.

For the selector, this specification resolves earlier ambiguous vocabulary: `TradeSelectionParameterSet` is the typed payload referenced by the existing `TradeSelectionHintProfileId` and version. They are one policy identity, not separate profiles. `TradeSelectionTemplateDefinition` is the reusable selected strategy definition identified by the existing `TradeTemplateId` and version. This was the prior V1 identity model. It is now superseded by the reusable ConfigurationDb catalog: map these existing identities explicitly to strategy, structure and deployment versions; do not create a second authoritative selector-only template catalog.

The workflow has five opening decision stages: RegimeDiscovery, MarketCondition, TradeSelection, OrderComposition and RiskManagement. Execution is downstream. IBKR emulator and actual broker integration remain separate. Strategy observation UI changes remain deferred until the five operators are code complete.

TradeSelection SHALL evaluate suitability only. It SHALL NOT fetch new quotes, query option chains, recompute either upstream stage, reserve financial risk, choose exact contracts/strikes/expirations/prices/leg ratios, determine final units or submit orders. Configuration/reference reads occur while freezing the workflow binding, not inside the pure selector.

## Catalog realignment required before implementation

The following baseline tables, enum lists, fixed profile mappings and test fixtures do not yet cover the expanded catalog. Replace the selector-only `trade_selection_template_definition` proposal with the canonical relational catalog; preserve exact existing parameter/profile identities through an explicit mapping. Add exact strategy, structure, variant and deployment versions/hashes to the frozen binding and result. Revisit wire layouts without repurposing existing fields.

Cover Long/Short futures, four credit/debit vertical variants, and Long/Short iron condors with independent Balanced/Bullish/Bearish bias. Add bounded Fund-authorized candidate enumeration, deterministic preference/tie handling, capability/input validation and compatibility fixtures. Unknown unsupported strategy capabilities must fail configuration validation, never fall back to a familiar enum. Future multi-expiry or asymmetric structures require qualified Composer/risk support before activation.

Retain single-trigger-horizon assessment, market-only upstream boundaries, immutable evidence, durable delivery, workflow acceptance and Portfolio-owned composition identity reservation. Full catalog-specific contract and test rewrites remain work for the resumed specification/plan, not completed implementation.

## 2. Decision scope and initial templates

```text
TriggerEvent.EntityId.TimePeriod
  = accepted RegimeDiscoveryResult.TargetHorizon
  = accepted MarketConditionAssessmentResult.TargetHorizon
  = frozen Fund/assignment horizon
  = TradeSelectionParameterSet.TargetHorizon
  = TradeSelectionResult.DecisionHorizon
```

Only Daily, Weekly and Monthly are valid. Each ITI signal starts at most its own horizon's workflow. Existing supporting regime observations may retain other horizons; do not request additional horizon results.

| Profile code | Target horizon | Catalog Family / Strategy | AssetType | Structure variant | Accepted regime direction -> selected direction |
| --- | --- | --- | --- | --- | --- |
| `TS.ES.Daily.Test` | Daily | Futures / Futures | Futures | DailyOutright | Up -> Long; Down -> Short |
| `TS.ES.Weekly.Test` | Weekly | FuturesOption / VerticalSpread | FuturesOptions | WeeklyDebitVertical | Up -> Bullish; Down -> Bearish |
| `TS.ES.Monthly.Test` | Monthly | FuturesOption / IronCondor | FuturesOptions | MonthlyDirectionalCreditCondor | Up -> Bullish; Down -> Bearish |

The initial weekly variant is a debit vertical: bullish call debit spread or bearish put debit spread. The monthly variant is a defined-risk credit condor whose construction profile must support positive net delta for Bullish and negative net delta for Bearish. Exact deltas, widths, DTE and quotes are OrderComposition parameters. Failure to construct that intent is `NoCandidate`, not an invitation to change the selection.

These mappings belong to Fund assignments and selected construction policy. Upstream market profiles remain family-independent. Neutral is a valid observed regime direction but produces NoTrade for all three initial templates. Unknown direction is an invalid upstream contract.

The futures trigger identifies the underlying signal source, not the product class to trade. The workflow SHALL resolve its authorized Fund/horizon assignment before choosing Futures versus FuturesOptions for selection. Passing the trigger's Futures asset type as a blanket resolver filter would incorrectly exclude the weekly and monthly assignments.

## 3. Existing implementation and required additions

| Existing component | Use in this specification | Required addition/change |
| --- | --- | --- |
| `StartTradeSelectionPipelineCommand`, keys 0-13 | Keep routing, workflow, trigger and correlation contract | Append schema and frozen selection binding |
| `PortfolioFundStrategySnapshot` | Reuse Portfolio, Fund, allocation, envelope, assignments and hash | Freeze and carry it through workflow; selection-specific permission semantics below |
| `PortfolioFundStrategyResolver` and query service | Reuse authoritative resolution logic and data sources | Add single-assignment selector resolution without treating a futures trigger as the traded product |
| `FundTradeTemplateAssignmentReadModel` | Reuse exact template/profile/family references | Require schema >= 2 and exact family for new selector workflows |
| `MarketAssessmentSelectionConsumer` | Existing mandate/assessment filtering evidence | Replace the runtime selection path with full typed evaluation; no candidate-list result adapter |
| Assessment `ValidateForSelection` | Reuse accepted lineage, availability and restriction checks | Enforce additional selector contract/clock/authority requirements |
| Generic `StrategyStageResultEnvelope` | Keep existing keys and payload hash | Add typed TradeSelection payload validation and explicit stage size limit |
| Processing/Completed/Failed events and routes | Keep Command/Realtime addresses and existing event keys | Implement persistent command actor, projector, realtime delivery and queries |
| `CompleteTradeSelection` | Workflow continuation boundary | Validate typed outcome, expiry and reservation before builder dispatch |
| ConfigurationDb TradeSelection kind/table | Store versioned selection policy | Typed payload serializer, validators, exact-version resolver and guarded lifecycle |

These are source-review findings, not evidence of end-to-end operational qualification.

## 4. Common contract rules

**Assembly prerequisite:** The [implementation plan TS-01](TradeSelection-Implementation-Plan-v1.0.md#5-ts-01-shared-contracts-and-dependency-foundation) moves the required existing Portfolio/Reference DTOs into a dependency-safe shared assembly before adding typed workflow bindings. Existing public namespaces, field keys, validation and hashes remain unchanged; this resolves project-reference cycles without replacing the specified contracts.

All new types SHALL use explicit integer MessagePack keys, defensive copies and immutable collections at boundaries. Old keys retain their meaning; new keys append. Unsupported schemas, missing required values, invalid enums and unknown required JSON properties fail validation. Omitted fields do not silently acquire valid test defaults during deserialization. Defaults are populated only by an explicit profile factory or fixture.

IDs follow existing contracts: Portfolio/Fund/family/product IDs are positive integers; definition and assignment versions are positive longs; template/profile IDs are non-empty GUIDs; selector parameter Version is a positive int. Converting assignment profile versions from long to int SHALL be checked, not truncated. Workflow ID retains `StrategyWorkflowId` UUIDv7 validation.

Use exact ordinal machine identifiers after normalization at configuration authoring. Legacy `FuturesOption` catalog Family and `FuturesOptions` Portfolio AssetType are mapped explicitly as in section 2. Do not use substring, display-name or SystemKey lookup. An explicit importer may normalize known strings before publication; runtime selection may not guess them.

Timestamps SHALL be UTC. Effective intervals are `[from, until)`; expiry occurs at `now >= until`. All numeric confidence comparisons are decimal and inclusive at the minimum. Enumerated categories use set membership, never ordinal greater-than comparisons. Invalid enum numbers are contract failures; a defined Unknown/None category has the explicit policy behavior below.

## 5. Frozen authority and definition resolution

### 5.1 Resolution timing

Before accepting a new workflow, freeze selection configuration together with existing regime/assessment bindings. This does not make Fund configuration an input to regime or assessment calculation. Persist the accepted binding in workflow state; dispatch copies it unchanged at TradeSelection.

Resolve the Portfolio and chosen Fund by configured Portfolio identity, trading year, target horizon and underlying scope. Reuse `PortfolioFundStrategySnapshot` and existing query/storage APIs. Add `ResolveForSelection` behavior to the Portfolio resolver/query layer rather than weakening its existing strict callers:

1. Identify exactly one structurally valid effective Fund and one effective assignment for the configured slot, before product-class filtering. Where the workflow has an explicit FundId, require exact equality.
2. Count effective assignments before applying Enabled. Zero or more than one is configuration failure. Preserve a disabled assignment so selection can explain NoTrade.
3. Validate all Portfolio/Fund/version relationships and effective intervals. Freeze allocation, financial policy and FundRiskEnvelope with their provenance.
4. Distinguish permission denial from malformed/missing authority. A valid paused Portfolio/Fund, blocked envelope or disabled family/assignment remains explicit permission evidence; it produces NoTrade at selection. Missing, ambiguous, corrupt or expired required configuration cannot start a new qualified workflow.
5. Resolve the exact assignment's family definition, template definition, selection profile and construction-profile descriptor. Never select latest versions in place of assigned versions.
6. Freeze all values and hashes once. Changes after activation affect later workflows; explicit emergency cancellation remains a separate authority.

The current resolver filters out disabled assignments, rejects blocked envelopes and can return several compatible assignments. Its existing behavior is not sufficient to claim these selection-specific semantics are implemented. Its canonical snapshot serializer and Portfolio ownership SHALL be reused.

### 5.2 `TradeSelectionBinding` schema 1

| Key | Field / type | Requirement |
| --- | --- | --- |
| 0 | `SchemaVersion : short` | Exactly 1 |
| 1 | `PortfolioSnapshot : PortfolioFundStrategySnapshot` | Existing complete frozen snapshot |
| 2 | `FamilyDefinition : TradeStrategyFamilyReadModel` | Exact assignment family ID/version; complete row |
| 3 | `Template : TradeSelectionTemplateDefinition` | Exact assigned template version |
| 4 | `Parameters : TradeSelectionParameterSet` | Exact assigned hint-profile ID/version |
| 5 | `ParameterPayloadSha256 : string` | Canonical selector parameter hash |
| 6 | `ConstructionProfile : SelectionConstructionProfileReference` | Immutable descriptor below |
| 7 | `FrozenAtUtc : DateTime` | Accepted workflow resolution time |
| 8 | `ValidUntilUtc : DateTime` | Earliest required authority/definition validity |
| 9 | `PayloadSha256 : string` | Hash of this binding with this field empty |
| 10 | `ParameterEffectiveFromUtc : DateTime` | Published version effective at FrozenAtUtc |
| 11 | `TemplateEffectiveFromUtc : DateTime` | Published definition effective at FrozenAtUtc |
| 12 | `RequestedTradeDate : DateOnly` | Frozen date derived using key 13 |
| 13 | `TradeDatePolicy : string` | UTC.TriggerCreatedDate.Test.v1 in this initial specification |

`SelectionConstructionProfileReference` keys: 0 SchemaVersion(short=1), 1 ProfileId(Guid), 2 Version(long), 3 PayloadSha256(string), 4 TradeTemplateId(Guid), 5 TradeTemplateVersion(long), 6 FamilyReference(existing exact reference), 7 StructureVariant(enum), 8 SupportedDirections(SelectionDirection[]), 9 EffectiveFromUtc(DateTime), 10 EffectiveUntilUtc(DateTime?). This descriptor is resolved from the real versioned OrderComposition profile; it is not an independently editable policy or proof that a builder is implemented.

Snapshot WorkflowRevision is the revision at freezing. It SHALL NOT be rewritten to every stage revision to make hashes match. Bind it to the accepted start history and require it to be no greater than the current invocation revision. Workflow/Fund identity must match throughout.

### 5.3 `TradeSelectionTemplateDefinition` schema 1

| Key | Field / type | Required value/meaning |
| --- | --- | --- |
| 0 | SchemaVersion / short | 1 |
| 1 | TradeTemplateId / Guid | Assigned reusable template ID |
| 2 | Version / long | Assigned immutable template version |
| 3 | Code / string | Stable ordinal code, 1-128 characters |
| 4 | FamilyReference / TradeStrategyFamilyReference | Exact reference, not SystemKey |
| 5 | TargetHorizon / TimeFrameType | Matches family row and assignment |
| 6 | InstrumentRoot / string | ES |
| 7 | AssetType / string | Futures or FuturesOptions |
| 8 | StructureVariant / SelectionStructureVariant | One of section 2 variants |
| 9 | SupportedDirections / SelectionDirection[] | Exactly the corresponding pair in section 2 |
| 10 | OrderCompositionProfileId / Guid | Matches assignment and descriptor |
| 11 | OrderCompositionProfileVersion / long | Positive exact version |
| 12 | Enabled / bool | Explicit operating flag |
| 13 | EffectiveFromUtc / DateTime | Effective interval start |
| 14 | EffectiveUntilUtc / DateTime? | Optional interval end |

**Superseded storage proposal, retained for mapping only:** The prior specification proposed immutable template definitions in ConfigurationDb table `reference_configuration.trade_selection_template_definition` with `(trade_template_id uuid, version bigint)` primary key, `schema_version smallint`, `status smallint`, `effective_from_utc timestamptz`, `retired_at_utc timestamptz`, `payload_json jsonb`, `payload_sha256 char(64)`, `description text`, `created_utc timestamptz`, `created_by text`. Require positive versions/schema and the same lifecycle rules as section 9. Do not implement this selector-only table. The reusable catalog design now governs storage; map these fields and identities explicitly when the specification is revised.

The family row supplies catalog Family, Strategy, product symbol, exchange and currency. New qualified selections require positive `TradeStrategySymbolId`, a non-empty Exchange and USD currency for this ES initial scope. Legacy zero-product rows cannot be substituted silently. The root ES is the economic scope; the assigned product symbol need not literally be ES. Product-to-root consistency SHALL be checked during binding resolution against the authoritative product catalog. Exact expiring instruments are not required here.

## 6. Complete invocation inputs

| Input group | Mandatory contents and source |
| --- | --- |
| Routing | Existing ActorSubject, EntityId and TradeSelection bounded context |
| Workflow | WorkflowId, current input revision, running TradeSelection stage, frozen binding and workflow deadline |
| ITI trigger | Original immutable FuturesItiSignalGeneratedEvent, ID, signal classification, underlying and target timeframe |
| Accepted regime | Complete accepted result envelope and typed RegimeDiscoveryResult, source identity/hash/schema/parameters, as-of and produced timestamps |
| Accepted assessment | Complete accepted result envelope and typed MarketConditionAssessmentResult, exact regime lineage, profile/parameters, target horizon and validity |
| Portfolio/Fund | Complete frozen snapshot, assignment and financial permission provenance; no latest query at evaluation |
| Reference/template | Exact family row, template and construction descriptor from binding |
| Selection policy | Complete typed parameter set, exact ID/version/hash and publication evidence |
| Clock | Injected TimeProvider for receipt/commit checks; persisted evaluation timestamp for deterministic evaluation |

Preserve the full accepted upstream envelopes. Selector decision fields read from `RegimeDiscoveryResult.Decision`: Direction, Confidence, Quality, Restrictions, TrendPhase, TrendStrength, VolatilityLevel, VolatilityChange and StructureClassification. DirectionalScore, RiskAdjustedConviction, TrendTimeFrameAgreement, TermStructure, Breakout and specialist results remain evidence in V1; no undocumented numerical gate is derived from them.

Assessment fields used are Availability, UpstreamContext, ConditionType, AssessmentConfidence, LiquidityCondition, SessionState, EventRiskState, StressState, VolatilityBehavior, TriggerAlignment, DataQuality, InheritedRestrictions and validity. EvidenceItems, ConflictingEvidenceItems and LimitationReasons are preserved. UpstreamContext SHALL equal the accepted regime decision, and inherited restrictions SHALL match exactly under existing acceptance rules.

No new external market data is needed by the selector. Raw quote thresholds, FMP download age, event windows and treasury inputs remain upstream-owned; selector parameters consume their resulting classifications instead of repeating their calculations.

### 6.1 Append-only command and workflow changes

Retain Start command keys 0-13 unchanged. Append key 14 `SelectionBinding : TradeSelectionBinding?` and key 15 `SelectionSchemaVersion : short`. New starts require binding and explicit value 1. An old payload missing key 15 decodes to 0 and fails with `TS.CONTRACT.SCHEMA`; constructor defaults must not promote it to 1.

Append `SelectionBinding` to workflow view key 27 and workflow state key 23, leaving existing fields intact. Add it to state/view mapping, snapshots, replay, dispatch and request validation. These are the next free keys verified in the source at this revision; future implementation SHALL recheck collisions before appending. Do not reuse legacy MarketCondition fields.

The command binding hash SHALL match the accepted workflow binding. The workflow state supplies both accepted upstream envelopes; a duplicate mutable copy in the command is unnecessary. The actor retains an immutable validated evaluation input privately.

## 7. Parameter schema

`TradeSelectionParameterSet` uses these MessagePack keys; JSON names are the exact field names below. Every field is required, including explicit empty sets where allowed. Published payloads contain no PortfolioId, FundId, exact contract IDs, strikes, prices or final quantities.

| Key | Field / type | Default or rule |
| --- | --- | --- |
| 0 | SchemaVersion / short | 1 |
| 1 | ParameterSetId / Guid | Assigned exact ID; no Guid.Empty default |
| 2 | Version / int | 1 for initial profiles; positive |
| 3 | ProfileCode / string | One of section 2 codes; 1-128 characters |
| 4 | InstrumentRoot / string | ES |
| 5 | TargetHorizon / TimeFrameType | Daily, Weekly or Monthly; never inferred |
| 6 | StructureVariant / SelectionStructureVariant | Matches template |
| 7 | MinimumRegimeConfidence / decimal | 0.50; inclusive [0,1] |
| 8 | MinimumAssessmentConfidence / decimal | 0.50; inclusive [0,1] |
| 9 | AllowedRegimeDirections / RegimeDirection[] | Up, Down |
| 10 | AllowedTrendPhases / TrendRegimePhase[] | See section 8 |
| 11 | AllowedTrendStrengths / TrendRegimeStrength[] | See section 8 |
| 12 | AllowedRegimeQualities / RegimeOverallQuality[] | Acceptable, High |
| 13 | AllowedRegimeVolatilityLevels / VolatilityRegimeLevel[] | Low, Normal, High |
| 14 | AllowedRegimeVolatilityChanges / VolatilityRegimeChange[] | Contracting, Stable, Expanding |
| 15 | AllowedStructureClassifications / MarketStructureClassification[] | See section 8 |
| 16 | RejectedInheritedRestrictions / RegimeRestriction[] | NoNewTrade, DirectionConflict, LowConfidence, Transition |
| 17 | AllowedAssessmentConditions / AssessmentCondition[] | See section 8 |
| 18 | AllowedLiquidity / AssessmentLiquidity[] | Healthy, Degraded |
| 19 | AllowedSessions / MarketSessionStatus[] | Open |
| 20 | AllowedEventRisk / AssessmentEventContext[] | Clear |
| 21 | AllowedStress / AssessmentStress[] | Normal |
| 22 | AllowedVolatilityBehavior / AssessmentVolatility[] | See section 8 |
| 23 | AllowedTriggerAlignment / AssessmentTriggerAlignment[] | Aligned, Neutral, NotApplicable |
| 24 | AllowedAssessmentDataQuality / MarketConditionDataQuality[] | Healthy, Degraded |
| 25 | UnknownEvidencePolicy / enum | NoTrade = 1, the only V1 supported value |
| 26 | MaximumExecutionMilliseconds / int | 2000; inclusive [1,60000] ms |
| 27 | ResultLifetimeSeconds / int | 30; inclusive [1,300] seconds |
| 28 | FutureClockSkewSeconds / int | 2; inclusive [0,60] seconds |
| 29 | MaximumResultPayloadBytes / int | 262144; inclusive [65536,1048576] bytes |
| 30 | ReasonCodeCatalogVersion / string | ts-reasons-v1 |
| 31 | SummaryTemplateVersion / string | ts-summary-v1 |
| 32 | DirectionMappingVersion / string | ts-direction-v1 |

No ranking, score weights, minimum DirectionalScore, separate opportunity strength or cross-horizon threshold is needed in V1. CompatibilityScore is null. SelectionConfidence is the minimum of the two accepted confidence values, rounded to six decimal places with MidpointRounding.ToEven for display/result only; comparisons use unrounded decimal inputs. This is a deterministic evidence summary, not a probability of profit.

All allowed sets SHALL be non-empty, duplicate-free and contain defined non-sentinel values. The sole V1 UnknownEvidencePolicy value is 1; zero is invalid. Reject None/Unknown/Undefined where those indicate unavailable evidence; permit Neutral, NotApplicable and other genuine observations only where explicitly listed. `RejectedInheritedRestrictions` SHALL include NoNewTrade; it cannot be removed. Unknown numeric restrictions always fail contract validation. `RegimeRestriction.None` is a defined absence of restriction and is never a rejecting rule.

The suspended baseline covered only the three structure variants and direction pairs above; the catalog-aligned specification must replace that closed set. Changing a threshold or permitted set requires a new published version. Removing a restriction other than NoNewTrade is an explicit policy change, not a runtime fallback. Field ranges, byte limits and cross-field horizon/variant rules remain mandatory even for test profiles.

## 8. Complete default profiles

All three profiles contain every field in section 7. Use the shared defaults there plus exactly these overrides; omitted override cells mean the shared default, not absent JSON fields.

| Field | Daily | Weekly | Monthly |
| --- | --- | --- | --- |
| ProfileCode | TS.ES.Daily.Test | TS.ES.Weekly.Test | TS.ES.Monthly.Test |
| TargetHorizon | Daily | Weekly | Monthly |
| StructureVariant | DailyOutright | WeeklyDebitVertical | MonthlyDirectionalCreditCondor |
| AllowedTrendPhases | Emerging, Established | Emerging, Established | Established |
| AllowedTrendStrengths | Moderate, Strong, Extreme | Moderate, Strong | Moderate, Strong |
| AllowedStructureClassifications | Trending, Expanding, BreakingOut | Trending, Expanding, BreakingOut | Trending, Ranging, Compressing |
| AllowedAssessmentConditions | Directional, VolatilityExpansion | Directional, VolatilityContraction | Directional, VolatilityContraction |
| AllowedVolatilityBehavior | Stable, Expanding, Contracting | Stable, Contracting | Stable, Contracting |

Parameter IDs SHALL be generated once when authoring the three version-1 rows and persisted in the exact Fund assignments. There is no prescribed production GUID and no assumed existing database seed. Repeat installation must use its saved IDs and cannot generate new IDs on every startup. Version-1 factory output must contain the complete parameter set and pass publication validation.

Each template supports both directions via the explicit mapping; no six-profile duplication is needed. The option profiles deliberately reject expanding/shock volatility by default as an engineering fixture choice. These numbers and categorical choices are adjustable test defaults and do not claim empirically superior strategies.

The one-assignment cardinality, authority hashes, NoNewTrade prohibition and required-field checks are invariants, not tuning switches. A test default never overrides a Fund prohibition or extends an expired assessment.

## 9. Parameter persistence, publication and hashing

Use the existing PostgreSQL `reference_configuration.trade_selection_parameter_set` table and `StrategyParameterSetKind.TradeSelection`. Retain its `(parameter_set_id, version)` identity and configuration metadata. Add typed `InsertTradeSelectionDraftAsync`, `GetTradeSelectionVersionAsync` and exact `ResolveTradeSelectionVersionAsync(id, version, effectiveAtUtc)` contracts to ConfigurationDb. These are required additions; the enum/table alone does not implement the payload resolver.

Rows follow Draft -> Published -> Retired. Draft is not executable; publication validates the entire typed payload and hash. Published content is immutable. Parameter changes create a new version; retirement blocks new binding resolution but does not rewrite an accepted binding. An emergency stop uses separate cancellation authority. Guard lifecycle transitions in storage and require exactly one affected row. Reject destructive replacement, duplicate identity with different content, malformed versions and missing audit provenance.

Resolve by the Fund assignment's exact ID/version, not latest for a horizon. Confirm Published and effective at FrozenAtUtc, plus matching root, horizon and variant. Overlap between published versions is not ambiguous because the assignment pins one version. If any authoring tool offers effective selection, it must first resolve and save an explicit assignment before a workflow starts.

### 9.1 Hash contracts

| Payload | Exact hash authority |
| --- | --- |
| Existing upstream stage envelope | Existing SHA-256 of exact MessagePack payload bytes; preserve bytes and metadata |
| Existing Portfolio snapshot | Existing `PortfolioCanonicalHash.Compute(snapshot with PayloadSha256 empty)`; preserve camelCase JSON and existing lower-case digest |
| New selection parameters/template | Canonical typed JSON below; SHA-256 UTF-8, uppercase hexadecimal |
| New selection binding | SHA-256 of exact MessagePack schema-1 binding with its PayloadSha256 empty; normalize new set-valued fields before sealing |
| TradeSelection result envelope | Existing StrategyStageResultEnvelope SHA-256 over exact result MessagePack bytes |

Do not rehash a Portfolio snapshot using the selector's JSON settings or replace an upstream hash with a hash of a reconstructed summary. Validate hex digests as 32 bytes; preserve source casing in carried fields. Binding/result identity comparison uses decoded hash bytes, not casing-sensitive string equality.

Canonical selector JSON uses explicit property order equal to the key order, exact PascalCase field names, numeric enum values, unindented UTF-8, no ignored/null omissions, canonical GUID D form, UTC round-trip timestamps, and invariant decimal G29 formatting. Set-valued arrays are sorted by underlying enum value before serialization; duplicates are rejected first. No serializer-dependent dictionary ordering is allowed. Reject unknown properties, duplicate properties, unknown enums and omitted fields on authoring/deserialization. Persist golden byte/hash vectors as tests.

Normalize only the new selector objects before publication. Do not reorder existing Portfolio arrays or accepted upstream evidence while preserving their source hashes. The binding's frozen Portfolio snapshot must remain byte/semantic equivalent to its authoritative hashed form.

## 10. Validation and deterministic evaluation

### 10.1 Validation order

1. Reject malformed transport, unsupported schema, unknown keys, oversized request, wrong subject/stage/entity or invalid IDs. Do not create an actor stream for a message whose routing identity cannot be validated.
2. Check duplicate identity before evaluating. Identical committed input returns the recorded outcome; changed-payload reuse is a conflict, never a second terminal event.
3. Validate running workflow, exact invocation revision and binding equality against the accepted workflow snapshot. Require valid original trigger and supported target horizon.
4. Validate binding, Portfolio snapshot hash and all identity/version/interval relationships; exact template/profile/family/product consistency; exactly one effective assignment.
5. Reuse existing assessment acceptance validation for full regime lineage, profile binding, hashes, immutable context and inherited restrictions. Require completed accepted regime/assessment and Available/current assessment with no inherited NoNewTrade.
6. Validate types/ranges of every selector-consumed field, including both confidence values in [0,1], known enum numbers and valid timestamps. No current-data reread is performed.
7. Record Processing acceptance and a stable EvaluatedAtUtc. Run the pure evaluator with the frozen input and parameter set.
8. Assemble result, validate invariants and recheck deadline/validity at commit. Commit exactly one terminal event and publication intent.

NoNewTrade/unavailable assessments should have stopped before selection. A direct caller bypassing this rule receives `TS.UPSTREAM.NOT_ELIGIBLE` failure; it does not obtain a fabricated selection or normal candidate evaluation. The workflow's upstream normal NoTrade semantics remain unchanged.

### 10.2 Rule order and NoTrade reasons

After structural validation, evaluate all applicable rules in this fixed order. Preserve every rejection, and use the first rejection as PrimaryReasonCode. Do not stop after one ordinary incompatibility, but do stop on contract/calculation failure. Rule values come only from the frozen binding and accepted results.

| Rule | Predicate required to pass | Rejection code |
| --- | --- | --- |
| R01 | Frozen Portfolio/Fund operating permission allows new selection | TS.PERMISSION.OPERATING_STATE |
| R02 | Frozen financial policy and delegated envelope permit new exposure; no live sizing | TS.PERMISSION.ENVELOPE |
| R03 | Family row Active, template Enabled and assignment Enabled | TS.PERMISSION.DISABLED |
| R04 | Exact family reference and product class/root allowed by Fund and Portfolio family permission | TS.PERMISSION.FAMILY |
| R05 | Mapped direction permitted by template and Fund | TS.PERMISSION.DIRECTION |
| R06 | Assessment condition allowed by Fund mandate | TS.PERMISSION.CONDITION |
| R07 | No rejected inherited restriction present | TS.REGIME.RESTRICTION |
| R08 | Regime direction in AllowedRegimeDirections | TS.REGIME.DIRECTION |
| R09 | Regime confidence >= MinimumRegimeConfidence | TS.REGIME.CONFIDENCE |
| R10 | Regime quality in AllowedRegimeQualities | TS.REGIME.QUALITY |
| R11 | Trend phase in AllowedTrendPhases | TS.REGIME.PHASE |
| R12 | Trend strength in AllowedTrendStrengths | TS.REGIME.STRENGTH |
| R13 | Regime volatility level in AllowedRegimeVolatilityLevels | TS.REGIME.VOLATILITY_LEVEL |
| R14 | Regime volatility change in AllowedRegimeVolatilityChanges | TS.REGIME.VOLATILITY_CHANGE |
| R15 | Structure classification in AllowedStructureClassifications | TS.REGIME.STRUCTURE |
| R16 | Assessment confidence >= MinimumAssessmentConfidence | TS.ASSESSMENT.CONFIDENCE |
| R17 | ConditionType in AllowedAssessmentConditions | TS.ASSESSMENT.CONDITION |
| R18 | LiquidityCondition in AllowedLiquidity | TS.ASSESSMENT.LIQUIDITY |
| R19 | SessionState in AllowedSessions | TS.ASSESSMENT.SESSION |
| R20 | EventRiskState in AllowedEventRisk | TS.ASSESSMENT.EVENT |
| R21 | StressState in AllowedStress | TS.ASSESSMENT.STRESS |
| R22 | VolatilityBehavior in AllowedVolatilityBehavior | TS.ASSESSMENT.VOLATILITY |
| R23 | TriggerAlignment in AllowedTriggerAlignment | TS.ASSESSMENT.TRIGGER |
| R24 | DataQuality in AllowedAssessmentDataQuality | TS.ASSESSMENT.DATA_QUALITY |

Fund permitted directions use their existing Bullish/Bearish or Up/Down vocabulary. The explicit normalization is Up/Long/Bullish -> Bullish and Down/Short/Bearish -> Bearish; Neutral stays Neutral. Validate configuration strings against that finite vocabulary. All three initial templates reject Neutral regardless of broader Fund permission. An observed Neutral produces R08 NoTrade; do not invent a bullish or bearish bias. R05 is not applicable when no initial direction mapping exists, so the primary explanation is R08.

An empty valid Fund permission set permits nothing. An unknown permission string is configuration invalid. Known deny states are business NoTrade, while an absent required policy/family rule or malformed envelope is Failed. Restriction None is informational; NoNewTrade remains a mandatory upstream entry prohibition.

### 10.3 Missing and unknown values

| Situation | Required result |
| --- | --- |
| Missing required object, required confidence, hash, identity, date or ConditionType for Available assessment | Failed: TS.CONTRACT.REQUIRED_FIELD or TS.UPSTREAM.INVALID |
| Out-of-range confidence or unrecognized enum number | Failed: TS.CONTRACT.VALUE_RANGE |
| Known Unknown value in an otherwise accepted optional classification used by R10-R24 | NoTrade: TS.EVIDENCE.UNKNOWN at that rule position, recording exact field |
| Defined None trend strength | Evaluate R12 membership; initial defaults reject it |
| Known unfavorable classification such as Poor/Closed/Elevated/Unusable | Corresponding ordinary rule rejection |
| Optional source absent but no selector rule depends on an unknown derived value | Preserve limitation; no extra implicit rejection |
| Pure evaluator exception | Failed: TS.CALCULATION.FAILED |

Thus an Available assessment can reach selection with a closed session or poor liquidity, and the default selector can return NoTrade. MarketCondition does not acquire these template policies. No field is filled from another timeframe or replaced with zero to make evaluation pass.

## 11. Time and expiry

At receipt require `now < ExpectedCompletionAtUtc` and `now < binding.ValidUntilUtc` and `now < assessment.ValidUntilUtc`. Proposed stage deadline is the minimum of workflow deadline, RequestedAtUtc + MaximumExecutionMilliseconds, binding validity and assessment validity. The workflow writes this value into ExpectedCompletionAtUtc; the actor independently verifies it.

FutureClockSkewSeconds applies only to observed/produced/evaluation timestamps from clocks, not to expiry. Reject timestamps more than the configured skew into the future. It never grants extra lifetime beyond an expired authority or workflow deadline. Regime as-of/production relationships remain governed by existing accepted contracts; do not invent a RegimeDiscovery ValidUntil field or require a fresh regime lookup.

```text
SelectionValidUntilUtc = min(
    EvaluatedAtUtc + ResultLifetimeSeconds,
    AcceptedAssessment.ValidUntilUtc,
    SelectionBinding.ValidUntilUtc,
    WorkflowDeadlineUtc)
```

Execution deadline limits committing the decision; result validity limits subsequent consumption. Do not cap an already committed result to a now-expired execution deadline if its separately computed validity remains current. Recheck both relevant limits at their boundaries. If no positive result lifetime remains, commit Failed with TS.TIME.EXPIRED; never publish Selected and extend it downstream.

Use injected TimeProvider for actor/workflow checks and a persisted evaluation instant for the pure evaluator. ElapsedMilliseconds is measured monotonically and is non-negative; clocks do not affect outcome beyond explicit validity rules.

## 12. Typed selection result

New enums are byte-backed and explicit:

```text
SelectionOutcome: Unknown=0, Selected=1, NoTrade=2
SelectionFamily: None=0, Future=1, OptionVertical=2, IronCondor=3
SelectionInstrumentClass: None=0, Futures=1, FuturesOptions=2
SelectionDirection: Undefined=0, Long=1, Short=2, Bullish=3, Bearish=4
SelectionStructureVariant: Unspecified=0, DailyOutright=1,
    WeeklyDebitVertical=2, MonthlyDirectionalCreditCondor=3
SelectionRuleStatus: NotApplicable=0, Passed=1, Rejected=2
UnknownEvidencePolicy: NoTrade=1
```

These are new selector transport enums, not replacements for Reference family/strategy enums. The section 2 mapping is explicit. New parameter/result schema version is 1 even though the document is v1.0 and existing upstream schemas have other versions.

### 12.1 `TradeSelectionResult` keys

| Key | Field / type | Invariant |
| --- | --- | --- |
| 0 | SchemaVersion / short | 1 |
| 1 | ResultId / Guid | Stable terminal result identity |
| 2 | WorkflowId / StrategyWorkflowId | Same workflow |
| 3 | EntityId / IntrinsicTimeStrategyWorkflowEntityId | Same routing entity |
| 4 | InvocationId / Guid | Equal to accepted Start CommandId |
| 5 | InputWorkflowRevision / long | Accepted selection invocation revision |
| 6 | TriggerEventId / Guid | Original accepted trigger identity, using existing Id/CommandId fallback |
| 7 | PortfolioId / int | Frozen authority |
| 8 | FundId / int | Frozen authority |
| 9 | DecisionHorizon / TimeFrameType | Single trigger horizon on both outcomes |
| 10 | SelectionOutcome / SelectionOutcome | Selected or NoTrade only |
| 11 | TradeTemplateId / Guid? | Required Selected; null NoTrade |
| 12 | TradeTemplateVersion / long? | Required Selected; null NoTrade |
| 13 | TradeFamilyReference / TradeStrategyFamilyReference? | Required Selected; null NoTrade |
| 14 | TradeFamily / SelectionFamily | Selected family; None NoTrade |
| 15 | InstrumentClass / SelectionInstrumentClass | Selected class; None NoTrade |
| 16 | DirectionalBias / SelectionDirection | Explicit mapped direction; Undefined NoTrade |
| 17 | StructureVariant / SelectionStructureVariant | Assigned variant; Unspecified NoTrade |
| 18 | CompositionPolicyId / Guid? | Same assigned OrderComposition profile; null NoTrade |
| 19 | CompositionPolicyVersion / long? | Exact profile version; null NoTrade |
| 20 | CompositionPolicyPayloadSha256 / string? | Descriptor's exact profile hash; null NoTrade |
| 21 | DecisionContext / TradeSelectionDecisionContext | Full immutable context below |
| 22 | SelectionConfidence / decimal | min(regime, assessment), rounded as section 7, both outcomes |
| 23 | CompatibilityScore / decimal? | Always null in V1 binary policy |
| 24 | PrimaryReasonCode / string | TS.SELECTED or first ordered rejection |
| 25 | Evidence / SelectionRuleEvidence[] | Ordered R01-R24 evaluation trace |
| 26 | EvaluatedAtUtc / DateTime | Persisted pure evaluation instant |
| 27 | ProducedAtUtc / DateTime | Terminal assembly time, before expiry |
| 28 | ValidUntilUtc / DateTime | Section 11 bound |
| 29 | ParameterSetId / Guid | Frozen selection policy identity on both outcomes |
| 30 | ParameterSetVersion / int | Exact version on both outcomes |
| 31 | ParameterPayloadSha256 / string | Frozen policy hash on both outcomes |
| 32 | SelectedProduct / SelectionProductReference? | Product metadata for Selected; null NoTrade |
| 33 | SummaryText / string | Deterministic template below; maximum 2048 characters |

The table above is the wire schema; there is no implicit runtime conversion from an old candidate list. NoTrade can preserve observed direction inside DecisionContext while all selected-only direction/family/product fields remain absent or sentinel-valued.

`TradeSelectionDecisionContext` keys: 0 SchemaVersion(short=1), 1 RegimeResultEnvelope(existing type), 2 AssessmentResultEnvelope(existing type), 3 SelectionBinding(full frozen binding). It contains the complete accepted inputs and authority, not only latest-result lookup keys. Reuse immutable objects internally but preserve the full serialized contract.

`SelectionProductReference` keys: 0 TradeStrategySymbolId(int), 1 Symbol(string), 2 Exchange(string), 3 Currency(string). All values exactly match the frozen family/product identity; the product is not an expiry-specific instrument.

`SelectionRuleEvidence` keys: 0 RuleId(string), 1 FieldPath(string), 2 Status(SelectionRuleStatus), 3 ActualJson(string), 4 ExpectedJson(string), 5 ReasonCode(string). Canonical JSON scalars/arrays make values machine-readable, preserving numbers as numbers and enum names as exact strings. FieldPath references the binding/regime/assessment object. Array values are ordered and no dynamic explanatory prose is interpreted by downstream code. Each row is bounded to 4096 bytes; exceeding any evidence or result bound fails instead of truncating authority.

### 12.2 Result invariants

Selected requires exactly the evaluated authorized template, family, product, direction and construction descriptor. Every one must agree with the assignment and binding, and all required rules must have passed. NoTrade requires at least one Rejected rule; its PrimaryReasonCode is the first rejection. An evaluated template remains visible only in context/evidence, not selected-only fields. Failed is a lifecycle event, never a third successful selection outcome.

Envelope ResultType is `TradeSelectionResult`, SchemaVersion 1 and ContentType `application/x-msgpack`. Its ResultId equals payload ResultId; ProducedAtUtc equals payload ProducedAtUtc. MarketDataAsOfUtc preserves the accepted assessment envelope timestamp. Compute PayloadSha256 from exact bytes using existing envelope functions.

The default generic envelope cap is currently 65536 bytes. This result carries full upstream context, so all TradeSelection producers, validators, persistence and consumers SHALL use the explicit parameter MaximumResultPayloadBytes, initially 262144. Do not globally enlarge other stages' limits. Transport request/event payload limit is 1048576 bytes for this stage; reject oversize before dispatch/commit. Configured result size must fit the transport after outer serialization. Never truncate upstream context to squeeze under a limit.

Deterministic summaries use invariant formatting and exact enum/code strings:

```text
Selected: "{Horizon} {Root}: selected {TemplateCode} ({Direction}); confidence {Confidence:F6}."
NoTrade:  "{Horizon} {Root}: NoTrade ({PrimaryReasonCode}); {RejectedCount} rule(s) rejected."
```

NoTrade confidence describes available evidence, not a probability of choosing no trade. Display summaries cannot supply missing typed fields or change continuation.

## 13. Actor identity, lifecycle and durable delivery

Implement `TradeSelectionPipelineCommandActor` at existing Command actor name `TradeSelectionPipelineCommand`, Start verb and TradeSelectionPipelineBoundedContext. Retain entity routing; store invocation state keyed by WorkflowId and InputWorkflowRevision within the routed entity stream. Define InvocationId = Start CommandId, preserving the workflow's existing deterministic command-identity generation. Do not create a competing unrelated StageInvocationId.

An internal persisted `TradeSelectionInvocationAccepted` record SHALL include accepted immutable evaluation input, command business-payload hash, start time, EvaluatedAtUtc, deadline, expected stream version and allocated result/Processing/terminal event IDs. Generate UUIDv7 IDs once at acceptance and persist them. Crash recovery reuses them. Command business-payload hash covers schema, EntityId, WorkflowId, InputWorkflowRevision, original trigger, binding hash, accepted upstream envelope hashes, RequestedAtUtc and ExpectedCompletionAtUtc in that order. Exclude diagnostics and delivery timestamps; correlation/causation must still match accepted workflow lineage.

Use existing event keys for `TradeSelectionPipelineProcessingEvent`, `TradeSelectionPipelineCompletedEvent` and `TradeSelectionPipelineFailedEvent`. Processing is the existing lifecycle name; do not introduce Started. Completed carries the result through its existing Result key. Failed uses existing metadata plus stable machine-readable failure data defined below. Do not invent new wire event IDs or reuse the existing ErrorId constants for individual business reasons.

| Current persisted state | Input | Transition/action |
| --- | --- | --- |
| Absent | Valid Start | Append accepted input and Processing publication intent atomically; evaluate |
| Absent | Routable but invalid Start | Persist one Failed rejection with command identity/hash; no Processing required |
| Processing | Same command/hash redelivery or restart | Resume from persisted input/evaluation time if current; otherwise commit one expiry failure |
| Processing | Evaluation succeeds and commit checks pass | Append Completed and publication intent with optimistic concurrency |
| Processing | Calculation/validation/deadline fails | Append Failed and publication intent with optimistic concurrency |
| Completed/Failed | Same command/hash | Return recorded acceptance/outcome; recover delivery if required; never recalculate |
| Any existing invocation | Same identity, different business hash | Reject caller and emit diagnostic TS.INVOCATION.CONFLICT; do not mutate original outcome |

Restarting an uncommitted pure calculation from persisted inputs is technical recovery, not a new strategy workflow or business retry. An optimistic append conflict reloads state and returns the committed winner. No callback may publish a losing terminal event.

### 13.1 Persistence and publication ordering

Use the existing event-source infrastructure and durable EventProjector mechanism. Terminal actor event append is authoritative. Persist event and projection/publication work in the same event-source transaction. Complete the command acknowledgement only after durable acceptance/terminal storage appropriate to that command API, never after a best-effort in-memory enqueue alone.

The projector SHALL idempotently update Scylla read models before publishing the same logical event through the existing TradeSelection Realtime route to the Strategy Workflow. Projection/publication failure leaves durable work pending. Retried delivery reuses event ID and payload hash. A crash after publish but before marking work complete can duplicate delivery; workflow acceptance must deduplicate it. Physical exactly-once network delivery is not assumed.

Require durable projection for lifecycle events using the repository's `IRequireDurableProjection` integration or the equivalent registered durable work path, verified by integration tests. Scylla failure cannot erase a committed selection or cause a second evaluation. Rebuilding projections reads committed events; projections never determine the actor's outcome.

Persist private input/state in PostgreSQL event-source storage. Scylla is query/history storage; Redis, if later added, is not required and is not decision authority. No cross-database transaction is assumed. Operational projection delays may make an otherwise correct result expire before workflow acceptance; stop under expiry rules without changing the original decision.

### 13.2 Failure data and timeout

Keep the shared Failed event ErrorId/route constants. Encode structured safe ErrorData as JSON with `schemaVersion=1`, `reasonCode`, `fieldPath`, `invocationId`, `inputPayloadSha256`, `parameterSetId`, `parameterSetVersion`, `failedAtUtc`, `elapsedMilliseconds`; omit unavailable optional identity values on malformed input. ErrorMessage is a bounded operator explanation, not the authoritative reason.

Stable failure reasons: TS.CONTRACT.SCHEMA, TS.CONTRACT.REQUIRED_FIELD, TS.CONTRACT.VALUE_RANGE, TS.CONTRACT.IDENTITY, TS.CONTRACT.HASH, TS.CONTRACT.PAYLOAD_SIZE, TS.CONFIG.MISSING, TS.CONFIG.AMBIGUOUS_ASSIGNMENT, TS.CONFIG.PROFILE_MISMATCH, TS.CONFIG.INVALID, TS.UPSTREAM.INVALID, TS.UPSTREAM.NOT_ELIGIBLE, TS.TIME.FUTURE, TS.TIME.EXPIRED, TS.CALCULATION.FAILED, TS.RESULT.INVALID and TS.INVOCATION.CONFLICT. Stable success reason: TS.SELECTED. Ordinary NoTrade reasons are section 10, including TS.EVIDENCE.UNKNOWN.

V1 adds no separate user cancellation command for TradeSelection. Workflow stop/timeout and late-event guards remain authoritative. A later cancellation feature needs an explicit atomic state transition; no optional undefined race semantics are required to implement V1.

## 14. Workflow acceptance and composition reservation

`CompleteTradeSelection` SHALL deserialize and validate the typed result before advancing. Its existing generic-success behavior is insufficient. Require correct source event, workflow/entity/stage/revision, exact invocation, valid envelope/hash, exact frozen binding, accepted upstream context, parameter versions and all section 12 invariants. The workflow SHALL re-run the pure selector against the frozen inputs and persisted EvaluatedAtUtc to verify outcome/evidence; it SHALL NOT fetch new inputs or create another business invocation. Recompute validity bounds and perform current-time expiry checks separately.

| Event/result | Workflow action |
| --- | --- |
| Valid Selected, current workflow | Accept selection once; persist composition-reservation intent; do not dispatch builder yet |
| Valid NoTrade | Complete workflow with NoTrade and ordered reason; no reservation or builder |
| Expired assessment/binding/selection or workflow deadline | Stop with expiry/timeout reason; no dispatch |
| Malformed, inconsistent or wrong-input result | Fail contract; no reservation or builder |
| Failed event | Stop as failed unless workflow deadline already won |
| Duplicate source event or already terminal workflow | No second transition or side effect |

Recomputing validation uses the existing selected input bytes, not regenerated upstream facts. Full result equality excludes processing-duration metadata and uses the deterministic fields/evidence defined here. A runtime must not accept a plausible-looking Selected result whose rules actually reject.

### 14.1 Pending reservation state

Add `CompositionHandoff : WorkflowCompositionHandoffState?` at workflow view key 28 and state key 24, following the new SelectionBinding keys. This is an internal workflow substate, not a sixth decision operator.

`WorkflowCompositionHandoffState` keys: 0 Status(enum None=0, ReservationPending=1, Reserved=2, Stopped=3), 1 SelectionSourceEventId(Guid), 2 AcceptedSelectionRevision(long), 3 Request(ReserveFundOrderCompositionRequest), 4 Reservation(FundCompositionReservationResult?), 5 ReservationRequestSha256(string), 6 UpdatedAtUtc(DateTime). Persist a durable dispatch intent with the transition; do not call Portfolio and hope to save the result later.

Accepting Selected advances workflow revision once and retains CurrentStage=TradeSelection with its processing Completed and reservation pending. Only a validated committed reservation advances to OrderComposition at another revision. This permits recovery after selection acceptance without redispatching selection or prematurely running a builder.

### 14.2 Request mapping

Use the existing `ReserveFundOrderCompositionRequest` and `FundCompositionReservationResult`. Map fields as follows:

| Request field | Value |
| --- | --- |
| WorkflowId | Current workflow's Guid value |
| WorkflowRevision | Frozen PortfolioSnapshot.WorkflowRevision, as required by the existing Portfolio contract |
| TradeSelectionInvocationId | Accepted result InvocationId (= original Start CommandId) |
| TradeSelectionResultId / SHA256 | Exact accepted result envelope identity/hash |
| Portfolio/Fund identities and versions | Frozen snapshot values |
| TradeTemplate ID/version | Selected template |
| OrderCompositionProfile ID/version | Selected construction policy |
| UnderlyingRoot / DecisionHorizon | Frozen ES root and trigger horizon |
| RequestedTradeDate | Binding.RequestedTradeDate, computed from TriggerEvent.CreatedOn under the explicit test date policy |
| RequestedMaturityDate | Null; selector has not selected an expiration |
| TradeInstructions | Exactly one Primary instruction for the selected strategy intent |
| Origin | CompositionOrigin.StrategyWorkflow (1) |
| IdempotencyKey | Accepted TradeSelection ResultId |
| RequestedAtUtc | Persisted reservation-intent creation time |
| ExpiresAtUtc | min(selection validity, binding validity, workflow deadline) |
| PortfolioFundStrategySnapshotSha256 | Exact original snapshot hash, with original casing |

The request's WorkflowRevision is the snapshot revision for this existing Portfolio contract. Current continuation revision is stored separately in WorkflowCompositionHandoffState.AcceptedSelectionRevision. Do not overwrite the snapshot revision/hash to satisfy Portfolio validation. Reservation callbacks are fenced against the current pending handoff, accepted selection ID/hash and current workflow revision.

The one Primary TradeInstruction has the selected catalog trade-family string, TradeRole=Primary, IsPrimaryTrade=true, selected DirectionOrBias, TradeAction=Open, ES root, the same requested trade date, null maturity, selection ResultId formatted D as Reference, persisted RequestedAtUtc as CreatedOnUtc and authenticated service principal as CreatedBy. It reserves one OrderId and one TradeId for a strategy instruction, not one TradeId per option leg. Exact leg construction follows later.

The initial engineering policy is exactly `UTC.TriggerCreatedDate.Test.v1`: require non-default UTC TriggerEvent.CreatedOn and set RequestedTradeDate = DateOnly.FromDateTime(TriggerEvent.CreatedOn). Freeze both date and policy in the binding; no receipt-time fallback or live calendar lookup is allowed. This is an explicit test date convention, not a claim to calculate the exchange trading-session date. A later exchange-session date policy requires a versioned binding schema/policy extension and qualified calendar data. It does not block implementing these complete initial test inputs.

### 14.3 Reservation recovery and downstream input

On a timeout with unknown Portfolio response, query/retry the identical saved request with the same idempotency key while current. Do not regenerate timestamps, request hash or IDs. The existing Portfolio service returns the committed reservation for identical replay and rejects changed-payload reuse. This is recovery of one logical side effect, not new selection.

After response, validate Portfolio/Fund/template/profile/result bindings, committed reservation identity, positive integer OrderId/TradeId and exactly one Primary instruction. If still current, atomically record reservation and the durable StartOrderComposition intent. Pass accepted selection unchanged, complete frozen Portfolio snapshot/binding, reservation and workflow deadline. Append versioned fields to the existing OrderComposition Start contract without changing existing keys; its detailed builder contract remains the authority for live construction data.

If the workflow expires/stops while a reservation is pending, it SHALL not dispatch construction on a late response. Reconcile any committed FundOrder to the Portfolio's Expired/Cancelled state using its supported command. Integer IDs are retained and never reused. A permanent Portfolio validation failure stops the workflow; it cannot be downgraded to selecting another template.

OrderComposition returns Composed, NoCandidate or Failed through its own boundary. NoCandidate stops normally. It cannot change selected horizon/family/template or widen permissions to obtain a candidate. Final sizing and risk reservation remain Portfolio Risk Manager responsibilities after one-unit composition.

## 15. Read models and query contracts

Add selector projection methods to `ITradeDbContext`/TradeDb and Scylla schema initialization, plus typed queries through the existing actor/query API pattern. These are proposed read models; no table is created by this document.

Project immutable lifecycle rows keyed by workflow and invocation; query current actor status by the largest persisted source stream version. Do not implement last-arriving-message-wins. Processing replay after Completed cannot make status regress.

```sql
CREATE TABLE IF NOT EXISTS trade_selection_invocation_event (
    workflow_id uuid,
    invocation_id uuid,
    source_sequence bigint,
    event_id uuid,
    portfolio_id int,
    fund_id int,
    target_horizon smallint,
    lifecycle_status tinyint,
    outcome tinyint,
    occurred_at_utc timestamp,
    reason_code text,
    parameter_set_id uuid,
    parameter_version int,
    parameter_sha256 text,
    result_id uuid,
    result_sha256 text,
    result_payload blob,
    event_payload blob,
    PRIMARY KEY ((workflow_id, invocation_id), source_sequence)
) WITH CLUSTERING ORDER BY (source_sequence DESC);

CREATE TABLE IF NOT EXISTS trade_selection_history_by_fund_date (
    portfolio_id int,
    fund_id int,
    value_date date,
    occurred_at_utc timestamp,
    workflow_id uuid,
    invocation_id uuid,
    event_id uuid,
    target_horizon smallint,
    outcome tinyint,
    reason_code text,
    result_id uuid,
    result_sha256 text,
    PRIMARY KEY ((portfolio_id, fund_id, value_date),
                 occurred_at_utc, workflow_id, invocation_id)
) WITH CLUSTERING ORDER BY
    (occurred_at_utc DESC, workflow_id ASC, invocation_id ASC);
```

Use the configured TradeDb keyspace; do not hard-code a deployment keyspace. LifecycleStatus projection values are Processing=1, Completed=2, Failed=3. Outcome is Selected=1 or NoTrade=2 only for Completed; otherwise null. History contains terminal rows only and uses UTC date of the original terminal event for value_date, independently of the trigger-derived RequestedTradeDate. Retried projection uses original timestamps and keys.

The durable projection work item carries immutable routing/Portfolio/Fund/parameter metadata captured at acceptance. Where a standard lifecycle event lacks a field, obtain it from the committed acceptance record in the same invocation stream, never from latest Portfolio configuration. A missing acceptance record for a routable rejected command permits null unavailable metadata in its invocation projection; it cannot fabricate a Fund history row. Source sequence comes from authoritative persisted EventId/stream version, not receive time. Conflicting content under an identical event ID/sequence fails projection diagnostics.

| Query | Inputs | Semantics |
| --- | --- | --- |
| GetTradeSelectionInvocationQuery | WorkflowId, InvocationId | Latest lifecycle row plus typed result/failure if present; absent is NotFound |
| GetTradeSelectionResultQuery | WorkflowId, InvocationId, ResultId | Exact committed result; identity mismatch is NotFound/contract error, never latest substitution |
| GetTradeSelectionHistoryPageQuery | PortfolioId, FundId, UTC ValueDate, PageSize, PagingState? | Terminal history, stable clustering order |

PageSize default is 50, valid range 1-200. Paging tokens bind query scope, schema and page size; wrong-scope tokens fail validation. No ALLOW FILTERING or unbounded date-range scan. Finding history on another date is an explicit query. Initial schema has no automatic TTL; retention is a separate operational policy, not a hidden deletion default.

Queries report eventually consistent actor decisions; they do not assert workflow acceptance, successful reservation or risk approval. Authorization SHALL check Portfolio/Fund access. The underlying full result remains available for audit without exposing sensitive account details in display summaries.

## 16. Observability and operational controls

Record workflow/invocation/source event IDs, Portfolio/Fund identities, target horizon, exact family/template/profile versions, input/result hashes, outcome, primary and all rejection reasons, timestamp/expiry and elapsed milliseconds. Preserve Activity/W3C propagation through existing correlation headers; do not start unrelated traces for projection retry.

Metrics: selection duration, Completed Selected count, Completed NoTrade count by primary reason, failure count by stable category, projection backlog/age, duplicate/conflict count, reservation pending age and late-result discard count. Use bounded labels such as horizon, outcome and reason; workflow IDs and hashes belong in structured logs/traces.

No UI change is required for selector code completion. Read models enable the later observation view to distinguish Available assessment, selection outcome, pending composition reservation and construction/risk status. A Selected result alone is not an executable order or financial approval.

Initial profile seeding is an explicit authoring operation. Create three draft policies, three matching templates and exact assignments using actual existing product/family IDs. Resolve real construction descriptors. Publish only after schema/fixture checks. This specification neither seeds nor publishes them automatically on startup. Repeated setup reuses saved identities and refuses changed-payload collisions.

## 17. Boundary fixtures and numerical verification

Use one accepted, immutable upstream/authority fixture per horizon with valid identities, versions, hashes and one exact assignment. A common positive baseline has Up direction, Established phase, Moderate strength, Acceptable quality, Normal regime volatility, Stable regime volatility change, Trending structure, no restrictions, regime confidence 0.75; Available Directional assessment with confidence 0.80, Healthy liquidity, Open session, Clear event context, Normal stress, Stable volatility, Aligned trigger and Healthy data quality. All timestamps are current relative to an injected clock. Baseline selection confidence is 0.750000.

| Fixture | Change from valid baseline | Expected |
| --- | --- | --- |
| TS-F01 | Daily baseline | Selected Future/Long/DailyOutright |
| TS-F02 | Daily Down | Selected Future/Short |
| TS-F03 | Weekly Up | Selected OptionVertical/Bullish/WeeklyDebitVertical |
| TS-F04 | Weekly Down | Selected OptionVertical/Bearish |
| TS-F05 | Monthly Up | Selected IronCondor/Bullish/MonthlyDirectionalCreditCondor |
| TS-F06 | Monthly Down | Selected IronCondor/Bearish |
| TS-F07 | Both confidence values exactly 0.50 | Selected; confidence 0.500000 |
| TS-F08 | Regime confidence 0.499999, otherwise valid | NoTrade TS.REGIME.CONFIDENCE |
| TS-F09 | Assessment confidence 0.499999 | NoTrade TS.ASSESSMENT.CONFIDENCE |
| TS-F10 | Assessment confidence null or 1.000001 | Failed, not NoTrade |
| TS-F11 | Neutral regime direction | NoTrade TS.REGIME.DIRECTION; selected-only fields absent |
| TS-F12 | Monthly Emerging trend phase | NoTrade TS.REGIME.PHASE |
| TS-F13 | Closed session with Available assessment | NoTrade TS.ASSESSMENT.SESSION |
| TS-F14 | Poor liquidity and Elevated event risk | Both rule rejections; liquidity reason first |
| TS-F15 | Known Unknown StressState | NoTrade TS.EVIDENCE.UNKNOWN with StressState field |
| TS-F16 | Unknown numeric StressState | Failed TS.CONTRACT.VALUE_RANGE |
| TS-F17 | NoNewTrade inherited restriction | Workflow stops before selection; direct invocation fails TS.UPSTREAM.NOT_ELIGIBLE |
| TS-F18 | Duplicate exact SystemKey but different family IDs | Only the assigned exact ID/version qualifies |
| TS-F19 | Zero or two effective assignments | Configuration failure; no arbitrary first choice |
| TS-F20 | Weekly option assignment with futures ITI trigger | Resolves options assignment and selects; no Futures-only filter |
| TS-F21 | Parameter version retired after binding freeze | Existing workflow retains frozen version; new resolution rejects retired row |
| TS-F22 | Disabled assigned template with intact authority | NoTrade TS.PERMISSION.DISABLED |
| TS-F23 | Assessment expired exactly at now | Expiry failure/stop; no skew grace |
| TS-F24 | Upstream schema/hash/context mismatch | Failed TS.UPSTREAM.INVALID; no downstream dispatch |

Each fixture SHALL seal real payloads rather than mocking hash checks. Changes to an upstream decision must update all corresponding accepted envelope hashes, preserved assessment context and workflow references. Tests intended to provoke mismatch SHALL explicitly leave only the target link inconsistent.

Numerical boundary tests additionally cover confidence 0 and 1, lower/upper parameter ranges, G29-equivalent decimals 0.50/0.5, enum set order, duplicate entries, integer overflow converting profile version, 2000 ms exact deadline and 30-second result lifetime capping. At baseline EvaluatedAt=12:00:00Z, assessment expiry=12:00:10Z, binding expiry=12:00:20Z and workflow expiry=12:00:15Z, selected validity is 12:00:10Z. A consumer at exactly that instant cannot continue.

## 18. BDD scenarios

```gherkin
Feature: Single-timeframe authorized trade selection
  Scenario Outline: Select the assigned directional template
    Given a current accepted <horizon> regime and Available assessment
    And one frozen authorized <variant> assignment with both confidences at least 0.50
    When TradeSelection processes that workflow invocation
    Then it completes with Selected for the exact assigned template and family version
    And its direction follows the accepted regime
    And its decision context is unchanged
    Examples:
      | horizon | variant                       |
      | Daily   | DailyOutright                 |
      | Weekly  | WeeklyDebitVertical           |
      | Monthly | MonthlyDirectionalCreditCondor |

  Scenario: Compatible evidence does not override Fund permission
    Given valid accepted market evidence and an intact disabled assignment
    When TradeSelection evaluates the invocation
    Then it completes with NoTrade and TS.PERMISSION.DISABLED
    And no composition reservation or builder is invoked

  Scenario: Accepted Selected requires a committed composition reservation
    Given a valid current Selected result
    When the workflow accepts it
    Then the workflow persists one reservation intent
    And OrderComposition is not dispatched until the reservation is committed
    And replay uses the same idempotency key and frozen snapshot revision

  Scenario: Projection failure does not recalculate a decision
    Given a committed Completed event whose Scylla projection fails
    When durable projection recovers
    Then the same event identity and result hash are projected and published
    And the workflow advances at most once
```

## 19. Unit, integration and verification requirements

| Test layer | Required evidence |
| --- | --- |
| Contract/unit | Complete parameter/default serialization; every omitted field; invalid/duplicate enums; typed result invariants; exact identity/version mapping; canonical hashes; immutable copies; deterministic rule order; all section 17 vectors |
| Actor/unit | Absent/Processing/Completed/Failed transitions; input conflicts; stable IDs; optimistic append races; recovery from accepted input; expiry at receipt/commit; no external market/configuration calls in pure evaluation |
| Workflow/unit | Strict typed result acceptance; outcome recomputation; NoTrade normal stop; one pending reservation; frozen versus continuation revisions; late/duplicate callbacks; no premature builder dispatch |
| Configuration integration | Real PostgreSQL draft/publish/retire, exact-version lookup, immutable published payload, wrong-hash rejection, missing template/profile and repeated authoring identity |
| Actor/projector integration | Real event-source transaction and durable work; Scylla unavailable/recovery; publish failure; crash after publish before acknowledgement; identical event replays; no status regression |
| Query integration | Both Scylla access patterns, page limits and wrong-scope tokens, source-sequence ordering, original UTC history dates, authorized scope, NotFound vs empty history |
| Portfolio integration | Actual snapshot resolver and composition reservation service; one-assignment behavior, future-option product resolution, same-key replay, expired/late reservations, retained integer IDs |
| Pipeline verification | Capture actual Start/results over isolated actor transport; pass real accepted RD/assessment fixtures; accept Selected/NoTrade correctly; compare IDs/hashes at OrderComposition boundary |

Use the existing Domain.Trade UnitTests, BDDTests, IntegratedTests and VerificationTests projects, plus Portfolio/storage integration projects where those APIs live. Add tests to their appropriate owners instead of simulating every database with an in-memory dictionary. Use controllable time and isolated test subjects/keyspaces/schema fixtures; do not run business workflows or publish profiles in a user's active environment as a side effect of tests.

Fault matrix SHALL include: crash before acceptance commit, after acceptance before evaluation, after terminal append before projection, between projection and publication, and after publication before durable acknowledgement. For each, assert one logical terminal result and no duplicate composition reservation/dispatch.

No completed upstream stage or newly written documentation alone proves five-operator qualification. First complete selector code and isolated tests, then integrate the five operators one at a time as requested. Live broker connectivity and observation UI are not selector acceptance gates.

## 20. Implementation work packages and completion criteria

| Package | Deliverable | Completion condition |
| --- | --- | --- |
| TS-01 | Shared enums, parameter/template/binding/result contracts, explicit serializers and validators | Golden serialization/hash tests; key compatibility; every required input defined |
| TS-02 | ConfigurationDb typed policy/template storage and lifecycle; three complete draft factories | Exact-version resolution and actual storage tests; no automatic publication |
| TS-03 | Portfolio selection binding resolver and workflow freeze/dispatch mapping | Single assignment, product-class and snapshot-hash/revision tests |
| TS-04 | Pure deterministic evaluator, direction mapping, reasons and summaries | All boundary and positive/negative fixtures pass |
| TS-05 | Command actor, authoritative state and durable lifecycle projection/publication | Restart, concurrency and failure matrix passes |
| TS-06 | Typed workflow completion and idempotent Fund composition reservation | Selected/NoTrade/expiry and real reservation integration pass |
| TS-07 | Scylla schema/projection/query contracts and API mapping | Query/paging/idempotence tests pass |
| TS-08 | Isolated BDD/integration/verification suite and recorded evidence | Results recorded with environment and versions; no unsupported completion claims |

The runtime consumer path SHALL be the full typed selector. The current candidate-filter helper may be retained as a private implementation detail only if its behavior agrees with this specification; it cannot be a second legacy fallback. No ranking or latest-definition repair path is introduced.

Code completion requires all TS-01 through TS-08 deliverables, build success for affected projects, passing required tests and a traceable list of any externally blocked runtime qualification. Operational profile publication, combined five-stage exercising and the future Strategy observation UI remain separate deliverables.

## 21. Explicitly excluded work

V1 does not implement multiple-template ranking, family membership expansion, equities/non-ES selection, option-chain scoring, exact construction parameters, final sizing, financial policy calibration, cross-Fund netting, execution, IBKR emulator, UI changes or LLM summaries. Those boundaries remain explicit; they do not leave any selector input or tuning parameter unspecified.

Construction profiles are required real versioned dependencies. Their actual wing/delta/DTE/price settings and the availability of a complete downstream builder belong to OrderComposition work. Selector tests use immutable descriptors/fixtures with full provenance and do not present fixture descriptors as published production profiles.

## 22. Source verification references

- [Existing Start command](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/Commands/StartTradeSelectionPipelineCommand.cs) and [pipeline routes](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Routing/IntrinsicTimeStrategyPipelineRoutes.cs).
- [Regime result fields](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/RegimeDiscovery/Model/RegimeDiscoveryResults.cs) and [enums](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/RegimeDiscovery/Model/RegimeDiscoveryEnums.cs).
- [Assessment models](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/MarketCondition/Assessment/MarketConditionAssessmentModels.cs), [acceptance contracts](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/MarketCondition/Assessment/MarketConditionAssessmentContracts.cs) and [parameter/hash implementation](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/MarketCondition/Assessment/MarketConditionAssessmentParameters.cs).
- [Portfolio snapshot and reservation contracts](../../../../../../TomasAI.IFM.Domain.Portfolio.Shared/Contracts/PortfolioWorkflowContracts.cs), [resolver](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundStrategyResolver.cs), [snapshot hash](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioCanonicalHash.cs) and [composition aggregate](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundCompositionAggregate.cs).
- [Fund template assignments](../../../../../../TomasAI.IFM.Domain.Portfolio.Shared/ViewModels/FundTradeTemplateAssignmentReadModel.cs) and [family definition](../../../../../../TomasAI.IFM.Domain.Reference.Shared/ViewModels/TradeStrategyFamilyReadModel.cs).
- [Configuration parameter kind/lifecycle](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationParameterSet.cs) and [schema initialization](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/Schema/ConfigurationSchemaDb.cs).
- [Current selection helper](../MarketAssessmentSelectionConsumer.cs), [current workflow continuation](../../Command/CompleteTradeSelection.cs) and [generic result envelope](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Model/StrategyStageResultEnvelope.cs).
- [Durable projection marker](../../../../../../TomasAI.IFM.Shared/EventSourcing/IRequireDurableProjection.cs).

The source review distinguishes existing contracts/infrastructure from proposed selector implementation. Documentation validation covers links, schemas, default completeness, enum names and internal consistency; runtime tests listed here will be executed with the implementation.
