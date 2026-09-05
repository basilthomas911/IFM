# Market Data Resiliency Stage 4 Implementation Record v1.0

## Reference catalog extension (2026-09-05)

The [trade-strategy symbol catalog and family creation implementation](../../TomasAI.IFM.Domain.Reference/Docs/Trade-Strategy-Symbol-Catalog-Implementation.md) adds bounded Databento definition discovery, persistent product IDs and References creation. It does not start tick subscriptions, enable a strategy for a Fund, provide option-chain pricing, or close outstanding Stage 4 operational gates. Its provider tests use fixtures; live entitlement qualification remains separate.

| Item | Value |
| --- | --- |
| Record ID | `MDR-S4-REC` |
| Date | 2026-09-04 |
| Audited code baseline | Original audit `dea86871`; offline implementation entry `e98c06ce` |
| Plan | [Stage 4 implementation plan](Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md) |
| State | `S4-EX-01` approved; offline host-intent and isolated storage subsets implemented/tested; full Stage 4 integration and acceptance remain open |
| Scope | ES monthly iron condors, weekly vertical spreads and daily outright futures |
| Stage 4 runtime changes | Disabled host-intent contracts/coordinator and isolated PostgreSQL persistence subset; no provider/runtime registration or live enablement |

Latest prerequisite work adds bounded host publication, isolated real NATS outage verification,
Windows/Linux process-tree qualification, watchdog policy corrections and a read-only operations
health panel. The [Stage 3 as-built specification](Market-Data-Resiliency-Stage-3-As-Built-Specification-v1.0.md)
now separates passing offline evidence from remaining engineering and live/operational acceptance.
That prerequisite progress did not itself open `S4G-01` or waive owner acceptance. The later
explicit sequencing exception in section 6 permits disabled offline implementation only.

## 1. Original request and entry decision

The owner requested implementation to completion with unit, integration and verification tests,
and authorized sub-agents where useful. Two bounded read-only audits examined Stage 3 entry
readiness and Stage 4 pricing/workflow/ownership dependencies while the main agent ran baseline
verification. This authorization does not make unimplemented prerequisites pass.

At the original entry audit, the plan required Stage 3 completion and explicit acceptance before Stage 4 implementation.
The Stage 3 implementation plan also requires explicit acceptance before Stage 4 starts. No such
completed entry evidence is present, and actual integration gaps exist. Therefore `S4G-01` through
`S4G-11` had not started at that audit. No gate was waived and no synthetic-only Stage 4 substitute was created then.

## 2. Original blocking Stage 3 findings

Line references identify the audited baseline and may move in later commits. The subsequent
remediation in section 5 supersedes the first three findings; this table preserves the audit trail.

| Finding | Source evidence | Required prerequisite resolution |
| --- | --- | --- |
| No complete current desired-subscription manifest | `SupervisedDatabentoLifecycleRuntime.cs:50-75` passes startup `--contracts` and `ManifestRevision = 1`; `DatasetWorkerControlProtocol.cs:6` exposes only lifecycle/control message kinds | Implement/reconcile `S3-SUB-01` through `S3-SUB-03`, including revision acknowledgment and reconstruction from current desired state |
| Replacement reuses startup intent | `DatasetWorkerProcessRecoveryService.cs:133` creates the next request from the stored request with new worker/generation identity | Restore the current qualified manifest, not only the original launch arguments |
| Supervised API readers are not integrated | `DatasetWorkerPublicationProtocol.cs:81` forwards admitted publications; `DatabentoMarketDataApi.cs:145-156` reads `_epoch`, and `:817-818` requires that in-process epoch | Add and qualify stable API-side current-value mirrors and supervised query routing |
| Live profile is intentionally blocked | `TomasAI.IFM.Application.Api.Server/Startup.cs:624-626` rejects non-Synthetic Stage 3 enablement | Preserve guard until the missing integration and acceptance are complete |
| Acceptance is outstanding | Stage 3 implementation plan acceptance section and implementation record | Complete documented platform/provider/UI/soak/rollback evidence and obtain owner acceptance, or obtain an explicit reviewed sequencing exception |

