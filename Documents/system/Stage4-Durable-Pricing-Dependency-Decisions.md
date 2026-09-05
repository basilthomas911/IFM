# Stage 4 durable ownership and pricing dependency decisions

| Item | Value |
| --- | --- |
| Status | Ownership mappings and stated pricing requirements recorded 2026-09-05; active composer selection requested; implementation and detailed profile qualification remain open |
| Date | 2026-09-04 |
| Audited baseline | `e98c06ce`; concurrent disabled Stage 4 contract/coordinator work is not certified by this audit |
| Related gates | `S4G-00`, `S4G-03`, `S4G-04`, `S4G-08` |
| Authority | [Stage 4 implementation plan](Market-Data-Resiliency-Stage-4-Implementation-Plan-v1.0.md), including sequencing exception `S4-EX-01` |
| Evidence record | [Stage 4 implementation record](Market-Data-Resiliency-Stage-4-Implementation-Record-v1.0.md) |

## 1. What is authorized, and what is still undecided

The owner approved proceeding with disabled-by-default implementation and offline testing while
Stage 3 acceptance and live acceptance requirements remain open. This permits generic persistence,
reconciliation, pricing-context and handoff engineering. On 2026-09-05 the owner additionally
specified pricing requirements and requested explicit selection policies for all three composers.
The linked specifications below record those decisions and proposed details. This does not
approve every proposed financial parameter, provider subscriptions, production enablement, or a
claim that a synthetic adapter is the production authority.

Accepted scope is ES monthly four-leg iron condors, weekly two-leg vertical spreads and daily
single-contract outright futures. New composition must fail closed when required data is stale or
recovery is incomplete; open-position subscriptions remain retained. Neither a stale quote nor a
terminal strategy workflow authorizes closing a position or cancelling an order.

The decision register below separates necessary engineering contracts from domain policy that must
be resolved before connecting production authorities. Proposed type and table names are targets,
not assertions that implementations exist.

## 2. Exact authoritative source inventory

Paths below are relative to the repository root.

| Source | Existing version/identity boundary | Suitable use and limitation |
| --- | --- | --- |
| `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/ViewModels/IntrinsicTimeStrategyWorkflowReadModel.cs` | Workflow ID, `WorkflowRevision`, `LastEventId`, status/outcome and terminal time; active read model also contains revision/event ID | Strategy workflow authority for that execution. A terminal strategy is not terminal evidence for a working order or position |
| `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/ServiceApi/IIntrinsicTimeStrategyWorkflowQueryApi.cs` and corresponding query actor | Minimum-revision queries; `IntrinsicTimeStrategyWorkflowQueryActor` returns `SnapshotNotReady` for lagging projection | Reuse the revision fence. This is not a globally complete, watermarked snapshot of all strategy/order/position ownership |
| `TomasAI.IFM.Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Events/WorkflowStrategyStateUpdatedEvent.cs` | Committed workflow state carrying workflow revision | Candidate lifecycle notification source; adapter must validate source identity and scope, not trust an arbitrary caller-supplied state |
| `TomasAI.IFM.Domain.Portfolio/Command/Model/PortfolioFundDomainEvents.cs` | Domain-event `Revision`, event ID, command ID, principal | Candidate versioned authority for scoped Portfolio/Fund planned-composition lifecycle |
| `TomasAI.IFM.Domain.Portfolio.Shared/ViewModels/FundCompositionProjectionReadModels.cs` | Portfolio/Fund/Order identity, `AggregateVersion`, workflow revision, selected template/profile and result references | Planned-order identity and composition state, not live broker order/position authority. Trade instructions do not contain resolved option-contract legs |
| `TomasAI.IFM.Domain.Portfolio/Workflow/PortfolioFundCompositionAggregate.cs` | Contiguous order versions and expected-version mutations; composing/risk/cancel/expiry state | Explicitly has no broker or live-position capability. Its cancellation cannot release another authority's position leases |
| `TomasAI.IFM.Domain.Trade.Shared/TradeOrder/Events/TradeOrderFilledEvent.cs` and neighboring order events | `EventId`, `AggregateId`, `EventSource`, order identity, fill/state payload | Candidate source-scoped order facts. A partial fill must preserve both remaining working-order ownership and resulting position ownership |
| `TomasAI.IFM.Domain.Trade.Shared/Events/TradePositionClosedEvent.cs` and neighboring position events | `EventId`, `AggregateId`, `EventSource`, position identity and action | Candidate explicit terminal evidence for the exact position, after authoritative mapping and source ordering are defined |
| `TomasAI.IFM.Domain.Trade.Shared/TradeOrder/ViewModels/TradeOrderReadModel.cs` and `TomasAI.IFM.Domain.Trade.Shared/ViewModels/TradePositionReadModel.cs` | Legacy identity/state/quantity fields; no source revision or snapshot watermark | Useful data projections, insufficient alone for restart-safe terminal reconciliation |
| `TomasAI.IFM.Application.Storage/TradeDb/ITradeDbReadContext.cs` | Workflow revision-aware models alongside legacy order/position queries | Current collection APIs do not provide a complete ownership snapshot plus source watermark. Empty results cannot prove closure |

