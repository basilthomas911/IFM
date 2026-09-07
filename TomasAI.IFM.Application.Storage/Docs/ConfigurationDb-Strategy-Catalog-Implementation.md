# ConfigurationDb Strategy Catalog Implementation

Revised: 2026-09-06

This implements the [strategy catalog design](ConfigurationDb-Strategy-Catalog-Design-v1.0.md), Reference command/query integration, the replacement Reference UI and exact deployment references in Portfolio configuration. TradeSelection TS-01 through TS-08 remain on hold; catalog integration does not implement trading algorithms.

## Storage and physical model

`ConfigurationSchemaDb` additively creates the catalog in PostgreSQL `reference_configuration`. Existing pipeline parameter tables, ReferenceDb family tables and legacy wire contracts are preserved.

The design's repeated identity/version tables use a normalized common supertype in this implementation: `strategy_catalog_identity` and `strategy_catalog_version`. `kind` distinguishes Family, Strategy, Structure, Variant, ParameterSchema, ParameterSet and Deployment. These seven persistence kinds are not trading strategy enums. New named strategies/variants do not need a new kind, table or column.

The identity primary key is `(kind,id)` with a unique `(kind,code)`. Each version has `(kind,id,version)`, immutable content/audit metadata, canonical SHA-256, lifecycle fields and a versioned schema marker. Variant-to-structure, parameter-set-to-schema and deployment-to-strategy references are typed composite foreign keys. Horizon and variant side/bias/premium mode are relational columns. Specialized settings and parameter shapes/values use JSONB.

Relationships are stored in these separate tables:

| Table | Data |
| --- | --- |
| `strategy_family_membership` | Exact strategy/family version membership |
| `strategy_structure_assignment` | Structures permitted by an exact strategy version |
| `strategy_deployment_structure_variant` | Variants allowed by an exact deployment |
| `strategy_capability_requirement` | Required role/code/version triples |
| `structure_expiry_group` | Named expiry groups and relative ordering |
| `structure_leg_definition` | Instrument class, side, right, ratio and expiry group |
| `structure_variant_leg_rule` | Side/ratio overrides referencing legs in the exact parent structure |
| `strategy_deployment_product` | Product identity and symbol/exchange/currency evidence |
| `strategy_deployment_parameter_binding` | Existing pipeline kind/ID/version/hash, without copying its payload |
| `strategy_deployment_catalog_parameter_binding` | Specialized catalog parameter versions by role |
| `legacy_trade_strategy_family_mapping` | Explicit exact legacy IDs/versions; no automatic permissions |

The complete definition JSON returned by reads is assembled from these relational rows. It is not stored as a second authoritative graph document. Settings cannot substitute for structural references. A trusted capability validator must reject inappropriate specialized settings for its implementation.

## Context operations

The partial `IConfigurationDbContext` exposes:

| Operation | Behavior |
| --- | --- |
| `InsertStrategyCatalogDraftAsync` | Defensively freezes input; validates shape; atomically inserts identity, version and all children; returns content hash |
| `GetStrategyCatalogAsync` | Exact version read, including retired versions; verifies reconstructed identity/content hash |
| `ListStrategyCatalogAsync` | Latest version per identity, including Draft/Retired; bounded stable-code keyset paging, maximum 128 rows |
| `PublishStrategyCatalogAsync` | Requires matching Draft/hash, validates the complete published dependency graph and capabilities, then publishes atomically |
| `RetireStrategyCatalogAsync` | Requires matching Published/hash and a valid retirement timestamp; preserves exact content |
| `GetPublishedStrategyDeploymentAsync` | Resolves and revalidates an exact effective deployment graph; returns immutable-version evidence and dependency hash |

Pass the last returned `Code` as the next list cursor. Ordering uses PostgreSQL `C` collation. Listing never substitutes an older Published version when the latest version is Draft or Retired. A version list is not a Fund-authorized candidate list.

Draft content is immutable from insertion. Editing creates version `expectedPreviousVersion + 1`, carrying the full replacement definition. A stale expected version is an authoring conflict. There is no destructive delete, in-place rename or silent retry that overwrites someone else's content. Failed multi-table writes roll back the identity and all child rows. Cancellation is propagated through database operations.

Every operation owns its PostgreSQL connection and transaction through the existing provider's connection factory. Concurrent calls do not share the repository's mutable ambient transaction. A catalog-specific transaction advisory lock serializes small catalog writes; exact dependency reads hold shared row locks through publication. Deployment reads use a repeatable-read transaction. Failed or cancelled operations roll back on disposal. Callers may retry transient transaction conflicts; no hidden retries publish a different version.

Database triggers prohibit version deletion, identity/content mutation, invalid lifecycle transitions and child additions after sealing. A deferred constraint prevents an incomplete unsealed draft from committing. Publication validates economic capabilities through the context; SQL lifecycle constraints are not an alternative capability authorization API for privileged database administrators.

Use non-default UTC timestamps at PostgreSQL microsecond precision. Publication may be future-effective. Retirement immediately excludes a version from new catalog bindings, even when requesting a historical as-of time; historical reconstruction uses exact reads or a previously frozen snapshot. Retirement does not rewrite existing workflow evidence.