Worker-related paths above are under `TomasAI.IFM.Application.MarketData/DataBento/Workers/`;
the API implementation is under `TomasAI.IFM.Application.MarketData/DataBento/`.

The Stage 3 record was corrected to distinguish missing implementation from missing operational
evidence. Passing its existing tests does not close the manifest/query-mirror gaps.

## 3. Stage 4 prerequisite inventory

- Existing `FinancialModelingPrepTreasuryCurve` implements `ITreasuryCurve` and is registered in
  API startup. Reuse this boundary; do not create a second rate source or use a fabricated rate.
- Production `DatabentoMarketDataEpoch.StartOptionChainAsync` still throws
  `MarketDataPricingInputUnavailableException` for the Treasury session rate. The native Black-76
  calculator is reusable (`Framework.OptionPricer/Black76/OptionCalculator.cs`: Actual/365 Fixed,
  continuously compounded annual rate), but production chain enrichment/wiring remains work.
- The Order Composition pipeline has dispatch contracts, but the requested complete production
  composer integration must be established rather than assumed from those contracts. The audit
  found Daily/Futures, Weekly/VerticalSpread and Monthly/IronCondor hints in
  `Domain.Trade/Strategy/Workflow/IntrinsicTime/MarketCondition/Model/MarketConditionCalculationModel.cs:523-530`,
  but no production receiver implementing `StartOrderCompositionPipelineCommand`. These hints
  are not contract/strike selection implementations. `S4G-08` remains a real dependency gate.
- Authoritative strategy state has versioned event-backed storage. Existing order/position read
  models do not alone establish the complete versioned closure/reconciliation contract needed by
  Stage 4. Missing projected rows must not be interpreted as closed positions.

These belong to the explicitly planned Stage 4/prerequisite integration gates. They are not grounds
to invent production trading policy or to replace required integrations with test fakes.

## 4. Verification performed during entry audit

These are existing-baseline tests, not Stage 4 acceptance tests. Each test invocation builds its
target project with `--no-restore`; no full-solution build is claimed for this entry audit.

| Check | Result |
| --- | --- |
| `scripts/Test-DatabentoLifecycleOwnership.ps1` | Passed |
| Application.MarketData focused worker/policy/health/resiliency suite | 45 passed, 0 failed, 0 skipped; includes synthetic worker-process tests |
| Framework.MarketData.DataBento unit suite | 136 passed, 0 failed, 0 skipped |
| MarketDataService PostgreSQL integration suite | 3 passed, 0 failed, 0 skipped against the dedicated local test database |
| New Stage 4 unit/integration/verification tests | Not started; entry gate blocked |
| Live-provider, Linux execution, UI journey and elapsed soak | Not run during this audit |

Commands:

```powershell
& ./scripts/Test-DatabentoLifecycleOwnership.ps1
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj --no-restore --filter 'DatasetWorkerProcessSupervisorTests|DatasetIncidentStateMachineTests|MarketDataOperationsHealthServiceTests|DatabentoResiliencyTests' --verbosity quiet
dotnet test TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj --no-restore --verbosity quiet
dotnet test TomasAI.IFM.Application.Storage.IntegrationTests/TomasAI.IFM.Application.Storage.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~MarketDataServicePostgresIntegrationTests' --verbosity quiet
```

The integration connection was verified as the local dedicated `event-source-test-db` on
`localhost:5432` without logging credentials. Its fixture truncates its four market-data-service
test tables during setup/cleanup; it is not safe to point this test at an application database.

## 5. Authorized Stage 3 remediation and current entry status

The owner answered **yes** to completing the missing Stage 3 integration first. This authorizes
the prerequisite code/tests; it is not implicit Stage 3 acceptance or a Stage 4 sequencing waiver.

The current working tree now contains:

- a parent-owned, versioned complete core futures manifest with worker apply/acknowledgment;
- reset and replacement convergence to current desired subscriptions and actual native identity;
- generation-fenced, stable host current-value readers and supervised API query routing;
- actual worker-native/drain/aggregation watchdog evidence instead of placeholder counters; and
- additional shutdown, cancellation and real publisher lifecycle integration qualification.

