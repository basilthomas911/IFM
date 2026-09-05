# Stage 4 caller and legacy-behavior inventory

| Item | Value |
| --- | --- |
| Gate | `S4G-01` characterization subset |
| Baseline | `e98c06ce`; reviewed 2026-09-05 UTC |
| Authority | [Stage 4 implementation plan](Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md) |
| Scope | Existing callers and behavior; not a claim of completed Stage 4 routing or live acceptance |

## Production callers at the application boundary

Paths below are relative to the repository root; project names omit the `TomasAI.IFM.` prefix.
Inventory was obtained from all non-generated C# files, including ignored source directories;
tests, build outputs and artifacts were excluded from the production classification.

| Caller | API use and owner identity | Stage 4 migration implication |
| --- | --- | --- |
| `Domain.MarketData.Feed/FuturesTickData/Event/FuturesTickDataStreamingStarted.cs`, `FuturesTickDataStreamingStopped.cs` | Futures start/stop; owner `(FuturesTickDataEventActor, entityId.Format(), contractId)` | Preserve actor-owned transient semantics; these events alone do not prove durable strategy/order/position authority |
| `Domain.MarketData.Feed/FuturesTickData/Event/Actor/FuturesTickDataEventActor.cs` | Shutdown drains actor-owned registrations via futures stop | Must not tear down an independently owned position route |
| `Domain.MarketData.Feed/FuturesOptionTickData/Event/FuturesOptionTickDataStreamingStarted.cs`, `FuturesOptionTickDataStreamingStopped.cs` | Individual option start/stop; owner `(FuturesOptionTickDataEventActor, entityId.Format(), contractId)` | Existing multi-leg UI requests are separate commands, not atomic discovery-to-selected-leg handoffs |
| `Domain.MarketData.Feed/FuturesOptionTickData/Event/Actor/FuturesOptionTickDataEventActor.cs` | Shutdown drains actor-owned option registrations | Actor shutdown is not authoritative position closure |
| `Domain.MarketData.Analytics/FuturesItiSignal/Realtime/FuturesItiSignalStreamOwnership.cs` | Futures start/stop; `(FuturesItiSignal, CurrentContracts, ES)` and `(FuturesItiSignal, CurrentContracts, VX)` | Preserve independently owned core analytic routes and rollover behavior |
| `Domain.MarketData.Analytics/FuturesVwapSignal/Realtime/Model/FuturesVwapStreamOwnership.cs` | Futures start/stop; `(FuturesVwapSignal, CurrentSession, Trades)` | Daily futures lease release cannot stop VWAP ownership |
| `Domain.MarketData.Analytics/FuturesVxTermStructureSignal/Realtime/Model/FuturesVxTermStructureStreamOwnership.cs` | Futures start/stop; `(FuturesVxTermStructureSignal, CurrentCurve, Front)` and `(FuturesVxTermStructureSignal, CurrentCurve, Back)` | Preserve separate front/back analytic references |

No production caller of `IMarketDataApi.StartStreamingFuturesOptionChainDataAsync`,
`StopStreamingFuturesOptionChainDataAsync` or `GetFuturesOptionChainContractsAsync` was found beyond
their declarations and implementation. Calls in application contract/production-epoch tests are
test evidence, not deployed workflows. All located direct production individual-stream callers
above supply explicit `TickerStreamOwner` values.

## Existing indirect UI route

`UI.Net.ViewModels/Trade/IronCondor/IronCondorViewModel.cs` and
`IronCondorTradeOrderViewModel.cs` call
`UI.Net.Services/MarketDataFeed/MarketDataFeedCommandService.cs` to start/stop individual legs.
That service queries definitions, submits `StartFuturesOptionTickDataStreamingAsync` once per
leg through the Feed command API, and currently waits two seconds between legs. Stop is also
per leg. The Feed event handlers above then call the application market-data API.

`UI.Net.ViewModels/App/IFMAppViewModel.cs` also calls the service's futures-stop method.
These service methods have similar names to the application API but are not direct calls to it.
They are neither an atomic two-/four-leg batch nor durable position-owned subscriptions.
UI-supplied risk-free rates in the old workflow do not establish the approved immutable
Treasury-pricing provenance required by Stage 4.

## Actual legacy ownership behavior

