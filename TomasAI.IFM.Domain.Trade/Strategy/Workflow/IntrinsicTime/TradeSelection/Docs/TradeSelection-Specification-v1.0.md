# TradeSelection Detailed Specification v1.1

## Composition preparation alignment - 2026-09-08 UTC

Construction policy schema 1 retains its original required fields and exact serialization/hash. Schema 2 additionally requires `marketData`, a finite reviewed source/date/definition universe consumed only during composition preparation. Both schemas pass the existing exact ConfigurationDb policy reference/identity/hash checks; other pipeline schemas are unchanged. Unknown fields and incomplete scope fail. This does not make the selector price options or choose individual contracts.

After the committed Fund reservation, the workflow now verifies immutable Scylla evidence through `AcceptOrderCompositionPreparationCommand` and commits the saved Start dispatch before sending it. Redispatch reuses the same evidence/IDs/timestamps; expired evidence cannot recapture under that revision. See the [OCP implementation record](../../OrderComposer/Docs/OrderComposition-Prerequisite-Implementation-Record-v1.0.md) for tests and remaining durable-source/live qualification work. This amendment supersedes earlier wording that implied reservation alone immediately dispatches composition.

## Typed execution-policy alignment - 2026-09-07

The Function actor now has five maps, including exact-command `_executionPolicyMap`. Its `ResolveExecutionPolicy` override only calls the base mapped dispatcher. A `Resolve*ExecutionPolicy` extension in `Function/` returns the typed clock/deadline policy. No actor override reads policy settings, computes deadlines or constructs commit/replay callback contexts. The base enforces timers/cancellation and routes `FunctionEventPhase.Committed`/`Replayed` through `_eventMap`; Complete handlers observe and return the same completed event. Observation faults are logged without replacing durable completion. See [system actor conventions](../../../../../../Documents/system/Actor-Implementation-Conventions.md), section 13.3, for the normative contract.

Loading keeps a fresh bounded replay-read budget; execution, projection and append retain the original request deadline.


## Function actor and serialization alignment ? 2026-09-07

Trade Selection follows the Regime Discovery/Market Condition Function convention. `TradeSelectionFunctionActor` injects `ITradeSelectionFunctionContext`; the domain and closed generic interfaces share the same singleton in both hosts. The context exposes `ITradeSelectionCalculator`, implemented by `Model/TradeSelectionEvaluator`. Calculations retain the exact catalog/fund authority, all twelve variant rules and single Daily/Weekly/Monthly triggering horizon.

The actor uses frozen `_parseMap`, `_validationMap`, `_receiveMap`, `_executionPolicyMap` and `_eventMap`. Validation calls base `ValidateMappedCommand` and ordered `List<ValidationError>` extensions with local FluentValidation adapters, shared cross-field evidence rules and registered capability validation. The validation map is instance-owned to capture its context capability registry; its keys/delegates remain immutable. `ValidateAsync` contains no catalog loop. `Function/ExecuteTradeSelectionPipeline`, `CompleteTradeSelectionPipeline` and `FailTradeSelectionPipeline` own domain execution, event construction, reasons and telemetry. Workflow transport failures also enter the terminal event map. The base owns deadline/cancellation/late-worker mechanics; loading has a fresh bounded read budget for expired replay, while new execution/projection/append use the request deadline. Commit and replay observations go through the completion map without repeating projection or saving.

New completions carry typed `SelectionResult` at appended `StrategyStageResultEnvelope` MessagePack key 10, with empty legacy `Payload` and media type `application/vnd.ifm.trade-selection.v1`. Existing keys 0?9 remain unchanged. Typed content uses a canonical semantic fingerprint and defensive collection access. Legacy eight-field byte envelopes remain readable. Consumers, workflow acceptance, handoff, query readers and projection use the representation-aware contract reader. Existing producers and consumers must be upgraded together before enabling typed-only writes.

The shared `MessagePackBinarySerializer.Options` owns the ContractlessStandard resolver and Lz4BlockArray settings used by both binary and NATS serializers; NATS retains direct output-writer serialization. Storage event/result blobs, workflow-state reads and paging tokens use the shared compression-aware serializer. `result_sha256` retains the envelope digest: a semantic fingerprint for typed content, a byte digest for old opaque content. Projection compares normalized completed evidence across old/new envelope representations, ignoring only EventId stream sequencing. Historical candidate-set and mandate byte digests explicitly use the shared uncompressed compatibility encoding; original Portfolio/catalog hash algorithms remain unchanged.

Command copying/normalization and typed evidence comparisons do not serialize and deserialize domain messages. Explicit trigger normalization preserves historical null-string and UTC defaults; invariant decimal fingerprinting preserves numeric meaning. Size checks call shared measurement support: existing binding/result budgets count uncompressed MessagePack content, and the 1 MiB request/completed-event cap also checks the configured encoded representation. LZ4 compression cannot make oversized content admissible. Completion validation and result-size rejection occur before projection or persistence. Frozen count limits still reject overflow without truncating candidates. Serialization for measurement does not create an inner message payload.


| Item | Value |
| --- | --- |
| Revised | 2026-09-07 |
| Status | Implemented specification; final qualification in progress |
| Implementation | TS-01 through TS-08 complete for isolated selector scope |
| Authority | [High-level design, revision 0.8](TradeSelection-High-Level-Design-v0.1.md) |
| Work plan | [Implementation plan v1.1](TradeSelection-Implementation-Plan-v1.0.md) |
| Actor standard | [Shared Function actor conventions](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention); RegimeDiscovery and MarketCondition are the implementation references |
| Scope | Fund-authorized deployment/variant selection for one ES Daily, Weekly or Monthly trigger |
| Document compatibility | Existing filenames retained to preserve links; earlier proposed selector schemas were never implemented |

This revision replaces the suspended single-template specification. It uses the implemented PostgreSQL ConfigurationDb catalog, permits multiple authorized candidates, and covers all twelve initial side/bias/premium combinations. It does not implement actors, create/publish configuration, activate workflows or qualify trading capabilities. Numerical defaults are explicit engineering fixtures, not calibrated trading advice.

## 1. Authorities and ownership

- [ConfigurationDb catalog contracts](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Reference/StrategyCatalog/StrategyCatalogContracts.cs), [storage implementation](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md) and [catalog design](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md) own reusable definitions, relationships and exact versions.
- [Portfolio assignments](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Portfolio/ViewModels/FundTradeTemplateAssignmentReadModel.cs) and [Portfolio snapshot/reservation contracts](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Portfolio/Contracts/PortfolioWorkflowContracts.cs) own Fund permission, priority, financial policy and composition identity reservation.
- [MarketCondition specification](../../MarketCondition/Docs/MarketCondition-Specification-v2.0.md) owns market-only upstream assessment. RegimeDiscovery and MarketCondition do not depend on a selected strategy family.
- [Order Composition specification](../../OrderComposer/Docs/OrderComposition-Specification-v1.0.md) owns exact one-unit construction and adopts this specification's catalog, horizon and reservation handoff.

TradeSelection determines suitability and chooses at most one authorized deployment/structure/variant/product intent. It SHALL NOT fetch new quotes or option chains, recompute upstream results, choose exact contracts/expiries/strikes/prices, calculate final quantities, reserve financial risk or submit orders. Reads of catalog and Portfolio data occur during workflow binding, outside the pure evaluator. Existing accepted upstream evidence remains immutable.

## 2. One trigger and one timeframe

```text
ITI trigger timeframe = accepted regime target horizon = accepted assessment target horizon
 = frozen Fund horizon = candidate deployment horizon = selection policy horizon = result horizon
```

Only Daily, Weekly and Monthly are valid. A Daily invocation never waits for Weekly/Monthly results or borrows their candidate configuration. Existing supporting evidence inside the accepted regime context is retained without requesting additional horizon results.

All supported strategies and variants may be deployed on any of these three horizons. Daily does not imply futures, Weekly does not imply verticals, and Monthly does not imply condors. Horizon belongs to the Deployment; reusable Strategy, Structure and Variant definitions have Horizon=None in the current catalog. The futures ITI trigger identifies the underlying signal source, not the instrument class to trade: do not exclude FuturesOption candidates by passing the trigger's Futures class to the current strict Portfolio resolver.

Initial product scope is ES with authoritative product ID, symbol, exchange and USD currency. Products are roots/product definitions, not expiring contracts. More than one permitted product is possible only through explicitly assigned deployments; exact-product ties use section 10 ordering. Other roots and currencies require a later qualified policy, not symbol guessing.

## 3. Existing implementation and exact identity mapping

| Existing contract | Normative use |
| --- | --- |
| `CatalogKey(Kind, Id, Version)` | Kind discriminator, non-empty GUID and positive **int** version; each catalog entity has its own exact key |
| Deployment.Parent | Exact Strategy key |
| Strategy.Families / Structures | Exact grouping and allowed Structure keys; family membership is not Fund permission |
| Deployment.Variants; Variant.Parent | Exact allowed Variant keys and their exact Structure parents; structure must belong to the selected Strategy |
| Deployment.Products | Exact ProductId/symbol/exchange/currency evidence |
| Deployment.PipelineParameters | Role + kind + GUID + int version + stored hash; pipeline policy payloads remain in existing parameter tables |
| Deployment.Parameters; ParameterSet.Parent | Role + exact catalog ParameterSet key, with exact ParameterSchema parent and both content hashes |
| StoredStrategyCatalogDefinition | Complete definition, original ContentHash, publication and audit evidence |
| StrategyCatalogSnapshot | Exact deployment dependency graph, AsOfUtc and graph ContentHash; catalog evidence alone grants no Fund permission |
| Fund assignment schema 3 | `TradeStrategyFamily.CatalogDeployment` is authoritative; legacy integer family fields are zero |
| Assignment.TradeTemplateId / TradeTemplateVersion | **Deployment** GUID/version for schema 3, not Strategy or Structure identity; must equal CatalogDeployment |
| AssignmentVersion | Positive long Fund aggregate revision, independently preserved; not a catalog version |
| SelectionHint / OrderComposition profile fields | Exact deployment pipeline policy identities; assignment long versions convert to int with checked overflow validation |