Event IDs must be compared within their proven source stream/aggregate scope. This audit does not
establish a single global order across Portfolio, Strategy, TradeOrder and TradePosition streams.
Do not compare unrelated `EventId` or `Revision` numbers as though they share one clock. UTC
timestamps identify observations, not authoritative ordering.

## 3. Proposed durable authority contract

Define the provider-neutral interface in `Application.MarketData/Subscriptions`. Implement Trade
and Portfolio translation outside that project so MarketData does not acquire a dependency on
Trade domain actors or introduce a dependency cycle.

### Approved business ownership mappings — 2026-09-05

The owner explicitly confirmed: **"Yes, those proposed mappings are correct"** after the
subscription ownership/release explanation. Record the following source choices as approved,
not as outstanding user questions:

| Subscription purpose | Authoritative business owner | Approved release boundary |
| --- | --- | --- |
| Temporary discovery/composition | Requesting discovery/composer workflow | Explicit release or expiry of its renewable temporary lease |
| Durable strategy monitoring | The specific IntrinsicTime strategy workflow | Confirmed workflow transition ending that workflow's need for the feed |
| Durable working-order monitoring | The specific `TradeOrder` | Confirmed completion, cancellation or rejection removes only the order's claim; resulting position monitoring must remain owned |
| Durable position monitoring | The specific `TradePosition` | Confirmed closure or an authoritative change to that position's required contracts |

Claims remain distinguished by account/portfolio scope, business-object identity and contract/leg.
Partial fills can require simultaneous working-order and position claims. A composer/UI disconnect,
strategy termination or order cancellation must never release an independent position's claim.
The UI, watchdog and missing projection rows are not closure authorities. Shared physical feed
teardown requires removal of all effective claims/dependencies; physical routing integration is
still pending, not made operational by this approval.

This resolves the business source selection in `S4DEP-01`. It authorizes the corresponding adapter
engineering under the existing offline implementation scope. It does not establish that those
adapters exist, approve a permissive authorization boundary, make raw event IDs contiguous, or
certify complete restart snapshots. Exact event-to-transition translation, identity encoding,
authentication and version/watermark handling still need implementation and verification.
Portfolio planned-composition state is not substituted for live `TradeOrder`/`TradePosition`
authority. The subsequent pricing/composer decisions are recorded below; live enablement remains
a separate decision.

### Source facts

Each accepted lifecycle fact must carry:

- Authenticated source kind and stable source-stream/aggregate identity.
- Authorized account/portfolio scope and stable `SubscriptionOwnerKey` mapping.
- Positive source version, stable logical event ID and correlation/causation identifiers.
- Explicit state: active, terminal, or unknown; exact resolved contract/dependency set for active
  ownership; terminal reason/evidence for release.