See the [Stage 3 remediation and test evidence](Market-Data-Resiliency-Stage-3-Implementation-Record-v1.0.md).
These findings are no longer described as missing implementation. No option chain, owner lease,
order composer or Stage 4 persistence implementation is claimed.

`S4G-00` remains pending the remaining Stage 3 requirement/platform/provider/UI/soak/rollback
review and explicit owner acceptance. The local pipe-to-host-publisher topology and legacy host
publisher backlog behavior are explicitly recorded for that review. If a sequencing exception is
desired for disabled synthetic Stage 4 work, its exact scope must be approved and recorded; it
must not be called full Stage 3 acceptance or Stage 4 completion. All live-enablement guards remain.

## 6. Owner-approved sequencing exception and offline implementation

The owner explicitly answered **"yes, i approve proceeding"** to implementing and testing Stage 4
disabled by default while leaving unresolved Stage 3 requirements and live acceptance gates open.
This supersedes the earlier implementation stop, not the outstanding acceptance requirements.
See `S4-EX-01` in the plan. Baseline `e98c06ce` is committed; earlier references to its working-tree
remediation describe historical evidence. No live provider connection, production change, paid data
request or trading-policy invention is authorized by this exception.

| Gate | Current disposition |
| --- | --- |
| `S4G-00` | Conditional offline entry under `S4-EX-01`; full decision/Stage 3 acceptance remains open |
| `S4G-01` | Partial: typed host lease/batch/query contracts and legacy caller characterization pass; final option-worker/composition schemas and qualified enabled API integration remain |
| `S4G-02` | Offline ephemeral host-intent subset implemented/tested; no physical routes, durable coordinator integration or deployed public admission claimed |
| `S4G-03` | Partial: isolated transactional PostgreSQL ticker-intent store verified; business ownership mappings approved 2026-09-05; source-adapter/coordinator integration, chain/unfinished cross-source handoffs, dispatcher and retention remain |
| `S4G-04` | Not completed: production rate conversion/interpolation/freshness conventions and real chain enrichment remain prerequisites; existing pricing guard retained |
| `S4G-05` through `S4G-07` | Pending canonical physical source sharing, option worker protocol/mirrors and process recovery integration |
| `S4G-08` | Blocked production integration: versioned owner adapters and actual composer/selection implementation are missing; no trading rules invented |
| `S4G-09` | Pending Stage 4 operations/authorization/UI integration; existing Stage 3 panel unchanged |
| `S4G-10` | Partial evidence only: managed Windows/Linux tests and short capacity check, not the complete sustained load/fault/native matrix |
| `S4G-11` | Pending live-provider/platform/session/soak/rollback evidence and explicit owner acceptance |

### Implemented offline host-intent subset

- Additive typed APIs for futures/options/chains, renewal/release, atomic selected-intent batches
  and bounded exact-owner queries. All public application defaults return `Disabled`; none starts
  a legacy feed. The explicit configuration guard rejects application Stage 4 enablement.
- Immutable validated scope/owner/ticker/chain identities, host-epoch/incarnation fencing and exact
  chain-universe equality. There is no native instrument or credential in these contracts.
- Bounded serialized ephemeral ownership with monotonic TTL, timer coalescing, desired manifests,
  reference union, atomic batch rejection/commit, precommit cancellation/deadline/expiry checks,
  query bounds, recovery-aware admission and explicit failure for uninstalled durable persistence.
- Short-window ephemeral UUIDv7 idempotency is bound to the immutable issued-at time, with an
  epoch on acquisition. Pruned IDs cannot be revived by changing deadlines. This is not the
  durable PostgreSQL operation-retention implementation.
- Legacy characterization exercises the real route registry and API compatibility boundary.
  [Caller inventory](Stage4-Caller-Inventory.md) distinguishes those tests from production chain
  wiring. [Dependency decisions](Stage4-Durable-Pricing-Dependency-Decisions.md) identify exact
  missing authority, pricing and composer integrations.

No physical option/chain feed, qualified composition result, worker option manifest or production
composer integration is implemented by this host-intent subset. `RealizedRevision` remains zero.
Do not interpret desired route counts as provider subscriptions or call synthetic lease owners
authenticated trading authorities.