The existing names `TradeTemplateId`, `TradeTemplateVersion`, `TradeStrategyFamily` and `PermittedTradeStrategyFamilies` retain their wire meanings. New schema-3 interpretation is explicit; do not rename/reuse old keys or map GUIDs by display labels. New selector bindings require assignment schema exactly 3 initially. Legacy schema-1/2 assignments can be replayed historically but cannot start a catalog-qualified selector; an explicit authorized migration creates schema-3 assignments and exact permissions. Never promote an integer family mapping into execution permission automatically.

Current scaffolding includes Start keys 0-13, Processing/Completed/Failed event contracts, workflow transitions, and `MarketAssessmentSelectionConsumer`. There is no complete selector actor, binding, typed evaluator, selector query projection or guarded reservation continuation. The generic completion handler currently proceeds to OrderComposition; section 14 replaces that behavior during implementation.

## 4. Supported variant semantics and capability boundaries

The twelve rows below match [current catalog examples](../../../../../../TomasAI.IFM.Domain.Reference.Shared/StrategyCatalog/StrategyCatalogExamples.cs). Codes locate engineering authoring examples only. Runtime authority is exact keys/hashes plus a trusted capability validating topology and semantics; a code/name alone cannot activate behavior.

| Example code | Structure builder capability v1 | Side | Bias | PremiumMode | Logical intent |
| --- | --- | --- | --- | --- | --- |
| LongFuture | Future | Long | Bullish | None | Buy future |
| ShortFuture | Future | Short | Bearish | None | Sell future |
| BullCallDebit | CallVertical | Long | Bullish | Debit | Buy lower call, sell upper call |
| BearCallCredit | CallVertical | Short | Bearish | Credit | Sell lower call, buy upper call |
| BullPutCredit | PutVertical | Short | Bullish | Credit | Buy lower put, sell upper put |
| BearPutDebit | PutVertical | Long | Bearish | Debit | Sell lower put, buy upper put |
| ShortBalancedIronCondor | IronCondor | Short | Balanced | Credit | Short put spread plus short call spread; neutral intent |
| ShortBullishIronCondor | IronCondor | Short | Bullish | Credit | Same short-condor topology; positive net-delta intent |
| ShortBearishIronCondor | IronCondor | Short | Bearish | Credit | Same short-condor topology; negative net-delta intent |
| LongBalancedIronCondor | IronCondor | Long | Balanced | Debit | Reverse short-condor legs; neutral intent |
| LongBullishIronCondor | IronCondor | Long | Bullish | Debit | Long-condor topology; positive net-delta intent |
| LongBearishIronCondor | IronCondor | Long | Bearish | Debit | Long-condor topology; negative net-delta intent |

Initial option structures require one common expiry group and unit leg ratios. A Short condor buys the outer put/call and sells the inner put/call; Long reverses those sides. Side, directional bias, premium mode and wing symmetry are distinct. Symmetric wing widths do not by themselves imply a delta-balanced position. Composer must verify prices and actual net delta against the exact variant and composition settings; the selector promises intent only. No supported variant may be silently inverted or substituted when construction fails.

Existing example Settings contain TargetNetDelta 0 or +/-0.15, BalanceTolerance 0.05, SymmetricWings=true, and zero wing-width placeholders. They are draft authoring data. A qualified builder/semantic validator must specify delta units and valid width bounds and reject unfinished placeholders before live publication. This alignment does not certify the examples' construction or risk economics.

Use the current capability registry roles `evaluator`, `data`, `builder`, `risk`, `validator`. Initial selector capability targets are evaluator/RegimeAligned@1 and data/AcceptedMarketAssessment@1, with StructureVariant@1 semantic validation. Future/CallVertical/PutVertical/IronCondor builder and risk capability names exist as requirements, not implementations. API startup currently registers an empty capability registry. TS-02/TS-04 add real selector validators and evaluator registration; actual downstream capabilities remain separate deliverables. Production publication and execution binding SHALL fail on any unavailable required capability. Isolated tests may register explicit fixture validators; they must never be enabled in API production composition or represented as completed builders.

## 5. Frozen authority, catalog and candidate binding

### 5.1 Resolution and authorization

Freeze selection configuration when accepting the workflow, together with existing upstream bindings. This does not pass family/variant policy into upstream calculations. Use one captured UTC as-of instant and persist the binding once.

1. Resolve the explicitly configured Portfolio and Fund, trading year, trigger horizon and ES root. If Fund selection is implicit, exactly one structurally eligible effective Fund is required; multiple Funds are a configuration error, not a ranking choice. Validate Portfolio/Fund/version relationships, allocation, exact financial policy and delegated envelope. Preserve valid paused/blocked states as denial evidence; missing/corrupt/expired required authority is a configuration failure.
2. Add a selector-specific Portfolio resolver/query operation. Keep the existing strict resolver's filtering and hashing behavior intact for existing callers. Obtain the bounded effective assignment set for the exact Fund/mandate/horizon/root before asset-type filtering, including disabled assignments for explanation. This snapshot may have zero assignments. More than one distinct assigned deployment is normal. Duplicate effective assignments for the same deployment key in a Fund/mandate are configuration ambiguity, even if priorities differ; never pick a later assignment silently.
3. Freeze exact assignment references and priorities plus Fund/Portfolio permission evidence. Only enabled assignments, exact `PermittedTradeStrategyFamilies` deployment permissions, eligible traded asset classes and effective operating authority may yield candidates. Empty permission/assignment sets authorize nothing. Legacy string `TradeFamily` permission remains an additional existing mandate constraint, not a replacement for the exact deployment permission; preserve any current Portfolio financial-policy deployment denial as well.
4. Resolve only enabled, effective, exactly permitted deployments. Disabled or unpermitted assignment rows are recorded as excluded with reasons and do not require publication of their draft graphs. For an enabled permitted assignment, Draft/Retired/missing graph, invalid hash, unsupported capability, conflicting profile mapping or unknown schema is a **Failed configuration**, not an ordinary rejected candidate to skip in favor of another deployment.
5. Call `GetPublishedStrategyDeploymentAsync(exactKey, asOfUtc)` and reuse its graph/publication validation. Verify assignment horizon and underlyings, traded instrument class and root/product metadata against the graph. Map catalog FuturesOption to Portfolio FuturesOptions explicitly; Futures maps to Futures. Unknown strings fail. Options are not excluded because the source signal is a future.
6. Resolve the exact deployment-level TradeSelection and OrderComposition pipeline roles and payloads, and every referenced specialized parameter set/schema. Validate assignment legacy-named profile fields agree; freeze existing hashes unchanged. Other required pipeline kinds and graph capability requirements remain exact evidence. Regime/assessment bindings, if declared by a deployment, must match the accepted workflow's common upstream bindings; no candidate requests a family-specific upstream rerun.
7. Enumerate candidates from each authorized assignment x its matching products x its allowed Variants. Resolve Variant.Parent and Strategy.Structures exactly. Retain all Family memberships as provenance, without duplicating candidates per family. Candidate identity is `(AssignmentVersion, DeploymentKey, StrategyKey, StructureKey, VariantKey, ProductId)` scoped to Portfolio/Fund/mandate. Repeated keys with inconsistent content fail; do not silently truncate, coalesce differently authorized assignments or rank by database arrival order.
8. Apply count and byte bounds before accepting the workflow: default maximum 16 effective assignments/deployments, 64 candidate intents, 256 distinct catalog definitions across the binding, 32 dependency levels, and 262144 binding bytes. Exceeding a bound is TS.CONFIG.CANDIDATE_LIMIT or TS.CONTRACT.PAYLOAD_SIZE. Read at most limit+1 assignments for overflow detection; do not enumerate the entire reference catalog or select the first page as if complete. Shared graph definitions/payloads are deduplicated by exact key/hash; conflicting content fails.

TS-02 adds an explicit exact SelectionPolicyReference to the versioned workflow activation configuration for the root/horizon; it is required even when there are no assignments. Validate its payload during binding and carry it as CommonPolicy. Existing workflows without that new configuration require explicit reauthoring, not a latest-policy fallback.

Initial all-candidate ranking requires the same exact TradeSelection policy ID/version/hash across contributing deployments for the invocation. The deployment-specific specialized policy may override only validated per-variant eligibility/preferences through the role described in section 7. It cannot override common confidence, authorization, restrictions, horizon, bounds or ranking semantics. Conflicting common policies are TS.CONFIG.PROFILE_MISMATCH; no arbitrary first-candidate policy controls another candidate. An empty authorized candidate set uses the exact workflow-configured horizon policy and produces an auditable NoTrade; it must not infer a latest profile.

Capture the existing Portfolio snapshot and canonical hash under its owning serializer; do not rewrite its frozen WorkflowRevision later. Snapshot assignment ordering is defined by the new selector-specific resolver as Priority, deployment canonical key, AssignmentVersion; other serializers/callers are unchanged. Catalog cross-deployment reads are separate repeatable-read transactions at the same as-of instant, not one transaction spanning Portfolio and ConfigurationDb. Recheck graph/key/hash consistency and required publication before sealing; a concurrent change may make binding fail/retry before acceptance, never silently substitute a version. After acceptance, immutable evidence governs; emergency stop/revocation follows explicit workflow/Portfolio safety controls and current financial checks downstream.

### 5.2 Proposed `TradeSelectionBinding` wire schema 1

All new schemas in this document are proposed first wire versions; the earlier unimplemented layouts are withdrawn. Existing wire types remain append-only. New fields require explicit presence/schema validation, not constructor defaults that make incomplete payloads valid.

| Key | Field / type |
| --- | --- |
| 0 | SchemaVersion / short = 1 |
| 1 | PortfolioSnapshot / existing PortfolioFundStrategySnapshot |
| 2 | CatalogDefinitions / SelectionCatalogDefinitionSnapshot[]; deduplicated complete graph nodes |
| 3 | DeploymentSnapshots / SelectionDeploymentSnapshot[]; exact graph hash and definition-key membership per deployment |
| 4 | PipelinePolicies / SelectionPipelinePolicySnapshot[]; kind/ID/int version/hash, full canonical typed payload and lifecycle evidence |
| 5 | Candidates / SelectionCandidateBinding[]; deterministic canonical identity order |
| 6 | ExcludedAssignments / SelectionAssignmentExclusion[]; exact assignment/version and reason |
| 7 | CommonPolicy / exact pipeline policy reference, kind TradeSelection |
| 8 | FrozenAtUtc / UTC DateTime |
| 9 | ValidUntilUtc / UTC DateTime |
| 10 | RequestedTradeDate / DateOnly |
| 11 | TradeDatePolicy / string = UTC.TriggerCreatedDate.Test.v1 |
| 12 | PayloadSha256 / string; hash with this field empty |

