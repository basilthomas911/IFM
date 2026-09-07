# Trade Selection implementation and qualification evidence

Date: 2026-09-07. Scope: TS-01 through TS-08 of the catalog-aligned implementation plan. TS-01 through TS-08 are code complete and passed the scoped qualification below. The broader legacy-host failure is recorded separately; no full-repository or combined live qualification is claimed.

## Implemented behavior

The workflow binds one triggering Daily, Weekly or Monthly horizon to an exact published common selector policy, a frozen Portfolio/Fund authority snapshot, and the complete authorized catalog graphs. Disabled assignments and known deny states retain explained NoTrade evidence. Missing or corrupt required configuration fails the invocation. Candidate enumeration is bounded and cannot silently truncate the winning set.

The pure evaluator supports long/short futures, four credit/debit vertical signatures and six long/short iron condors with balanced/bullish/bearish bias. Selection uses Fund priority, variant preference and explicit stable identity tie breakers. Every result contains the accepted upstream context, exact catalog/parameter identities, global gate evidence and ordered candidate evidence. No quotes, option-chain lookup, broker call or cross-timeframe synthesis occurs in the selector.

`TradeSelectionFunctionActor` uses `BaseEventSourceFunctionActor`, three frozen maps, a typed context and domain extension, completed-only PostgreSQL state and synchronous Scylla projection. A projected candidate row is not evidence of workflow acceptance. The exact invocation/result/history APIs distinguish unknown acceptance and suspected orphan projections.

Workflow acceptance recomputes the full decision from its saved dispatch. Selected persists ReservationPending before calling Portfolio. The request uses the selected deployment GUID/version, the original authority revision and one PrimaryTrade instruction, irrespective of option leg count. The workflow advances only after validating and saving a committed reservation. NoTrade never allocates order/trade identities. Expiry/cancellation cannot reopen the workflow through a late callback.

## Qualification results

The final test counts below come from completed runs. There were no skipped tests in these selected runs. Logs reside in the ignored local `.test-results/trade-selection` directory; permanent tests and this document are the reproducible evidence.

| Suite | Result | Evidence scope |
| --- | --- | --- |
| Trade unit | 554 passed | All variants/horizons, policy boundaries, actor ingress/lifecycle, frozen evidence, deterministic ranking and handoff |
| Trade BDD | 31 passed | Business Selected/NoTrade scenarios and existing workflow scenarios |
| Trade verification (selector/assessment filter) | 10 passed | Three-horizon reservation qualification and upstream assessment verification |
| Trade Selection live integration | 5 passed | NATS Function/query/workflow, PostgreSQL state/catalog and Scylla projection/paging |
| Portfolio unit regression | 139 passed | Existing authority/contracts and strict resolver compatibility |
| Reference unit regression | 58 passed | Existing catalog and reference behavior |
| Shared Function regression | 8 passed | Shared completed-only lifecycle and ingress |
| API Server build | 0 warnings, 0 errors | Production project graph, registration and compile validation |

### Reproduction

Run from the repository root after package restore, with the PostgreSQL/Scylla/Redis test services available and an isolated NATS server on port 14222:

```powershell
dotnet test TomasAI.IFM.Domain.Trade.UnitTests --no-restore
dotnet test TomasAI.IFM.Domain.Trade.BDDTests --no-restore
dotnet test TomasAI.IFM.Domain.Trade.IntegratedTests --no-restore --filter 'FullyQualifiedName~TradeSelectionRuntimeTests'
dotnet test TomasAI.IFM.Domain.Trade.VerificationTests --no-restore --filter 'FullyQualifiedName~TradeSelectionQualificationTests|FullyQualifiedName~MarketAssessmentQualificationTests'
dotnet test TomasAI.IFM.Shared.UnitTests --no-restore --filter 'FullyQualifiedName~Function'
dotnet test TomasAI.IFM.Domain.Portfolio.UnitTests --no-restore
dotnet test TomasAI.IFM.Domain.Reference.UnitTests --no-restore
dotnet build TomasAI.IFM.Application.Api.Server --no-restore
```

