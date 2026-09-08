# Order Composition Prerequisite Implementation Record v1.0

Current closure status and schema-2 premium tick-rule requirements: [closure audit](OrderComposition-Closure-Audit-v1.0.md).

The FMP Treasury source/convention blocker is superseded by the implemented, live-tested [official Treasury provider](OrderComposition-Official-Treasury-Implementation-v1.0.md). Historical results below describe the earlier FMP adapter. FMP remains the economic-calendar source; current Treasury imports/pricing use `USTreasury` with an explicit 2026 publication policy.

| Item | Value |
| --- | --- |
| Date | 2026-09-08 |
| Status | Runtime prerequisites complete for the reviewed scope; 4,268 definitions published, 30-minute combined live and maximum-scope controlled qualification passed; broader deployment acceptance is separate |
| Plan | [Prerequisite plan](OrderComposition-Prerequisite-Implementation-Plan-v1.0.md) |
| Composer document | [Order Composition implementation plan](OrderComposition-Implementation-Plan-v1.0.md) |
| Change scope | Uncommitted working-tree implementation; preserve unrelated pre-existing actor/workflow changes |

## 1. Implemented components

### Reference publication and combined qualification

The [publication and qualification record](OrderComposition-Reference-Publication-and-Qualification-v1.0.md) defines the current immutable bundle, the complete 726-contract expiry scope, supported product/calendar limits, diagnostic additions and repeatable runner. The current profile is `/v2`; the initial `/v1` holiday treatment was corrected through a new immutable version. Real Scylla bundle restart/idempotency/conflict tests pass. The combined DataBento/worker/Black-76/Scylla/PostgreSQL test has passed a short canary; elapsed evidence and failed attempts remain explicit in the live register.

This continuation also verifies next-value-date reconstruction using committed PostgreSQL two-/four-leg ownership and controlled UTC transport. Worker diagnostics now validate inside the observation boundary so malformed evidence cannot crash the failure-response path; unavailable evidence remains unhealthy.

### Current continuation: committed ownership and clock repair (2026-09-08)

- Concrete `CommittedCompositionSubscriptionSource` reloads genuine committed event-log entries and validates event identity, stream version, exact selected legs and immutable pricing plan. Workflow, option working-order and option position authorities are separate. Position acquisition projects before working-order release. Explicit closure releases only that owner; unknown state and UI deletion retain existing ownership.
- `PostgresCommittedBusinessEventJournal` uses per-event durable projection receipts, including late commits and crash-between-intent-and-receipt retries. This is separate from UI notification. Complete source snapshots carry the real stream version; contiguous deltas retain their original validation. Legacy workflows without composition ownership are receipted without consuming bounded authority capacity.
- `composition_route_plan` in MarketDataServiceDb preserves exact canonical/native identities and reference policy, without quotes, generations or ephemeral leases. Both new PostgreSQL repositories use the standard provider credential resolver. Startup schema initialization includes the plan and receipt tables.
- `DurableCompositionRuntime` is registered as one hosted singleton and one reconciler interface instance. It reloads committed intent, merges durable futures with core subscriptions atomically, reconstructs option chains after generation/date changes, refreshes business-owned contexts away from callbacks and acknowledges delivery only after complete current-revision realization. Core rollover cannot remove an owned old future. Reference failure never makes expired context ready.
- `CompleteOrderComposition.ExecutePreparedAsync` is the mapped domain boundary: accepted Scylla evidence, scope/plan and selected contracts must match before workflow append. Selected contracts use explicit shared MessagePack keys: command 13, workflow view 31, legacy workflow state 27 and option trade view 21. Existing keys are preserved.
- `CompositionDiscoveryHandoff` retries from durable receipts, realizes selected ownership before exact discovery release, and never recaptures or refreshes accepted evidence. Old-generation discovery is already destroyed by replacement; release is not sent to the new generation under an old identity.
- Worker control operation 22 remains a distinct revision-fenced business-owner update derived from PostgreSQL. Two-/four-leg controlled-feed tests retain pricing after discovery expiry, refresh reference context under business ownership and drain only after the last owner ends.
- Approved Windows Time synchronization and bounded clock correction succeeded. Both native backends now reserve sufficient process working set for concurrent required ring locks. StrictProduction DataBento live quotes subsequently passed. See the [live evidence](OrderComposition-Live-Evidence-v1.0.md) for observations and limits.

The completed workflow status alone does not prove transfer to an order: it retains workflow ownership until an explicit terminal/transfer fact. The future composer/order-dispatch implementation must persist the origin-linked transfer before ending that owner; matching an unrelated order's contracts is insufficient. Actual option order/position events are covered here; a new outright-futures order lifecycle is not claimed. Full composer construction remains OC-01..08 work.

