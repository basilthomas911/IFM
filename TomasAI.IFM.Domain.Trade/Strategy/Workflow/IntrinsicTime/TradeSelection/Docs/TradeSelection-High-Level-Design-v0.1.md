# TradeSelection High-Level Design

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
| Revision | 0.8 / 2026-09-07 |
| Status | Catalog-backed selector implemented; final qualification in progress |
| Specification | [Detailed specification v1.1](TradeSelection-Specification-v1.0.md) |
| Plan | [Implementation plan v1.1](TradeSelection-Implementation-Plan-v1.0.md) |
| Actor standard | [Shared Function actor conventions](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention); RegimeDiscovery and MarketCondition are the implementation references |
| Historical filename | TradeSelection-High-Level-Design-v0.1.md retained for existing links |

This revision supersedes the earlier single-template and timeframe-specific strategy mapping. The ConfigurationDb catalog and Portfolio deployment references now exist. The selector actor, evaluator, persistence, queries and reservation continuation are implemented; the evidence document tracks qualification. The specification contains normative fields, parameter defaults, ordering, failure semantics and test fixtures; this document explains the resulting design.

## 1. Purpose and position in the workflow

TradeSelection answers: given the accepted market evidence and this Fund's exact permissions, which configured deployment and structure variant should proceed to construction? It returns one explicit Selected intent or an explained NoTrade. Technical/configuration failure remains distinct from ordinary incompatibility.

```text
Futures ITI signal for one timeframe
 -> RegimeDiscovery
 -> MarketCondition assessment
 -> TradeSelection
 -> OrderComposition (one normalized unit)
 -> RiskManagement / Portfolio financial authority
 -> downstream execution
```

Only Daily, Weekly and Monthly are supported. Each invocation uses its trigger's single timeframe throughout; it does not wait for three independent timeframe results. A future is the signal source, not a restriction that excludes options on that root.

RegimeDiscovery describes direction, phase, strength, volatility and structure. MarketCondition describes market conditions, availability, liquidity, session/event/stress context and limitations for that horizon. Neither decides the Fund's preferred family/variant. TradeSelection applies strategy-specific suitability to those immutable accepted results.

## 2. Ownership and existing catalog

| Owner | Responsibility |
| --- | --- |
| ConfigurationDb | Versioned Family, Strategy, Structure, Variant, ParameterSchema, ParameterSet and Deployment graph |
| Reference / MarketData | Authoritative underlying product ID, symbol, exchange/currency; actual contracts are downstream data |
| Portfolio | Exact Fund permissions, deployment assignments, priority, effective authority, policies and business-ID reservation |
| TradeSelection | Bounded candidate enumeration, suitability, deterministic choice and complete decision evidence |
| OrderComposition | Exact contracts, expiries, strikes, prices, validated leg ratios and one-unit economics |
| Portfolio Risk Manager | Final units, current exposure/reservations, financial limits and atomic risk authority |

Reuse the [implemented normalized catalog](../../../../../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Implementation.md). Common identity/version rows and typed relationship tables already support the required combinations; no selector-only template table or new strategy-specific columns are needed for this scope.

Family membership groups trading approaches and is not execution permission. A Deployment pins one Strategy, allowed Structure/Variant versions, products, one horizon and exact parameter bindings. A Variant pins its Structure. Every reference uses CatalogKey `(kind, GUID, int version)` and a verified content hash. Specialized parameter sets pin their own schema versions. Unknown future strategies can remain draft catalog definitions; they require qualified capabilities before execution, not a silent fallback to an existing shape.

## 3. Compatibility with Portfolio contracts

Current schema-3 assignments already use `TradeStrategyFamily.CatalogDeployment` as the exact permission reference. Their legacy-named `TradeTemplateId/TradeTemplateVersion` fields carry the Deployment ID/version. They do not identify the Strategy or Structure. Preserve old keys and historical integer-family records; new selection requires schema-3 mappings and exact permissions.

Resolve pipeline parameters by the unique TradeSelection and OrderComposition kinds as the current Portfolio authoring path does, preserving each stored Role string and hash. Multiple bindings of either kind are ambiguous. Specialized parameters use the deployment's existing role-to-ParameterSet links; there is no implemented per-variant pipeline-binding column to assume.

## 4. Requested strategy coverage

The existing catalog examples contain twelve initial variants:

- Futures: Long/Bullish and Short/Bearish.
- Verticals: bullish call debit, bearish put debit, bullish put credit and bearish call credit.
- Iron condors: Short/Credit or Long/Debit, each independently Balanced, Bullish or Bearish.

All twelve may be configured for Daily, Weekly or Monthly. The initial three policy defaults differ by horizon identity, not by forcing one structure onto each timeframe. Existing default UI families remain Futures, Vertical Spreads and Iron Condor.

Side, bias and premium mode are independent intent fields. A short condor is not necessarily bearish; a long condor is not necessarily bullish. Balanced maps to a Neutral directional mandate permission. Wing symmetry is independent of delta balance. Actual premium/net-delta/width feasibility is checked by the exact composition profile and builder; selection does not choose strikes or infer final option economics.