An additional unfiltered Trade verification run discovered 56 tests: 51 passed and five older RegimeDiscovery workflow-host tests failed during SimpleInjector startup because `TradeStrategyFamilyCreationService` is missing from that test host. Those tests did not reach pipeline execution. They are not counted as passing selector qualification. The scoped ten-test selector/assessment verification run passed. This evidence does not claim a green unfiltered repository suite or combined five-operator verification.

## Fixture traceability

Fixture IDs identify test groups, not a fixed count of test methods. These groups are distributed across the following permanent tests:

| Specification fixtures | Main evidence |
| --- | --- |
| TS-C01–C07 | `TradeSelectionEvaluationTests`, `TradeSelectionCandidateMatrixTests`, `TradeSelectionBehaviorTests`: all 36 positive combinations, option/future semantics and categorical variant rejection matrix |
| TS-C08–C10 | `TradeSelectionRejectionTests`: confidence equality and adjacent values, known unavailability, Unknown and malformed required upstream fields |
| TS-C11–C14 | `TradeSelectionRankingTests`, binding validation in shared contracts: priority, preference, stable ties, culture and collection permutations |
| TS-C15–C20 | `PortfolioSelectionResolverTests`, `TradeSelectionRejectionTests`, `TradeSelectionArchitectureTests`: deny/empty/disabled/ambiguous scopes, exact permissions, asset vocabulary and checked version boundaries |
| TS-C21–C24 | `TradeSelectionPolicyTests`, `TradeSelectionFunctionTests`, `TradeSelectionRuntimeTests`: owning policy schemas, exact lifecycle/hash/capability checks, strict specialized replacement and actual PostgreSQL publication immutability |
| TS-C25–C29 | `TradeSelectionEvaluationTests`, `TradeSelectionCompatibilityTests`, `TradeSelectionArchitectureTests`, `TradeSelectionRejectionTests`: transport/hash preservation, historical contract vectors, schema/identity/size/time guards |
| TS-C30–C33 | `TradeSelectionFunctionTests`, `TradeSelectionRuntimeTests`: Selected/NoTrade invariants, actual actor ingress, exact replay/conflict, cancellation, synchronous projection failure, actual PostgreSQL expected-version append rejection and recovery from an orphan projection after append failure |
| TS-C34–C36 | `TradeSelectionHandoffTests`, `TradeSelectionQualificationTests`, `TradeSelectionHandoffRuntimeTests.cs` (partial `TradeSelectionRuntimeTests`): full-result acceptance, real Portfolio aggregate, durable PostgreSQL reservation recovery, NATS callbacks, late cancellation/expiry and deterministic logical composer command |
| TS-C37–C39 | `TradeSelectionRuntimeTests`, `TradeSelectionArchitectureTests`, `TradeSelectionCompatibilityTests`, Portfolio/Reference regressions: scoped queries/paging, production capability boundary, type forwarding and pre-extraction golden vectors |
| TS-C40 | PostgreSQL lifecycle test, frozen replay tests and `TradeSelectionNotificationTests`: retirement affects new resolution, accepted context remains fixed, current authoritative state fences delayed notifications |

## Recovery defects caught during qualification

The PostgreSQL workflow test exposed defensive-copy JSON population and decimal-scale changes that were not visible in a MessagePack-only round trip. The binding now replaces the snapshot on JSON reads, preserves the original Portfolio canonical JSON through a property-scoped converter, and uses scale-independent hashes for new typed selector evidence. Original Portfolio/catalog hashes and old wire keys remain unchanged. All 36 positive cases also validate actual Newtonsoft event-store JSON round trips and unchanged request fingerprints. Handoff request/reservation properties explicitly replace copied values on JSON reads. Expired completions retain timeout evidence without marking their candidate payload accepted. Composer redispatch retains the committed reservation causation ID as well as command ID, so repeated commands have identical bytes.

## Integration boundaries