- Schema version and canonical content digest. The same source version with different content is
  a conflict, not a newer fact. A repeated matching fact is idempotent.

An adapter is responsible for proving that a source is authorized to assert that owner/purpose.
The coordinator/store must not allow a UI request to label itself `Position` and thereby become
authoritative, or to release position feeds by guessing a lease ID.

### Reconciliation snapshots

The proposed snapshot interface returns typed availability, the covered scope, an explicit
completeness assertion, per-source watermarks and active/terminal ownership facts. Paged snapshots
must identify one consistent capture; all pages must be complete before absence is considered.
If the source cannot establish those guarantees, return unknown/incomplete and retain known
ownership. Never convert an exception or empty legacy query into a successful empty snapshot.

Subscribe/catch up lifecycle events against the snapshot watermarks. Persist accepted source
versions so duplicate or delayed events cannot resurrect terminal ownership after tombstone
retention. Gaps in a source requiring contiguous versions trigger bounded reconciliation rather
than speculative state transitions. Sources supporting only monotonic complete-state facts need
their semantics declared explicitly; the generic adapter cannot assume every event is complete.

Working-order and position leases are independent. A cancel/reject/expiry can remove only the
exact authority it terminates. Already filled exposure remains position-owned. A workflow's
discovery/composer expiry cannot terminate strategy/order/position ownership. A value-date change
rebuilds provider mapping for the held canonical contract, not an automatic contract roll.

### Retain-on-unknown behavior

On authority outage, retain persisted leases and restore monitoring where possible, mark
`OwnershipUnverified`, and block new composition. Reconciliation uncertainty is a health incident,
not evidence of closure. Proposed periodic reconciliation is 60 seconds, with escalation after
15 minutes of uncertainty; those intervals never become a position TTL.

## 4. Proposed PostgreSQL integration contract (`S4G-03`)

### Placement and schema

`TomasAI.IFM.Application.Storage` already references `Application.MarketData`; put store interfaces
in the application and the PostgreSQL implementation in Storage. Reuse
`MarketDataServiceDbConnection`, the `market_data_service` schema and `ObjectDataRepository`
conventions unless a reviewed storage decision selects another existing boundary.

Existing examples are `MarketDataServiceSchemaDb.cs`, `MarketDataServiceSchemaSql.cs`,
`MarketDataServiceDbSql.cs`, and `MarketDataServiceDbContext.cs` under
`TomasAI.IFM.Application.Storage/MarketDataServiceDb`. They demonstrate additive schema definitions,
typed parameters, row-version checks and transactions. They do not already implement the complete
Stage 4 transaction/outbox contract.

Proposed entities:

| Entity | Required constraints/role |
| --- | --- |
| Subscription lease | Server lease UUID/incarnation version, scope/owner/purpose, exact target, durable state and source evidence; unique active scoped owner/target/purpose |
| Chain intent and contract members | Exact bounded contract set plus canonical digest; member uniqueness; never rely on digest equality alone |
| Dataset subscription revision | Scoped routing identity, committed monotonic revision and desired digest; compare-and-swap or transaction row lock |
| Subscription operation | Scoped operation UUID, canonical request hash, committed typed result and handoff state; conflicting reuse rejected |
| Subscription outbox | Unique transition ID, revision, source version, reason/correlation, delivery state; insert in the same transaction as intent |
| Authority watermark | Stable source scope/stream, highest accepted version, relevant event/content identity; retained independently of released-lease tombstones |

Do not persist prices, Greeks, native handles, provider credentials, live process identity as
ownership authority, or monotonic clock readings. Host epoch identifies runtime fencing; durable
ownership must restore under a new host epoch. Ephemeral client tokens must not survive API
restart. The durable identity/token restoration contract needs an explicit serialization version.

### Store operations and transaction semantics

A proposed store interface provides bounded, cancellation-aware operations for:

