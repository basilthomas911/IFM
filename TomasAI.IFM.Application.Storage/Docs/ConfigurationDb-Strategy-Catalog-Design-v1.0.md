# ConfigurationDb Strategy Catalog Design v1.0

| Item | Decision |
| --- | --- |
| Revised | 2026-09-06 |
| Status | Catalog storage, Reference UI/APIs and Portfolio deployment references implemented; trading capabilities and TradeSelection remain deferred |
| Implementation | [ConfigurationDb catalog implementation](ConfigurationDb-Strategy-Catalog-Implementation.md) |
| Persistence owner | PostgreSQL `ConfigurationDbContext`, schema `reference_configuration` |
| TradeSelection | Implementation on hold at the user's request; TS-01 through TS-08 must be realigned before resumption |
| Scope | Reusable trading strategies, structures, variants, parameter definitions and product/timeframe deployments |

## 1. Purpose and authority

A small group of assets must support many trading approaches without adding a database column or extending `TradeStrategyType` for every new strategy. Separate trading logic from instrument class, payoff structure, variant and deployment. This document is the current authority for that separation and the proposed catalog ownership. It supersedes earlier documentation that treats the existing three strategy shapes as the permanent strategy taxonomy.

ConfigurationDb storage, Reference APIs/UI, draft legacy imports and Portfolio deployment references are implemented; the physical model, limits and verification are documented in the linked implementation document. Imports do not grant Fund permissions or publish executable strategies. TradeSelection remains on hold. Earlier integer-family contracts are historical compatibility records.

## 2. Terminology and ownership

| Concept | Meaning | Owner |
| --- | --- | --- |
| Instrument class | Futures, FuturesOption and later supported instrument classes | Existing Reference/MarketData contracts |
| Product | Reusable underlying/root with exchange and currency; distinct from an expiry contract | Existing Reference/MarketData product catalog |
| Contract | Actual future or option security, expiry, strike and option right | Existing MarketData/Securities services |
| Strategy family | Grouping of trading approaches, such as trend following, mean reversion or volatility strategies | ConfigurationDb catalog |
| Strategy definition | Trading hypothesis, applicability, required inputs and entry/exit policy references | ConfigurationDb catalog |
| Structure definition | Economic construction, such as a future, vertical, iron condor, Jade Lizard or double calendar | ConfigurationDb catalog |
| Structure variant | Versioned choices within a structure, including side, bias, credit/debit and shape constraints | ConfigurationDb catalog |
| Deployment | Exact strategy/structure/variant/parameter versions restricted to products and a timeframe | ConfigurationDb catalog |
| Fund assignment and permission | Which deployments a Fund may use, effective periods, priorities and financial constraints | Portfolio domain |
| Constructed trade | Selected contracts, leg quantities, prices and economics | OrderComposition/Trade domains |

The existing `TradeStrategyFamilyReadModel` is a compatibility catalog row combining instrument class, a limited strategy enum, product and timeframe. Its integer ID and definition version retain their existing meaning. It is not the identity of the new reusable strategy family, strategy definition or structure.

One mean-reversion strategy can be deployed on several approved roots. Several strategies can use the same root. A strategy can permit several explicitly assigned structures; a structure can serve several strategies. Neither an asset nor a timeframe determines the trading approach automatically.

## 3. Verified ConfigurationDb baseline

`ConfigurationDbContext`, `ConfigurationParameterSet` and `ConfigurationSchemaDb` already exist. PostgreSQL schema `reference_configuration` contains the six pipeline parameter tables for workflow, RegimeDiscovery, MarketCondition, TradeSelection, OrderComposition and RiskManagement, plus the separate MarketCondition assessment parameter table. Existing records use UUID parameter IDs, integer versions, schema versions, JSONB payloads, hashes and lifecycle/audit metadata.

The relational catalog now has its own context operations, transactional writes, lifecycle guards and tests. Existing pipeline parameter lifecycle enforcement still varies by kind. Actual trading capability implementations and the TradeSelection binding are not supplied merely by creating catalog tables.

Sources: [context](../ConfigurationDb/ConfigurationDbContext.cs), [parameter contract](../ConfigurationDb/ConfigurationParameterSet.cs), [schema registration](../ConfigurationDb/Schema/ConfigurationSchemaDb.cs), [SQL schema](../ConfigurationDb/Schema/ConfigurationSchemaSql.cs).

## 4. Proposed relational model

Names below describe the conceptual model. The implemented physical model factors common identity/version metadata into `strategy_catalog_identity` and `strategy_catalog_version`, with constrained entity kinds, typed parent foreign keys and the normalized relationship tables described in the [implementation document](ConfigurationDb-Strategy-Catalog-Implementation.md). It does not create a duplicate version table for each conceptual type. Catalog identities use stable UUIDs, separate from existing sequence-generated family IDs. Versioned entities use `(id, version)` primary keys; references identify both values. Identity rows have unique stable codes and creation audit. Codes and display labels are never executable behavior.