Verification artifacts are under `.test-results/ocp`; focused counts overlap broad suites:

| Current check | Result | Artifact |
| --- | --- | --- |
| Full Application.MarketData suite | 425 passed, 1 skipped, 0 failed | `ocp-market-final.trx` |
| Full Domain.Trade unit suite | 797 passed, 0 failed | `ocp-trade-final.trx` |
| Real PostgreSQL intent and Scylla convention/preparation | 18 passed, 0 failed | `ocp-storage-final.trx` |
| Committed PostgreSQL projection plus joined 2/4-leg worker pricing/replacement/closure | 6 passed | `ocp-joined-pricing.trx` |
| Real NATS/PostgreSQL/Scylla acceptance and identical redispatch | 1 passed | `ocp-handoff-nats-final.trx` |
| Trade Selection BDD / verification regressions | 8 / 3 passed | `ocp-bdd-final.trx`, `ocp-verification-final.trx` |
| Actual supervised worker replacement, core rollover and terminal pin removal | 1 passed; included in broad suite | `ocp-runtime-process.trx` |
| Worker chain/pricing suite including 2/4 business legs | 17 passed; included in broad suite | `ocp-worker-business-pricing.trx` |
| Required native concurrent locks | C++ CTest 1 passed; Rust 7 passed | native test output / `rust-required-locks.log` |
| StrictProduction authenticated DataBento quotes | 1 passed | `ocp-strict-live-clock-corrected.trx` |
| Authenticated FMP endpoints | 2 passed | `ocp-live-fmp.trx` |
| API build | 0 warnings, 0 errors | `api-final.log` |

The combined projection/NATS run initially failed host construction because the Test environment lacked Redis configuration. All four projection cases passed. Running the NATS case in the integration host's configured Development environment passed; it used the isolated test NATS port and random workflow fixtures. Earlier numerical test counts below are historical, not additional unique coverage. The actual API process was not started by this verification; API build and the integration host exercise different levels of wiring.

| Component | Current behavior |
| --- | --- |
| [Pricing contracts](../../../../../../TomasAI.IFM.Framework.MarketData/Contracts/Pricing/OptionPricingContracts.cs) | Explicit MessagePack keys for exact contract conventions, reviewed calendar coverage, source-time quotes, structured failures and immutable pricing reference context |
| [Contract qualification](../../../../../../TomasAI.IFM.Framework.MarketData/Pricing/OptionPricingQualification.cs) | European ES options on futures only; unknown metadata fails; exact expiry, positive multiplier/tick and explicit calendar/day-count required; fractional T and exchange-trading-date count remain separate |
| [European universe](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/EuropeanOptionUniverse.cs) | Exact version/digest/provider identity lookup; complete bounded scope; American/expired exclusions; unresolved classification fails rather than silently removing candidates |
| [Convention store](../../../../../../TomasAI.IFM.Application.Storage/ReferenceDb/OptionPricingConventionStore.cs) | Additive ReferenceDb schema `option_pricing_convention`, key `(contract_id, mapping_version)`, shared MessagePack blob; IF NOT EXISTS and read-back equality preserve immutable published versions |
| [Treasury conversion](../../../../../../TomasAI.IFM.Framework.MarketData/ReferenceData/TreasuryRateConversion.cs) | ITreasuryCurve pure conversion method; exact 0/30/60/90 trading-day buckets; verified nominal semiannual CMT conversion, canonical curve digest and FlatSelectedCmtProxy provenance; no interpolation/fallback |
| [Treasury pricing provider](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/TreasuryPricingProvider.cs) | Bounded serialized asynchronous refresh, immutable cached curve, explicit publication schedule and source convention, no future/unobserved data admission, qualifying-cache reuse on outage |
| [Context provider](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/OptionPricingContextProvider.cs) | Builds a typed immutable reference context away from ticks; pins contract/calendar/rate/publication/engine/generation and validity; typed prerequisite failure |
| [Black-76 calculator](../../../../../../TomasAI.IFM.Framework.OptionPricer/Black76/OptionCalculator.cs) | Additive positive finite explicit-T constructor and process-pinned EngineVersion; legacy DateOnly behavior retained |
| [Pricing Model](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/Black76PricingModel.cs) | Frozen source-time bid/ask midpoint pricing, exact-T recalculation per pass, context/tenor/generation validation, IV/Greeks without zero fallbacks and semantic context digest |
| [Chain enricher](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/Black76OptionChainGreeksEnricher.cs) | Concrete production implementation of IOptionChainGreeksEnricher, bounded worker-local input store, generation-fenced callback, trade updates use quote-derived marks; no I/O in enrichment |
| [Snapshot assembler](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/MarketCompositionSnapshotProvider.cs) | Complete bounded page enumeration, scope/generation fencing, one evaluation instant, deterministic ordering/hash, explicit size/freshness/skew failures; independent futures branch |
| [Qualified discovery](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/QualifiedCompositionDiscovery.cs) | Complete bounded definitions -> exact reviewed mappings -> FMP pricing contexts -> canonical shared worker scope; American-only complete scopes allocate no feed; temporary acquisition/release is explicit |
| [Worker chains](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/WorkerOptionChainRuntime.cs) | Actual framework session manager and Black-76 enricher attached behind the dataset epoch; exact scope sharing, bounded temporary leases, expiry/last-owner drain, cancellation cleanup and released-lease tombstones |
| [Supervised API](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/ICompositionMarketDataApi.cs) | Implemented by the existing process recovery owner; generation/admission/lifecycle checks route typed acquire/release/capture requests over the existing worker control pipe |
| [Capture preparation](../../../../../../TomasAI.IFM.Application.MarketData/Pricing/CompositionPreparationService.cs) | Reads committed capture before touching market data; immutable first capture, binding-conflict rejection, expiry without recapture; this is the market-evidence half of workflow preparation |
| [Scylla capture store](../../../../../../TomasAI.IFM.Application.Storage/MarketDataDb/CompositionPreparationStore.cs) | Additive MarketDataDb `composition_preparation`, key `(workflow_id, input_revision)`; IF NOT EXISTS and validated read-back return the same winner to racing captures |
| API startup | Registers qualified discovery, supervised composition API and capture preparation/store in addition to reference/pricing services; native chain and enricher instances remain worker-owned |

