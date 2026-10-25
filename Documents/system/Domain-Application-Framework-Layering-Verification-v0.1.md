# Domain / Application / Framework layering verification

Date: 2026-10-25
Status: implementation and offline qualification complete. Coordinated live-market/client acceptance remains a separate rollout gate.

## Result

The declared graph has **92 production projects, 402 references, zero forbidden edges and zero cycles**. The compiled audit covers all 92 assemblies, including two projects outside the supported solution. Domain has no bound Framework/provider SDK references or references to another Domain implementation. Reusable Application libraries have no Domain implementation reference. Framework has no Application/Domain reference. Neutral Shared has no implementation-project reference.

The public-port audit follows **2,341 exported Domain types and 4,529 reachable contract types** and reports zero Framework/provider leaks. Composition roots are identified explicitly; tests are not production libraries. No migration reference exceptions remain.

## Ownership

| Previous ownership | Current ownership |
|---|---|
| Shared actor bases, contexts, supervisor, factories, mailboxes, schedulers, queues and batching runtime | `TomasAI.IFM.Application.EventModelActor` |
| Shared generic event-sourcing runtime, codecs and replay helpers | `TomasAI.IFM.Application.EventSourcing` |
| Domain.EventSourcing audit/deduplication/repositories | `TomasAI.IFM.Application.EventSourcing.Persistence` |
| Domain.EventSourcing specialized source projections | `TomasAI.IFM.Application.EventSourcing.Projections` |
| Shared.Domain / Shared.EventProducers implementations | `Application.CommandPipeline` / `Application.EventServices` |
| Eleven Domain.*.Shared contract projects | Eleven `Application.Contracts.*` projects; see the project move map |
| Monolithic Shared neutral contracts/values/helpers | `Shared.Contracts`, retaining assembly name `TomasAI.IFM.Shared` |
| NATS-specific carriers, headers, serialization/reply/trace helpers | Framework.Messaging.Nats |
| Framework-facing market data, pricing and storage signature values/ports | Neutral Shared contracts |
| Domain sequence, pricing and encoding implementation calls | Application.Identity, Application.OptionPricing and Application.Serialization boundaries |
| BrokerAccount SQL projection implementation and bounded workflow recovery journal | Application.Storage |
| Backup execution journals, recovery orchestration and provider DI | Application.DatabaseBackup.Adapters.AwsCloud / LocalWorkstation |

Neutral message ports, event/command/query contracts, identities, compatibility metadata and shared generation-fencing helpers remain available to all layers. Runtime implementations have Application namespaces. Existing business-contract CLR namespaces remain where compatibility requires them; their actual assembly ownership is Application or neutral Shared.

Trade reads an authoritative Fund order through `IPortfolioFundOrderReader`, without referencing Portfolio's concrete aggregate. The API registers the existing Portfolio event store for that capability. Existing actor responsibilities remain in Domain; this refactoring adds no business actors. The composition root now registers disposable realtime reset eligibility explicitly, preserving the previous market-data and two option-router boundaries outside generic runtime code.

## Persisted event compatibility

- The **733 original event write identities** were captured before assembly relocation. Production event-log appenders, financial metadata registration, outbox and durable replay writers use the frozen policy.
- All 733 contracts pass exact legacy write-name and old/current CLR read-name checks. Stable read aliases remain explicit; this migration does not switch writers to stable IDs.
- The legacy/current target catalog is embedded in neutral Shared. Maintenance tools and direct storage readers do not depend on API startup to resolve relocated contracts.
- Frozen payload checks cover 98 provider-contract payloads and 48 pricing/wire payloads, plain and LZ4, plus the previously committed historical corpus.
- A real namespace/assembly relocation fixture runs old and relocated executables in separate processes. Both compression modes preserve write identity and payload bytes, and the original reader reads output from the relocated writer.
- A read-only Development database comparison reread the **same 126 stored event records** captured before relocation. All decoded, with identical source bytes, reconstructed payload hashes and outcomes. This is a representative latest-per-type sample, not an exhaustive replay of every historical row or backup. No production event data or metadata was rewritten.
- The existing Portfolio legacy contractless reader remains supported. Unknown event identities remain observable; corrupt known payloads fail replay.