## 5. Freeze authority and enumerate bounded candidates

Before workflow acceptance, resolve the exact Portfolio/Fund authority and pin a common root/horizon selection policy in activation configuration. Capture one UTC as-of time. A selector-specific Portfolio resolver preserves known denial states and zero/multiple effective assignments, while the existing strict resolver remains unchanged for its other callers.

From enabled, effective, exactly permitted deployment assignments, resolve published catalog graphs and exact policies; enumerate matching products and allowed variants. Candidate identity retains Fund AssignmentVersion, Deployment, Strategy, Structure, Variant and product. Preserve all Family memberships as provenance without producing duplicate candidates.

Defaults bound the set to 16 assignments, 64 candidate intents, 256 distinct catalog definitions, 32 dependency levels and 262144 binding bytes. Overflow fails explicitly; no incomplete first page or truncated ranking is valid. Graph definitions/payloads shared by candidates are retained once with exact hashes. The complete accepted Portfolio/catalog/upstream evidence remains available in the result context.

Missing or malformed enabled authorized configuration, unsupported capability and mismatched versions fail the binding. Disabled/unpermitted assignment rows are explained exclusions and do not require their drafts to be published. No authorized candidates is a normal NoTrade with the pinned policy and authority evidence.

ConfigurationDb graph transactions and Portfolio reads are separate. Capture and revalidate consistent exact evidence before acceptance; do not claim a cross-database atomic snapshot. After acceptance, replay uses frozen evidence. New publication/retirement affects later bindings, while explicit cancellation and current Portfolio financial safety remain effective downstream.

## 6. Suitability and deterministic choice

All candidates share the same exact common selection policy for the invocation. It defines thresholds, mandatory restrictions, execution/result limits and ranking semantics. Initial confidence minima are 0.50. The optional exact specialized `TradeSelectionVariants` parameter role can replace a deployment's complete variant rule set within common constraints. Neither specialized policy nor favorable market evidence can broaden Fund permission.

Apply global permission/market gates, then each candidate's direction and categorical compatibility rules. Neutral can select a balanced condor when its policy permits the condition; it does not select a directional future. The specification supplies complete defaults for debit/credit and long/short condor compatibility using already accepted classifications. These test defaults do not estimate option premium edge and need no new provider download.

Among compatible candidates choose by:

1. Lowest numeric Fund assignment Priority.
2. Lowest effective variant Preference.
3. Exact deployment, strategy, structure and variant GUID text/version, then product ID and assignment version.

The last step uses specified ordinal/canonical ordering, not display names, runtime hashes, retrieval timing or random selection. Preserve compatible lower-ranked alternatives separately from incompatible candidates. There is no weighted probability or financial opportunity score in V1. A candidate failing permission/compatibility never wins because of priority.

## 7. Output and boundaries

Selected contains the exact assignment/deployment/strategy/structure/variant/product intent, independent Side/Bias/PremiumMode, policy references, all specialized parameter/schema evidence, and full immutable upstream/authority context. NoTrade has no selected intent, but retains evidence and ordered reasons. Failed records technical/configuration problems.

The pure evaluator receives only frozen inputs and a workflow-frozen evaluation instant. It does not read latest data, call broker/provider services, size trades, select exact contracts, modify upstream assessments or widen permissions. Every output is bounded and hashed; it cannot omit authority to fit transport.

## 8. Mapped Function lifecycle

TradeSelection will inherit BaseEventSourceFunctionActor, like RegimeDiscovery and the aligned MarketCondition, using immutable _parseMap, _validationMap and exact-type _receiveMap. Domain execution remains a command extension. The workflow freezes and durably records the request, evaluation time, identity and deadline before invoking TradeSelectionPipelineFunction/Execute over Core NATS.

The base validates and loads completed-only state, evaluates when absent, synchronously projects the candidate completion to Scylla, appends completed state to PostgreSQL and returns the typed result. Matching retries return the original completion without reevaluation/projection. Conflicts fail closed. Failed calculations, projection or append return typed failures; no Function Processing/Failed event is saved or published. A projection without a successful append is orphan evidence, not workflow authority.

The workflow Realtime actor translates the direct Function reply into deterministic workflow commands. The workflow owns durable acceptance/failure, expiry and later reservation recovery. This replaces the earlier selector Command actor/durable EventProjector proposal. Historical contracts remain readable; they do not create a second active selection path.

The implementation reference is the [mapped MarketCondition actor](../../MarketCondition/Function/Actor/MarketConditionFunctionActor.cs), alongside [RegimeDiscovery](../../RegimeDiscovery/Function/Actor/RegimeDiscoveryFunctionActor.cs). The selector uses the same typed context, command extension, completed-only state repository and synchronous projector boundaries. Request attempts are logged without reserving command IDs in the command audit path, so an uncommitted failed attempt does not suppress a legitimate identical retry. Actor conformance is an implementation gate, independently of the strategy-selection rules.