1. Loading current durable intent, source watermarks and incomplete handoffs in bounded pages.
2. Reading a prior operation outcome by authorized scope and operation ID after a lost response.
3. Applying an immutable ownership transition with expected dataset revision and source version.
4. Leasing/acknowledging bounded outbox batches with retry-safe transition identities.
5. Retaining/pruning only eligible operational records in bounded batches.

One database transaction must check idempotency, validate version expectations, mutate all affected
leases/chain intent, advance revision/source watermarks, store the operation result and append the
outbox transition. A multi-leg handoff commits all selected legs or none. No success or physical
route teardown may precede the corresponding durable commit.

The coordinator's serialized mutation loop must not hold its gate while awaiting storage/network
I/O. Use versioned completion messages; revalidate the expected revision before applying results.
Provider/worker realization is asynchronous after intent acceptance and cannot run inside the
database transaction. `DesiredAccepted` is not `Active`, price-qualified, or permission to trade.

If a commit outcome is uncertain, reconcile by operation ID. Cancellation after commit does not
undo ownership. A cancelled caller must not cause an already committed acquisition to disappear.
Database failure before commit returns `PersistenceUnavailable`; existing routes remain active.
An uncommitted release cannot remove a route. A durable startup load failure keeps admission
non-ready; it is not treated as no durable positions.

Outbox publication is at least once, with idempotent transition IDs. It is an operational ownership
audit, not a realtime tick replay queue. Workers restore the latest current desired manifest, not
every historical acquire/release event. Multi-host active/active writers remain out of scope; an
enabled deployment must enforce the initial single-authoritative-owner restriction.

### Proposed retention and verification

The plan proposes 30-day operation results/released tombstones and 90-day delivered audit records.
These are not approved rollout settings. Active intent, incomplete handoffs, undelivered outbox
records and source watermarks protecting ordering must not be age-deleted. Retention must not
break retry safety: expired operation IDs need an explicit rejection/horizon contract, not silent
re-execution of an old release or acquisition.

Independent real PostgreSQL tests should cover before/after-commit failure, lost result,
same/different-payload duplicates, concurrent expected-version conflict, atomic two-/four-leg
handoff, out-of-order sources, watermark survival after pruning, outbox retry, database outage,
restart hydration and migration/rollback compatibility. Test fixtures must use a verified dedicated
test database, never the running application's data. No database was modified for this document.

Rollback retains tables and active leases. An older binary unable to restore them needs an
approved monitoring handover; simply disabling the flag is not evidence of safe rollback.

## 5. Pricing dependency and financial-convention boundary (`S4G-04`)

### Owner pricing requirements — 2026-09-05

The owner specified daily FMP Treasury data; remaining trading days <30 → one month, <60 → two
months, <90 → three months, >=90 → Failed; continuous annual decimal conversion as an additional
Treasury-curve interface function; Toronto/New York timezone; contract-specific day count; and
Failed with error details for missing inputs/calculation failures. These source/tenor/output
requirements are resolved, not pending questions. No tenor interpolation or invented fallback.

The [pricing specification](Market-Data-Resiliency-Stage-4-Pricing-Specification-v1.0.md) now defines
the conversion contract, calendar/day-count separation, publication-aware daily freshness,
coherent context and P1–P6 test packages. Source-series evidence, publication deadlines/allowance,
contract metadata mappings and actual integration remain qualification work. The existing
DateOnly/365 pricer and percent/100 property do not satisfy the newly recorded requirements.