FMP Treasury `RetrievedAtUtc` now records response availability rather than request start, so a slow request cannot make data appear observable before its response. Shared serializer remains responsible for wire/storage encoding. Pricing/snapshot semantic hashes normalize object-property order and decimal storage scale independently of MessagePack.

The existing market-data API contract test expected 35 methods but omitted the already-existing GetFuturesMarketHealth method. Its explicit expected list/count now includes that method; no IMarketDataApi method was added by this prerequisite change.

## 2. Important limits of the implementation

The assembler now consumes concrete worker option-chain state and worker futures quotes, delivered through typed supervisor request/response messages. This is bounded on-demand snapshot transport, not a continuously published option mirror. Passing `EvaluatedAtUtc=default` to the worker requests capture-then-valuation: the worker copies all observations, chooses one UTC instant, and calculates against the frozen copy. Explicit supplied valuation times remain supported. The preparation store saves the resulting exact valuation in its accepted request.

The workflow now consumes preparation through the mapped acceptance transition described above. **A Scylla market capture is not itself a committed workflow dispatch:** only the subsequent PostgreSQL acceptance allows sending the saved Start request. The future complete Execute Function contract, including final composer fee/session evidence and builders, remains OC work.

The qualified epoch overload now reaches the actual session manager. The old string-only DatabentoMarketDataEpoch.StartOptionChainAsync still retains its prerequisite guard because it supplies neither qualified metadata nor ownership. The existing generic Stage 4 owner-aware APIs still default to Disabled and Stage4SubscriptionOptions still rejects broad application enablement. The additive ICompositionMarketDataApi supports only explicitly qualified temporary discovery, not durable Strategy/WorkingOrder/Position authority. No additional host-owned native feed was introduced.

No real reviewed convention rows, FMP series evidence, publication deadlines/provider allowance or complete product calendars were fabricated or seeded. Both additive schemas were applied to local `reference_test_db.option_pricing_convention` and `market_data_test_db.composition_preparation` during continuation. Fixtures remain confined to randomly named `ifm_ocp_<guid>` keyspaces, dropped in test cleanup. Existing application definition rows were not changed.

Worker control frames append keys 17..21 for chain request, exact release, typed result, snapshot request and snapshot result. Message kinds 17..21 are additive. Existing keys and protocol major 2 remain; producer and consumer must be deployed together before using new operations. Encoding uses the shared uncompressed MessagePack options with the existing untrusted decoder policy. The control frame cap is now 1 MiB to carry bounded 512 KiB snapshots plus envelope/reference fields; manifest's existing independent 240 KiB/16-futures limits are unchanged. Snapshot requests append key 10 for explicit futures definitions, with absent arrays normalized to empty.