Binding validity is the minimum of frozen Portfolio snapshot validity, effective authorized assignments and any actual parameter/capability validity constraints. With zero authorized assignments, use the remaining authority/policy bounds; do not call Min on an empty assignment set or invent an infinite Fund lifetime. Stored catalog versions have EffectiveFromUtc and RetiredAtUtc, not an invented EffectiveUntilUtc. Before acceptance every required graph is Published/effective/not retired. A later retirement blocks new bindings but does not rewrite the accepted snapshot. Workflow and assessment expiries further bound the result in section 11.

### 5.3 Typed transport projection and provenance

Catalog authoring DTOs contain JsonElement and are not currently a complete MessagePack transport contract. TS-01 adds explicit selector snapshot DTOs without changing catalog persistence or relying on typeless serialization. A typed definition snapshot retains all original fields; Settings is bounded canonical JSON validated through the exact capability/schema, never executable code.

- `SelectionCatalogDefinitionSnapshot` keys 0 SchemaVersion, 1 Key, 2 Code, 3 Name, 4 Description, 5 DefinitionSchemaVersion, 6 Parent, 7 Horizon, 8 Side, 9 Bias, 10 PremiumMode, 11 SettingsJson, 12 Families, 13 Structures, 14 Variants, 15 Capabilities, 16 ExpiryGroups, 17 Legs, 18 VariantLegs, 19 Products, 20 PipelineParameters, 21 Parameters, 22 LegacyFamilies, 23 ContentHash, 24 Status, 25 CreatedUtc, 26 CreatedBy, 27 EffectiveFromUtc, 28 PublishedBy, 29 RetiredAtUtc, 30 RetiredBy. Child tuple DTOs use keys in the exact positional order of the existing Catalog* records, validated against their owning schemas.
- `SelectionDeploymentSnapshot`: 0 DeploymentKey, 1 AsOfUtc, 2 DefinitionKeys, 3 ContentHash. Preserve the existing graph hash algorithm and order; it hashes SchemaVersion=1, Deployment and each definition Key/ContentHash, not selector-normalized substitute content.
- `SelectionPipelinePolicySnapshot`: 0 Kind, 1 Id, 2 Version, 3 SchemaVersion, 4 PayloadJson, 5 PayloadSha256, 6 Status, 7 EffectiveFromUtc, 8 RetiredAtUtc. The complete payload and referenced catalog schemas are retained, not fetched during evaluation.
- `SelectionCandidateBinding`: 0 SchemaVersion, 1 AssignmentVersion, 2 DeploymentKey, 3 StrategyKey, 4 StructureKey, 5 VariantKey, 6 Product, 7 SelectionPolicyReference, 8 CompositionPolicyReference, 9 SpecializedParameterBindings, 10 FamilyKeys, 11 AssignmentPriority, 12 CandidateHash. Referenced node/policy content must exist exactly once in the frozen tables; CandidateHash binds the tuple, priorities and all referenced hashes.
- `SelectionAssignmentExclusion`: 0 AssignmentVersion, 1 DeploymentKey (nullable only for explicitly recorded legacy denial), 2 ReasonCodes. A malformed enabled assignment still fails rather than becoming exclusion evidence.