| Existing source | Verified contract | Missing production decision/integration |
| --- | --- | --- |
| `Framework.MarketData/Contracts/ReferenceData/ITreasuryCurve.cs` | Latest curve on/before requested date; missing curve is null | Add continuous-rate function; FMP daily source accepted, publication-policy implementation still required |
| `Framework.MarketData/Contracts/ReferenceData/TreasuryCurveSnapshot.cs` | Value date, rate points, retrieval time, source, country/currency | Immutable source version/digest and approved validity interval |
| `Framework.MarketData/Contracts/ReferenceData/TreasuryTenor.cs` | Rate percent units; `DecimalRate` divides by 100; enum values are months | Implement approved 30/60/90 trading-day boundaries without interpolation and verified yield-convention conversion |
| `Framework.MarketData.FinancialModelingPrep/FinancialModelingPrepTreasuryCurve.cs` | Real normalized FMP data; missing/conflicting points rejected | Newest within lookback is not necessarily an approved current pricing curve |
| `Framework.OptionPricer/Black76/OptionCalculator.cs` | Date-only Actual/365 Fixed; continuously compounded annual decimal rate; explicit failed Greeks | Add exact contract year-fraction path and model/exercise-style qualification; existing date-only convention is not blanket approval |
| `Framework.MarketData.DataBento/OptionChain/OptionChainSessionContracts.cs` | Synchronous `IOptionChainGreeksEnricher` boundary | No production enricher found in the audited baseline; only test fake |
| `Application.MarketData/DataBento/DatabentoMarketDataEpoch.cs` | `StartOptionChainAsync` throws for unavailable Treasury session rate | Complete wiring before removing guard; validate pricing before creating provider resources |

Paths in this table omit the `TomasAI.IFM.` prefix for readability.

There is no safe implicit rate convention to copy from legacy callers. Treasury import preserves
percentage-point units in `Domain.MarketData/YieldCurveRate/Event/YieldCurveRatesImported.cs`.
`UI.Net.Services/MarketData/MarketDataQueryService.cs` divides the one-month value by 100, while
`Domain.MarketData.Feed/Event/TradeLiveFeedAdded.cs` selects one/two/three-month fields without that
normalization. Neither helper establishes accepted continuously compounded session-rate policy.
This finding is an integration warning, not authorization to change unrelated legacy behavior.

Propose an application `IOptionPricingContextProvider` returning an immutable context or typed
unavailable result. The context should carry source/version/hash, curve value date, valuation and
maturity, approved convention/policy/model IDs, selected normalized session rate and validity.
Capture it with one underlying quote context and recompute final selected-leg Greeks synchronously
through the existing pricer. Do not combine independently enriched legs from different contexts.
No zero/dummy rate or zero-success Greeks fallback is allowed.

The daily outright futures profile must not depend on the pricing-context provider or option
solver. Missing Treasury data can block option composition without blocking a current qualified
daily futures quote. Engineering can test both behaviors with explicitly synthetic inputs while
production source-series metadata, calendar mappings and wiring remain unqualified.

## 6. Composer boundary and ownership of selection (`S4G-08`)

`Domain.Trade.Shared/Strategy/Workflow/IntrinsicTime/Pipeline/Commands/StartOrderCompositionPipelineCommand.cs`
already carries immutable workflow input/revision, correlation and deadline. Processing/completed/
failed events and routing exist. The audited baseline contains no production Order Composition
pipeline actor or contract/strike-selection implementation. Portfolio composition reservation and
legacy iron-condor UI editing do not fill that gap.

`Domain.Portfolio.Shared/Contracts/PortfolioWorkflowContracts.cs` contains
`OrderCompositionResultReference`, an ID/hash/evaluation/expiry reference. It does not define a
resolved four-leg/two-leg/one-futures candidate. The missing production producer is not permission
for the market-data layer to choose strikes, deltas, direction, quantity, expiry or entry rules.

MarketData can validate an authoritative resolved selection, acquire all selected legs atomically,
retain discovery until handoff commits, and return a coherent qualified snapshot or unavailable
result. A synthetic caller can qualify that integration contract. It cannot prove that the absent
production composer or broker/position lifecycle works. Keep those `S4G-08` acceptance rows open.

### Owner composer direction — 2026-09-05