Physical scope limits: eight chains, 512 options per scope, 2048 options total, 128 simultaneous ephemeral leases and 128 business owners per scope, 4096 admitted/ended ephemeral identities and 4096 ownership watermarks per generation, and maximum temporary TTL 120 seconds. Tombstones prevent release/expiry from being undone by late replay. Automatic business-source delivery, durable context refresh and chain reconstruction are now implemented above; stale context and replaced generations continue to fail admission/snapshot readiness.

## 3. Read-only Scylla definition audit

Local Scylla was available. The published `reference_test_db.instrument_definition_snapshot` row reported snapshot `62764663-2e6f-41e5-bbfe-0a87c40d18fe`, dataset GLBX.MDP3 and **1,567,072 records**. This is the snapshot's total recorded count, not a count of eligible ES options.

A bounded read of bucket 0 returned `ESU6 C9600` and `ESM7 C5650`. Both raw records retain underlying IDs, currency USD, exchange XCME, CFI `OCAFPS`, exact expiration, `tick_rule=17` and `unit_of_measure_qty=50000000000`. Both also contain undefined sentinels `min_price_increment=9223372036854775807` and `contract_multiplier=2147483647`.

This establishes why blindly using the projected tick/multiplier fields is insufficient. No exercise style was inferred from those symbols/CFI values, and no tick schedule/multiplier mapping was promoted by this audit. The new qualifier requires explicit reviewed mappings. Only two records from one partition were sampled; this is not complete universe/series qualification.

## 4. Verification evidence

### Runtime continuation evidence

The continuation added 18 application cases and one Scylla case. All **58 prerequisite application cases** passed within the final broad suite (the focused filter previously passed 57, before the final drain-failure case). Two Scylla cases passed, including a real concurrent first-capture race, repository restart, conflicting input hash and immutable retry. The final broad run passed **402**, failed zero and skipped one (403 total). The final API build passed with zero warnings/errors. Scylla read-back confirmed both new application tables exist and contain zero rows; fixture rows were confined to isolated test keyspaces.

Positive worker tests run the real DatabentoOptionChainSessionManager, record consumer, last-price store, Black76OptionChainGreeksEnricher and snapshot Model against a controlled UTC feed. They verify new underlying quotes without context restart, two-owner sharing, last-owner drain, bad reference admission with no allocation, conflicting scopes, failed startup cleanup, lease expiry/replay, failed drain health signaling and typed wire identity. A failed option drain marks the epoch feed unhealthy for the existing worker supervisor, and shutdown continues cleaning the core aggregations even if one chain disposal fails.

The actual child-process test calls ICompositionMarketDataApi through DatasetWorkerProcessRecoveryService and the worker control pipe. Native SyntheticCi timestamps come from the Windows performance counter (`native.rust/DatabentoFeed.Rust/src/windows.rs`), not UTC Unix event time. The real process path correctly returns StaleData for these quotes and rejects the prior generation after reset. This is process/freshness rejection evidence, **not** a positive live quote/pricing qualification. No event timestamps or freshness limits were weakened to make synthetic records pass.

Current artifacts: `.test-results/ocp/ocp-runtime-focused.trx`, `ocp-runtime-storage.trx`, and `ocp-runtime-full.trx`. Focused/broad counts overlap. Original foundation evidence follows for provenance.

| Check | Evidence |
| --- | --- |
| New pricing tests | 25 passed: tenor/conversion, unknown style, same-day fractional expiry, stale/crossed/skewed quotes, source generations, context caching, calendar coverage, canonical digest and shared serialization |
| New snapshot/universe/enricher tests | 15 passed: full pagination, conflicting/incomplete/overflow scope, pricing failure propagation, stricter quote-limit expiry, futures on three horizons, empty complete scope, cancellation, style filtering and real enricher callbacks |
| New Scylla integration | 1 passed against actual localhost Scylla: exact-version read, restart, identical retry, conflicting overwrite refusal and independent new version |
| Full OptionPricer unit suite | 59 passed, zero skipped, including existing managed/native cases |
| FMP suite | 15 passed; 2 live endpoint tests skipped (economic calendar and Treasury); no live FMP qualification claimed |
| Application/Storage/API builds | Completed with zero warnings/errors; API build succeeded after foundation integration |
| Broad MarketData regression | 384 passed, 0 failed, 1 skipped (385 total); includes all 40 new application cases. Initial stale API-surface and native-fixture date failures corrected as described below. Final cache-admission correction also passed the 40 focused cases |