`SelectionPipelinePolicyReference` keys: 0 Kind (CatalogPipelineParameterKind), 1 Id (Guid), 2 Version (int), 3 PayloadSha256, 4 Role (exact source string; empty only for the workflow's common activation reference). Policy identity comparisons use kind/ID/version/hash; the Role is provenance and remains validated against the specific deployment binding. Conversions to Storage.StrategyParameterSetKind use an explicit supported-kind mapping. `SpecializedParameterBindings` carries each exact Role/ParameterSet key, with ParameterSet/ParameterSchema hashes resolved from the binding's complete node table. `Product` uses keys 0 ProductId, 1 Symbol, 2 Exchange, 3 Currency.

The planned narrow `SelectionConstructionProfileReference` descriptor contains keys 0 SchemaVersion=1, 1 exact CompositionPolicyReference, 2 exact DeploymentKey, 3 allowed Structure/Variant key pairs, 4 required capability triples, 5 canonical full source payload, 6 source effective timestamp. It is derived by the real exact-version profile adapter plus verified deployment graph; it is not independently authored and cannot assert a builder exists. The adapter must validate declared composition constraints through the actual owning schema/capability. Until a required schema/capability is available it returns an explicit unsupported-configuration error. Isolated fixtures supply complete declared schemas/values; no production placeholder is considered executable. The full source policy remains in PipelinePolicies, and the descriptor can be rebuilt solely from frozen evidence for handoff validation.

Canonical key order for new selector sets is numeric Kind, lowercase GUID D text with ordinal comparison, numeric Version; product ID is numeric. Catalog/Portfolio source arrays and hashes retain their original order/algorithm. Defensive copies, bounded arrays, required-field presence, enum validity and duplicate-property rejection apply across all new DTOs.

`CandidateSetSha256` for projection diagnostics is lowercase SHA-256 of the MessagePack array of CandidateHash strings in canonical candidate identity order, including the empty array when there are no candidates. The complete binding hash additionally covers exclusions and all frozen authority. Neither digest substitutes for the full typed context.

## 6. Invocation, assembly dependency and transport changes

The new Execute Function request requires routing/workflow/correlation identity, the original ITI trigger, the accepted regime and assessment envelopes in WorkflowView, the exact frozen selection binding, requested time and deadline. The market assessment must be Available/current with accepted matching regime lineage and no inherited NoNewTrade. Reuse `MarketConditionAssessmentContracts.ValidateForSelection`; an attempt to bypass upstream ineligibility is TS.UPSTREAM.NOT_ELIGIBLE, not a normal candidate decision.

Preserve historical Start keys 0-13; introduce the new Function request in section 13 rather than extending the inactive Command route. Append binding to workflow view key 27/state key 23 and composition handoff to view key 28/state key 24. Append the complete frozen selector dispatch request/intent at view key 29/state key 25 (verify all slots in TS-01). Store its original input workflow revision separately from subsequent workflow revisions. TS-01 also extends clones, state application and transport round-trip tests for these fields. Missing new request schema decodes to invalid 0; producers explicitly supply 1.

Trade.Shared cannot reference Portfolio.Shared or Reference.Shared because both already reference Trade.Shared. TS-01 extracts the necessary pure Portfolio DTO closure, TradeStrategyFamilyReference and catalog keys/types to a dependency-safe Strategy.Contracts.Shared assembly, preserving public namespaces, MessagePack keys, existing JSON canonicalization and type forwarding. Include CatalogKey in that extraction: the earlier legacy-family-only extraction is insufficient. Do not introduce loose JSON authority or a second set of public catalog identities to avoid the cycle. Selector typed transport DTOs can then refer to the foundation from Trade.Shared.

## 7. Complete selection policy and parameter roles

Use the existing `reference_configuration.trade_selection_parameter_set` and `StrategyParameterSetKind.TradeSelection`. No selector-only template table or duplicate strategy catalog is permitted. Introduce typed draft/read/exact-resolve operations with strict lifecycle/payload validation. All three initial profiles are versioned complete instances of the following proposed `TradeSelectionParameterSet` schema 1. Table row order is explicit MessagePack key order; JSON names match exactly. Every field is required.

| Key | Field | Engineering default / rule |
| --- | --- | --- |
| 0 | SchemaVersion / short | 1 |
| 1 | ParameterSetId / Guid | Explicit persisted ID |
| 2 | Version / int | Positive; initial 1 |
| 3 | ProfileCode / string | TS.ES.Daily.Test, TS.ES.Weekly.Test or TS.ES.Monthly.Test |
| 4 | InstrumentRoot / string | ES |
| 5 | TargetHorizon / TimeFrameType | Exactly corresponding Daily, Weekly or Monthly |
| 6 | MinimumRegimeConfidence / decimal | 0.50, inclusive [0,1] |
| 7 | MinimumAssessmentConfidence / decimal | 0.50, inclusive [0,1] |
| 8 | AllowedRegimeDirections | Up, Down, Neutral |
| 9 | AllowedTrendPhases | RangeBound, Emerging, Established |
| 10 | AllowedTrendStrengths | None, Weak, Moderate, Strong, Extreme |
| 11 | AllowedRegimeQualities | Acceptable, High |
| 12 | AllowedRegimeVolatilityLevels | Low, Normal, High |
| 13 | AllowedRegimeVolatilityChanges | Contracting, Stable, Expanding |
| 14 | AllowedStructureClassifications | Trending, Ranging, Compressing, Expanding, BreakingOut |
| 15 | RejectedInheritedRestrictions | NoNewTrade, DirectionConflict, LowConfidence, Transition; NoNewTrade mandatory |
| 16 | AllowedAssessmentConditions | Directional, RangeBound, VolatilityExpansion, VolatilityContraction |
| 17 | AllowedLiquidity | Healthy, Degraded |
| 18 | AllowedSessions | Open |
| 19 | AllowedEventRisk | Clear |
| 20 | AllowedStress | Normal |
| 21 | AllowedVolatilityBehavior | Stable, Expanding, Contracting |
| 22 | AllowedTriggerAlignment | Aligned, Neutral, NotApplicable |
| 23 | AllowedAssessmentDataQuality | Healthy, Degraded |
| 24 | UnknownEvidencePolicy | NoTrade=1 |
| 25 | VariantRules / SelectionVariantRule[] | Complete twelve-row expansion of section 8 |
| 26 | RankingPolicyVersion / string | ts-rank-v1 |
| 27 | MaximumAssignments / int | 16; range 1-16 |
| 28 | MaximumCandidates / int | 64; range 1-64 |
| 29 | MaximumCatalogDefinitions / int | 256; range 1-256 |
| 30 | MaximumBindingPayloadBytes / int | 262144; range 65536-262144 |
| 31 | MaximumExecutionMilliseconds / int | 2000; range 1-60000 |
| 32 | ResultLifetimeSeconds / int | 30; range 1-300 |
| 33 | FutureClockSkewSeconds / int | 2; range 0-60 |
| 34 | MaximumResultPayloadBytes / int | 262144; range 65536-524288 |
| 35 | ReasonCodeCatalogVersion / string | ts-reasons-v1 |
| 36 | SummaryTemplateVersion / string | ts-summary-v1 |
| 37 | DirectionMappingVersion / string | ts-direction-v1 |

Sets are typed arrays of the existing upstream enums; permitted sets are nonempty, duplicate-free and contain known observations. Unknown numeric enum values fail; RegimeRestriction.None indicates no restriction. Confidence comparisons use unrounded decimals; result confidence is min(regime, assessment) rounded to six places with ToEven. It is not a probability of profit. CompatibilityScore remains null: ranking is explicit lexicographic preference, not a synthesized financial score.

Resolve exactly one deployment pipeline binding of kind TradeSelection and exactly one of kind OrderComposition, matching the existing Portfolio assignment writer. Preserve each binding's actual unique Role string in the frozen reference; do not impose a new capitalization/name convention on existing rows. Multiple bindings of either kind are ambiguous even when Role differs; assignment profile IDs/versions must match the unique references. No per-variant pipeline parameter column exists in the implemented catalog.

Optional deployment `Parameters` role `TradeSelectionVariants` references an exact catalog ParameterSet and its exact ParameterSchema. Its schema-1 typed Settings payload is `{ SchemaVersion, Rules }`; Rules is a complete replacement for VariantRules for that deployment, not an ambiguous merge. Missing this role uses the common policy Rules. All other specialized roles remain required validated evidence for their owning capabilities; unknown required roles fail the relevant capability validator. Role names are unique across pipeline and specialized bindings under existing catalog constraints. Changing any rules/parameter/schema/variant dependency creates new immutable versions and an updated deployment/assignment; there is no runtime override from UI text.

`SelectionVariantRule` keys: 0 BuilderCapabilityCode, 1 BuilderCapabilityVersion(int), 2 Side, 3 Bias, 4 PremiumMode, 5 AllowedRegimeDirections, 6 AllowedTrendPhases, 7 AllowedTrendStrengths, 8 AllowedStructureClassifications, 9 AllowedAssessmentConditions, 10 AllowedVolatilityBehavior, 11 Preference(int 0-100000). The signature (keys 0-4) is unique. Every authorized initial candidate must match exactly one rule; omitted/duplicate/unsupported signatures are configuration errors, not implicit rejection. Specialized rules may alter those allowed sets/preference but must respect common gates, mandatory side/bias direction mapping, actual capability support and the deployment's allowed Variant keys.

## 8. Three defaults and twelve variant rules

Create exactly three common draft policies, one per horizon, with all section 7 values and the same complete rule matrix below. No structure is assigned automatically by timeframe. Use persisted caller-supplied IDs or an explicit saved identity manifest for idempotent authoring; never generate new IDs at every startup. The existing default Family/Strategy/Structure/Variant rows remain draft authoring assets. This work does not repopulate test data or create deployments in the user's database.

Each row expands to the exact matching side/bias/premium combinations in section 4. Unspecified rule columns below equal the explicit corresponding common allowed set, and must still be serialized as fields. Bias restricts regime directions: Bullish=Up, Bearish=Down, Balanced=Neutral. An independent neutral long-volatility condor is supported; Neutral is not globally rejected.

| Rule signatures | Phases | Strengths | Market structure | Assessment conditions | Assessment volatility | Preference |
| --- | --- | --- | --- | --- | --- | --- |
| LongFuture / ShortFuture | Emerging, Established | Moderate, Strong, Extreme | Trending, Expanding, BreakingOut | Directional, VolatilityExpansion | Stable, Expanding, Contracting | 10 |
| BullCallDebit / BearPutDebit | Emerging, Established | Moderate, Strong, Extreme | Trending, Expanding, BreakingOut | Directional, VolatilityExpansion | Stable, Expanding | 20 |
| BullPutCredit / BearCallCredit | Emerging, Established | Moderate, Strong, Extreme | Trending, Ranging, Compressing | Directional, VolatilityContraction | Stable, Contracting | 30 |
| ShortBalancedIronCondor | RangeBound, Established | None, Weak, Moderate | Ranging, Compressing | RangeBound, VolatilityContraction | Stable, Contracting | 40 |
| ShortBullishIronCondor / ShortBearishIronCondor | Emerging, Established | Weak, Moderate, Strong | Trending, Ranging, Compressing | Directional, VolatilityContraction | Stable, Contracting | 40 |
| LongBalancedIronCondor | RangeBound, Emerging, Established | None, Weak, Moderate | Ranging, Expanding, BreakingOut | VolatilityExpansion | Expanding | 50 |
| LongBullishIronCondor / LongBearishIronCondor | Emerging, Established | Weak, Moderate, Strong, Extreme | Trending, Expanding, BreakingOut | Directional, VolatilityExpansion | Expanding | 50 |

These are test defaults to exercise the pipeline. They do not estimate option richness, implied/realized volatility edge or profitability. Selection uses only the accepted upstream evidence; no extra external data is required for this scope. Fund assignment Priority outranks the fixture Preference, so the defaults do not override an explicit Fund preference. Composition retains quote-dependent feasibility and may return NoCandidate without reselecting another intent.

## 9. Publication, snapshots and hashing

Reuse `InsertStrategyCatalogDraftAsync`, `GetStrategyCatalogAsync`, `PublishStrategyCatalogAsync`, `RetireStrategyCatalogAsync` and `GetPublishedStrategyDeploymentAsync`. Add typed selection policy operations to the existing parameter store; do not add immutable-template storage. Draft creation and authoring can precede capabilities; publication/executable binding cannot.

Pipeline policies use Draft -> Published -> Retired, exact positive versions and audited effective times. Guard content/lifecycle writes across typed and generic operations. Published/retired content cannot mutate; exact reads preserve history. Deployment graph validation already verifies referenced pipeline ID/version/hash/publication, but does not by itself implement selector semantic parsing or OrderComposition descriptors. TS-02 explicitly adds these checks without assuming the generic parameter table supplies them.

| Evidence | Hash authority |
| --- | --- |
| Catalog node / deployment graph | Existing ConfigurationDb canonical algorithm, lowercase SHA-256; preserve exact source hashes |
| Portfolio snapshot | Existing PortfolioCanonicalHash, existing JSON ordering and lowercase digest |
| Accepted regime/assessment and selection envelopes | Typed content fingerprints or legacy exact-byte digests, selected by the envelope representation |
| New typed selection policy | Explicit JSON property order equal to section 7 key order; PascalCase names, numeric enums, G29 decimals, GUID D, no omissions; SHA-256 lowercase to satisfy current catalog pipeline Hash validation |
| New binding / candidate | Lowercase SHA-256 over canonical typed evidence with own hash empty; preserve declared ordering and original Portfolio/catalog hashing |

Existing policy hashes are verified using their original serializers; do not case-rewrite or reserialize upstream payloads with selector settings. Compare carried digest identity as decoded bytes where appropriate, while new catalog writes satisfy the existing lowercase format validator. Duplicate/unknown required JSON properties, invalid enum values, incomplete schemas and conflicting key/hash pairs fail closed. The typed catalog transport projection must round-trip to the original catalog definition hash using the authoritative algorithm; golden vectors are an implementation exit gate.

## 10. Validation and deterministic selection

Validate transport/size/schema/identity, duplicate invocation hash, running workflow and exact binding, frozen authority/catalog/policies, accepted upstream lineage/availability, required values/ranges and deadlines before new Function execution. Unsupported configured capability is TS.CONFIG.CAPABILITY_UNSUPPORTED. NoNewTrade at entry is TS.UPSTREAM.NOT_ELIGIBLE. These failures never become favorable evidence for another candidate.

### 10.1 Ordered evidence and ordinary denial

Preserve all applicable ordinary rejections. Global rules run once; candidate rules run in canonical candidate identity order, independent of input collection order. A rule has Passed, Rejected or NotApplicable plus typed actual/expected values and a stable reason. Missing mandatory objects/hashes/confidence or unknown numeric enums are Failed. A defined Unknown optional classification used by a rule becomes TS.EVIDENCE.UNKNOWN at that rule position. Known unfavorable values are ordinary NoTrade evidence.

| Rule | Required predicate | Rejection code |
| --- | --- | --- |
| G01 | Frozen Portfolio/Fund operating permission allows exposure | TS.PERMISSION.OPERATING_STATE |
| G02 | Frozen policy/envelope permits exposure, without live sizing | TS.PERMISSION.ENVELOPE |
| G03 | Fund permits accepted assessment condition | TS.PERMISSION.CONDITION |
| G04 | No rejected inherited restriction | TS.REGIME.RESTRICTION |
| G05 | Accepted direction in common policy | TS.REGIME.DIRECTION |
| G06 | Regime confidence meets minimum | TS.REGIME.CONFIDENCE |
| G07-G12 | Regime quality, phase, strength, volatility level/change, structure in common sets, in that order | TS.REGIME.QUALITY / PHASE / STRENGTH / VOLATILITY_LEVEL / VOLATILITY_CHANGE / STRUCTURE |
| G13 | Assessment confidence meets minimum | TS.ASSESSMENT.CONFIDENCE |
| G14-G21 | Condition, liquidity, session, event, stress, volatility, trigger alignment, data quality in common sets, in that order | TS.ASSESSMENT.CONDITION / LIQUIDITY / SESSION / EVENT / STRESS / VOLATILITY / TRIGGER / DATA_QUALITY |
| C01 | Exact enabled effective assignment and deployment permission retained | TS.PERMISSION.DEPLOYMENT |
| C02 | Product, traded asset class and legacy family-string constraints allowed | TS.PERMISSION.PRODUCT |
| C03 | Candidate bias matches accepted direction and Fund permitted direction | TS.PERMISSION.DIRECTION |
| C04-C09 | Variant rule direction, phase, strength, structure, condition, volatility membership, in that order | TS.VARIANT.DIRECTION / PHASE / STRENGTH / STRUCTURE / CONDITION / VOLATILITY |

Normalize Fund direction vocabulary explicitly: Up/Long/Bullish -> Bullish; Down/Short/Bearish -> Bearish; Neutral -> Neutral. Balanced variant intent requires Neutral permission; Short condor Side does not mean bearish direction, and Long condor Side does not mean bullish direction. Unknown permission strings are configuration errors. Empty permission sets authorize nothing.

If any global rule rejects, result is NoTrade with that first global reason; candidate rows remain NotEvaluated with the global blocker. Otherwise evaluate every candidate. An empty candidate set gives TS.NO_AUTHORIZED_CANDIDATE with the ordered excluded-assignment evidence. If all candidates reject, return TS.NO_COMPATIBLE_CANDIDATE with every candidate's ordered rejection reasons. A valid rejected candidate can never outrank a compatible one.

### 10.2 Ranking and ties

For candidates passing every applicable gate, choose the lexicographically smallest tuple:

```text
(Assignment.Priority ascending,
 effective VariantRule.Preference ascending,
 Deployment GUID D lowercase ordinal, Deployment version numeric,
 Strategy GUID D lowercase ordinal, Strategy version numeric,
 Structure GUID D lowercase ordinal, Structure version numeric,
 Variant GUID D lowercase ordinal, Variant version numeric,
 ProductId numeric, AssignmentVersion numeric)
```

Kind is fixed at each tuple position. Do not use runtime GetHashCode, culture sorting, Guid byte-layout comparison, database order, completion order, label text or random choice. Lower number means higher preference. Other compatible candidates are EligibleNotSelected with TS.RANK.LOWER_PREFERENCE and their full comparison tuple, not mislabeled as incompatible. Identical candidate identities are an invalid binding, so there is always exactly one winner. Two eligible variants with equal preferences are resolved by exact identity order, never a configuration failure merely because preferences tie.

Freeze all inputs, policies, capabilities and evaluation time. Given the same invocation evidence, the evaluator returns identical selection and ordered explanations. No adaptive scoring, weighted probability, automatic fallback to another horizon or unsupported strategy is part of V1.

## 11. Time and expiry

At receipt require `now < ExpectedCompletionAtUtc`, binding.ValidUntilUtc and accepted assessment.ValidUntilUtc. Deadline is the minimum of workflow deadline, RequestedAtUtc + MaximumExecutionMilliseconds, binding validity and assessment validity. Actor independently verifies it. FutureClockSkewSeconds applies to observed/produced timestamps, never to expiry; it cannot extend permission. Regime as-of/production relationships follow existing contracts; do not invent a regime expiry field or fresh lookup.

```text
Result.ValidUntilUtc = min(EvaluatedAtUtc + ResultLifetimeSeconds,
    accepted assessment.ValidUntilUtc, binding.ValidUntilUtc, workflow deadline)
```

Execution deadline governs committing a decision; result validity governs consuming that committed result. Recheck the relevant bounds before terminal commit and workflow handoff. Use injected TimeProvider and a workflow-frozen evaluation instant; monotonic elapsed milliseconds are diagnostic. An expired result cannot be refreshed by replay or projection. Common policy limits apply to the entire bounded evaluation, not separately per candidate.

## 12. Typed result and complete decision context

Proposed new enums: SelectionOutcome Unknown=0/Selected=1/NoTrade=2; SelectionRuleStatus NotApplicable=0/Passed=1/Rejected=2; SelectionCandidateStatus NotEvaluated=0/Ineligible=1/EligibleNotSelected=2/Selected=3. UnknownEvidencePolicy NoTrade=1. Side/Bias/PremiumMode retain the exact capability-validated catalog strings. Do not introduce a closed strategy-family or timeframe-specific variant enum as catalog identity.

| Key | TradeSelectionResult field / type |
| --- | --- |
| 0 | SchemaVersion / short = 1 |
| 1-5 | ResultId / Guid; WorkflowId / StrategyWorkflowId; EntityId / existing workflow entity; InvocationId / Guid; InputWorkflowRevision / long |
| 6-9 | TriggerEventId / Guid; PortfolioId / int; FundId / int; DecisionHorizon / TimeFrameType |
| 10 | Outcome / SelectionOutcome |
| 11 | SelectedCandidate / SelectionCandidateIntent?; null for NoTrade |
| 12 | DecisionContext / TradeSelectionDecisionContext |
| 13 | GlobalEvidence / SelectionRuleEvidence[] |
| 14 | CandidateDecisions / SelectionCandidateDecision[] in canonical identity order |
| 15 | SelectionConfidence / decimal; evidence summary on either outcome |
| 16 | CompatibilityScore / decimal?; null in V1 |
| 17 | PrimaryReasonCode / string |
| 18-20 | EvaluatedAtUtc, ProducedAtUtc, ValidUntilUtc / UTC DateTime |
| 21 | CommonPolicyReference / exact pipeline kind/ID/version/hash |
| 22 | SummaryText / string, at most 2048 characters |

`SelectionCandidateIntent` keys: 0 CandidateHash, 1 AssignmentVersion, 2 DeploymentKey, 3 StrategyKey, 4 StructureKey, 5 VariantKey, 6 Product, 7 Side, 8 Bias, 9 PremiumMode, 10 SelectionPolicyReference, 11 CompositionPolicyReference, 12 SpecializedParameterBindings, 13 FamilyKeys. Every field must match the frozen candidate and complete graph; all parameter and schema versions/hashes remain in context. Product includes positive ID, symbol, exchange, currency; it is not a selected contract.

Regime evidence uses `StrategyStageResultEnvelope.RegimeResult` for new results and
`ReadRegimeResult()` for typed/legacy compatibility. Validate the content fingerprint and envelope
metadata without serializing the regime envelope for equality. Preserve this original representation
in `TradeSelectionDecisionContext`; do not repack typed Regime evidence into an inner byte payload.
Market Condition now follows the same typed-result boundary: new envelopes carry `AssessmentResult` at appended key 9 and an empty `Payload`. `MarketConditionAssessmentContracts.ReadResult` reads typed content and retains legacy byte decoding. Compare accepted envelopes with `HasSameContent`, preserving their original representation in the decision context. The assessment content fingerprint uses canonical assessment JSON for integrity only; transport and storage serialize the outer message.

`TradeSelectionDecisionContext`: 0 SchemaVersion=1, 1 original RegimeResultEnvelope, 2 original AssessmentResultEnvelope, 3 complete SelectionBinding. `SelectionCandidateDecision`: 0 CandidateHash, 1 Status, 2 RuleEvidence, 3 comparison tuple (typed fields in section 10.2 order), 4 ReasonCodes. `SelectionRuleEvidence`: 0 RuleId, 1 FieldPath, 2 Status, 3 ActualJson, 4 ExpectedJson, 5 ReasonCode. Preserve canonical numeric values and enum strings; each evidence row is at most 4096 UTF-8 bytes. No truncation of authoritative context/evidence is permitted.

Selected requires exactly one Selected candidate decision, zero failed global/selected-candidate rules, the deterministic winning tuple and TS.SELECTED. NoTrade has no SelectedCandidate and no Selected candidate decision; it has global rejection, empty authorized set or all-candidate incompatibility with the corresponding reason. Failed is a lifecycle outcome, never a successful third selector outcome. Observed Neutral remains in context even when no variant qualifies.

Envelope ResultType=TradeSelectionResult, schema=1, ContentType=application/x-msgpack; envelope/payload IDs and ProducedAtUtc must agree. MarketDataAsOfUtc preserves the accepted assessment envelope value. All producers, validators, persistence, queries and continuation use the explicit selector result cap, default 262144; other stages retain their limits. Stage transport cap is 1048576 bytes including outer serialization. Count/individual-node caps do not guarantee the full binding/result fits: enforce actual serialized byte bounds before acceptance/terminal commit and fail oversized input, never select a smaller subset to fit.

Deterministic summary: `{Horizon} {Root}: selected {DeploymentCode}/{VariantCode} ({Side}, {Bias}, {PremiumMode}); confidence {Confidence:F6}.` NoTrade: `{Horizon} {Root}: NoTrade ({PrimaryReasonCode}); {CandidateCount} candidate(s) evaluated.` Invariant formatting only. Summary text cannot fill missing machine-readable fields or authorize continuation.


## 13. Function actor identity, lifecycle and direct delivery

Implement `TradeSelectionFunctionActor : BaseEventSourceFunctionActor` with `TradeSelectionFunctionContext`, `TradeSelectionFunctionState` and its completed-only repository. Follow RegimeDiscovery and the aligned MarketCondition. Use `ActorType.Function`, actor name `TradeSelectionPipelineFunction`, verb `Execute`, existing TradeSelectionPipelineBoundedContext and Core NATS request/reply. The earlier Command/Processing/durable EventProjector design is superseded. Preserve old Start/Processing/Completed/Failed contracts for historical reads; new workflow routing must not dispatch the old selector Command or publish selector Realtime lifecycle events.

Add `ExecuteTradeSelectionPipelineCommand : ICommand<TradeSelectionExecutionId>`. Composite execution identity contains workflow entity, WorkflowId and InputWorkflowRevision, with canonical formatting, validation and serialization following existing upstream execution IDs. Proposed MessagePack keys: 0 SchemaVersion (explicit 1, missing=0); 1 CommandId; 2 Subject; 3 PostEvents=false; 4 EntityId; 5 ErrorCode; 6 RouteTo; 7 InputWorkflowRevision; 8 WorkflowView; 9 TriggerEvent; 10 CorrelationId; 11 CausationId; 12 RequestedAtUtc; 13 ExpiresAtUtc; 14 SelectionBinding; 15 EvaluatedAtUtc; 16 RegimeResultEnvelope; 17 AssessmentResultEnvelope. Bind all duplicate identities/evidence to the frozen workflow. Allocate stable error-code constants using the repository error-code registry during TS-01. New completed/failed Function contracts use this Function actor subject, typed workflow result entity, complete lineage and request fingerprint; do not repoint historical event actor constants.

InvocationId = Execute CommandId. Persist the complete execution request, input fingerprint, evaluation instant and deadline in the owning workflow's durable dispatch intent before requesting the Function. Generate command identity using the existing deterministic workflow scheme. ResultId = CommandId; terminal completion event ID = ResultId. The fingerprint covers the canonical complete request (including lineage, exact binding/upstream hashes and frozen timestamps), with no receive-time diagnostics. Retries use the identical request; they never recapture authority or reset deadlines.

The actor SHALL declare frozen `_parseMap` keyed by ordinal verb, `_validationMap`, `_receiveMap` and `_executionPolicyMap` keyed by exact command Type, plus `_eventMap` keyed by the exact completed/failed event Types. The four request maps contain the same supported request set; the instance-owned validation map captures the typed context capability registry. ParseMessage uses `ParseMappedFunction`, validation runs before state loading, and ExecuteFunctionAsync uses `ResolveMappedFunctionHandler` to await `ExecuteTradeSelectionPipeline.ExecuteAsync`. No type-name strings, assignable fallback, switch dispatch, reflection discovery or direct transport-handler bypass. The extension contains only deterministic domain evaluation; the base owns payload release, completed-state replay, projection, persistence and exactly one typed reply. Query actors use their own parse/receive/exception conventions.

The following mapping and responsibility contract is normative:

| Component | Required convention |
| --- | --- |
| `_parseMap` | Immutable `static readonly IReadOnlyDictionary<string, Func<IActorMessage, ExecuteTradeSelectionPipelineCommand>>`; ordinal verb key `Execute`; exactly one typed deserialization |
| `_validationMap` | Immutable instance `readonly` dictionary keyed by exact command CLR `Type`; accumulates command ID, entity, request/envelope, schema/hash and frozen-binding validation errors before state loading |
| `_receiveMap` | Immutable `static readonly` dictionary keyed by the same exact command CLR `Type`; awaits the command extension and returns `ValueTask<FunctionResult<TCompletedEvent,TFailedEvent>>` |
| `_eventMap` | Frozen exact completed/failed event Type keys; dispatches Complete/Fail extensions for execution, lifecycle, conflict and workflow transport outcomes |
| Typed context | Inject `ITradeSelectionFunctionContext`, registered against the same singleton as `IFunctionActorContext<TradeSelectionFunctionActor>`; explicit repository, projector, calculation model, TimeProvider and typed logger dependencies |
| Base lifecycle | Inherited mailbox start/stop, payload release even on parse failure, completed-state loading/replay, projection, append and one typed reply; no overridden transport loop bypassing these steps |
| State/repository | `Matches`, `TryComplete` and event application preserve the canonical request fingerprint and sole completed event; expected initial stream version zero; no command denormalizer |
| Deadline hooks | Dispatch `ResolveExecutionPolicy` through `_executionPolicyMap`; the base fences bounded stages and dispatches explicit Committed/Replayed phases through the event map |
| Attempt diagnostics | Structured Function logging; no command-audit ID reservation that suppresses retries after an uncommitted failure |

TS-C32 SHALL verify that the parsed request type set equals both validation and receive key sets; all maps are immutable and actor inheritance is correct. Tests enter through `HandleMessageAsync`, not a test-only direct evaluator call, for actor type/name/verb/entity mismatch, malformed/null payload, invalid command, exactly-once payload release/reply, duplicate/conflicting requests and cancellation. Invalid ingress cannot load state, capture data, project or append. TS-C33 verifies real registration, request/reply, synchronous projection and completed append failures. Existing MarketCondition and RegimeDiscovery Function tests remain regression gates whenever shared infrastructure changes.

| Existing Function state | Request | Action |
| --- | --- | --- |
| Absent | Valid Execute | Evaluate frozen input; return failure or a candidate completion |
| Absent | Invalid Execute | Return typed failure; no projection or Function append |
| Absent | Selected or NoTrade | Project candidate completion, append completed state at expected version zero, reply |
| Completed | Matching request fingerprint | Return original completion without evaluation/projection, even after expiry; workflow rechecks authority |
| Completed | Conflicting fingerprint | Return TS.INVOCATION.CONFLICT; preserve original completion |
| No committed completion after failure/restart | Same still-current request | May evaluate identical frozen input again; failures are not Function state |

### 13.1 Completed-only persistence and projection

Use `IEventSourceFunctionState`, `IEventSourceFunctionStateRepository` and `IFunctionProjector<TCompletedEvent>`. Ordering is calculation -> synchronous idempotent Scylla projection -> PostgreSQL completed-event append -> direct typed reply. No Processing/Failed append, no Function event publication, no durable Function projector queue/checkpoint/replay. The shared base owns the lifecycle; domain stage hooks enforce deadlines without reordering it.

A projection failure prevents completion persistence. A persistence failure after projection can leave an orphan query row; it returns failure and cannot advance the workflow. Projection rows are evidence, not authority. A matching retry after a committed completion returns that event without recapture or reprojection. Concurrent append conflicts must not return an uncommitted winner; use the shared persistence failure path, then retry/load the committed completion. No cross-database ACID or autonomous projection recovery is claimed.

The existing workflow Realtime actor requests the Function and translates its direct completed/failed reply into deterministic CompleteTradeSelection/FailTradeSelection commands. The workflow command actor owns durable terminal status, timeout precedence, duplicate suppression and composition reservation recovery. A lost reply is recovered by the same saved Function request while valid; a late completed reply cannot reopen an expired workflow. Function technical retries are not a new strategy evaluation input.

### 13.2 Failure data and timeout

New Function failure contracts retain typed error metadata and a stable allocated ErrorId. Encode bounded safe ErrorData as JSON with schemaVersion=1, reasonCode, fieldPath, invocationId, inputPayloadSha256, commonPolicyId/version, deploymentKey/candidateHash when known, failedAtUtc and elapsedMilliseconds. Malformed input omits unknown identities. Failure is returned to the caller; only the workflow persists its failed transition.

Stable reasons: TS.CONTRACT.SCHEMA, TS.CONTRACT.REQUIRED_FIELD, TS.CONTRACT.VALUE_RANGE, TS.CONTRACT.IDENTITY, TS.CONTRACT.HASH, TS.CONTRACT.PAYLOAD_SIZE, TS.CONFIG.MISSING, TS.CONFIG.AMBIGUOUS_ASSIGNMENT, TS.CONFIG.CANDIDATE_LIMIT, TS.CONFIG.CAPABILITY_UNSUPPORTED, TS.CONFIG.PROFILE_MISMATCH, TS.CONFIG.INVALID, TS.UPSTREAM.INVALID, TS.UPSTREAM.NOT_ELIGIBLE, TS.TIME.FUTURE, TS.TIME.EXPIRED, TS.CALCULATION.FAILED, TS.PROJECTION.FAILED, TS.PERSISTENCE.FAILED, TS.RESULT.INVALID and TS.INVOCATION.CONFLICT. Success is TS.SELECTED; ordinary NoTrade reasons remain section 10.

The request deadline is bounded by workflow expiry and frozen execution budget. Exact-boundary expiry beats new completion; caller cancellation propagates distinctly. Bound completed-state loading independently so previously completed requests can replay after market expiry. Before/after execution, projection and persistence, check remaining deadline; cancel and observe late dependencies and prevent subsequent stages from running. Cancellation cannot roll back a database write already in progress; query evidence and workflow acceptance remain separate. No additional selector cancellation command is introduced.

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
| TradeSelectionInvocationId | Accepted result InvocationId (= frozen Execute CommandId) |
| TradeSelectionResultId / SHA256 | Exact accepted result envelope identity/hash |
| Portfolio/Fund identities and versions | Frozen snapshot values |
| TradeTemplate ID/version | Selected **Deployment** GUID/version, preserving schema-3 Portfolio semantics; not Strategy/Structure GUID |
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

The one Primary TradeInstruction has the selected assignment TradeFamily string (currently deployment code), without inventing a grouping-family permission, TradeRole=Primary, IsPrimaryTrade=true, Long/Short for futures, Bullish/Bearish/Neutral for options from the selected Bias (Balanced maps to Neutral); selected Side and PremiumMode remain independently present in the full intent, TradeAction=Open, ES root, the same requested trade date, null maturity, selection ResultId formatted D as Reference, persisted RequestedAtUtc as CreatedOnUtc and authenticated service principal as CreatedBy. It reserves one OrderId and one TradeId for a strategy instruction, not one TradeId per option leg. Exact leg construction follows later.

The initial engineering policy is exactly `UTC.TriggerCreatedDate.Test.v1`: require non-default UTC TriggerEvent.CreatedOn and set RequestedTradeDate = DateOnly.FromDateTime(TriggerEvent.CreatedOn). Freeze both date and policy in the binding; no receipt-time fallback or live calendar lookup is allowed. This is an explicit test date convention, not a claim to calculate the exchange trading-session date. A later exchange-session date policy requires a versioned binding schema/policy extension and qualified calendar data. It does not block implementing these complete initial test inputs.

The existing Portfolio reservation request checks deployment/profile presence in the frozen assignment snapshot; it does not carry structure/variant fields. Keep that contract intact. TS-06 must validate the complete selected intent in workflow authority and persist its hash/context alongside the reservation. Append the full typed intent/binding/reservation to OrderComposition start; do not reconstruct the variant from TradeInstruction.DirectionOrBias or deployment display text. The reservation is a business-ID reservation, not financial-risk approval.

### 14.3 Reservation recovery and downstream input

On a timeout with unknown Portfolio response, query/retry the identical saved request with the same idempotency key while current. Do not regenerate timestamps, request hash or IDs. The existing Portfolio service returns the committed reservation for identical replay and rejects changed-payload reuse. This is recovery of one logical side effect, not new selection.

After response, validate Portfolio/Fund/deployment/profile/result bindings, committed reservation identity, positive integer OrderId/TradeId and exactly one Primary instruction. If still current, atomically record reservation and the durable StartOrderComposition intent. Pass accepted selection unchanged, complete frozen Portfolio snapshot/binding, reservation and workflow deadline. Append versioned fields to the existing OrderComposition Start contract without changing existing keys; its detailed builder contract remains the authority for live construction data.

If the workflow expires/stops while a reservation is pending, it SHALL not dispatch construction on a late response. Reconcile any committed FundOrder to the Portfolio's Expired/Cancelled state using its supported command. Integer IDs are retained and never reused. A permanent Portfolio validation failure stops the workflow; it cannot be downgraded to selecting another candidate.

OrderComposition returns Composed, NoCandidate or Failed through its own boundary. NoCandidate stops normally. It cannot change selected horizon/deployment/strategy/structure/variant or widen permissions to obtain a candidate. Final sizing and risk reservation remain Portfolio Risk Manager responsibilities after one-unit composition.

## 15. Read models and query contracts

Add selector projection methods to `ITradeDbContext`/TradeDb and Scylla schema initialization, plus typed queries through the existing actor/query API pattern. These read models are implemented by the additive TradeDb schema and selector repositories.

Project candidate completed-result rows keyed by workflow and invocation. Function queries contain Selected/NoTrade evidence only; obtain Processing/Failed/TimedOut status and acceptance from the workflow observation. Each Function stream admits one committed completion; use source_sequence=1 as its deterministic projection slot before append, not a claimed persisted sequence. Compare immutable result hashes on retries.

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
    selected_deployment_id uuid,
    selected_deployment_version int,
    selected_strategy_id uuid,
    selected_strategy_version int,
    selected_structure_id uuid,
    selected_structure_version int,
    selected_variant_id uuid,
    selected_variant_version int,
    candidate_set_sha256 text,
    binding_sha256 text,
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

Use the configured TradeDb keyspace; do not hard-code a deployment keyspace. Selected identity columns are null for NoTrade; parameter columns refer to CommonPolicy on every routable invocation. Full candidate and dependency evidence remains in the bounded result/event bytes. LifecycleStatus is Completed=2 for these Function projections; no Processing/Failed Function rows are written. History contains candidate completions only and uses the original event UTC date. Repeated projection uses identical IDs, payload hashes, timestamps and keys.

The completed Function event carries all immutable routing/Portfolio/Fund/parameter evidence from the frozen request. There is no separate selector acceptance record or durable projector work item. A projected row does not prove a completed PostgreSQL append or workflow acceptance. Observation must expose candidate projection versus accepted workflow result and suspected orphan status, as MarketCondition does. Never infer successful execution from source_sequence=1 alone. Exact result query wording refers to exact projected evidence; committed authority is verified against Function/workflow state when required.


| Query | Inputs | Semantics |
| --- | --- | --- |
| GetTradeSelectionInvocationQuery | WorkflowId, InvocationId | Exact projected completion evidence; absent is NotFound; no inferred workflow status |
| GetTradeSelectionResultQuery | WorkflowId, InvocationId, ResultId | Exact projected result (acceptance reported separately); identity mismatch is NotFound/contract error, never latest substitution |
| GetTradeSelectionHistoryPageQuery | PortfolioId, FundId, UTC ValueDate, PageSize, PagingState? | Terminal history, stable clustering order |

PageSize default is 50, valid range 1-200. Paging tokens bind query scope, schema and page size; wrong-scope tokens fail validation. No ALLOW FILTERING or unbounded date-range scan. Finding history on another date is an explicit query. Initial schema has no automatic TTL; retention is a separate operational policy, not a hidden deletion default.

Queries report eventually consistent actor decisions; they do not assert workflow acceptance, successful reservation or risk approval. Authorization SHALL check Portfolio/Fund access. The underlying full result remains available for audit without exposing sensitive account details in display summaries.

## 16. Observability and operational controls

Record workflow/invocation/source IDs, Portfolio/Fund/assignment versions, single target horizon, exact deployment/strategy/structure/variant keys, policy and specialized schema/set hashes, input/result hashes, candidate count, ranking tuple, reasons, timestamps, expiry and elapsed milliseconds. Preserve existing Activity/W3C correlation through retries. Use bounded metric labels (horizon/outcome/reason); catalog GUIDs and workflow IDs belong in traces/logs.

Expose selection duration, Selected/NoTrade/failure counts, candidate counts, duplicate/conflict counts, projection failure/orphan counts and reservation pending age. Queries distinguish committed selection from workflow acceptance/reservation. UI rendering is deferred until the five operators are code complete, followed by combined testing one stage at a time.

Author three common draft policies explicitly, reusing the existing basic three families and their versioned structures/variants. Create only requested deployments/assignments with saved IDs and explicit permissions. Draft authoring is permitted before builder/risk capabilities exist; publication and operational qualification are not. This document does not populate or publish the user's catalog or authorize automatic trading.

## 17. Qualification fixtures

These are the required qualification fixture groups; executed coverage is recorded in the implementation evidence. Preserve existing upstream, Portfolio and catalog regression coverage; replace the old TS-F01-TS-F24 single-template matrix with this catalog-aligned matrix. Fixture IDs identify families of parametrized tests and must appear in the implementation evidence.

| Fixture | Required evidence |
| --- | --- |
| TS-C01 | All twelve section 4 variants x Daily/Weekly/Monthly = 36 positive cases with compatible exact graph and Fund permission |
| TS-C02 | Futures Up->Long and Down->Short; no futures candidate for Neutral |
| TS-C03 | Bull-call debit, bear-call credit, bull-put credit, bear-put debit side/right/leg-sign semantics |
| TS-C04 | Short/Long condors each Balanced/Bullish/Bearish; premium, topology, delta intent and wing symmetry remain independent |
| TS-C05 | Neutral+RangeBound/Stable selects an authorized short balanced condor under the defaults |
| TS-C06 | Neutral+VolatilityExpansion/Expanding selects an authorized long balanced condor under the defaults |
| TS-C07 | Directional credit/debit eligibility follows the frozen categorical matrix; no option-chain/IV request |
| TS-C08 | Each confidence at 0.499999, 0.500000 and 0.500001; below rejects, equality/above passes if otherwise eligible |
| TS-C09 | Every common/candidate rule has an unfavorable and Unknown case; malformed required fields are Failed |
| TS-C10 | Available but Poor/Closed/Elevated/Unusable becomes NoTrade under policy; unavailable/NoNewTrade direct entry fails |
| TS-C11 | Multiple assignments are valid; lower numeric Fund Priority wins over variant Preference |
| TS-C12 | Equal Fund priority uses lower variant Preference; rejected candidate never wins |
| TS-C13 | Exact ties resolve by canonical GUID text/version/product/assignment tuple; collection/culture/task-order permutations preserve bytes/evidence |
| TS-C14 | Shared family membership does not duplicate a candidate; repeated identity with conflicting hash fails |
| TS-C15 | Zero assignments, empty permissions and all-disabled assignments yield explained NoTrade without publishing drafts |
| TS-C16 | Multiple Fund ambiguity and duplicate overlapping assignment for the same deployment fail distinctly |
| TS-C17 | Exact deployment permission: same name/new version/unassigned product is not authorized |
| TS-C18 | Paused Fund/Portfolio and blocked envelope cannot be bypassed by confidence; unknown authority is Failed |
| TS-C19 | Futures ITI can select FuturesOption deployment; explicit instrument-class/Portfolio vocabulary mapping |
| TS-C20 | Schema-3 TradeTemplate fields equal deployment GUID/int-version; legacy schema and checked version overflow fail for new starts |
| TS-C21 | Missing/retired/draft/corrupt authorized deployment, absent capability or mismatched graph edge fails whole binding |
| TS-C22 | Same common policy across candidates; different ID/version/hash fails; zero candidates use pinned activation policy |
| TS-C23 | Unique pipeline kind mapping preserves actual Role strings; duplicates by kind and conflicting assignment profiles fail |
| TS-C24 | Exact specialized ParameterSet->ParameterSchema versions; complete rule replacement, invalid/duplicate/missing rule signature and prohibited override |
| TS-C25 | Node/graph/policy/Portfolio/envelope hashes survive typed transport, replay and source collection ordering rules |
| TS-C26 | Every required field omission/invalid schema/enum, duplicate JSON property, key/hash collision and forged Selected intent fails |
| TS-C27 | Candidate/assignment/definition/depth/byte limits at boundary and +1; overflow cannot silently choose a truncated set |
| TS-C28 | One trigger needs only its horizon; mismatched regime/assessment/deployment/policy rejected; family changes do not alter upstream |
| TS-C29 | Exact expiry boundaries, timestamp skew, unchanged frozen revision and no invented regime validity |
| TS-C30 | Typed NoTrade/Selected invariants, ordered candidate rejections versus eligible alternatives, invariant summaries |
| TS-C31 | Same command/hash is idempotent; changed hash under same identity cannot replace outcome |
| TS-C32 | Completed-only append, restart replay after expiry, map parity, ingress rejection and optimistic append conflict |
| TS-C33 | Synchronous projection/append failure, orphan evidence, lost Function reply and same-request completion replay |
| TS-C34 | Workflow validates winner against full frozen context; generic Completed and forged lower-ranked intent cannot dispatch |
| TS-C35 | Reservation identity/hash/revision mapping uses selected deployment, not strategy; one order/trade instruction, not one trade per leg |
| TS-C36 | Reservation response lost/replayed, expiry/cancel before callback, committed result before dispatch; no duplicate logical construction |
| TS-C37 | Actual Scylla exact invocation/result/history, paging scope, orphan indication and separation of candidate projection from workflow acceptance |
| TS-C38 | Production bootstrap resolves real selector services and fails unknown capabilities; fixture registry cannot leak into production |
| TS-C39 | Existing source/wire/Portfolio hash vectors and strict resolver callers unchanged after dependency extraction |
| TS-C40 | Before/after retirement and configuration races: new binding denied, accepted evidence immutable, explicit stop still prevents dispatch |

For every positive variant fixture, use the authoritative example topology with explicitly valid test parameter values and qualified test-only capability validation, full upstream envelopes, exact authorized assignment and recomputed source hashes. A fixture is not a live deployment. Defaults only supply numbers; no test may invent missing identity by deserializer fallback. Rank tests exercise a mixed authorized set and a single-candidate set to ensure lower-priority variants can be selected when explicitly preferred or when alternatives are incompatible.

## 18. BDD scenarios and authority boundaries

1. Given an eligible ES Daily workflow with two permitted deployments, when the lower-numbered Fund priority has a compatible variant, select that exact deployment/version regardless of retrieval order.
2. Given a futures signal and an authorized options deployment at the same horizon, evaluate all four vertical variants permitted by its graph; select at most one without requesting option contracts.
3. Given a Neutral expanding-volatility assessment and permission for the long balanced condor, produce Long/Balanced/Debit intent. Given stable range evidence and short balanced permission, produce Short/Balanced/Credit intent.
4. Given bullish/bearish evidence, choose only matching directional bias; long/short premium side never substitutes for directional permission.
5. Given no permissions or all incompatible candidates, finish NoTrade with complete authority/evidence and no reservation.
6. Given an enabled authorized candidate with an unsupported builder/data/evaluator requirement, fail binding explicitly; never silently select another known strategy.
7. Given a projection failure, return failure without Function completion; given a committed completion and a lost reply, replay the same result and accept it once in the workflow. Given a lost reservation response, recover the saved idempotent request and same business IDs.
8. Given identical frozen inputs after configuration changes, retain the accepted decision evidence; a new workflow binds only currently published exact dependencies. Explicit cancellation fences late handoff.

## 19. Test ownership and evidence

Unit tests cover contracts, hashing, candidate enumeration, common policy, semantic rules, deterministic ranking, result acceptance and actor state transitions. BDD tests express section 18 outcomes. PostgreSQL integration tests exercise real catalog/policy lifecycle, exact graph resolution and frozen Portfolio authority. Scylla tests exercise projections and paging. NATS/event-source tests exercise actual Function request/reply, projection, completed append, workflow translation and reservation recovery boundaries. Verification tests capture source-to-accepted-handoff fixtures, with named downstream probes clearly distinguished from implemented Composer/risk operators.

Record commands, source revision, nonzero discovered count, passed/failed/skipped count, fixture IDs and artifacts per gate. Do not claim passing integration from mocked storage, or operational trading support from fixture capabilities. A skipped external-service test remains missing evidence. Use only isolated owned schemas/subjects/tables; never delete current reference/market data to qualify selection.

## 20. Work packages and completion criteria

| Gate | Deliverable | Current status |
| --- | --- | --- |
| TS-01 | Dependency foundation, typed catalog/binding/result contracts, append-only transport and hash vectors | Complete; scoped qualification passed |
| TS-02 | Typed common policy/activation reference, specialized schema rules, existing catalog integration and capability validation adapters | Complete; scoped qualification passed |
| TS-03 | Selector-specific Portfolio authority, bounded exact deployment resolution, candidate freeze and workflow dispatch | Complete; scoped qualification passed |
| TS-04 | Pure evaluator, all twelve variants, deterministic preference/ranking and complete evidence | Complete; scoped qualification passed |
| TS-05 | Mapped completed-only Function actor and synchronous projection | Complete; scoped qualification passed |
| TS-06 | Typed workflow acceptance and recovered Portfolio reservation/Composer intent handoff | Complete; scoped qualification passed |
| TS-07 | Scylla repositories, scoped query actors/APIs and production registration | Complete; scoped qualification passed |
| TS-08 | Isolated BDD/unit/integration/verification qualification across sections 17-19 | Complete; scoped qualification passed |

Document readiness is complete when the catalog mapping, candidate policy, variants, numerical defaults, hashes, lifecycle and gate ownership are specified without unresolved selection-policy decisions. This revision meets that boundary. Selector code completeness requires all gates and applicable isolated tests to pass. Live publication/operation additionally requires actual downstream builder/risk/data capability implementations, published profiles and enabled authorized assignments. Those operational prerequisites do not block beginning TS-01 or implementing/testing selector code with explicit fixture dependencies.

## 21. Excluded work and next action

No new selector-only catalog, general-purpose scripting, cross-asset ranking, option-chain edge modeling, exact one-unit construction algorithm, financial sizing, live risk reservation, IBKR emulator/live integration, new UI or combined five-stage live qualification is included in this selector scope. Future strategies use new qualified capability implementations and exact catalog definitions; unsupported definitions may remain drafts without a schema change.

See the [implementation plan](TradeSelection-Implementation-Plan-v1.0.md) and [executed gate evidence](TradeSelection-Implementation-Evidence-v1.0.md). Operational activation requires published exact profiles and real downstream capabilities; the selector implementation does not auto-publish the catalog.

## 22. Verified source references

- [Catalog context operations](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/IConfigurationDbContext.StrategyCatalog.cs), [graph resolution](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationDbContext.StrategyCatalog.cs), [canonical validation](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/StrategyCatalog/StrategyCatalogValidation.cs).
- [Catalog defaults](../../../../../../TomasAI.IFM.Domain.Reference.Shared/StrategyCatalog/StrategyCatalogDefaults.cs), [deployment choice](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Reference/StrategyCatalog/StrategyDeploymentChoice.cs), [legacy-compatible exact reference](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Reference/ViewModels/TradeStrategyFamilyReference.cs).
- [Portfolio assignment validation](../../../../../../TomasAI.IFM.Domain.Portfolio/Command/Actor/PortfolioFundCommandActor.cs), [current strict resolver](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundStrategyResolver.cs), [composition aggregate](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundCompositionAggregate.cs).
- [Start contract](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/Commands/StartTradeSelectionPipelineCommand.cs), [current helper](../MarketAssessmentSelectionConsumer.cs), [generic continuation](../../Command/CompleteTradeSelection.cs), [API capability registration](../../../../../../TomasAI.IFM.Application.Api.Server/Startup.cs).

Verification date 2026-09-07: implementation, test commands and results are recorded in the gate evidence.

## 23. Implemented boundary details (2026-09-07)

The runtime uses `TradeSelectionFunctionActor : BaseEventSourceFunctionActor` with frozen `_parseMap`, `_validationMap` and `_receiveMap`, an exact `Execute` request, a typed domain extension, completed-only PostgreSQL state, and a synchronous Scylla projector. It never publishes Function terminal events. Workflow acceptance recomputes the entire decision over the saved request before accepting its winner. Historical `StartTradeSelectionPipelineCommand` remains readable but has no active execution route.

`SelectionConstructionPolicy` schema 1 is the implemented narrow owning payload for the exact OrderComposition parameter version: SchemaVersion, ParameterSetId, Version, MaximumLegs (1-4), MinimumDaysToExpiry (1+), MaximumDaysToExpiry (through 730), MinimumWingWidth, MaximumWingWidth (through 10000), DeltaUnits (`UnderlyingEquivalent`), and MaximumDeltaTolerance (0-1). All fields are required; unknown/duplicate fields fail. Decimal serialization is invariant G29. This describes declared construction constraints; it does not implement quotes, strike selection, sizing, builders or risk. `ISelectionConstructionProfileResolver` and `SelectionConstructionProfileReference.FromFrozen` validate the exact published policy against each allowed structure/variant. Publication and reads retain the full source payload/hash.

Variant Settings require TargetNetDelta, BalanceTolerance, SymmetricWings, MinimumWingWidth, MaximumWingWidth and DeltaUnits. Future delta intent is +1/-1; options use underlying-equivalent delta. Balanced has zero target; Bullish/Bearish has the corresponding sign. Option wing widths must be positive. Semantic validators check the existing basic catalog leg keys and sides/rights, one expiry group and unit ratios. Example placeholder settings are authoring examples until qualified; their names never grant execution permission.

The optional `TradeSelectionVariants` role must have its exact ParameterSchema with `validator/TradeSelectionVariants/1`; both the shape DSL and complete typed rule replacement are validated. Other required roles must have owning semantic validators. Unsupported production builder/risk requirements remain fail-closed.

The saved engineering profile identities are Daily `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef701`, Weekly `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef702`, and Monthly `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef703`, version 1. `TradeSelectionDefaultProfiles.EngineeringDefaults()` returns these three complete payloads without inserting or publishing them. Workflow options pin activation ID/version/hash for each chosen horizon; each activation pins its common selector profile. There is no selector lookup by latest timeframe.

Assignment retrieval uses native Scylla pages of 64 within the exact Portfolio/Fund/mandate partition, retaining enabled and disabled effective rows matching root/horizon before asset filtering. It returns at most 16 rows plus a seventeenth overflow sentinel. A 4096 historical-row scan budget fails explicitly rather than truncating. Legacy strict resolution is unchanged.

CandidateHash is lowercase SHA-256 over UTF-8 canonical candidate JSON with an empty CandidateHash, followed by UTF-8 of the exact deployment graph ContentHash. BindingHash covers canonical typed JSON of the explicitly normalized binding with its own hash empty, after canonical selector-set ordering. New typed-evidence hashes use invariant G29 decimal values so JSON event storage cannot change identity merely by decimal scale. Envelope PayloadSha256 covers the canonical typed result fingerprint for new completions and exact payload bytes for legacy envelopes. Original catalog and Portfolio hash algorithms remain unchanged; the Portfolio snapshot property uses a scoped JSON converter to retain its original decimal spelling and defensive-copy semantics. Historical trigger constructor defaults are normalized explicitly on typed records before dispatch is saved.

`RedispatchCurrentStrategyPipelineCommand` reloads the authoritative workflow and republishes its saved intent only when workflow/revision/stage still match. It preserves the original Function request, reservation request, idempotency key, timestamps and deadline. A lost transport response does not turn a possibly committed result into rejection while recovery remains possible. Reservation waits are bounded by the fixed result deadline; late replies are observed and recorded as stopped reservations, and terminal workflows reconcile open Portfolio orders to Expired without reusing identities. Explicit redispatch is the restart/reconciliation entry point; no new automatic scheduler is introduced. The existing workflow snapshot projector remains conventional. After a projector/notification outage, operators invoke redispatch against the current PostgreSQL state; Scylla candidate projections are never approval authority.

Before executing a saved selector/composer notification, realtime loads the current PostgreSQL workflow and compares execution ID, revision, stage, running status and handoff status. Delayed snapshots from stopped or superseded workflows do not dispatch. A stop racing after this read remains a downstream acceptance concern: OrderComposition must fence its command against the current workflow and Portfolio order before any irreversible action. Repeated notification uses one deterministic command ID; delivery is at-least-once, not an exactly-once network guarantee.

## Downstream Order Composition alignment - 2026-09-07

The [Order Composition specification v1.0](../../OrderComposer/Docs/OrderComposition-Specification-v1.0.md) defines the downstream exact-contract boundary using the current five-map Function convention. It preserves accepted single-horizon upstream evidence, exact Fund-authorized selection/catalog versions and committed business-ID reservation. Composition produces one normalized unit for any of the twelve variants on Daily, Weekly or Monthly; Portfolio Risk Management owns final units and financial approval. No family policy is introduced into Regime Discovery or Market Condition. Composition gates are planned; this cross-reference does not change upstream qualification status or claim combined pipeline readiness.