### Isolated PostgreSQL subset and verified failure correction

The new `IDurableSubscriptionIntentStore` and `PostgresDurableSubscriptionIntentStore` persist
bounded typed current ticker intent, operation outcomes, outbox transitions, source watermarks and
lease-ID reservations/tombstones in one transaction. The initial source contract is one source
stream per owner with contiguous logical versions; that contract is not an approved production
adapter. Explicit release/terminal evidence affects only that owner. Unknown/empty active facts
retain leases. Reserved IDs cannot be reused after release by the same or another source.

Five additive `market_data_service.stage4_*` tables are created only by the isolated integration
fixture. It verifies loopback port 5432 and database `event-source-test-db`, checks the connected
database name, and cleans only its registered randomized `stage4-test-<32hex>` scopes. No app
database migration, truncation, table drop or startup registration was performed.

The first database run failed all 12 database cases because the existing repository write helper
joined the ambient transaction but query helpers opened separate connections. The new store's
transactional reads now use the same owned ambient PostgreSQL command/connection, with bounded
timeouts and disposed readers/commands. No shared storage-provider behavior was changed. A fifth
write boundary and two lease-ID reuse regressions were added; the resulting 15 database cases
and six pure safety checks all pass. These tests inject transaction-boundary failures; they are
not evidence of a killed database server, real network disconnect or elapsed operational soak.

The store remains unregistered and is **not connected to the ephemeral coordinator**. No durable
lease can currently be acquired through the new API. Retention/disk bounds, actual outbox delivery,
source snapshot paging, complete chain/handoff persistence and connection-start failure cleanup
remain open; see the store README. Existing current routes are not recreated from this storage yet.

### Verification performed for this subset

| Check | Result |
| --- | --- |
| Initial coordinator requirement tests against stub | 10 failed as expected; implementation removed all stubs and made them pass |
| Stage 4 managed contract/coordinator/lifecycle/characterization | 47 passed on Windows and 47 passed on Linux |
| Maximum shared-chain allocation test (separate process) | 1 passed on each OS; 10,000 leases, 512 options + one underlying, 513 unique desired route entries |
| Standalone PostgreSQL tests | 15 real database cases + 6 safety cases passed; no skips |
| Full Application.MarketData regression | 315 passed, 1 Linux-only skip on Windows, 316 total |
| Legacy Framework.MarketData.DataBento regression | 136 passed, no skips |
| UI presentation/architecture regression | 303 passed, no skips |
| Full solution build | 0 warnings, 0 errors |
| Windows offline runner including PostgreSQL | Passed |
| Linux offline runner | Passed in cached SDK 10.0 Linux container, network disabled, repository read-only |
| Final cleanup/whitespace | 0 dataset/qualification workers, 0 Stage 4 test containers; whitespace validation passed |

Suites overlap; their counts must not be added as distinct tests. Linux evidence is execution of
the managed test assembly, **not** Linux native feed/platform qualification. No changed C++/Rust
algorithm is claimed and no new Rust parity evidence is claimed. No live feed or running UI journey
was started. Numerical test artifacts are ignored under `artifacts/Stage4Acceptance/`.

The initial Windows maximum-shared-chain mutation measured 172.62 ms and 5,388,152 allocated bytes.
After the exact-object chain equality fast path, the isolated runner measured **31.05 ms and
5,388,112 bytes** on Windows; Linux measured **22.16 ms and 5,382,584 bytes**. Allocation is
process-wide during that mutation, not retained heap or per-tick allocation. These are short Debug
measurements with fake policy time, not approved latency/GC budgets, 30-minute quote load, 100
process replacement cycles, an elapsed canary or full `S4G-10` evidence.

Commands:

```powershell
dotnet build TomasAI.IFM.sln --no-restore --verbosity quiet -m:1 -nr:false
./scripts/Test-DatabentoStage4.ps1 -IncludePostgres
dotnet test TomasAI.IFM.Application.MarketData.UnitTests/TomasAI.IFM.Application.MarketData.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.Framework.MarketData.DataBento.UnitTests/TomasAI.IFM.Framework.MarketData.DataBento.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
dotnet test TomasAI.IFM.UI.Net.Presentation.UnitTests/TomasAI.IFM.UI.Net.Presentation.UnitTests.csproj --no-restore --verbosity quiet -m:1 -nr:false
```