Counts overlap across focused and broad suites; do not add them as unique coverage. The new focused test cases total 41 including Scylla. Pure application callback/source-fixture tests are not a live DataBento session or full Function/workflow BDD qualification. The final refresh-admission test verifies that a response arriving after a frozen valuation is retained for the next pass without being admitted retrospectively to the earlier pass.

The five worker/native tests initially failing in the broad run were:

- SupervisedHostPublisherLifecycleTests.Noncooperative_actor_send_cannot_block_worker_shutdown_or_allow_unsafe_session_restart.
- SupervisedHostPublisherLifecycleTests.Real_host_publisher_starts_before_worker_admission_and_delivers_to_actor_and_api.
- DatasetWorkerManifestIntegrationTests.Unexpected_child_exit_clears_host_values_and_retained_readers.
- DatasetWorkerManifestIntegrationTests.Reset_and_forced_replacement_restore_latest_manifest_and_preserve_other_dataset_readers.
- DatasetWorkerContainmentQualificationTests.Stopped_stage_3_can_roll_back_to_real_stage_2_synthetic_epoch.

The last test reproduced independently both inside and outside the sandbox: its ten-second wait for a last trade at line 165 expired. Inspection established the cause: fixtures fixed the manifest/store value date to 2026-09-04, while EpochValueDateProvider now resolves incoming records against the current futures session. Store writes therefore failed their value-date check. The three affected test classes now derive their manifest/reset date from the existing FuturesTradingValueDate.GetOperational policy, matching their native synthetic producer's live clock. The 11 focused worker/publisher tests passed after correction, followed by the complete MarketData suite. No native runtime or worker-containment logic was weakened.

Original documentation validation covered six current documents and 67 local links. The continuation verified eight documents and 83 local links with no errors; code fences and git whitespace checks passed. The MarketData skip is the Linux-only SIGTERM-resistant descendant test; the two original FMP skips are the explicit live endpoint cases.

Commands used (results under ignored `.test-results/ocp` where specified):

```powershell
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj --no-restore --filter 'FullyQualifiedName~OrderCompositionPricingPrerequisiteTests|FullyQualifiedName~OrderCompositionSnapshotTests' --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~OptionPricingConventionScyllaTests --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Domain.OptionPricer.UnitTests/TomasAI.IFM.Domain.OptionPricer.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests/TomasAI.IFM.Framework.MarketData.FinancialModelingPrep.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
dotnet build TomasAI.IFM.Application.Api.Server/TomasAI.IFM.Application.Api.Server.csproj --no-restore --verbosity quiet -m:1 -nr:false
```

## 5. Gate disposition and work still required

| Package | Disposition |
| --- | --- |
| OCP-00 | Foundation logical/wire contracts implemented and tested; full composer nested domain manifests remain OC-01 work |
| OCP-01 | Qualifier, immutable reference/bundle storage and complete reviewed publication implemented: 4,268 definitions across seven September Tuesday/Thursday ES expiries; other profiles require their own reviewed publication |
| OCP-02 | Converter and publication-aware cache now use the implemented official Treasury adapter; real 1/2/3-month source admission and explicit 2026 publication policy verified. See the official Treasury record; full combined live option-chain qualification remains separate. |
| OCP-03 | Explicit-T Model/enricher and temporary/durable context refresh implemented; bounded supervised snapshots exercised with actual native DataBento and official Treasury; elapsed results are in the live register |
| OCP-04 | Committed-source projection, plan persistence, startup reconciliation, selected-leg ownership and release receipts implemented; real storage, 100 supervised recovery cycles and combined live canary executed; elapsed results are in the live register |
| OCP-05 | Supervised capture, immutable Scylla storage, mapped acceptance/dispatch and prepared completion validation implemented; actual NATS/PG/Scylla acceptance replay passes; future complete composer Function contract remains OC work |
| OCP-06 | Repeatable unit/storage/ownership/process/load/live runner implemented. The 30-minute 512-contract managed load passed; current live-run result is in the register. Full-session/platform deployment acceptance and future composer acceptance remain separate |
| OCP-07 | Full composer implementation document created, with prerequisite dependencies and actor conventions; composer implementation not claimed |

Milestones D and the runtime prerequisites now have implemented contracts, ownership/recovery/refresh and workflow preparation. Reviewed September mappings/calendar/rate-source policies are published. Controlled two/four-leg tests join genuine committed PostgreSQL events to pricing, replacement, next-value-date reconstruction and final drain. Actual child-process stress and a combined native live canary are separate executed evidence. The [live register](OrderComposition-Live-Evidence-v1.0.md) records sustained results, failed attempts and the distinction between bounded qualification and full-session/platform deployment acceptance. Composer OC-01..08 and broker/emulator execution remain downstream work.