## 9. Workflow acceptance and guarded composition handoff

Validate the typed result, exact winner, input lineage, binding/hash, revision and current expiry before acceptance. Generic Completed cannot mean Selected. NoTrade stops without composition; invalid/expired results cannot dispatch.

For Selected, persist a pending Portfolio composition-ID reservation with exact saved request/hash/idempotency key. Preserve the original frozen snapshot revision and hash. Map the legacy-named template fields to the selected Deployment. The existing reservation request does not hold the full variant graph, so retain that graph in accepted workflow context and pass it explicitly to OrderComposition.

Only a validated committed reservation can advance the workflow and create a durable composition-start intent. Recover unknown responses with the same idempotency key and IDs; expired/cancelled workflows cannot be reopened by late replies. Reserve one primary strategy instruction, not one trade ID per option leg. This reserves business identities, not risk capacity.

Composer builds one unit against the selected exact intent and may return NoCandidate. It cannot switch to another deployment/variant or timeframe. Portfolio Risk Manager then determines final units and applies current risk, policy, reservation and emulator/account safety evidence. Detailed payoff/sizing belongs to those operators; no credit-condor shortcut may be reused as validation of a long debit condor.

## 10. Capability and operational readiness

The catalog already validates required capability role/code/version through a trusted registry. The API currently registers an empty registry, and example structures merely declare requirements. Implement real selector evaluator/data/semantic capabilities through TS-02/04; actual builder/risk implementations remain separate.

Isolated selector tests may use explicitly named downstream fixture validators and captured composition boundaries. Production publication/binding must continue to reject unsupported requirements. Catalog-aligned documentation and selector code completion do not by themselves qualify live strategies, create deployable construction profiles or connect a broker. IBKR emulator precedes live broker integration.

## 11. Verification and current stage

The [specification](TradeSelection-Specification-v1.0.md#17-qualification-fixtures) defines TS-C01 through TS-C40, including all 36 variant/timeframe positive cases, Fund denial, multiple candidates, deterministic ties, exact profile/schema mapping, graph bounds, hashes, lifecycle recovery and reservation handoff. Unit, BDD, PostgreSQL/Scylla/NATS integration and verification evidence have separate ownership in the plan.

The runtime now uses the typed selector Function, exact catalog binding, deterministic evaluator, completed-only persistence, scoped queries and guarded Portfolio reservation handoff. Generic historical completion cannot bypass the typed winner validation.

Implementation and isolated qualification are recorded in [gate evidence](TradeSelection-Implementation-Evidence-v1.0.md). The Strategy observation UI and combined five-stage qualification remain deferred until the five operators are code complete.

## 12. Source evidence

- [Catalog contracts](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Reference/StrategyCatalog/StrategyCatalogContracts.cs), [examples](../../../../../../TomasAI.IFM.Domain.Reference.Shared/StrategyCatalog/StrategyCatalogExamples.cs), [context](../../../../../../TomasAI.IFM.Application.Storage/ConfigurationDb/ConfigurationDbContext.StrategyCatalog.cs).
- [Portfolio assignments](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Portfolio/ViewModels/FundTradeTemplateAssignmentReadModel.cs), [current resolver](../../../../../../TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundStrategyResolver.cs), [reservation contracts](../../../../../../TomasAI.IFM.Domain.Strategy.Contracts.Shared/Portfolio/Contracts/PortfolioWorkflowContracts.cs).
- [Assessment boundary](../../../../../../TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/MarketCondition/Assessment/MarketConditionAssessmentContracts.cs), [current selector helper](../MarketAssessmentSelectionConsumer.cs), [generic completion](../../Command/CompleteTradeSelection.cs), [API startup](../../../../../../TomasAI.IFM.Application.Api.Server/Startup.cs).

Source/document review: 2026-09-07. No runtime qualification is claimed by this revision.

## Implementation alignment ? 2026-09-07

The catalog-backed selector now implements the Function actor boundary, frozen authority and accepted upstream evidence, all twelve basic variants, deterministic Selected/NoTrade decisions, Scylla history/query projections, and workflow-owned reservation handoff. The workflow stores its exact Function dispatch and pending reservation before sending them. Recovery resends those saved identities; only an accepted selected result with a committed valid Portfolio reservation can advance to Order Composition. See the specification section 23 and implementation evidence for operational boundaries and test results. Combined five-operator qualification and observation UI remain separate work.

## Downstream Order Composition alignment - 2026-09-07

The [Order Composition specification v1.0](../../OrderComposer/Docs/OrderComposition-Specification-v1.0.md) defines the downstream exact-contract boundary using the current five-map Function convention. It preserves accepted single-horizon upstream evidence, exact Fund-authorized selection/catalog versions and committed business-ID reservation. Composition produces one normalized unit for any of the twelve variants on Daily, Weekly or Monthly; Portfolio Risk Management owns final units and financial approval. No family policy is introduced into Regime Discovery or Market Condition. Composition gates are planned; this cross-reference does not change upstream qualification status or claim combined pipeline readiness.