## Verification results

| Gate | Result |
|---|---|
| Supported solution build | Passed, zero warnings/errors |
| Two additional Framework project builds | Passed |
| Strict project graph / complete compiled references / public ports | Passed |
| Shared/runtime and Application boundary tests | 338 passed |
| Portfolio / Trade / SystemAdmin / Analytics / Feed / Reference unit suites | 479 / 1,609 / 175 / 1,211 / 595 / 247 passed |
| NATS unit tests | 126 passed |
| Storage provider unit tests | 393 passed |
| AWS backup unit tests, including original 120-contract wire fingerprint | 112 passed |
| .NET presentation/service tests | 480 passed, one pre-existing skipped test |
| Isolated snapshot/range / alternate event-log-layout integration tests | 37 / 12 passed |
| Frozen identity/payload and option-router acceptance tests | 14 passed |
| Recovery, API restart handoff and notification integration tests | 18 passed; notification tests used a separate JetStream broker |
| Real financial actor tests over isolated NATS and PostgreSQL | 5 passed |
| Backup Docker build target, Linux Release publish | Passed; no running backup service was replaced |

The table contains **5,851 passing test cases** across the selected suites, with one existing UI test skipped. The 26 focused identity tests are part of the 338 shared tests and are not counted twice. Database replay and process-relocation checks are separate qualifications.

### Issues found and corrected during qualification

- Corrected test discovery across the split backup-contract assemblies while retaining the original golden fingerprint. Generic CLR argument spellings are normalized only for assembly ownership in that fingerprint; serialized members and keys remain unchanged.
- Updated an old Trade test's expected array length from 16 to the already-existing schema-v4 length of 18, preserving its 15-field historical payload check. Production trade keys were not changed.
- Adapted a risk recovery test double to the new read-only Fund order port; its lost-reply recovery assertions remain intact.
- Fixed repeated stop/dispose of the Market Outlook publisher during failed startup rollback, with a regression test.
- Made the integration harness preserve the initial failure when cleanup also fails, and pointed its MarketData relay at the same isolated broker/scope as its actor transport. The financial actor rerun passed without the previous connection-binding/background-service failure.
- Updated the backup Docker context to include the full 15-project closure and both embedded identity catalogs. Linux publish passed.

One short wall-clock batching assertion failed under concurrent compiler load; the complete 338-test shared suite passed on rerun without changing that assertion or batching behavior.

## Reproduce

Run `scripts/Test-LayeringArchitecture.ps1`. It builds isolated outputs, includes the two Framework projects outside the solution, and fails on forbidden or missing compiled assemblies and public-port leaks.

Run `scripts/Test-EventIdentityRelocation.ps1` for independent old/new executable verification. The focused compatibility tests live in `LivePipeline.IntegrationTests/EventTypeCompatibilityAcceptanceTests.cs`. Fixture provenance and tool limits are documented under `test-support/LayeringContractFixtures` and `tools/PersistedEventIdentityInventory`.

The read-only stored-row qualification requires the configured local PostgreSQL container and the saved before-report; it must compare the same event versions. Reports contain identities and hashes rather than payloads. Local execution evidence is under `.artifacts/layering-*` and `.artifacts/layering-solution`.

## Deployment and remaining acceptance

Deploy a clean, matching build of API, MarketData Host/workers and any .NET UI/bridge components. This is a source/assembly ownership migration, not a drop-in replacement of one DLL in an old binary set. Stored payloads and legacy write identities remain compatible for rollback to the prior complete binary set. Do not emit stable write IDs or remove historical aliases as part of rollout.

The running Development servers were not restarted or fault-injected. Rust screen source was not moved or changed; dependent .NET clients/tools were rebuilt against the relocated contracts. Live-market delivery, market close/open and visible hybrid/standalone UI acceptance on the new binaries require the coordinated rollout session; the offline tests do not claim to establish those live observations.

The synthetic all-event capture tool also identified an existing unused, unkeyed `MarketDataFeedResetApiEvent` schema. Its wire contract was left unchanged, and it was absent from the inspected Development event metadata. The inventory's 733 identities should not be misread as 733 fully serialized historical payload fixtures.