## Validation and extensibility

Drafts validate identity/kind, code/text bounds, schema version, exact relationship kinds, unique child keys, supported deployment horizons, expiry references/cycles, leg rights/sides/ratios, parameter references and bounded JSON. Maximum definition size is 262144 UTF-8 bytes, each child collection is limited to 128 entries, and resolution is bounded to 256 definitions and 32 dependency levels.

`CatalogParameterShape` is a documented small parameter-shape DSL, not a general JSON Schema engine. It supports Object, Array, String, Decimal, Integer and Boolean; required object properties; numeric bounds; string/array length bounds; string choices; nested item/property shapes; and units. Unknown shape fields, unknown parameter properties, missing required fields and null/type mismatches are errors. All values are explicit; no silent default injection occurs. Cross-field/economic rules belong to the registered semantic validator.

Hashing sorts named relationships and JSON object properties, preserves array order where meaningful, rejects duplicate JSON properties, normalizes numeric spelling and rejects numbers that cannot be represented without decimal precision loss. Metadata and complete child contents participate in the definition hash. Lifecycle timestamps/status do not change the content hash. Existing Portfolio and pipeline hash algorithms are unchanged.

`StrategyCatalogCapabilityRegistry` resolves exact `(role,code,version)` validators registered by trusted server composition. Roles are evaluator, builder, validator, risk and data. Registration is server code, not an uploaded script or catalog assertion. Publication requires evaluator/data support for strategies; builder/risk support for structures; and semantic validators for variants, parameter schemas and deployments. Parameter-set values are checked against their exact schema and its semantic validator. Deployment structures must belong to the selected strategy version.

`IStrategyCatalogReferences` is the domain adapter contract for exact product evidence and reviewed legacy mappings. Missing adapters or capabilities block publication/binding. The server now registers an authoritative product/legacy-reference adapter. Its trading capability registry remains empty pending qualified TradeSelection implementations; direct contexts without adapters also fail closed. This change does not claim builders for Jade Lizards, calendars or any other strategy exist. Application composition must register actual qualified capabilities and an authoritative Reference adapter before those deployments can be published. Tests use explicitly isolated fixture capabilities and references.

Existing pipeline parameter bindings use a closed table-name mapping and exact ID/version/hash. Publication and binding require an effective Published row. Specialized catalog parameter sets use their own exact schema; pipeline payloads are not duplicated there. Product and legacy-family references cross database boundaries and therefore require domain validation rather than SQL foreign keys.

## Reference APIs, migration and Portfolio integration

`Domain.Reference.Shared/StrategyCatalog` owns the catalog DTOs. Storage owns persistence and capability enforcement; `Domain.Reference/StrategyCatalog/StrategyCatalogService` is the application boundary. ReferenceQuery's `StrategyCatalog` verb supports paged lists, exact reads, deployment choices and published graph validation. The existing command actor host accepts the new `Catalog` verb for SaveDraft/Publish/Retire and rejects old family mutation verbs. NATS and HTTP clients use the same actors. HTTP routes are `/api/reference/strategy-catalog/query` (POST query parameter wrapper) and `/api/reference/strategy-catalog/command` (POST typed command).

The transport sends explicitly typed JSON inside MessagePack envelopes so exact decimal settings do not depend on a dynamic formatter. Deployment choice pages have `Items` and `NextCode`, at most 64 rows and a bounded byte size; clients follow the cursor rather than assuming a full page. Exact versions remain immutable. Repeating an unchanged save does not create a new row; a conflicting same-version payload is rejected.

Normal API startup creates the catalog schema and runs the repeatable `StrategyCatalogMigration`. It inserts only the three named default families (Futures, Vertical Spreads, Iron Condor) and their supporting graph (22 definitions). Legacy imports are explicit maintenance: `--migrate-strategy-catalog-only` also imports each latest active legacy family as a separate Draft deployment with exact source provenance and product metadata, without listeners/actors/feeds. Ordinary startup does not recreate removed legacy drafts. Neither mode publishes or overwrites a catalog version, grants Fund permissions or enables assignments. Missing/ambiguous products require explicit resolution; startup verification takes precedence.

Reference Data Manager now hosts `StrategyCatalogReferenceView` and `StrategyCatalogDefinitionEditor`. These cover metadata, structure legs/expiries, long/short and balanced/bullish/bearish variants, credit/debit premium, wing controls, nested parameter schemas/values, products and deployment bindings. See the [UI guide](../../TomasAI.IFM.UI.Net/Docs/Strategy-Catalog-Reference-UI.md).

Fund mandates, assignments and Portfolio risk policies write schema-v3 exact Deployment GUID/version references. Legacy fields remain readable and are omitted from new authority decisions. Null new fields are omitted from historical JSON, preserving historical canonical hashes. Names do not implicitly map permissions. Policy limits for newly selected deployments start disabled/zero; assignments start disabled and take their product/timeframe/profile identities from the selected deployment. Enabled assignments and active policy limits require published, capability-supported deployments. Fund assignment schema-v3 versions use the next Fund aggregate revision, under its existing optimistic concurrency guard.