Integration uses the existing PostgreSQL/Scylla test databases with unique workflow and policy IDs and an isolated NATS server at `127.0.0.1:14222`. The assignment paging test uses its own Portfolio/Fund partition in `trade_test_db`, creates schema additively, and deletes only that owned partition. The tests do not drop user tables or alter published user profiles. PostgreSQL test history remains available as evidence. The owned `ifm-trade-selection-verification` NATS container was stopped after qualification; unrelated services were left running.

The query test substitutes the Portfolio access API at its boundary and verifies rejected missing access as well as successful scoped queries. It is not a test of external account authentication. The handoff test uses the real Portfolio aggregate and event store; its composer endpoint only captures commands and does not build orders. The catalog fixture registers explicitly named test-only downstream validators for supported test shapes. Production registers only the implemented selector capabilities and rejects unsupported builder/risk requirements.

These are selector qualification tests, not combined live qualification of all five operators, market-data feeds or a broker. No trading performance or suitability claim is implied by deterministic engineering defaults.

## Activation and recovery runbook

1. Author the three full payloads returned by `TradeSelectionDefaultProfiles.EngineeringDefaults()` through `InsertTradeSelectionDraftAsync`. Saved version-1 IDs are Daily `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef701`, Weekly `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef702`, Monthly `ec56ea27-d625-4bb2-a6a1-f4ac3c2ef703`. Startup does not insert or publish these records.
2. Author exact construction policies and supported structure/variant settings. Publish only after the actual required builder/risk/data capabilities are registered and qualified. A test validator must never be used for live publication.
3. Publish the exact selector policy and a typed `TradeSelectionActivation` containing PortfolioId, optional FundId, ES root, one horizon and the exact common-policy ID/version/hash. Pin its ID/version/hash in the corresponding `IntrinsicTimeStrategyWorkflowOptions.Activations` entry. Keep matching Regime Discovery and MarketCondition profile versions and effective windows.
4. Give the Fund exact deployment permissions and effective schema-3 assignments whose template fields equal that deployment GUID/version. Selection/composition references are matched by parameter kind and exact version/hash; display names grant no authority.
5. Enable new workflow starts only after these dependencies and the remaining operator qualifications are ready. No automatic catalog publication or broker activation is part of this change.
6. Following a notification outage or unknown reply, issue `RedispatchCurrentStrategyPipelineCommand` with a new recovery CommandId, the original workflow EntityId/WorkflowId, its current authoritative revision/stage, UTC RequestedAtUtc and RequestedBy. The command reloads PostgreSQL and republishes saved intent without replacing request IDs, reservation idempotency key, evidence or deadlines. A stale recovery request is ignored.
7. For a stopped workflow, redispatch triggers reconciliation of any known committed open Portfolio order. If the Portfolio projection has not caught up yet, repeat after it becomes visible. Retain allocated IDs. Do not create a fresh reservation key to work around an unknown outcome.

Recovery is explicit. The workflow snapshot projector remains conventional and there is no new automatic restart scanner or retry scheduler. Function completed replies are replayable after their original calculation expiry; workflow acceptance still observes its fixed deadline. Transport delivery can repeat the same deterministic composition command. A downstream composer must fence acceptance against current workflow/order authority, including a cancellation racing after the realtime pre-dispatch read, before any irreversible action.

## Final logs and gate closure

Local logs: `unit-final.log` (554), `bdd-final.log` (31), `integration-final.log` (5), `verification-final.log` (10), `shared-function-qualified.log` (8), `portfolio-regression.log` (139), `reference-regression.log` (58), and `api-final.log` (clean build), all under `.test-results/trade-selection/`. Together these selected suites report 805 passed tests without counting the separate unfiltered attempt. `git diff --check` passed.

Gate closure covers selector implementation and isolated qualification. It does not publish any production deployment, certify a builder/risk algorithm, create an automatic recovery scheduler, change the observation UI, or resolve the separate legacy full-workflow test-host registration failure. Live activation remains gated by the runbook prerequisites above.