| Proposed table | Key / relationship | Responsibility |
| --- | --- | --- |
| `strategy_family` / `strategy_family_version` | Family UUID; immutable version | Grouping, description and lifecycle |
| `strategy_definition` / `strategy_definition_version` | Strategy UUID; immutable version | Hypothesis, evaluator capability, required input contract and policy references |
| `strategy_family_membership` | Exact strategy version + exact family version | Many-to-many grouping without making grouping an execution permission |
| `structure_definition` / `structure_definition_version` | Structure UUID; immutable version | Builder capability, topology and structure validation rules |
| `strategy_structure_assignment` | Exact strategy version + exact structure version | Structures explicitly supported by this strategy; assignment belongs to the immutable strategy version |
| `structure_variant` / `structure_variant_version` | Variant UUID; version references exact structure version | Side, bias, economic mode and validated specialized attributes |
| `structure_expiry_group` | Exact structure version + group key | Relative expiry roles and same/different-expiry relationships |
| `structure_leg_definition` | Exact structure version + leg key | Instrument class, buy/sell role, option right where relevant, ratio rules and expiry-group reference |
| `structure_variant_leg_rule` | Exact variant version + leg key from its exact structure version | Validated side/ratio/strike-rule choices for a variant; no unvalidated topology overrides |
| `strategy_parameter_schema` | Schema UUID + version | Parameter shape, units, required fields, bounds and semantic-validator capability |
| `strategy_catalog_parameter_set` / `strategy_catalog_parameter_set_version` | Parameter UUID; version references exact schema | Specialized strategy/variant parameters with validated JSONB payload |
| `strategy_deployment` / `strategy_deployment_version` | Deployment UUID; immutable version | Exact strategy version, horizon, effective interval and operational lifecycle |
| `strategy_deployment_structure_variant` | Exact deployment version + assigned structure/variant versions | Explicit allowed variants and their selection/composition parameter bindings |
| `strategy_deployment_product` | Exact deployment version + authoritative product reference | Small approved asset universe without copying instrument definitions |
| `strategy_deployment_parameter_binding` | Exact deployment version + stage/role | Exact existing pipeline parameter kind/ID/version/hash or catalog parameter reference |
| `strategy_capability_requirement` | Owning strategy/structure version + capability key/version | Evaluator, builder, validation, risk or data capabilities required for activation |
| `legacy_trade_strategy_family_mapping` | Legacy integer ID/version + exact deployment version | Explicit reviewed compatibility mapping; no inference from enum/display name |

Fund strategy assignments are owned and versioned by Portfolio. The conceptual `fund_strategy_assignment` references exact deployment versions and permitted variant subsets; it is not an authoritative ConfigurationDb table. Its physical migration from `FundTradeTemplateAssignmentReadModel` belongs in the Portfolio specification.

Relational foreign keys enforce references within the same PostgreSQL database. References to Scylla products or Portfolio-owned records require domain validation and frozen identity/version evidence; they cannot use cross-database SQL foreign keys. The implementation document and schema source define physical columns, indexes, constraints and context APIs. Domain-facing actor/API contracts and Portfolio migration still require their own specifications.

A deployment may select only variants whose exact structures are assigned to its exact strategy version. Each variant leg rule must reference a leg in that same structure version. Enforce these relationships with relational constraints where possible and publication-time semantic validation; changing topology creates a new structure version.

Identity and relationship columns stay relational. Specialized selection, construction and management settings use schema-validated JSONB. Do not put ownership, version references, permissions or the complete relational graph into an opaque JSON document. Child rows and membership/assignment sets become immutable with their owning published version and participate in its canonical hash.

## 5. Common structures and variants

These are desired catalog definitions, not a claim that their builders are available. Strategy logic and structure variants are independently versioned.

| Structure | Variant choices | Meaning |
| --- | --- | --- |
| Future | Long; Short | One underlying futures position with explicit side |
| Vertical | Bullish call debit; bullish put credit; bearish put debit; bearish call credit | Option right, direction and premium mode are explicit |
| Short iron condor | Balanced; Bullish; Bearish | Bull put credit spread plus bear call credit spread |
| Long iron condor | Balanced; Bullish; Bearish | Bear put debit spread plus bull call debit spread |
| Jade Lizard | Future definitions governed by supported topology and risk capabilities | Configurable legs; never assumed equivalent to a bounded-risk condor |
| Double calendar | Future definitions governed by multiple-expiry capabilities | Explicit near/far expiry groups and relationships |
| Mean-reversion futures strategy | May use the Long/Short future structure | Trading logic is a strategy definition, not a new instrument class |