Legacy UI and storage implementations are retained, marked Legacy and removed from normal authoring routes. Historical rows and contracts are not deleted. The [retirement register](../../TomasAI.IFM.Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md) separates code eligible for removal after user UI verification from historical evidence that still needs retention.

Fund authorization, risk allocation, reservations and workflow activation remain Portfolio-owned. A catalog snapshot is evidence, not a permission grant. No broker/emulator integration is changed. Actual evaluator/builder capabilities and TradeSelection algorithm gates remain deferred.

## Verification

The storage test project contains isolated `StrategyCatalogContractTests` (Category=Unit) and PostgreSQL `StrategyCatalogDbTests` (Category=Integration). The configuration storage fixture and catalog actor integration test use `IFM_POSTGRES_CONFIGURATION_TEST_CONNECTION`, defaulting to `Host=localhost;Port=5432;Database=ifm-configuration-integration-tests`. Provision this separate database before running them; tests create its schema. Do not point the override at the Development API database (`event-source-test-db`), because tests retain generated catalog fixtures with unique IDs/codes.

The Development catalog was cleaned of integration fixtures, the superseded Directional/RegimeAligned example, and four unreferenced imported Draft deployments. It retains exactly three Family definitions, three Strategy definitions, four Structures and twelve Variants. Fund, policy, assignment and order references were checked before removal, with a pre-cleanup schema backup retained under `.test-results/strategy-catalog-cleanup/`. This was an explicitly requested maintenance cleanup; normal catalog deletion/immutability rules are unchanged. Verification passed: 9 catalog unit cases (including default startup and explicit import), 19 storage integration cases, 1 actor integration case, and the API build. After those runs the Development database still contained 22 definitions and no test versions; generated fixtures appeared only in the separate test database. The API build used an isolated output directory; rebuild the normal API deployment before its next startup to apply the import change.

```powershell
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~StrategyCatalog'
```

Coverage includes deterministic hashing, defensive copies, unknown names/capabilities, parameter shape and semantic validation, multi-expiry topology, actual normalized PostgreSQL round trips, transaction rollback, concurrent authoring, publication guards, dependency retirement, external reference rejection, keyset paging and additive schema initialization. These are catalog persistence tests, not qualification of trading strategies or Portfolio authorization.

Verification on 2026-09-06: the Storage build succeeded with zero warnings/errors. The final `FullyQualifiedName~ConfigurationDb` run passed **59 tests, zero failures/skips**: 22 catalog contract tests, 19 catalog PostgreSQL tests and 18 existing MarketCondition configuration regression tests. Schema creation and test data writes were exercised against the configured PostgreSQL integration-test database; that earlier run did not populate application defaults. The subsequent Reference integration migration populated the configured Development catalog with drafts; no strategy was activated.


### Reference/Portfolio/UI integration verification (2026-09-06)

The updated API server and WinForms dependency builds passed. Final selected suites passed **277 tests with no failures or skips**:

| Suite | Passed | Evidence scope |
| --- | ---: | --- |
| Storage ConfigurationDb | 59 | Real PostgreSQL catalog transactions, lifecycle, references, hashes and affected configuration regressions |
| Reference unit | 51 | Authoring examples, exact transport payloads, retries, draft imports, legacy read-only commands and existing Reference behavior |
| Portfolio unit | 130 | Existing configuration behavior plus exact deployment risk limits and assignment revision/permission migration |
| Reference actor integration | 1 | Serialized command dispatch through the real catalog service to PostgreSQL: save/retry, publish, new version and retire |
| Selected WinForms system tests | 36 | Structured variants, nested parameters, product metadata, Fund/policy selectors and the rendered Reference dialog's Cancel/Close behavior |

The actor integration test invokes the serialized actor boundary with a fixture actor context; it is not a claim of a live NATS network acceptance run. The rendered UI test uses fixture Reference APIs; database persistence is exercised separately by the PostgreSQL suites. Manual user acceptance of the new UI is still required before deleting Legacy editor code.

The actual Development maintenance process ran successfully against configured PostgreSQL and Scylla connections, including a repeat run: **18 starter definitions, 4 legacy deployment imports, 0 imports requiring product resolution**. Exact reads verified persisted hashes. Existing unrelated catalog/test records were preserved. No imports were automatically published and no Fund permissions or assignments were changed by migration. A rendered Reference screenshot is retained under `test-support/strategy-catalog-evidence/configuration-strategy-catalog.png`.


### Three-default presentation correction

`StrategyCatalogDefaults` replaces the generic startup grouping with Futures, Vertical Spreads and Iron Condor. Reference defaults to these named families and their supporting definitions; the full catalog is an explicit display option. The initial UI update hid integration fixtures; the subsequently requested Development cleanup removed those fixtures, the obsolete generic starter definitions and their unused imported drafts. Normal startup restores only the 22 default/supporting definitions; explicit legacy maintenance imports target the corresponding named strategy. Earlier verification counts above describe the initial 18-definition seed.