| Boundary | Observed implementation |
| --- | --- |
| `Application.MarketData/DataBento/DatabentoOptionRouteRegistry.cs` | One string owner per option: `individual` or `chain:<underlying>:<maturity>`. Chain and individual routes are mutually exclusive. Identical normalized chain selections return `false`; different selections conflict without replacing the active chain. A repeated chain reservation creates no second lease/ref-count: one release removes the chain |
| `Framework.MarketData.DataBento/TickAggregation/TickAggregationService.cs` | Per-contract `HashSet<TickerStreamOwner>`: first owner activates the transient route, final owner deactivates it; duplicate owner is idempotent. This is legacy individual stream sharing, not Stage 4 chain/leg/dependency physical sharing |
| `Application.MarketData/DataBento/DatabentoTickerStreamRouteController.cs` | Individual option activation consults the exclusive registry; failed live-router activation rolls back its reservation |
| `Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainSessionManager.cs` | Sessions keyed by underlying/maturity; same selection is idempotent, different selection conflicts. A new session creates its own option-chain feed. No service-owned lease ID/expiry or chain-to-individual source handoff is present |
| `Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs:StartOptionChainAsync` | Production start throws `MarketDataPricingInputUnavailableException("Treasury curve session rate")` before reservation/provider allocation. The standalone framework chain manager is not evidence that the production application chain is wired |
| `Application.MarketData/DataBento/DatabentoMarketDataApi.cs` | Optional-owner compatibility calls synthesize `(DatabentoMarketDataApi, compatibility:<value-date>, futures|option)`. They have no TTL or durable lifecycle source. Legacy chain overloads are ownerless and return `Task<bool>` |

The registry validates ownership only; it does not validate provider definitions or maturity
membership. Production API/catalog preconditions perform contract validation before invoking
epoch operations. Registry-only characterization must not be interpreted as domain validation.

## Composer and authoritative lifecycle integration gaps

Trade workflow orchestration exists:

- `Domain.Trade/Strategy/Workflow/IntrinsicTime/Realtime/Actor/IntrinsicTimeStrategyWorkflowRealtimeActor.cs`
  dispatches `StartOrderCompositionPipelineCommand` when the workflow reaches Order Composition.
- `Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/Commands/StartOrderCompositionPipelineCommand.cs`,
  processing/completed/failed event contracts, and `Routing/IntrinsicTimeStrategyPipelineRoutes.cs`
  define that pipeline boundary.
- `Domain.Trade/Strategy/Workflow/IntrinsicTime/Command/CompleteOrderComposition.cs` and related
  command/state code accept workflow stage results.

No concrete `OrderCompositionPipelineCommandActor`, `OrderCompositionPipelineRealtimeActor` or
`OrderComposerActor`, approved strike-selection/pricing implementation, or adapter from that
pipeline to the option-chain application APIs was found. Folder entries and design descriptions
under `OrderComposer` are not implementations. Existing UI iron-condor screens do not fill this
missing server-side pipeline.

The owner-approved offline sequencing exception permits isolated contracts/coordinator tests,
but does not turn a fake composer into `S4G-08` completion. A real versioned lifecycle adapter
must map strategies, working orders, partial fills and positions into durable lease transitions;
the existing transient Feed actor owner tuple cannot be promoted to that authority by assumption.

## Characterization evidence and boundaries

`Application.MarketData.UnitTests/Stage4LegacyCharacterizationTests.cs` exercises the actual internal
production registry using a narrow reflection adapter; the implementation is not copied into a
test fake and production visibility is unchanged. It covers:

- Individual idempotency, normalized identical chains and absence of owner ref-counting.
- Chain/individual and different-universe conflicts, no partial reservation on failure.
- Capacity rejection, independent maturities, concurrent same-selection acquisition and clear.
- Additive source compatibility of legacy optional-owner and ownerless-chain overloads.
- Real application API compatibility-owner forwarding and release isolation using a fake epoch.

The existing `MarketDataApiStreamingContractTests` use the real `DatabentoMarketDataApi` but
`FakeMarketDataEpoch`/`FakeOptionRouteRegistry`; their chain-start success does not override the
real production epoch's pricing guard. `DatabentoProductionEpochTests` separately verifies that
guard with the real epoch and a synthetic provider. Existing framework `TickAggregationServiceTests`
cover individual-owner reference semantics. None of these tests proves Stage 4 physical sharing,
live provider removal, durable intent, composer readiness or complete `S4G-01` exit criteria.

Requested targeted test filter: `FullyQualifiedName~Stage4LegacyCharacterizationTests`.
The consolidated implementation record owns execution results; this inventory does not assert
a test pass before the root verification run.
