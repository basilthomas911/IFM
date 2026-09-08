# Order Composition Implementation Record v1.0

| Item | Value |
| --- | --- |
| Date | 2026-09-08 |
| Baseline | `943d2226`; composer changes are in the working tree |
| Scope | OC-01..08 implementation and owned automated qualification |
| Status | OC-01..08 code complete; automated qualification passed |
| Authority | [Specification](OrderComposition-Specification-v1.0.md), [plan](OrderComposition-Implementation-Plan-v1.0.md), [actor conventions](../../../../../../Documents/system/Actor-Implementation-Conventions.md#133-functionactor-convention) |

## Implemented behavior

Order Composition now receives the exact accepted Trade Selection intent, immutable ConfigurationDb graph, Fund reservation and a qualified frozen market snapshot. It builds one unapproved futures, vertical-spread or iron-condor unit on the single triggering Daily/Weekly/Monthly horizon. All twelve side/bias/premium variants have positive construction fixtures. A valid scope without an eligible construction returns NoCandidate; missing/invalid required data or a pricing failure returns Failed.

The Function has five frozen maps, strongly typed context, List<ValidationError> extensions, typed execution policy, separate Execute/Complete/Fail extensions and pure Models. Its completed result is a typed field in the outer message. Shared MessagePack serialization handles transport, storage and content-size measurements; semantic hashing does not serialize MessagePack or clone through it.

Preparation acceptance saves the exact Execute request in the authoritative workflow event before dispatch. Recovery reuses its identity, timestamps, snapshot and fingerprint. The Scylla result projection precedes the completed-only PostgreSQL Function append. Retry repairs projection-before-append orphans. Workflow acceptance independently recomputes the candidate from the saved inputs, then records one risk intent or a NoTrade stop. Old opaque successes cannot enter the new risk path. Function/workflow/candidate expiry and stale revisions prevent late continuation.

## Gate closure map

| Gate | Implemented code and owned evidence |
| --- | --- |
| OC-01 | Execute keys 0..20; result 0..19; candidate 0..30; leg 0..13; typed envelope key 11; append-only workflow keys 32/28; independent pricing DTOs and explicit adapter; key/round-trip/legacy/hash/size tests |
| OC-02 | Exact `OrderCompositionRules` ParameterSet/ParameterSchema role; complete defaults and strict recursive schema authoring; builder/rules capabilities; declared deployment rule validation; bound/grid validation; real PostgreSQL publish/reject/retire tests |
| OC-03 | Existing qualified capture and Scylla preparation plus atomic saved Execute acceptance; real reservation/preparation/lost-reply recovery test; no historical Start dispatch; generation-aware discovery cleanup |
| OC-04 | Pure ES futures/vertical/condor builders; 36 variant/horizon constructions; real Black-76 inversion and Greeks; signed prices/ticks, participation, general unequal-wing payoff, deterministic ranking, definition/freshness/expiry/roll checks |
| OC-05 | Mapped Function/context/policy/repository/projector and bounded telemetry; cancellation, late projection, failure injection, replay and conflict tests; real NATS/Scylla/PostgreSQL completion and orphan retry |
| OC-06 | Typed recomputation and exact reservation/selected-leg checks; NoCandidate stop; once-only risk revision; real workflow acceptance and replay; timeout mapping and terminal discovery release |
| OC-07 | Additive invocation/history tables, LWT/idempotence/conflict checks, exact queries and partitioned paging; Fund access checks; acceptance read from PostgreSQL authority even when Scylla workflow projection is withheld |
| OC-08 | Unit, BDD, verification, affected integration regressions and API compilation; precise final results below |

## Persistence and compatibility

- `TradeSchemaDb.CreateAllAsync` creates `order_composition_invocation` and `order_composition_history`; no truncation/backfill is performed. Integration tests populated the test keyspace with actual Function completions.
- Existing Start command/envelope/workflow slots remain readable. New shared route metadata identifies the Function actor. Legacy inflight prepared Start requests require a new workflow; they are not silently recaptured or interpreted as new Execute requests.
- The final request omits saved dispatch/self references from its nested workflow context. The owning workflow separately retains both preparation evidence and the accepted request.
- Domain pricing records preserve the prerequisite producer's fields, including pricing-convention schema 2 key 24 premium tick policy. Forward quote, exercise style, linked underlying, multiplier, currency, settlement, calendar, Treasury conversion and pricer version remain explicit.
- Lifecycle loading/execution budgets are frozen and cannot be adaptive rule targets. Other numeric adjustments require explicit bounds/grid and are checked against the original variant/outer policy.
- Schema authoring supports eight predicate levels. The general catalog shape limit is 32 levels / JSON representation limit 96, with the existing 256 KiB definition bound and strict unknown-field rejection. Existing SelectionConstructionPolicy v1 content/hashes are unchanged.
- Canonical composition hashing excludes fields marked non-contract (`IgnoreMember`), including local username/current diagnostic clock. Cached JSON contract metadata supplies that exclusion; binary transport remains independent.

## Verification results

Final runs on 2026-09-08:

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Full Domain.Trade unit suite | 901 | 0 | 0 |
| Full Domain.Trade BDD suite | 36 | 0 | 0 |
| Pipeline qualification verification (Market Assessment, Trade Selection, Order Composition) | 13 | 0 | 0 |
| Composer and affected upstream/catalog/business-projection integration | 25 | 0 | 2 |
| **Total automated test cases** | **975** | **0** | **2** |

The two existing opt-in live checks were `Inspect_live_reference_definitions` and `Published_reference_live_worker_pricing_durable_handoff_replacement_and_close`; they were not enabled in this regression run. They are outside the controlled composer qualification claim. The earlier targeted composer integration run passed all eight cases with no skips, including actual PostgreSQL catalog publication and NATS/Scylla/Function/workflow persistence.

Reproduction commands (PowerShell, from repository root; required local test services running):

```powershell
dotnet test TomasAI.IFM.Domain.Trade.UnitTests --no-restore
dotnet test TomasAI.IFM.Domain.Trade.BDDTests --no-restore
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests --no-restore --filter FullyQualifiedName~QualificationTests
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests --no-restore --filter 'FullyQualifiedName~TradeSelectionRuntimeTests|FullyQualifiedName~CompositionBusinessProjectionTests|FullyQualifiedName~MarketConditionAssessmentRuntimeTests|FullyQualifiedName~RegimeDiscoveryContextRegistrationTests'
dotnet build TomasAI.IFM.Application.Api.Server --no-restore -v minimal
```

TRX: `trade-unit-all.trx`, `trade-bdd-all.trx`, `pipeline-qualification.trx`, `composer-and-upstream-integration.trx`. Final API compilation passed with zero warnings/errors. [Explicit nested wire manifest](OrderComposition-Wire-Manifest-v1.md).

Artifacts are under `.test-results/ocp/composer/` (ignored runtime output), with build/test logs under `.test-results/ocp/`. Tests use controlled complete snapshots and the actual Black-76 implementation; the integration tests use real local NATS, PostgreSQL and ScyllaDB. The run used an isolated temporary NATS server on loopback port 14222 (removed after verification). Configuration tests used the host's test event-source database; Scylla composition results used `trade_test_db`. Named downstream risk/Portfolio fixtures remain confined to tests.

## Operational boundary

This completes the composer implementation, not financial risk approval, sizing, broker submission or emulator qualification. Production publication remains blocked when a required Risk Management capability is unavailable. Engineering profiles are authoring output, not automatic production defaults. Promotion requires exact reviewed deployments, assignments, activation and qualified live reference/snapshot inputs. Existing provider/pricing prerequisite evidence remains in its separate records; this test run does not claim a new live-provider soak or five-stage trading acceptance exercise. No UI or broker connection changes are included.