For iron condors, Long/Short describes the structure side, independently of bullish/bearish bias. Balanced means a configured near-neutral net-delta target/tolerance; wing-width symmetry is a separate constraint. Premium sign and achieved exposure must be verified from actual constructed legs and prices. Display labels never establish these facts.

Leg count, expiry relationships, risk characteristics and permitted ratios belong to each structure/version. Do not impose a universal four-leg or same-expiry rule. Vertical and iron-condor definitions can impose their own same-expiry constraints; calendars require distinct expiry groups. Strategy selection does not choose actual strikes or expiry contracts.

The original Daily future, Weekly vertical and Monthly condor profiles remain useful proposed engineering deployments. They are not an exhaustive catalog, production calibration or a permanent one-strategy-per-timeframe rule. Each ITI invocation still evaluates only its triggering Daily, Weekly or Monthly horizon.

## 6. Unknown and future strategies

The catalog permits authoring a new strategy or variant as Draft without changing a closed enum. It does not make an unknown runtime implementation executable.

1. Choose or define the strategy hypothesis, family memberships, required observations and decision rules.
2. Reuse supported structures, leg/expiry patterns and versioned evaluator/builder capabilities where possible.
3. Define a versioned parameter schema, units, bounds, cross-field constraints and explicit defaults. Reject unknown or misspelled fields unless the schema deliberately defines an extension field.
4. Validate a draft against that schema and the server's supported capability registry. JSON shape validation alone is insufficient for economic or cross-leg validity.
5. Add a deployment for approved roots and one horizon, with exact parameters and allowed variants. Portfolio independently assigns and authorizes it for a Fund.
6. Publish and activate only after all required evaluator, builder, data and risk capabilities are available and qualified. Unsupported definitions remain Draft and cannot participate in live selection.

A new configuration using existing capabilities can avoid a schema or code change. A new payoff topology, evaluation algorithm, data requirement or risk model may require code and tests. Future persistence needs may still justify a schema migration. Do not support arbitrary uploaded scripts, reflection-selected class names or silent fallback to the closest known strategy.

Capability identities resolve through a trusted runtime registry with exact supported versions. Catalog metadata describes requirements; it cannot declare unimplemented code supported. Validate capabilities at publication, activation and execution binding. If a deployed process lacks a required capability, return an explicit configuration/capability failure and do not dispatch construction.

A mean-reversion policy needs its own anchor/deviation, entry and invalidation evidence. It must not obtain a trading direction merely by reversing RegimeDiscovery's direction. Required features must come from named, versioned input providers or deterministic computations over frozen evidence.

## 7. Parameters and existing template identities

Retain the existing pipeline parameter tables as their current authority. `strategy_deployment_parameter_binding` references exact existing kinds/IDs/versions/hashes for workflow, regime, assessment, selection, composition and risk. Do not duplicate those same payloads under new catalog parameter identities. The proposed catalog parameter tables hold specialized settings not already owned by a pipeline profile.

The earlier TradeSelection specification proposed `TradeSelectionTemplateDefinition` and a selector-only `trade_selection_template_definition` table. That proposal is superseded by this reusable catalog direction. Before resuming, map existing `TradeTemplateId`/version, selection hint profile and construction profile references explicitly to the new strategy, structure and deployment graph. Do not create two authoritative template/strategy catalogs or rename existing identities in place.

Required parameter categories include input requirements/freshness, applicability, entry compatibility, permitted side/bias/premium modes, variant preference/tie handling, construction constraints, and exit/management policy references where applicable. TradeSelection only consumes the selection subset; Composer and management stages consume their own settings. Portfolio hard financial limits remain separate and cannot be expanded by a strategy parameter set.

The prior selector's numeric thresholds remain engineering examples pending this mapping. The expanded catalog does not authorize unrestricted strategy optimization. Specify bounded candidate enumeration, deterministic ranking/ties and rejection evidence over Fund-authorized deployments before enabling multiple candidates.

## 8. Versioning, lifecycle and workflow freezing

Use Draft -> Published -> Retired with audit identities/timestamps and optimistic concurrency for authoring. The storage implementation seals every complete draft at insertion; editing creates a new version using an expected-previous-version check. Publish validates the entire dependency graph atomically within ConfigurationDb. Published content and its child collections are immutable; changes create a new version. Retirement blocks new activation/binding and preserves exact historical reads. Emergency disabling and in-flight cancellation remain explicit Portfolio/workflow controls rather than rewriting a frozen definition.