The owner explicitly requested that the Order Composer in the strategy workflow choose expiry,
strikes, deltas and quantities using a designed policy for monthly ES iron condors, weekly ES
verticals and daily outright ES futures. The choice between production selection and supplied-leg
infrastructure is resolved in favor of production selection. This does not transfer selection
authority to MarketData or authorize order submission.

The [selection specification](Order-Composition-Strategy-Selection-Specification-v1.0.md) defines
the three algorithms, versioned policy inputs, risk/sizing boundary, Failed versus NoTrade,
atomic subscription handoff and C1–C7 acceptance packages. Monthly EOM, weekly debit-first and
numeric delta/width/entry profiles are explicit proposals for review, not assumed owner approvals.
Existing Portfolio/RiskManagement authority supplies actual budgets and approves/reserves risk.

### One-unit builder and final sizing clarification — 2026-09-05

The owner subsequently clarified Portfolio Risk Manager's responsibility for contract count and
requested an Option Strategy Builder using MarketCondition, a construction policy and a leg
selector to create **one complete unit**. The [builder design](../../TomasAI.IFM.Domain.Trade/Strategy/Workflow/IntrinsicTime/OrderComposer/Docs/Trade-Strategy-Builder-Design-v1.0.md)
now defines this component inside OrderComposition. Its result includes exact contracts, sides,
ContractsPerUnit and unit economics, but no final strategy-unit quantity or risk approval.

Portfolio Risk Manager issues authenticated construction constraints, then independently sizes
the resulting unit against current Portfolio/family/Fund capacity and atomically reserves risk.
Approving multiple units is initial sizing, not increasing a Composer-approved order. This
supersedes the earlier Composer-sized/RiskManager-reduce-only proposal. Stage 4 subscriptions still
identify contracts and owners; additional units do not require duplicate physical feeds.

## 7. Decision register and what may proceed now

| ID | Decision/status | Work allowed before decision |
| --- | --- | --- |
| `S4DEP-01` | Approved 2026-09-05: IntrinsicTime workflow → strategy; TradeOrder → working order; TradePosition → position; independent release/retention boundaries as above | Implement and verify the corresponding source adapters; approval is not evidence of deployed integration |
| `S4DEP-02` | Remaining engineering: exact scope/identity encoding, service authorization and aggregate completeness/watermark semantics; business owner selection is approved | Implement/test adapters and restart reconciliation; do not register a permissive production adapter or treat raw event IDs as a contiguous revision |
| `S4DEP-03` | Proposed: current-intent PostgreSQL tables and transactional result/outbox, single authoritative host | Additive disabled schema/store implementation and dedicated database tests; deployment/production migration approval remains separate |
| `S4DEP-04` | Pending: final retention/retry horizon and operational ownership | Bounded retention eligibility tests; no production deletion policy activation |
| `S4DEP-05` | Owner requirements recorded: daily FMP; 30/60/90 trading-day tenor buckets, no interpolation; interface continuous-rate conversion; Eastern time, contract-specific day count; typed failures. Detailed metadata/publication qualification remains | Implement pricing specification P1–P6 offline; retain production guard until wiring and source/model/calendar requirements qualify |
| `S4DEP-06` | Owner requests active composer selection with complete one-unit construction; builder design created, Portfolio Risk Manager owns final sizing/reservation; financial profiles proposed and runtime integration missing | Implement builder B1–B5 and composer C1–C7 offline; preserve distinct unit/sized-result types and existing authority boundaries; production activation remains separate |

`S4G-03` does not have to wait in its entirety for the ownership mapping: transactional schema,
store interfaces, atomicity, idempotency, outbox, failure recovery and retention protections are
independent engineering. Full gate completion still requires a real approved authority adapter,
source-watermarked restart reconciliation and matching integration evidence. Similarly, an
isolated pricer adapter does not complete `S4G-04`, and a fake composer does not complete `S4G-08`.

This document records approved business ownership mappings, owner-specified pricing requirements,
the requested composer direction, proposed implementation details and remaining activation inputs.
It does not approve every proposed financial parameter or claim gate completion/live readiness.