Linux managed runner takes an already-built test assembly and an explicit writable results path:

```bash
bash scripts/Test-DatabentoStage4.sh /path/to/TomasAI.IFM.Application.MarketData.UnitTests.dll /path/to/results
```

### Next decision/integration checkpoint

On 2026-09-05 the owner confirmed **"Yes, those proposed mappings are correct"**. Business
subscription authority is now approved as IntrinsicTime workflow → strategy, TradeOrder → working
order and TradePosition → position, with renewable temporary discovery/composer ownership and
independent order/position release boundaries. See the approved mapping table in
[the dependency decisions](Stage4-Durable-Pricing-Dependency-Decisions.md). The source-choice
question is resolved; versioned/authenticated adapters and restart reconciliation are not yet
implemented by that decision.

The separate pricing/composer question was answered on 2026-09-05. The owner specified daily FMP
Treasury observations, trading-day tenor boundaries (<30 one month, <60 two months, <90 three
months, >=90 Failed), an additional continuous annual decimal conversion function on the Treasury
interface, Toronto/New York timezone, contract-specific day count and Failed with error details.
The owner also requested that the workflow's Order Composer actively choose expiry, strikes,
deltas and quantities under explicit policies for all three profiles, rather than stop at
caller-supplied-contract infrastructure.

Created the [pricing specification](Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md)
and [Order Composition selection specification](Order-Composition-Strategy-Selection-Specification-v1.0.md),
and updated the plan/dependency register. They define P1–P6 pricing and C1–C7 composer implementation/
test packages. Monthly EOM, weekly debit-first and numerical profile parameters are marked as
proposals; actual budgets remain Portfolio-owned. Detailed publication/source-series/calendar
qualification and financial-profile activation remain open.

This decision/specification update changes documentation only. No Treasury interface, calculator,
selection actor, runtime registration, application data or live trading setting was changed by
this update. Existing test evidence above belongs to the earlier subset; it is not evidence that
the newly specified pricing/selection behavior is implemented or tested.

Documentation verification passed for all five affected documents: local Markdown links,
balanced code fences and trailing-whitespace checks, plus `git diff --check`. A standalone
arithmetic check confirmed the synthetic 5% continuous-rate example and discount-factor identity.
No application unit/integration suite was rerun for this documentation-only update; no new runtime
test coverage or financial-profile acceptance is claimed.

All Stage 3 gaps, production pricing/ownership/composer integration, physical routing/recovery,
sustained qualification and live acceptance stay open. The application flag remains false and
true is explicitly rejected. This record does not report all Stage 4 gates completed.

### One-unit Option Strategy Builder design — 2026-09-05

Following the owner's sizing clarification and request for a one-unit builder, created
[Trade Strategy Builder Design v1.0](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md). It places a pure
builder, condition-driven construction-policy resolver and option-leg selector inside the existing
OrderComposition stage. It produces all four condor legs or both vertical legs, with one-unit
economics and no final unit quantity. Daily futures remains a separate one-unit construction path.

Portfolio Risk Manager owns final strategy-unit sizing, independent current-capacity validation
and atomic risk reservation. One is a normalization basis, not an approved order or an upper bound.
The design includes typed unsized/sized result separation and B1–B5 verification packages.
Updated the composer specification, Stage 4 plan/dependency register, TradeSelection HLD, Portfolio
HLD and Portfolio detailed specification to reconcile the prior reduce-only sizing language.
Historical runtime contracts/results are not reinterpreted by these documentation changes.

No builder, policy actor, risk-sizing engine, runtime registration or live setting was implemented
in this design-only update. All previous implementation/test counts remain historical subset
evidence, not new builder acceptance. Numerical construction profiles remain proposals for review.

Verification for this update: local Markdown links and code fences passed in all eight affected
documents; unit-contract/acceptance-package presence checks and `git diff --check` passed. Reviewed
the affected design text for obsolete Composer-sized/reduce-only requirements. No runtime build
or unit/integration suite was run for this documentation-only change.