Publication is not Fund authorization or activation. Verify effective intervals, product eligibility, required capabilities, policy compatibility and exact Portfolio permission when binding a workflow. Missing, ambiguous, unsupported or retired configuration must not be replaced with defaults. Existing frozen executions retain their original version evidence subject to explicit operational stop rules.

Freeze exact strategy, structure, allowed variant, deployment, parameter schema and parameter-set versions/hashes, product references, capability requirements, assignment/mandate/policy versions, upstream result references and the triggering horizon. Persist canonical dependency content or resolvable immutable evidence sufficient for audit and deterministic replay. Hash ordered canonical content, not arbitrary JSON property order or mutable display metadata. Define the new hash schema without changing existing Portfolio hash semantics.

TradeSelection produces the selected strategy/structure/variant intent plus evidence. OrderComposition chooses actual securities and builds one valid unit against those exact references. RiskManagement/Portfolio retain final sizing and financial authority. Future exit rules belong to their owning management stage; storing a reference does not implement that stage.

RegimeDiscovery and MarketCondition remain market-only upstream stages. They do not query Fund strategy assignments or rank catalog variants. New strategy-specific observations must be declared and supplied downstream, not added as strategy-family branching inside MarketCondition.

## 9. Compatibility with current trade strategy families

Keep existing integer `TradeStrategyFamilyId`/`DefinitionVersion`, MessagePack keys, ReferenceDb catalog versions, product-query APIs and historical records unchanged until an explicit migration is implemented. `GetTradeStrategySymbols(family)` continues to filter instrument class and return product symbol/exchange/currency. New strategy-family taxonomy must not become that method's Futures/FuturesOption argument.

Migration must inventory all existing family references in mandates, assignments, Portfolio risk rows, workflow snapshots, query projections and UI selections. Map each exact legacy definition to reviewed deployment/strategy/structure references. A mapping may be one-to-many, but existing permissions must never expand implicitly: each new deployment requires explicit Portfolio assignment. Unknown or ambiguous mappings require correction; no display-name or SystemKey guessing.

Do not equate a new UUID strategy-family identity with an old integer risk-limit key. Preserve existing risk enforcement until a versioned Portfolio permission/risk model and migration define the replacement. Preserve original snapshots/hashes and historical reads; add new wire fields/schema versions rather than repurposing old ones. Do not delete legacy tables or dual-write competing authorities as part of this documentation change.

The current Reference Data family editor remains the existing product/timeframe catalog editor. A future Configuration UI may author families, definitions, variants and deployments; Portfolio UI remains responsible for Fund assignment. Dark Trading Theme still applies. No new editor is implemented or scheduled by this decision.

## 10. TradeSelection hold and subsequent work

All TradeSelection plan gates TS-01 through TS-08 are **on hold**. Do not implement the earlier selector-only template schema, fixed three-variant rule set or provisional wire contracts as written. Existing code remains as-is; the hold does not remove implemented upstream stages.

Before a resumed implementation plan can be treated as ready:

1. Use the implemented catalog persistence schema/context lifecycle and specify the remaining domain commands/queries, actual capability handlers and external-reference adapter registrations.
2. Define legacy family/template mapping and Portfolio assignment/risk compatibility without implicit permission expansion.
3. Reconcile the TradeSelection input/result/parameter contracts and shared-assembly dependency plan with exact catalog references.
4. Update Composer capability contracts for supported long/short futures, all four verticals and long/short condors with independent bias; distinguish desired definitions from implemented builders.
5. Revise the TradeSelection specification and TS gate plan with bounded candidate/variant selection, fixtures and tests. Preserve durable lifecycle, workflow acceptance and composition reservation requirements.
6. Resume implementation when the user returns to that work. This documentation update does not resume it automatically.

Future verification must cover relational integrity; immutable versions and concurrent publication; schema/semantic/capability rejection; exact product/horizon and Fund restrictions; no implicit permission expansion; deterministic variant choice; snapshot/hash replay; multi-expiry/leg validation; legacy wire compatibility; and end-to-end handoff through composition reservation. Historical pipeline test evidence does not qualify the catalog; the new catalog-specific tests and their limits are documented separately.

## 11. Related documentation

- [Storage implementation details](Storage-Implementation-Details.md)
- [Current Reference family contract](../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Family-Typed-Definition.md)
- [Current symbol/family catalog implementation](../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Symbol-Catalog-Implementation.md)
- [Portfolio/Fund specification](../../TomasAI.IFM.Domain.Portfolio/Docs/Portfolio-Fund-Specification-v1.0.md)
- [TradeSelection design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-High-Level-Design-v0.1.md)
- [TradeSelection specification](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-Specification-v1.0.md)
- [TradeSelection plan: on hold](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/TradeSelection/Docs/TradeSelection-Implementation-Plan-v1.0.md)
- [Trade Strategy Builder design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md)
