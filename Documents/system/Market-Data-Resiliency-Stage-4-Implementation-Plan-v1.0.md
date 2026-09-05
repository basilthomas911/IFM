# Market Data Resiliency Stage 4 Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Plan ID | `MDR-S4-IMP` |
| Status | Owner-approved offline sequencing exception; disabled Stage 4 development in progress; full acceptance remains open |
| Date | 2026-09-04 |
| Scope | Resilient live option-chain and futures streaming with strategy/order/position-owned ticker leases; ES monthly iron condors, weekly vertical spreads and daily outright futures |
| Requirements authority | [Four-stage roadmap, section 8](Market-Data-Reliability-Three-Stage-Implementation-Plan-v1.0.md#8-stage-4--resilient-option-chain-streaming-and-strategy-owned-ticker-leases) (`OCR-01` through `OCR-07`) |
| Recovery authority | [Stage 3 specification](Market-Data-Resiliency-Stage-3-Specification-v1.0.md) |
| Current recovery evidence | [Stage 3 implementation record](Market-Data-Resiliency-Stage-3-Implementation-Record-v1.0.md) |
| Execution evidence | [Stage 4 implementation record](Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md) |
| Pricing rules | [Stage 4 pricing specification](Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md): owner decisions recorded 2026-09-05; implementation/qualification pending |
| Selection policies | [Order Composition selection specification](Order-Composition-Strategy-Selection-Specification-v1.0.md): owner requested active selection for all three profiles; financial profile proposals remain reviewable |
| Unit construction | [Trade Strategy Builder design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md): accepted selected strategy + construction policy + live market snapshot build one unit for each of the three families; Portfolio Risk Manager owns final sizing |
| Implementation prerequisite | Stage 3 acceptance for rollout; owner-approved exception `S4-EX-01` permits disabled implementation and offline tests before that acceptance |
| Enablement | Disabled by default; synthetic qualification first; separate live/production approval |

## 1. What Stage 4 is specifically about

Stage 3 replaces a failed dataset process. Stage 4 retains and reconstructs the live option chains,
individual option/futures contracts and pricing dependencies that strategies and open positions need inside
that replacement process. The UI is a consumer, not the owner of position monitoring.

Example: an Order Composer discovers an ES option chain and selects four iron-condor legs. The
selected legs obtain their own leases before discovery is released. If the GLBX worker dies, its
replacement restores those subscriptions automatically. The composer sees `Recovering` until a
qualified current-generation snapshot exists; closing a browser does not remove position leases.

The first release also covers weekly two-leg option vertical spreads and daily single-contract
outright futures workflows. All three use the same host-owned lease and recovery infrastructure,
with workflow-specific contract validation and readiness. Scope is ES, continuing the original
underlying selection; additional underlyings require explicit qualification.

| Workflow profile | Selected instruments | Discovery and readiness |
| --- | --- | --- |
| `EsMonthlyIronCondor` | Four option legs: one call vertical and one put vertical, same exact expiry/underlying | Monthly chain discovery; all four quotes and required Greeks qualify |
| `EsWeeklyVerticalSpread` | Two options, same exact expiry/underlying and option type, distinct strikes, opposite sides, equal absolute quantities | Weekly chain discovery; both quotes and required Greeks qualify |
| `EsDailyFutures` | One resolved outright futures contract, long or short | Direct futures ticker lease; current futures quote qualifies; no option chain, IV, Greeks or Treasury-rate dependency |

Daily/weekly/monthly identify workflow horizons, not lease TTLs or permission to substitute
contracts. Daily futures means outright futures, not daily-expiry options. Weekly maturity must be
resolved from contract definitions, not assumed to be Friday. The data layer supports call/put and
debit/credit vertical shapes without selecting a trading direction. Existing approved strategy
rules supply direction and signal timing. Following the owner's 2026-09-05 direction, the Trade
Order Composer selects exact expiry, strikes and per-unit leg ratios through an explicit policy;
Portfolio Risk Manager determines final strategy-unit quantity and atomically reserves risk.
MarketData supplies qualified inputs and ownership, not selection decisions. This creates no new
signal or execution logic. End of day does not release an open-position lease or automatically close it.

This document includes proposed design decisions, contracts, storage, implementation packages and
acceptance evidence. It is not an as-built claim or authorization to enable live trading. Proposed
new type names below are implementation targets, not assertions that those types already exist.

### Non-goals

- Historical loading, scheduled downloads, backtesting or strategy profitability selection.
- A durable replay queue for option ticks, chains, Greeks or Market Outlook snapshots.
- New order execution, broker connectivity, automatic cancellation, liquidation or hedging policy.
- Replacing Stage 3 watchdog timing, process containment or session-calendar authority.
- Multi-host active/active ownership or distributed consensus; one authoritative API/Core owner is
  the initial deployment. Do not enable multiple writers against the same ownership scope.
- MarketData choosing strikes, deltas, sizing or entry rules on its own. The Trade-domain composer
  selection policy is now specified in the linked companion document; unapproved financial
  parameters cannot be invented or activated by infrastructure implementation.

## 2. Repository evidence and prerequisite gaps

Original reviewed baseline: `dea86871`; refreshed for offline entry at `e98c06ce`. Source remains
runtime truth; roadmap intent is not evidence of implementation. New offline subset evidence is
recorded separately in the implementation record.

| Existing boundary | Observed behavior | Required Stage 4 work |
| --- | --- | --- |
| `Framework.MarketData/Contracts/Ticker/TickerStreamContracts.cs` | `TickerStreamOwner` has workflow type, workflow ID and leg ID; no lease clock | Keep identity; add explicit lease lifecycle outside workers |
| `Framework.MarketData.DataBento/TickAggregation/TickAggregationService.cs` | First owner starts a transient route, final owner stops it; capture/restore is available in process | Preserve reference semantics; derive worker state from host-owned intent |
| `Application.MarketData/Contracts/IMarketDataApi.cs` | Individual ticker starts/stops accept an owner; chain starts/stops do not | Add owner-aware typed overloads while preserving compatibility |
| `Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs` | `StartOptionChainAsync` throws `MarketDataPricingInputUnavailableException` for the Treasury session rate | Complete production pricing-input and chain wiring; never replace the guard with a dummy rate |
| `Framework.MarketData.DataBento/OptionChain/DatabentoOptionChainSessionManager.cs` | Sessions keyed by underlying/maturity; identical selections share start state, different selections conflict; default capacity eight | Add service-owned leases and generation-aware state; separate logical sessions from physical routes |
| `Application.MarketData/DataBento/DatabentoOptionRouteRegistry.cs` and API contracts | Separate chain/individual ownership and documented route-conflict behavior | Characterize actual callers; replace exclusive ownership with safe shared routing/handoff |
| `Application.MarketData/DataBento/Workers/DatasetWorkerControlProtocol.cs` | Protocol v2 now applies/acknowledges complete bounded core-futures manifests | Extend with negotiated option capabilities and staged/chunked manifests; do not silently reinterpret v2 |
| `Application.MarketData/DataBento/Workers/SupervisedDatabentoLifecycleRuntime.cs` | Current host-owned core-futures intent is reconciled into replacement workers | Extend the registry with Stage 4 leased option/chain/dependency intent; existing core manifests are not option ownership |
| `Application.MarketData/DataBento/Workers/DatasetWorkerPublicationProtocol.cs` | Generation/revision envelope with trade, quote, market-price and statistics kinds | Add explicitly typed option/chain snapshots, invalidation and API-side mirrors |
| Stage 3 implementation record | Synthetic Development evidence; live enablement disabled; platform/provider/UI acceptance outstanding | Offline implementation may proceed under `S4-EX-01`; rollout still requires closure/acceptance |

Project prefixes above abbreviate `TomasAI.IFM.`. This inventory does not certify all Stage 3
requirements as implemented. Core desired manifests and host futures mirrors are implemented;
the remaining Stage 3 engineering/platform/operational gates remain in its as-built specification.

## 3. Decisions and owner questions

### S4-EX-01: approved offline sequencing exception

Following commit `e98c06ce`, the owner explicitly approved proceeding with disabled-by-default
Stage 4 implementation and offline testing while unresolved Stage 3 requirements and live
acceptance gates remain open. This permits implementation packages to proceed under the draft
configurable engineering defaults; it does not sign off Stage 3, approve trading thresholds,
invent missing authority/pricing/strategy policy, or authorize live subscriptions or production
enablement. The non-Synthetic Stage 3 startup guard remains. `S4G-00` is **conditional for offline
development**, not fully passed. Its outstanding decisions and all `S4G-11` evidence remain gates
for rollout. Gate order and honest implemented/tested/accepted distinctions still apply.

Draft defaults make the plan implementable for synthetic tests. They are not approved live settings.
Unanswered owner decisions remain visible; no lack of response is interpreted as approval.

| ID | Proposed decision | Approval boundary |
| --- | --- | --- |
| `D4-01` | First release includes ES monthly four-leg iron condors, weekly two-leg option vertical spreads and daily single-contract outright futures. All three require end-to-end qualification; infrastructure remains reusable | Expanded by owner on 2026-09-04; supersedes monthly-only scope |
| `D4-02` | Fail closed for new composition on stale/missing quotes, Greeks or recovery. Retain position subscriptions. Optional last-known monitoring display is explicitly non-ready, not a stale composition preview or tradeable result | Accepted by owner on 2026-09-04; no automatic order cancellation or position closure |
| `D4-03` | Discovery/composer leases: 120-second TTL, renew every 30 seconds, sweep every 15 seconds; server monotonic clock controls expiry | Proposed configurable engineering defaults; review at `S4G-00` |
| `D4-04` | Durable strategy/order/position leases have no UI-based TTL. Release only from authoritative lifecycle evidence; unknown reconciliation retains the lease and raises an alert | Source mapping approved 2026-09-05: IntrinsicTime workflow → strategy, TradeOrder → working order, TradePosition → position. Versioned/authenticated adapter implementation remains required; see `S4DEP-01/02` |
| `D4-05` | Initial qualification: option quote age <= 5 seconds, futures/underlying quote age <= 5 seconds, option inter-leg quote-time skew <= 2 seconds, composition wait <= 10 seconds. Inter-leg skew and option pricing checks do not apply to the single futures leg | Synthetic defaults only; owner approval and provider evidence required before live use |
| `D4-06` | Immutable daily FMP Treasury input; remaining trading days 0–29 → 1 month, 30–59 → 2 months, 60–89 → 3 months, >=90 → Failed; no interpolation. Add continuous annual decimal conversion to `ITreasuryCurve`; Eastern timezone, contract-specific day count; typed failed errors | Owner requirements recorded 2026-09-05 in the pricing specification. Source-series/convention verification, publication deadlines, contract metadata and implementation evidence remain required; existing DateOnly/365 behavior is not blanket approval |
| `D4-07` | Discovery session limit 8/dataset; 512 contracts/chain; 2,048 unique option contracts/dataset; 256 unique futures contracts/dataset; 10,000 combined leases/dataset; 128 leases/owner. Shared futures/dependency references count once toward unique-contract limits | Synthetic/load-test limits; provider entitlement/capacity validation before live enablement |
| `D4-08` | Durable current intent plus transactional command result/audit outbox in PostgreSQL; market prices remain latest-only transient state | Proposed architecture decision |
| `D4-09` | Trade Order Composer selects exact expiry, strikes/deltas and one-unit leg ratios under versioned monthly-condor, weekly-vertical and daily-futures policies | Owner requested 2026-09-05 and subsequently clarified one-unit construction; final sizing follows D4-10. EOM/debit-first choices and numerical profiles remain proposals |
| `D4-10` | OptionStrategyBuilder inside OrderComposition combines accepted MarketCondition, a versioned construction policy and leg selector to build one complete option unit; Portfolio Risk Manager determines final units and reserves risk | Owner-requested design, 2026-09-05. Builder B1–B5 packages supplement composer C1–C7; one unit is not an approved quantity or a maximum; no runtime implementation/activation is implied |

The owner accepted release scope (`D4-01`), stale-data behavior (`D4-02`), ownership mappings
(`D4-04`), the stated pricing requirements (`D4-06`) and active composer selection direction
(`D4-09`), with one-unit construction/Portfolio-owned sizing clarified in `D4-10`. Do not ask those
resolved questions again. Remaining profile parameters, publication/
contract mappings and detailed live freshness qualification are explicit activation requirements;
implementing configurable mechanics does not approve those settings for trading.

## 4. Ownership and project boundaries

```text
Strategy / Order / Position authority       UI / discovery workflow
                  \                         /
                   Application Market Data API
                              |
                Host-owned subscription coordinator
                + durable intent / lease reconciliation
                + immutable desired manifest (revision N)
                              |
                   Stage 3 dataset supervisor
                              |
                Replaceable dataset worker generation
                + canonical option route manager
                + chain views and pricing dependencies
                              |
                Fenced latest-value API snapshots
                              |
                  Composer / monitoring / UI
```

- Application layer owns lease policy, orchestration, authorized commands and worker manifests.
- Framework layer owns provider adaptation, native feeds, normalized option routes and synchronous
  pricing interfaces. It does not depend on Trade domain actors, UI or PostgreSQL.
- Trade application adapters translate authoritative workflow/order/position facts into commands.
  Do not introduce a MarketData-to-Trade-domain dependency cycle to query positions.
- Persistence implementation follows existing PostgreSQL conventions behind application interfaces.
- Workers contain no authoritative durable owner state and perform no workflow database queries.
- Stage 3 remains the only reset/process-replacement authority. Stage 4 reports route health into
  it; the lease expiry sweep is not a second watchdog.
- Serialize ownership mutation per dataset with a bounded command queue. Publish immutable read
  snapshots. Never hold its mutation gate while awaiting worker/provider/network I/O.
- Versioned completion messages return to the coordinator; late replies cannot mutate newer state.

### Proposed files/components

| Area | New or extended components |
| --- | --- |
| `Application.MarketData/Subscriptions/` | `MarketDataSubscriptionCoordinator`, `DesiredSubscriptionManifest`, `TickerLeasePolicy`, `OptionChainLeaseRegistry`, `SubscriptionReconciler`, `SubscriptionPersistence` interfaces |
| `Application.MarketData/Contracts/` | Lease commands/results, chain identity, immutable composition snapshot, readiness reason codes |
| `Application.MarketData/DataBento/Workers/` | Extend control/publication codecs, supervisor manifest apply and cancellation; integrate admission registry in `DataBento/Resiliency/` |
| `Application.MarketData.Worker/` | Manifest staging/apply, option runtime reconstruction, readiness and typed publication |
| `Framework.MarketData.DataBento/OptionChain/` | Shared physical route manager, generation-local session views, state invalidation and quote/Greeks enrichment |
| API Server composition root | DI, validated configuration, authorization, query and command adapters, host mirrors |
| Existing Trade workflow integration | Adapter for Order Composition pipeline and versioned strategy/order/position ownership reconciliation |
| Existing PostgreSQL storage layer | Explicit migrations, transactional intent store, outbox and retention worker |
| Existing test projects | Application lease/policy tests; provider/native tests; worker fault integration; Trade workflow tests; UI journeys |

Resolve exact Trade adapter and PostgreSQL project placement against dependency rules at `S4G-01`;
do not invent a second workflow engine or persistence framework.

## 5. Identity and lease contracts

### Stable identities

- `TickerKey`: provider routing scope + dataset + canonical domain contract ID + required schema.
  Domain IDs identify contracts; native instrument IDs are value-date-specific resolution data.
- `OwnerKey`: existing `TickerStreamOwner` tuple. Add validated account/portfolio scope where the
  application's authorization requires it; clients cannot choose another owner's scope.
- `LeaseId`: server-issued UUID. Unique active `(scope, owner, target, purpose)` prevents duplicate
  physical ownership. `LeaseVersion` fences renew/release requests against a reacquired lease.
- `ChainKey`: dataset, underlying contract, exact maturity, value date and a canonical hash of the
  sorted unique resolved option contract IDs. Compare the exact set as well as its hash.
- Lease intent survives value-date rollover; its realized chain key/provider mapping is rebuilt
  for the new date. No automatic option-contract roll or expiry substitution is allowed.
- `HostEpochId`, dataset generation, worker instance and manifest revision are distinct. Monotonic
  manifest revisions fence desired-state updates; generation/worker identity fence publication.

### Commands and results

Extend the existing `IMarketDataApi` start/stop method families with typed owner-aware overloads.
They remain the only public admission route; internal coordinators and wire commands are not a
second bypass. Add renewal, owner-scoped query and atomic selected-leg batch operations there.

Commands carry `OperationId`, authorized scope, owner, target, lease purpose, expected lease
version where applicable, correlation ID and deadline/cancellation. The server derives expiry.
Never accept a client assertion that an open position has closed without authoritative evidence.

Typed results distinguish `DesiredAccepted`, `Active`, `AlreadyOwned`, `Released`, `NotOwned`,
`Recovering`, `Closed`, `Expired`, `Conflict`, `InvalidContract`, `CapacityExceeded`,
`PricingUnavailable`, `StaleData`, `PersistenceUnavailable`, `OwnershipUnverified`, `Timeout`
and `Cancelled`. Return lease ID/version, desired revision, realized revision and retry guidance.
An accepted intent is not proof of a running or price-qualified subscription.

The old bool-returning overloads remain compatibility wrappers outside Stage 4 workflows. Preserve
their documented semantics in the legacy profile. Stage 4 callers must use explicit owners and
typed results; ownerless calls cannot create durable position leases. Migrate known callers before
live enablement and test that wrappers cannot bypass the new coordinator when Stage 4 is enabled.

### Idempotency and concurrency

- Repeated acquire with the same owner/target returns the existing lease; it does not increase
  physical subscriptions. Reusing an `OperationId` with different content is a conflict.
- Renew extends from server monotonic time, only for the same active lease/version. A late renewal
  cannot resurrect an expired lease. Reacquire creates a new incarnation.
- Release is idempotent. A delayed release of an older incarnation cannot delete a new lease.
- A retry after a lost response queries/reuses the original operation; cancellation after durable
  acceptance does not silently roll it back. Return an operation identifier for reconciliation.
- During reset reject new acquisitions as `Recovering`; allow valid renewal/release of existing
  intent. Supervisor reconstruction uses the latest committed revision, including those releases.
- Only final effective ownership removal tears down a route. Effective ownership includes chain,
  individual-leg and implicit pricing-dependency references, not just UI owners.

## 6. Shared chain routing and safe leg handoff

Chain sessions are logical selected-universe views, not independent exclusive owners of native
option tickers. Introduce a worker-local canonical route table so a contract has one physical
option source with fan-out to chain views and individual ticker consumers. Underlying aggregation
is shared through explicit dependency references. Preserve accepted futures aggregation behavior.

For the initial release, identical universes share a chain; different universes for the same
dataset/value-date/underlying/maturity return `Conflict` without changing the active chain. Do not
silently widen or replace another workflow's chain. Different maturities may coexist within limits.

Native/provider feeds can batch many contracts; this does not mean one OS process or connection
per option. A physical source cannot be destroyed while any chain, leg or pricing dependency uses
it. Handoff changes logical consumers on that source rather than stop/restart ownership.

Implementation must demonstrate provider-supported incremental subscription/removal or a bounded,
qualified source-reconfiguration protocol. If individual native unsubscribe is unavailable, do not
claim physical cleanup passed by merely dropping callbacks. Explicitly design and test a controlled
rebuild, mark the affected routes recovering, and require architecture approval before live rollout.
No unlimited retained-universe workaround or silent duplication of old/new physical subscriptions.

### Option selected-leg transition (four or two legs)

1. Acquire renewable discovery intent and await a qualified chain snapshot.
2. Select four distinct valid contracts for a monthly iron condor or two for a weekly vertical using
   the approved strategy policy and profile-specific shape validation.
3. Commit the complete selected-leg lease set as one coordinator batch; persist atomically when durable.
4. Apply one manifest revision and qualify every selected route/pricing dependency.
5. Publish the immutable selected-leg snapshot, then release only this workflow's discovery lease.

If any step fails, do not return a partial success. Retain discovery within its valid TTL and
compensate only leases newly created by this operation; never remove pre-existing/shared leases.
Persist a durable handoff record for durable ownership so a crash between steps 3 and 5 converges.
Working-order ownership must be established before composer ownership is released; position
ownership must be established before working-order ownership ends. Partial fills can require both.

### Daily futures transition

Resolve the strategy's exact futures contract through contract authority; acquire a workflow-owned
lease through `StartStreamingFuturesTickDataAsync`'s typed overload; qualify the quote; then return
a single-leg immutable snapshot. Do not allocate a discovery chain or fetch option pricing inputs.
Composer-to-order-to-position ownership uses the same durable handoff/version rules as options.
The snapshot and lease APIs must not assume there are four legs or require dummy Greeks.

The daily workflow can share its futures ticker with Stage 2/3 core aggregation and option pricing
dependencies. Releasing it removes only its own transient reference; it must not stop core futures
aggregation or a reference still needed by a weekly/monthly strategy. Restart restores each valid
reference once. Value-date rollover re-resolves mappings for the held contract; a roll to a different
contract requires authoritative strategy/order/position intent, not an automatic market-data action.

## 7. Pricing, freshness and coherent snapshots

Wire the existing application Treasury-curve boundary and approved Black-76 adapter into production
chain startup before removing the current pricing-input exception. A missing/invalid rate must
return `PricingUnavailable` before provider resources are allocated. Workers receive immutable rate
inputs and their provenance/version, not credentials or permission to fetch reference data.

Apply the [pricing specification](Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md):
daily FMP observation with publication-calendar freshness; exact trading-day tenor buckets with
no interpolation; verified source-convention conversion to continuous annual decimal; precise
expiry, Eastern timezone and contract-specific year fraction. Existing percent/100 and DateOnly/365
helpers do not implement those requirements. Validate option exercise/model compatibility,
multiplier, scale and structured failed results. Unresolved mappings fail, never use a dummy rate.

Define a discriminated `QualifiedStrategyMarketDataSnapshot` with an explicit workflow profile and
asset kind. Its option variant (`QualifiedOptionCompositionSnapshot`) validates four or two legs;
its futures variant (`QualifiedFuturesCompositionSnapshot`) validates one futures leg. Do not use
zero-filled option fields or a hard-coded four-element array as the common contract.

The option variant contains:

- snapshot ID, policy version, dataset/value date, worker/generation, manifest revision and capture
  time; a valid lease set and route versions;
- each leg's contract identity, quote bid/ask/sizes, provider event time, host monotonic receive age
  and source sequence; last trade is optional and not a substitute for a valid two-sided quote;
- underlying snapshot identity/time, curve/rate identity, model version and valuation instant;
- finite IV/Greeks with validity flags, solver status and exact input fingerprint; and
- overall readiness and per-leg typed rejection reasons.

All selected option legs must come from the admitted generation and a single immutable capture. The manifest
revision must be realized, every required lease valid, bids/asks non-crossed and finite, executable
sizes positive, and ages/skew within the approved policy. A coherent capture is not a claim that
all exchange updates occurred simultaneously. Compute final selected-leg Greeks against one captured
underlying/rate/model context rather than mixing asynchronous enrichment versions.

The futures variant carries the same ownership, profile, policy, generation/revision and timestamp
metadata plus its one contract's valid two-sided quote/sizes and optional last trade. It has no
option-pricing requirement. A missing Treasury curve or failed option solver must not make an
otherwise healthy daily futures snapshot unavailable. All profiles fail closed on their own
required stale/missing data, without releasing open-position monitoring leases.

Use host monotonic receive age plus provider event time/delay checks; receiving an old quote now
must not make it fresh. Provider timestamps outside an approved clock-skew tolerance are flagged.
Synthetic default tolerance: one second into the future; freeze live tolerance with clock evidence.
Quote freshness expires even without another message. Revalidate before returning a snapshot;
downstream order admission must independently revalidate, because a snapshot is not execution
authorization. Do not add or silently relax order execution policy in this stage.

The 5-second/2-second/10-second defaults in section 3 are configurable test starting points. Greeks
are current only while their inputs remain valid; missing Greeks never become zero-valued success.
Do not require every illiquid contract in discovery to have a fresh quote before any selection can
proceed: expose per-contract eligibility and require all selected option legs to qualify.

OffTrading composition uses the same quality checks unless a separately approved policy exists.
Closed-session snapshots are non-ready. A stale option alone does not prove a dead dataset: preserve
Stage 3's separation of quiet symbols, provider health, consumer progress and process failure.

## 8. Durable intent and restart reconciliation

Persist ownership, not tick history. Quotes/Greeks are transient latest-value state and are invalid
after startup/reset until refreshed. This does not introduce a JetStream display-replay queue.

Proposed PostgreSQL entities (adapt naming to existing storage conventions):

| Entity | Essential fields and constraints |
| --- | --- |
| `market_data_subscription_lease` | Scope, lease UUID/version, owner tuple, purpose, target, durable state, authoritative source/version, UTC timestamps; unique active owner/target/purpose |
| `market_data_chain_intent` | Chain intent ID, dataset, underlying, maturity, contract-set hash, policy/input references; exact contract set in child rows |
| `market_data_chain_contract` | Chain intent ID + canonical option ID unique; immutable resolved selection |
| `market_data_subscription_revision` | Scope/dataset, host epoch, monotonically increasing revision, desired digest; transactional optimistic concurrency |
| `market_data_subscription_operation` | Operation ID, payload hash, result, batch/handoff state, UTC timestamps; retries return the committed outcome |
| `market_data_subscription_outbox` | Transition ID, owner/target, revision, source version, reason, correlation, delivery state; same transaction as intent |

Do not persist native handles, process IDs as durable authority, secrets, live quotes or monotonic
clock values. Ephemeral leases survive worker resets in host memory, but not an API restart. Give
each API epoch new ephemeral lease tokens and require callers to reacquire after restart.

Durable lease/revision/operation/outbox writes commit before reporting `DesiredAccepted`. Database
failure rejects new durable acquisitions and leaves existing feeds running. Do not tear down a
durable route on an uncommitted release; return `PersistenceUnavailable` and reconcile later.
Ephemeral leases may continue in memory with explicitly degraded audit health. Outbox events can
be retried at least once; transition IDs make consumers idempotent. Workers converge from current
intent and revision, not from replaying every historical acquire/release event.

### API restart sequence

1. Start with route admission closed and a new host epoch; confirm Stage 3 process ownership.
2. Load durable intent and unfinished handoffs; invalidate all cached market-data readiness.
3. Obtain a versioned authoritative snapshot of active strategies, working orders and positions.
   Reconcile events against that snapshot watermark; out-of-order/duplicate events cannot undo a
   newer state. No cross-system transaction or exactly-once delivery is assumed.
4. Recreate missing active leases idempotently. Remove durable leases only on authoritative terminal
   evidence. If authority is unavailable, retain known leases as `OwnershipUnverified`, restore
   monitoring where possible, alert and block new composition. An empty/error response is not proof
   that all positions are closed. If the database itself is unavailable, startup remains non-ready.
5. Rebuild current-date mappings/dependencies and manifests; apply and qualify before ready results.

Periodic reconciliation: every 60 seconds plus startup and authoritative lifecycle notifications.
Unknown owners retained over 15 minutes raise a higher-severity alert, not forced deletion. Expired
contracts become explicitly unavailable and require authoritative resolution; do not substitute a
new expiry or drop an open position because a market session ended.

Initial retention proposal: operation deduplication results and released lease tombstones 30 days,
delivered operational audit transitions 90 days, undelivered outbox rows never age-deleted. Active
leases and incomplete handoffs are never removed by retention. These are operational records, not
a replacement for any existing trade-record retention. Delayed authoritative events remain fenced
by durable source-version watermarks after tombstone pruning. Approve storage policy before rollout.

## 9. Worker protocol, reconstruction and publication

Extend the current worker protocol with a negotiated capability/major-version change for required
manifest and option-publication semantics. An unsupported worker must fail startup explicitly;
do not reinterpret existing numeric message kinds or serialize process-local object graphs.

Add `PrepareManifest`, bounded manifest chunks, `CommitManifest`, `ManifestApplied` and
`ManifestRejected`. The manifest carries dataset/date, host epoch, revision, digest, normalized
contracts, logical chains, effective owners and immutable pricing inputs. Keep credentials out.
Chunk size must respect the existing configured control-frame bound; cap total serialized manifest
at 16 MiB for synthetic qualification, one staged revision per dataset, 30-second staging timeout.
Validate chunk count/order/hash, contract count, duplicates and cumulative size before allocation.

Worker receipt is not an apply acknowledgment. Only a complete staged manifest may be committed.
Duplicate revision+digest is idempotent; same revision with another digest is a protocol error.
Discard incomplete/stale staging without affecting the previous realized revision. A failed apply
must clean up its new resources and report exact failed routes, not acknowledge partial success.

Host separates desired revision, realized revision and price-qualified route status. An ownership
change can briefly invalidate affected reads; unaffected dataset workers never restart. Late output
must pass worker/generation/revision/sequence and current route-ownership checks before any API
mirror, composer or UI sees it. Control/health traffic must not queue behind quote volume.

### Reset/replacement sequence

1. Stage 3 fences failed generation and invalidates its mirrors/readiness.
2. Reject new acquisitions; continue serialized renew/release and durable lifecycle reconciliation.
3. Stage 3 attempts cooperative reset or confirms process death before replacement. No Stage 4
   path performs an independent process kill or waits without a deadline for failed-worker disposal.
4. Rebuild core contracts first, then current option definitions, pricing dependencies, route union
   and chain views from the latest desired manifest. Recheck leases that expired during recovery.
5. Apply and acknowledge the full revision. If intent changed meanwhile, converge to the newer
   revision before exposing affected routes. A removed lease cannot be resurrected by old state.
6. Stage 3 admits qualified infrastructure. Stage 4 separately qualifies option routes/snapshots;
   an illiquid optional leg must not hold healthy ES futures admission hostage.
7. Reopen acquisition and publish current readiness; record recovery duration and missing routes.

Price qualification timeout returns unavailable; it does not create a rapid second restart loop.
Terminal worker/feed faults go to Stage 3 immediately. Its live one-minute/five-minute and
OffTrading five-minute/15-minute policies remain unchanged. Closed sessions retain intent but stop
workers according to Stage 3, rebuilding date-specific realization when the session opens.

Add typed option quote/trade/chain/pricing-status publications; do not cast option messages into
futures-only payloads. API-side stable readers atomically exchange immutable current snapshots and
invalidate old generations immediately. A query must never retain a native reader across restart.

Live option snapshots use bounded latest-by-contract coalescing, not durable replay. Initial flush
cadence: 100 ms with at most one pending update per admitted contract; enforce total contract and
byte bounds (32 MiB pending payload budget for synthetic testing). Invalidation/control is not
silently dropped. Saturation records coalescing/drop counts and invalidates readiness if freshness
cannot be guaranteed. Downstream analytics that require every event keep their existing separate
contract; this stage does not silently convert such consumers to latest-only delivery.

## 10. Configuration, capacity and security

Proposed configuration root: `MarketDataRecovery:Stage4`. Fields: `Enabled` (false), allowed dataset/
workflow/maturity scope, lease durations, command timeout, reconciliation cadence, chain/contract/
owner limits, manifest limits, publication limits, and versioned `SnapshotQualification` policy.

- Startup rejects Stage 4 enabled without accepted Stage 3 containment/capabilities and required
  pricing/authority adapters. No fallback to a partially compatible legacy route owner.
- Validate adapters by enabled profile: option profiles require pricing; daily futures does not.
  All three profiles remain required for full first-release acceptance even if canaries enable them
  incrementally. Monthly/weekly/daily horizons must not become separate competing dataset owners.
- Capacity checks occur before committing intent. Reject new discovery first; never evict existing
  position-owned routes. Startup with more durable positions than configured capacity raises an
  explicit non-ready capacity incident, not silent truncation or an unbounded allocation.
- Reserve recovery resources for already accepted intent; load tests must cover the maximum
  configured manifest, not just one iron condor.
- Authorize scope, workflow identity and durable-purpose transitions server-side. UI callers may
  not release position-owned feeds by guessing IDs. Operator overrides require separate authority,
  reason and audit; no generic stop-all endpoint bypasses lease ownership.
- Logs contain IDs/reasons, not API keys or bootstrap tokens. Metrics use bounded labels; per-owner
  details belong in paged authorized queries, not high-cardinality time-series dimensions.
- No runtime download/provider entitlement changes or production enablement are part of creating
  this document. Obtain authority before adding services, paid data or live subscriptions.

## 11. Health, UI and failure behavior

Extend the existing central operations-health snapshot with per-dataset desired/applied revisions,
active/durable/ephemeral leases, chain/physical-route counts, oldest pending operation, reconciliation
age, pricing readiness, expired/orphan counts, coalescing and storage/outbox health.

Expose paged owner/route detail and typed reasons: recovering, closed, stale quote, missing Greeks,
missing rate, unknown ownership, capacity exhausted, unsupported protocol or failed subscription.
UI disconnect affects renewable UI leases only. The UI must distinguish last-known display from
ready pricing and show which selected leg/dependency prevents composition.

| Failure | Required behavior |
| --- | --- |
| One stale/illiquid option | Mark that route/composition unavailable; retain lease; no dataset restart solely for absent trades |
| Worker hangs or dies | Stage 3 contains it; Stage 4 restores current intent and requalifies |
| Reset while selecting legs | Coherent replacement snapshot or bounded retryable result; no mixed generation or partial option-leg set success; daily futures follows the same generation fence |
| UI disconnect | Discovery expires unless renewed by its real workflow; open positions unaffected |
| Persistence outage after startup | Retain existing routes; reject uncommittable durable mutations; expose degraded health |
| Authoritative workflow query fails | Retain known durable ownership, flag uncertainty, retry reconciliation; do not infer closure |
| Missing rate/solver failure | Pricing unavailable; do not fabricate rate, IV or Greeks |
| Final owner release | Remove logical route once; stop physical source only when all dependencies/consumers are gone |
| Lost manifest acknowledgment | Query/retry same revision+digest; never create a second route owner |

## 12. Gated implementation packages

Only one package is in progress by default. The owner requested implementation and tests on
2026-09-04. This supersedes the earlier documentation-only scope, but does not establish that the
Stage 3 completion/acceptance prerequisite has passed. The entry audit is recorded separately;
do not bypass it or describe isolated Stage 4 scaffolding as a completed implementation.
Every package starts with failing requirement/characterization tests, then implementation and
regression evidence. No unchecked gate may be reported as completed.

| Gate | Work and deliverables | Exit evidence |
| --- | --- | --- |
| `S4G-00` Baseline and decisions | Reconcile Stage 3 gaps/acceptance; approve scope, policy defaults and authoritative ownership/rate sources; map all `OCR` requirements | Signed decision register; accepted Stage 3 evidence; dependency checklist with no hidden prerequisites |
| `S4G-01` Contracts and characterization | Inventory callers; test current chain/individual conflicts; freeze typed APIs, identities, dependency layering and protocol schema | Legacy suites pass; new lease/compatibility tests initially fail for the right reasons; architecture checks |
| `S4G-02` Host lease coordinator | Serialized bounded commands, immutable manifests, monotonic ephemeral expiry, idempotency/version fences and limits | Fake-time acquire/renew/release/race/capacity tests; one route for shared ownership |
| `S4G-03` Durable intent | Migrations, transactional operations/outbox, source-watermarked reconciliation, startup and retention | Real PostgreSQL crash-window, duplicate/out-of-order event, rollback and outage tests |
| `S4G-04` Pricing and production chain prerequisites | Pricing specification P1–P6: Treasury interface conversion, approved tenor buckets, publication cache, exact contract year fraction/model compatibility, coherent production wiring | Converter/calendar/publication tests, managed/native pricer regression/parity, synthetic chain tests; missing input creates no provider resources; daily futures independent of Treasury |
| `S4G-05` Canonical physical routing | Shared chain/leg source ownership, dependency references, atomic four-/two-leg transfer, direct futures leases and provider removal/rebuild semantics | No subscription gap/duplicate source in handoff; daily release preserves core/option underlying references; conflicting universe leaves active chain unchanged; native C++/Rust parity where changed |
| `S4G-06` Dynamic worker protocol | Staged manifests, digest/revision acknowledgment, supervisor apply, typed option publication and host mirrors | Fragmentation/oversize/version rejection; stale ack/output rejected; bounded allocation and timeout tests |
| `S4G-07` Recovery integration | Restore latest intent through cooperative reset, forced replacement, API restart and value-date changes | Process-level kill/hang tests on Windows/Linux; unaffected dataset continuity; no expired/released lease resurrection |
| `S4G-08` Composer/Trade integration | Selection C1–C7 and builder B1–B5: actual one-unit policy/builder/actor integration, versioned authorities, four-/two-leg handoffs, separate unit futures path and Portfolio-owned final sizing | Condition-policy/expiry/leg/unit-payoff tests; unsized execution rejection and Portfolio sizing/reservation boundary; monthly/weekly/daily NoTrade vs Failed, partial fill, rejection, cancellation, reset and independent UI lifetime; supplied-leg fixtures alone are insufficient |
| `S4G-09` Operations and UI | Health/detail queries, non-ready reasons, live refresh, authorization and degraded-storage behavior | API/UI tests show stale/recovering/ready correctly; no bootstrap tokens or unauthorized owner mutations |
| `S4G-10` Synthetic qualification | Bounded sustained load, fault matrix, database outages, lease churn, both native backends and OS containment | Complete synthetic evidence matrix and measured memory/GC/latency; no assumption that synthetic proves provider behavior |
| `S4G-11` Live readiness and acceptance | Provider limits and freshness approval, Development canary, session rollover/soak, rollback rehearsal, as-built record | Explicit owner acceptance; live data/provider/platform evidence; separate production enablement approval |

`S4G-04` must be completed even if production chain availability was assumed in earlier roadmap
wording. If its pricing/domain dependencies do not exist, record prerequisite work and stop that
gate instead of removing the guard. If the selected strategy/composer implementation is unavailable,
its integration is similarly blocked; a fake composer only proves the synthetic boundary.

## 13. Required verification matrix

| Scenario | Required assertion |
| --- | --- |
| Two owners, same option | One physical source, two leases; first release leaves source active; final release stops it once |
| Duplicate/lost/reordered commands | Stable operation outcome; old release/renew cannot affect a new incarnation |
| TTL boundaries and clock changes | Monotonic expiry exact at boundary; UTC jumps do not expire a position or extend a temporary lease |
| Shared/conflicting chain universes | Same exact universe shares; conflict creates no mutation/provider side effects |
| Discovery plus four selected legs | Transfer has no source gap; discovery release cannot clear selected-leg state |
| Weekly vertical discovery plus two legs | Same guarantees for both call and put verticals; same expiry/type/underlying, distinct strikes and complete two-leg set validated; mismatched shapes rejected |
| Daily outright futures | One directly acquired futures lease; long/short profile validated; no option discovery or Greeks/rate dependency; stale futures quote blocks readiness |
| Simultaneous monthly/weekly/daily workflows | Shared underlying has one effective source; stopping daily preserves option dependencies/core aggregation; removing option leases preserves daily ownership |
| Per-profile recovery and session transition | All three restore through reset, forced worker death and API restart; exact weekly expiry and held futures contract preserved; daily position is not released just because the day ends |
| Failure at each handoff step | No partial success or loss of another owner's lease; durable unfinished handoff converges |
| Reset and forced worker death | Chains, legs and pricing dependencies restored from current intent without UI callbacks |
| Release/expiry during reconstruction | Replacement cannot resurrect removed ownership; superseded revisions not admitted |
| API restart and authority outage | Durable leases restore; ephemeral tokens invalid; unknown ownership retained and visibly non-ready |
| Database failure before/after commit | No false durable success; retry recovers operation; existing subscriptions survive |
| Position/order lifecycle | Partial fill, cancel/reject, close and duplicate/out-of-order facts preserve exact effective ownership |
| Freshness and generation | Old received quotes, stale Greeks, skew, expired lease, old worker output and old pricing input rejected |
| Quiet options and closed markets | No false restart loop from absent option trades; closed workers follow Stage 3 schedule |
| Protocol/resource abuse | Wrong identities, hashes, bounds, schema, chunk counts and unauthorized ownership rejected |
| Saturation and soak | Queues/maps stay bounded; freshness fails explicitly; no growth per reset or orphan task/feed |
| Cross-dataset isolation | GLBX recovery does not restart another dataset; optional pricing does not block core futures readiness |
| Native/platform qualification | C++/Rust changed behavior parity; Windows job and Linux process-group child termination evidence |

Use `TimeProvider`/fake monotonic time for all policy tests; actual process tests use bounded
deadlines, not long sleeps as proof. Extend existing application/framework/Trade/UI projects and
the Stage 3 process-test harness instead of relying solely on mocked feeds.

Propose `scripts/Test-DatabentoStage4.ps1` and a matching Linux entry point once tests exist. Each
must separate unit, PostgreSQL, synthetic-process, native and explicitly opted-in live tests. This
document does not claim these scripts or test results exist today.

Synthetic load minimum: 10,000 lease operations, 100 reset/replacement cycles, configured maximum
contracts/leases and sustained option quote load at twice the observed canary rate for 30 minutes.
Report allocated bytes/second, Gen 0/1/2 collections, pause durations, heap/RSS, pending bytes,
coalescing, p95/p99 snapshot/command latency and reset duration. After warm-up/cleanup, no sustained
resource growth by cycle and no configured bounds exceeded. Freeze numerical latency/GC budgets
from the baseline at `S4G-00`; do not invent passing measurements.

Live acceptance covers all three profiles and includes one full relevant trading session, an OffTrading/Closed transition,
value-date reconstruction, UI reconnect and approved reset/restart exercises. If these cannot be
run, mark them pending rather than treating accelerated tests as elapsed live evidence.

### Requirements traceability

| Roadmap requirement | Main gates |
| --- | --- |
| `OCR-01` authoritative subscriptions/leases | `S4G-01`, `S4G-02`, `S4G-03`, `S4G-05` |
| `OCR-02` chain ownership | `S4G-04`, `S4G-05` |
| `OCR-03` Order Composer | `S4G-04`, `S4G-05`, `S4G-08` |
| `OCR-04` reconstruction | `S4G-06`, `S4G-07` |
| `OCR-05` lifecycle/reconciliation | `S4G-02`, `S4G-03`, `S4G-08` |
| `OCR-06` qualification | `S4G-09`, `S4G-10`, `S4G-11` |
| `OCR-07` acceptance | All gates; final evidence in `S4G-11` |

## 14. Deployment and rollback

1. Ship additive migrations and disabled code. Preserve the existing Stage 2/3 runtime selection;
   do not activate two lifecycle owners. Migrations must be safe with the current binary.
2. Qualify synthetic workers and adapters, then approved Development live scope only. Validate
   entitlement, capacity, pricing and query mirrors before removing any live-enablement guard.
3. Rehearse rolling back with active durable ownership. Freeze new composition, persist/drain
   in-flight ownership changes, retain durable intent and verify no duplicate worker generations.
4. A legacy binary that cannot restore Stage 4 position leases is not a safe transparent rollback.
   Use an approved compatible version or an explicit operational monitoring handover. Do not simply
   switch off the flag and silently strand existing position feeds.
5. Do not drop tables or delete active leases during rollback. Keep migration reversal separate
   from operational fallback; report any period where monitoring is unavailable.
6. Production activation is an explicit reviewed configuration/deployment action after acceptance,
   not a side effect of merging code or finishing this document.

## 15. Completion record and checklist

Create `Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md` during implementation, with
gate status, requirement-to-test mapping, commits, commands, artifacts, measured outcomes and
accepted deviations. On completion create an as-built specification from actual source behavior.

- [ ] Stage 3 completion/acceptance and Stage 4 decision register approved.
- [ ] Authoritative ownership/rate integrations mapped and working; no placeholder live adapters.
- [ ] One physical route per effective contract source; reference ownership and handoff proven.
- [ ] Durable intent survives host failure; position ownership is independent of UI lifetime.
- [ ] Chains/selected legs recover from every supported reset with no old-generation admission.
- [ ] Composer returns coherent qualified data or a typed bounded unavailable result.
- [ ] Monthly four-leg, weekly two-leg and daily one-futures workflows each pass end-to-end qualification, including concurrent shared-underlying ownership.
- [ ] Current data remains realtime/latest-only; no durable display replay introduced.
- [ ] Resource bounds, fault isolation, security, native parity and both platforms verified.
- [ ] Provider-connected session/UI/soak and rollback evidence complete.
- [ ] Documentation accurately distinguishes implemented, tested, accepted and enabled behavior.
- [ ] Owner accepts the result; production enablement separately approved.

Until these gates pass, Stage 4 remains planned or partially implemented, never implicitly complete.
