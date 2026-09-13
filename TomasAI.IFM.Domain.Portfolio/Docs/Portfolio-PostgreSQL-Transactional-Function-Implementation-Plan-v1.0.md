# Portfolio PostgreSQL and Transactional Function Implementation Plan v1.0

| Item | Value |
| --- | --- |
| Status | Core implementation complete through the Portfolio Function boundary; RiskManager-to-Portfolio runtime integration remains |
| Created | 2026-09-12 |
| Scope | Replace the ScyllaDB `PortfolioDbContext` with one PostgreSQL context and provide every event-sourced FunctionActor an opt-in EventSource-owned atomic business/event transaction |
| Governing decision | Portfolio is the PostgreSQL financial book of record; Portfolio business rows and their completed event commit or roll back together |
| Supersedes | Every statement that defines `PortfolioDbContext` as a ScyllaDB context or permits authoritative Portfolio state to depend on an eventual Scylla projection |
| Related documents | [Portfolio-Fund-Specification-v1.0.md](./Portfolio-Fund-Specification-v1.0.md), [Portfolio-Fund-Implementation-Plan-v1.0.md](./Portfolio-Fund-Implementation-Plan-v1.0.md), [Portfolio-Financial-Implementation-Manifests-v1.0.md](./Portfolio-Financial-Implementation-Manifests-v1.0.md) |

## Implementation status (verified 2026-09-13)

The storage and Portfolio Function implementation is active in the working tree:

- `PortfolioDbContext` is a sealed, non-partial PostgreSQL context. `IPortfolioDbReadContext` and `IPortfolioDbWriteContext` are its only persistence context interfaces, and runtime dependency injection maps both to the same concrete singleton.
- All Portfolio-owned production DDL, DML, query, locking, and verification statements are centralized in `PortfolioDbSql.cs`. The former `Application.Storage/PortfolioFinancial` physical folder and `PortfolioFinancialDbContext` are absent.
- EventSource owns the request-scoped PostgreSQL connection, transaction, event append, receipt, commit, rollback, and uncertain-commit classification. The enlisted surface rejects use after its callback has completed.
- Startup validates that `PortfolioDbConnection` and `EventSourceActorDbConnection` name the same PostgreSQL host, port, database, and user before an atomic Portfolio operation can run.
- `PortfolioOrderCompositionFunctionActor` follows the standard FunctionActor maps and extension handlers. It commits the decision, per-Fund results, accepted orders and legs, completed event, operation receipt, and financial revision in one transaction. Duplicate identities replay the committed result; conflicting content, expiry, and database constraint failures leave no partial state.
- The typed NATS API and both application host registrations are implemented. No background poller or recovery coordinator was added for order composition.
- The compatible migration keeps existing immutable `portfolio_financial` definitions in their established PostgreSQL schema under the unified context. It creates the new `portfolio` tables additively and uses the bounded `PortfolioProjectionRebuilder`, legacy inventory, writer-fence, and retention workflows. It does not copy over or overwrite immutable definitions merely to rename their schema.

`CompositionLeg.InstrumentId` carries the canonical contract ID. The active market-data epoch already resolves that contract to its Databento instrument ID. The provider-neutral `IMarketDataApi.TryGetMarketInstrumentId` boundary exposes that existing translation for populating `TradeLegDefinition.MarketInstrumentId`; the composition snapshot must not duplicate the numeric provider identity. Translation occurs before the Portfolio Function call so Portfolio remains independent of Databento. A missing, conflicting, or zero mapping produces a classified workflow handoff failure and no Portfolio write.

Portfolio now determines accepted Funds before allocating identities. It obtains one sequence-backed Order ID per accepted Fund and one reserved Trade ID per accepted component through `IPortfolioBusinessIdAllocator`, then commits those accepted orders atomically with the Portfolio decision and completed event. No identity is allocated for a rejected Fund or a `NoTradeOrders` result, and no hash-derived or placeholder identity is permitted.

## 1. Objective

Implement one PostgreSQL `PortfolioDbContext` as the Portfolio business and financial book-of-record context. Remove the parallel `PortfolioFinancialDbContext` abstraction and fold its retained responsibilities into the Portfolio PostgreSQL boundary.

Extend the shared event-sourced FunctionActor lifecycle so any FunctionActor can opt into an EventSource-owned PostgreSQL transaction. A Function-specific PostgreSQL handler shall apply all required business-table changes using the enlisted EventSource connection and transaction. The EventSource context shall append the completed event and commit only after every enlisted write succeeds.

The design provides one outcome for an atomic Function execution:

```text
business rows + completed event + operation receipt = committed
or
business rows + completed event + operation receipt = rolled back
```

No application actor, projector, storage handler, or provider method may report committed success before PostgreSQL confirms the transaction commit.

## 2. Non-negotiable decisions

1. `PortfolioDbContext` uses the `System.Data.Postgres` provider.
2. Exactly one concrete runtime `PortfolioDbContext` owns current Portfolio persistence.
3. `PortfolioFinancialDbContext` is removed after its retained behavior is migrated.
4. `EventSourceDbContext` continues to own event serialization, event names, stream identity, expected-version enforcement, event append, and event-log reads.
5. EventSource owns the connection, transaction, commit, rollback, and uncertain-commit classification for atomic Function completion.
6. Portfolio and EventSource tables participating in one transaction reside in the same physical PostgreSQL database. Separate schemas are permitted and required for ownership clarity.
7. `PortfolioDbConnection` and `EventSourceActorDbConnection` must resolve to the same host, port, database, and compatible security identity when atomic Portfolio completion is enabled.
8. Actor classes contain no SQL and do not receive `NpgsqlConnection` or `NpgsqlTransaction`.
9. Function-specific mapped extension handlers retain domain execution responsibility. Storage implementations retain SQL responsibility.
10. Every event-sourced FunctionActor has access to the opt-in atomic completion contract. Calculation-only Functions remain on the event-only path.
11. PostgreSQL business handlers may enlist in the EventSource transaction. ScyllaDB, NATS, broker APIs, HTTP services, files, and other external resources may not enlist.
12. ScyllaDB projection failure never rolls back or relabels a committed PostgreSQL business transaction.
13. Existing EventProjector durable queues remain the only delivery mechanism for optional ScyllaDB projections. No second projection or recovery queue is introduced.
14. A duplicate command with the same identity and input hash returns the original committed completion. It does not rerun the business calculation or reapply writes.
15. A duplicate identity with different input content fails as an idempotency conflict.
16. No background poller or recovery service reruns an atomic Function.
17. Existing immutable event definitions and committed events are never overwritten.
18. Schema migration is additive until cutover has been verified. Destructive cleanup requires a separate explicit decision.
19. `PortfolioDbContext` is one sealed, non-partial concrete class. No partial `PortfolioDbContext` files are permitted.
20. Every Portfolio-owned production SQL statement or fixed SQL fragment resides in `PortfolioDbSql.cs`; Portfolio context, store, actor, handler, schema, and migration classes contain no inline SQL.
21. Portfolio persistence exposes exactly two context interfaces: `IPortfolioDbReadContext` and `IPortfolioDbWriteContext`. No capability-specific or composite Portfolio database interfaces are introduced.
22. All read members are declared in `IPortfolioDbReadContext.cs` and all write members are declared in `IPortfolioDbWriteContext.cs`, grouped inside each interface by related Portfolio area.

## 3. Current implementation and gaps

### 3.1 Current PortfolioDb

The current `Application.Storage/PortfolioDb/PortfolioDbContext.cs` uses `PortfolioDbConnection`, registered as `System.Data.ScyllaDb`. It stores JSON read models in CQL tables such as `portfolio_by_id`, `fund_by_portfolio`, `fund_allocation`, `fund_risk_envelope`, order indexes, composition indexes, and policy indexes.

This context is a rebuildable read projection and cannot serve as the PostgreSQL financial book of record.

### 3.2 Current PortfolioFinancial subsystem

`Application.Storage/PortfolioFinancial` contains PostgreSQL schema, ledger, capacity, authority, history, migration, query, export, and staged workflow stores. These stores use `IPostgresEventTransaction`, which opens a connection to `EventSourceActorDbConnection` and exposes an `EnlistedEventTransaction` capable of executing SQL and appending events.

The atomic mechanism is technically useful, but its ownership and naming are too narrow. The EventSource transaction capability shall become shared infrastructure, while retained Portfolio business storage moves under `PortfolioDb`.

### 3.3 Current provider behavior

The shared PostgreSQL provider opens a new connection whenever it does not observe its repository-owned transaction. Some command/query paths recognize a repository transaction, while other batch, query, and map/reduce paths create their own connection directly.

The repository transaction object stores mutable connection/transaction state on the repository. Because application DbContexts are registered as singletons and multiple actor mailboxes may execute concurrently, that mutable ambient mechanism shall not be used for atomic Function completion.

### 3.4 Current FunctionActor support

`BaseEventSourceFunctionActor` already distinguishes `FunctionCompletionMode.AtomicBusinessAndEvent` and requires `ITransactionalFunctionStateRepository`. This is the correct opt-in decision point. The current implementations are Portfolio-financial-specific and let the business store invoke event append. The final implementation shall invert that ownership so EventSource controls the entire commit protocol.

### 3.5 Current consistency limitation

ScyllaDB cannot join an `NpgsqlTransaction`. A synchronous Scylla write before PostgreSQL commit can remain visible when PostgreSQL rolls back. A Scylla write after PostgreSQL commit can fail after the authoritative event is durable. Therefore, Scylla writes cannot be classified as atomic Function participants.

## 4. Target physical and logical database layout

All atomic Portfolio and event tables shall share one physical PostgreSQL database:

```text
PostgreSQL server/cluster
└── IFM transactional database
    ├── event_source schema (or existing event tables during compatible migration)
    │   ├── event_stream_id
    │   ├── event_name_id
    │   ├── event_log
    │   ├── command log/receipt structures
    │   └── durable projection delivery structures
    │
    └── portfolio schema
        ├── Portfolio/Fund definition and version tables
        ├── financial policy and assignment tables
        ├── general-ledger tables
        ├── current balance and capacity tables
        ├── risk decision/history tables
        ├── accepted TradeOrder tables
        ├── operation receipt tables
        └── PostgreSQL query indexes/views
```

SQL shall schema-qualify table names. Correctness shall not depend on connection `search_path`.

Named connection strings may remain separate for dependency ownership, but startup validation shall prove that both identify the same physical PostgreSQL database. Merely sharing a server is insufficient.

## 5. Target project and folder ownership

```text
TomasAI.IFM.Application.Storage/
  EventSourceDb/
    EventSourceDbContext.cs
    IEventSourceFunctionTransaction.cs
    IEnlistedPostgresTransaction.cs
    EventSourceFunctionTransaction.cs
    Persistence/
      event appenders and codecs

  PortfolioDb/
    PortfolioDbContext.cs
    IPortfolioDbReadContext.cs
    IPortfolioDbWriteContext.cs
    PortfolioDbSql.cs
    Schema/
      PortfolioSchemaDb.cs
      PortfolioSchemaSql.cs
      PortfolioSchemaMigration.cs
    Portfolio/
    Fund/
    FinancialPolicy/
    GeneralLedger/
    Risk/
    Capacity/
    OrderComposition/
    Migration/
    Queries/
```

`Application.Storage/PortfolioFinancial` shall be removed after every retained class has a mapped destination or an approved retirement disposition. The migration shall not create a second PostgreSQL Portfolio context under another name.

`PortfolioDbContext.cs` shall contain the single sealed context implementation and shall not use partial class declarations. `PortfolioDbSql.cs` shall contain all Portfolio-owned production DDL, DML, query, locking, migration, and verification SQL constants. Event-store SQL remains in the EventSource-owned `EventSourceDbSql.cs`; SQL owned by another bounded context remains with that context.

`IPortfolioDbReadContext.cs` and `IPortfolioDbWriteContext.cs` are the only Portfolio persistence-interface files. Do not introduce `IPortfolioDbContext`, `IPortfolioLedgerStore`, `IPortfolioCapacityStore`, `IPortfolioRiskStore`, `IPortfolioOrderCompositionStore`, or similar persistence interfaces. `PortfolioDbContext` implements the read and write interfaces directly. Dependency injection registers the concrete context once and maps both interfaces to that same instance.

## 6. Shared transactional Function contracts

### 6.1 EventSource transaction coordinator

Introduce an EventSource-owned contract conceptually equivalent to:

```csharp
public interface IEventSourceFunctionTransaction
{
    ValueTask<TCompleted> CommitFunctionAsync<TRequest,TCompleted>(
        TRequest request,
        TCompleted candidate,
        ITransactionalFunctionEventHandler<TRequest,TCompleted> handler,
        CancellationToken cancellationToken = default);
}
```

The concrete implementation belongs to `EventSourceDbContext` or an EventSource-owned collaborator resolved by it. The public contract must not expose transaction lifecycle methods to actors.

### 6.2 Enlisted PostgreSQL operations

The transaction coordinator supplies a request-scoped interface conceptually equivalent to:

```csharp
public interface IEnlistedPostgresTransaction
{
    ValueTask<int> ExecuteAsync(...);
    ValueTask<T?> ScalarAsync<T>(...);
    ValueTask<IReadOnlyList<T>> QueryAsync<T>(...);
}
```

Required properties:

- all commands use the coordinator-owned `NpgsqlConnection` and `NpgsqlTransaction`;
- parameterized SQL is mandatory;
- queries are bounded or return an explicitly streamed result that is disposed before the next command;
- the participant cannot call commit, rollback, begin transaction, close connection, or retain the session;
- the session becomes invalid immediately after the callback finishes;
- use after completion throws;
- concurrent use of one enlisted session is forbidden unless explicitly implemented and tested;
- provider exceptions retain their PostgreSQL classification for rollback and retry policy.

### 6.3 Transactional Function event handler

Introduce a storage-side handler contract conceptually equivalent to:

```csharp
public interface ITransactionalFunctionEventHandler<in TRequest,TCompleted>
{
    ValueTask<TCompleted> PrepareAsync(
        IEnlistedPostgresTransaction transaction,
        TRequest request,
        TCompleted candidate,
        CancellationToken cancellationToken);

    ValueTask ApplyAsync(
        IEnlistedPostgresTransaction transaction,
        TRequest request,
        TCompleted completed,
        long eventVersion,
        CancellationToken cancellationToken);
}
```

`PrepareAsync` performs authoritative reads, locks, invariant checks, and result finalization. It may perform writes that do not require the assigned event version. `ApplyAsync` maps the finalized completed event to all required PostgreSQL tables and may store the assigned event version as provenance. Both calls execute inside the same transaction.

The precise interface may combine these operations if the implementation can preserve final-event construction and event-version provenance without ambiguity. The required order and ownership may not change.

### 6.4 EventSource special enlist function

The special EventSource function shall execute this algorithm:

1. Validate request identity, stream identity, expected stream version, command ID, input hash, and event contract before opening a transaction.
2. Open one pooled `NpgsqlConnection` using the EventSource transactional database identity.
3. Begin one `NpgsqlTransaction` using the approved isolation level.
4. Apply bounded lock and statement timeouts.
5. Read the command/function receipt using command ID plus input hash.
6. If an identical completion exists, deserialize and return the original completion without invoking the business handler.
7. If the identity exists with a different hash, throw the stable idempotency-conflict failure.
8. Invoke `PrepareAsync` to lock authoritative rows, revalidate current state, and finalize the completed event.
9. Append the finalized event using EventSource serialization and expected-stream-version enforcement.
10. Invoke `ApplyAsync` to map the completed event into the Function-owned PostgreSQL tables.
11. Insert the generic Function operation receipt, including command ID, input hash, stream, event version, event ID, completed-event type, and committed timestamp.
12. Execute deferred PostgreSQL constraints before commit where required for deterministic failure classification.
13. Commit exactly once.
14. Return the stored completed event only after confirmed commit.
15. On any exception before `COMMIT`, roll back and return no completed result.
16. On a PostgreSQL server response proving commit failure, classify the operation as rolled back.
17. On connection loss or cancellation while `COMMIT` is in flight, return `CommitOutcomeUnknown`; do not report rollback and do not execute the Function again automatically.
18. Permit a bounded, synchronous receipt lookup with the original identity to resolve an uncertain response. This is reconciliation of the committed result, not Function replay.

### 6.5 Retry policy

Only a database-only transaction delegate may retry, and only after PostgreSQL positively confirms rollback for a serialization failure or deadlock. The handler must be deterministic and perform no external side effects. Lock timeout, uniqueness conflict, validation refusal, caller cancellation, and uncertain commit do not automatically retry.

## 7. Base FunctionActor integration

### 7.1 Common capability

Retain `FunctionCompletionMode` as the explicit execution-policy switch:

- `EventOnly`: calculation result is persisted through the standard Function event path;
- `AtomicBusinessAndEvent`: EventSource calls the transactional handler and commits business rows plus completion together.

Every `BaseEventSourceFunctionActor` specialization can select either mode through its existing mapped execution-policy extension. No FunctionActor is required to become transactional merely because the capability exists.

### 7.2 Actor convention

Actor classes continue to contain only:

- actor name and typed base declaration;
- parse map;
- validation map;
- receive map;
- event map;
- execution-policy map;
- overrides that dispatch into those maps.

Domain calculation remains in the Function extension handler or `Model` folder. SQL remains in an Application.Storage handler. No raw provider types enter Domain projects.

### 7.3 Completion behavior

The base actor shall:

- reject `AtomicBusinessAndEvent` if no transactional repository/handler is registered;
- reject an independent pre-commit projector on the atomic path;
- leave in-memory Function state incomplete after confirmed rollback or unknown commit;
- finalize in-memory state only after confirmed commit;
- return the original stored completion on an identical duplicate command;
- preserve comprehensive failure stage, PostgreSQL classification, correlation identity, and inner exception detail without leaking financial payloads or SQL parameters.

### 7.4 Existing FunctionActor disposition

| FunctionActor | Initial completion mode after infrastructure migration | Required action |
| --- | --- | --- |
| `RegimeDiscoveryFunctionActor` | EventOnly | Requalify unchanged behavior against the new shared base |
| `MarketConditionFunctionActor` | EventOnly | Requalify unchanged behavior |
| `TradeSelectionFunctionActor` | EventOnly | Requalify unchanged behavior |
| Trade `OrderCompositionFunctionActor` | EventOnly | Retain as Portfolio-independent construction stage and requalify |
| `RiskManagementFunctionActor` | EventOnly or retired orchestration role according to the approved Portfolio-risk redesign | Do not add Portfolio SQL |
| `CapacityReservationFunctionActor` | Retire or absorb | Replace staged reservation with the atomic Portfolio order-composition decision |
| `CapacityConsumptionFunctionActor` | Reassess under OrderExecution boundary | Retain only if execution admission remains a distinct atomic business transition |
| `PortfolioOrderCompositionFunctionActor` | AtomicBusinessAndEvent | First production consumer of the generalized EventSource enlistment path |

## 8. PostgreSQL PortfolioDbContext design

### 8.1 One concrete context

`PortfolioDbContext` becomes the only concrete Portfolio database context and implements exactly `IPortfolioDbReadContext` and `IPortfolioDbWriteContext`. There are no additional capability-specific Portfolio persistence interfaces and no composite `IPortfolioDbContext` interface.

The concrete class is sealed and non-partial. Its complete implementation resides in `PortfolioDbContext.cs`. Capability-specific domain calculations remain in their domain `Model` folders and mapped extension handlers; they do not become partial DbContext implementations.

`IPortfolioDbReadContext.cs` groups read methods with clear sections for Portfolio, Fund, financial policy, General Ledger, capacity, risk, order composition, TradeOrder, operations, and migration verification. `IPortfolioDbWriteContext.cs` groups write methods using the same areas. Grouping is organizational only and does not introduce more interfaces.

Dependency injection shall register `PortfolioDbContext` once and register both interfaces by resolving that same concrete instance. Read-only consumers receive `IPortfolioDbReadContext`; mutation handlers receive `IPortfolioDbWriteContext`. `IDbContextFactory` shall expose the read/write interfaces separately or the concrete context internally as needed; it shall not require a third Portfolio persistence interface.

### 8.2 Connection handling

Standalone read methods may open and dispose pooled connections normally. Atomic mutation methods must require `IEnlistedPostgresTransaction` and must never invoke a provider method that opens another connection.

Do not store the current transaction in a singleton `PortfolioDbContext`, static field, `AsyncLocal`, actor context, or service provider scope. The request-owned enlisted handle must be passed explicitly down the atomic call chain.

### 8.3 Provider changes

The shared provider shall gain an explicit enlisted execution path or Portfolio SQL shall call the enlisted abstraction directly. The selected approach must cover command, scalar, single-row, bounded query, and batch operations.

Provider requirements:

- preserve existing standalone behavior for non-atomic callers;
- never silently fall back to opening a new connection when an enlisted call was requested;
- reject mismatched connection/database identity;
- reject commands whose `NpgsqlTransaction.Connection` differs from the supplied connection;
- support asynchronous execution and cancellation;
- avoid sync-over-async transaction creation and disposal;
- preserve prepared/batched parameter support where measurement demonstrates value;
- expose instrumentation proving whether a call was standalone or enlisted.

### 8.4 PostgreSQL schema

Create a versioned `portfolio` schema. Initial authoritative groups are:

| Group | Required records |
| --- | --- |
| Portfolio | Portfolio identity, immutable versions, operating state, current-version pointer |
| Fund | Portfolio membership, immutable mandate versions, operating state, effective intervals |
| Strategy permissions | Fund-to-strategy-family/deployment assignments with exact definition/version/hash |
| Allocation | Immutable allocation versions and current allocation pointer |
| Risk policy | Immutable Portfolio policy versions, active assignment, effective interval |
| Risk envelope | Immutable Fund envelope versions and current envelope pointer |
| General Ledger | Book, accounts, posting rules, transactions, journals, entries, balances, periods, valuation, reconciliation |
| Capacity | Current exposure/capacity by bounded scope, committed changes, provenance |
| Order composition | Portfolio decision, per-Fund decision, accepted TradeOrder, TradeOrder legs, source composition identity/hash |
| Operations | Function receipt, event version, input hash, financial revision, commit timestamp |
| Migration | Schema version, source inventory, cutover state, verification evidence |

Use normalized authoritative tables and targeted indexes. JSONB may retain immutable input/evidence snapshots, but indexed business identities, status, revisions, amounts, units, dates, and foreign keys must be typed columns.

All Portfolio-owned SQL is centralized in `PortfolioDbSql.cs`. Organize that single file with nested static groups such as `Schema`, `Portfolio`, `Fund`, `FinancialPolicy`, `GeneralLedger`, `Capacity`, `Risk`, `OrderComposition`, `Queries`, and `Migration`. The grouping does not create partial classes or additional SQL-definition files. Values are always parameters; dynamic identifiers and interpolated business values are prohibited. `PortfolioSchemaDb` and migration code reference the schema constants from `PortfolioDbSql.cs` rather than declaring SQL locally.

### 8.5 Replacement of Scylla query shapes

Replace the existing CQL tables with PostgreSQL tables, indexes, or views:

| Current Scylla shape | PostgreSQL replacement |
| --- | --- |
| `portfolio_by_id` | `portfolio.portfolio_version` plus current-version index/view |
| `portfolio_by_state` | operating-state/current-version index |
| `fund_by_portfolio`, `fund_by_id` | `portfolio.fund_mandate_version` indexes |
| `active_fund_by_portfolio_horizon` | partial/effective interval index or view |
| `fund_template_assignment` | typed strategy/deployment assignment table |
| `fund_allocation` | immutable allocation version table |
| `fund_risk_envelope` | immutable envelope version table |
| old Fund order/trade projection tables | accepted TradeOrder and TradeOrder-leg tables; do not preserve premature Trade IDs |
| `fund_composition_by_workflow` | Portfolio order-composition decision and source-workflow index |
| policy tables | policy identity/version plus active assignment tables |

Queries shall preserve existing actor API paging and filtering contracts unless a contract is explicitly superseded by the approved Portfolio risk/order flow.

## 9. Portfolio order-composition transactional event handler

### 9.1 Input

`EvaluatePortfolioOrderCompositionCommand` carries:

- command, correlation, causation, workflow, and composition identities;
- target `PortfolioId` used to route to the Portfolio authority;
- the complete immutable Portfolio-independent order composition;
- strategy family/variant and horizon;
- instrument and leg definitions;
- pricing, Greeks, payoff, liquidity, and objective risk evidence;
- exact parameter/configuration IDs, versions, hashes, and effective times;
- input content hash and expiration.

It carries no preselected `FundId`, `OrderId`, or `TradeId`.

### 9.2 Atomic preparation

Inside the EventSource transaction, the Portfolio handler shall:

1. acquire the Portfolio financial authority lock;
2. validate Portfolio state and financial-book availability;
3. load current eligible Funds and exact active mandates/policies/envelopes;
4. verify referenced configuration versions and expiry;
5. evaluate the one selected market opportunity against every eligible Fund;
6. calculate accepted size and required capacity per Fund;
7. reject Fund candidates independently with stable reasons;
8. allocate Order IDs only for accepted Fund decisions;
9. create one complete TradeOrder per accepted Fund;
10. produce `ExecuteTradeOrders` when at least one order is accepted;
11. produce `NoTradeOrders` when none is accepted;
12. preserve a complete per-Fund decision set for Portfolio risk history.

### 9.3 Transactional event-to-table application

Map `PortfolioOrderCompositionCompletedEvent` synchronously into:

- one Portfolio composition-decision row;
- one per-Fund decision row for every evaluated Fund;
- one accepted TradeOrder row per accepted Fund;
- all TradeOrder leg rows;
- capacity/exposure changes for each accepted order;
- immutable Portfolio risk-history evidence;
- operation receipt and event provenance.

Invariants:

```text
ExecuteTradeOrders => TradeOrders.Count > 0
NoTradeOrders      => TradeOrders.Count == 0
```

No staged reservation, separate Fund authorization, recovery coordinator, or background Function replay is permitted.

### 9.4 Dispatch boundary

RiskManager receives the committed completion. `ExecuteTradeOrders` causes deterministic commands to OrderExecution after PostgreSQL commit. `NoTradeOrders` terminates the workflow normally. External dispatch is not part of the database transaction.

## 10. ScyllaDB disposition

### 10.1 Portfolio Scylla context

The current Scylla `PortfolioDbContext`, CQL schema, registrations, and direct projection dependencies shall be removed from the Portfolio runtime after PostgreSQL query parity and cutover verification.

If a future performance requirement justifies a Scylla Portfolio read replica, it must be introduced under an explicit `PortfolioProjectionDbContext` name and may contain only rebuildable views.

### 10.2 Other Scylla projections

Other domains may continue using the existing EventProjector durable queue:

```text
PostgreSQL event + durable delivery record commit
    -> EventProjector queue
    -> Scylla projection
```

A Scylla failure leaves the delivery pending and visible through Supervisor/Actor Health. It does not rerun the Function and does not change the committed business result.

### 10.3 Prohibited synchronous projection behavior

Do not write Scylla before PostgreSQL commit and then roll back only PostgreSQL on failure. Do not delay PostgreSQL commit waiting for a Scylla acknowledgement. Neither ordering provides atomic cross-database consistency.

## 11. Data migration and compatibility

### 11.1 Inventory

Before schema changes, record:

- PostgreSQL event counts and stream revisions for every Portfolio/Fund/policy stream;
- current `portfolio_financial` table counts, keys, hashes, and schema version;
- Scylla Portfolio table counts and highest projected event IDs;
- orphan identities, invalid payloads, version gaps, and rows without source events;
- all application/UI queries that depend on the existing Scylla shapes.

Any unexplained authoritative mismatch is a migration blocker. A stale or missing rebuildable Scylla row is not an authoritative mismatch when the PostgreSQL event history is valid.

### 11.2 Additive schema creation

Create the new `portfolio` schema without modifying existing events or deleting existing tables. Register new completed-event types additively. Schema initialization shall be idempotent and version checked.

### 11.3 Portfolio aggregate materialization

Materialize Portfolio, Fund, allocation, envelope, assignment, and policy PostgreSQL tables from immutable EventSource history using a bounded, explicit migration operation. Preserve event IDs, aggregate versions, effective times, payload hashes, and deletion tombstones.

This is a controlled migration/rebuild operation, not a continuously running recovery service.

### 11.4 Financial table migration

Retain the established `portfolio_financial` ledger, authority, capacity, history, receipt, and immutable-definition tables in place while moving their code ownership under `PortfolioDb`. Both PostgreSQL schemas are owned through the one concrete Portfolio context and share the EventSource physical database and transaction. A schema-only rename or copy provides no correctness benefit and creates an unnecessary immutable-record conflict surface.

Requirements:

- preserve every immutable business identifier and source hash in place;
- preserve accounting dates, value dates, settlement dates, financial revisions, and event references;
- preserve journal balancing and foreign-key constraints;
- inventory counts and deterministic hashes before enabling new mutations;
- never overwrite an existing immutable definition;
- on identity/content conflict, stop and report the exact rows;
- use additive schema creation and explicit version checks only.

### 11.5 Cutover

1. Deploy additive PostgreSQL schema and transaction infrastructure with old runtime routes still disabled for the new path.
2. Run inventory and dry-run validation.
3. Materialize Portfolio event history and verify retained financial rows in place.
4. Validate counts, hashes, revisions, constraints, and representative queries.
5. Stop Portfolio mutations for a bounded maintenance window.
6. Apply the final delta using source event/revision watermarks.
7. switch `PortfolioDbConnection` to `System.Data.Postgres` and the shared physical database;
8. activate the PostgreSQL Portfolio registrations and queries;
9. run production-path verification;
10. resume Portfolio mutations;
11. retain old Scylla/schema sources read-only during the observation period;
12. remove legacy storage only under a later explicit cleanup gate.

There is no dual-write operational period.

## 12. Retirement and relocation matrix

| Current component | Disposition |
| --- | --- |
| Scylla `PortfolioDbContext` | Replace with PostgreSQL implementation |
| `PortfolioDbCql` / `PortfolioSchemaCql` | Replace with SQL/schema definitions |
| `PortfolioFinancialDbContext` | Remove after responsibilities move into `PortfolioDbContext` |
| `PortfolioFinancialSchema` | Merge retained DDL into versioned `portfolio` schema |
| `GeneralLedgerStore` | Move under `PortfolioDb/GeneralLedger`; retain behavior and atomic rules |
| `LedgerConfigurationStore` | Move under `PortfolioDb/GeneralLedger` |
| `FinancialQueryStore` | Merge its approved reads into grouped members of `IPortfolioDbReadContext` implemented by `PortfolioDbContext` |
| `FinancialAuthorityPreparationStore` | Merge into Portfolio transaction preparation; eliminate redundant context |
| `PortfolioAuthorityFence` | Move into Portfolio transaction/locking implementation |
| `CapacityReservationStore` | Absorb reservation/admission behavior into atomic Portfolio decisions where applicable |
| `FundRiskAuthorizationStore` | Retire; Portfolio order composition is the single acceptance decision |
| `FinancialWorkflowRecoveryJournal` | Retire from the Portfolio Function flow |
| `CapacityExpiryDispatchStore` | Reassess separately; expiry must not introduce a polling recovery loop |
| `FinancialHistoryJournal` / `FinancialHistoryProjection` | Retain only for external read projection delivery; do not duplicate PostgreSQL book-of-record state |
| legacy migration stores/fences | Move under `PortfolioDb/Migration` until migration closure |
| `AccountingExportStore` | Move under `PortfolioDb/GeneralLedger/Export`; external delivery remains after commit |
| `EmulatorExecutionStore` | Keep out of Portfolio migration scope pending the approved OrderExecution/emulator design |
| `FinancialTelemetry` | Generalize/relocate to Portfolio PostgreSQL telemetry |

## 13. Failure, idempotency, and concurrency rules

1. Lock one Portfolio authority row before evaluating mutable financial capacity.
2. Acquire additional locks in deterministic PortfolioId/FundId/resource-key order.
3. Use explicit expected aggregate and financial revisions.
4. Enforce one operation receipt per Portfolio/CommandId with an input hash.
5. Enforce unique accepted Order IDs and exact Portfolio/Fund ownership.
6. Use check, foreign-key, exclusion, and unique constraints for invariants that PostgreSQL can enforce.
7. Never mutate a committed historical decision or event; corrections append a new transition.
8. Return business rejection as a completed `NoTradeOrders` decision.
9. Return validation/infrastructure failure with full classified details and no committed business mutation.
10. Treat commit uncertainty separately from confirmed rollback.
11. Never allocate or expose Trade IDs before execution/fill creates the completed trade.
12. A timeout or lost actor response does not prove rollback; an original-identity receipt query determines whether a completion committed.

## 14. Observability requirements

Expose bounded metrics through the existing Supervisor runtime ownership:

- Function transaction attempt count and duration;
- preparation, event append, event application, constraint, and commit duration;
- confirmed commit, confirmed rollback, deadlock retry, serialization retry, lock timeout, statement timeout, idempotent hit, input conflict, and unknown-commit counts;
- Portfolio lock wait duration without Portfolio/Fund identity labels;
- rows written per table group using bounded group labels;
- independent connection attempts rejected during an enlisted scope;
- Scylla projection queue depth, oldest age, attempt count, and last failure through existing EventProjector metrics.

Logs shall include actor name, command ID, correlation ID, failure stage, PostgreSQL SQLSTATE/category, and Supervisor failure ID. Logs shall not include complete trade payloads, credentials, connection strings, or raw SQL parameters.

## 15. Test strategy

### 15.1 BDD tests

- accepted composition across one Fund commits one TradeOrder and one completed event;
- accepted composition across multiple Funds commits the complete TradeOrder list atomically;
- no eligible Funds commits `NoTradeOrders` with an empty list and complete decision reasons;
- one rejected Fund does not prevent another eligible Fund from receiving an order;
- failure in any required table update leaves every table and event stream unchanged;
- identical duplicate command returns the original result and IDs;
- conflicting duplicate identity changes nothing;
- concurrent evaluations cannot oversubscribe Portfolio/Fund capacity;
- no Order or Trade identity exists before Portfolio acceptance;
- no background process reruns a Portfolio decision.

### 15.2 Shared unit tests

- every FunctionActor may select `EventOnly` or `AtomicBusinessAndEvent`;
- atomic mode rejects a missing transactional handler;
- atomic mode rejects an independent pre-commit projector;
- actor state completes only after confirmed commit;
- confirmed rollback and unknown commit leave state incomplete;
- enlisted session rejects use after callback completion;
- participant cannot commit, roll back, or open a connection through the enlisted contract;
- retries occur only for confirmed database-only serialization/deadlock rollback;
- exception mapping retains complete failure stage and inner classification;
- all existing FunctionActor parse/validation/receive/event/policy map convention tests pass.

### 15.3 Storage unit tests

- PostgreSQL SQL/schema snapshots and migration-version transitions;
- an architecture test proves `PortfolioDbContext` is sealed, is not partial, and has exactly one production declaration;
- an architecture audit proves Portfolio-owned production SQL exists only in `PortfolioDbSql.cs`;
- an architecture test proves Portfolio persistence defines only `IPortfolioDbReadContext` and `IPortfolioDbWriteContext`, each in its matching file;
- a dependency-injection test proves both interfaces resolve to the same `PortfolioDbContext` instance;
- exact table/column/type/index/constraint contracts;
- deterministic completed-event-to-table mapping;
- command/input hash calculation;
- duplicate receipt equality and conflict rules;
- provider enlisted path never calls `CreateConnection`;
- standalone path still opens/disposes a pooled connection;
- connection/database identity comparison handles normalized equivalent connection strings;
- schema-qualified SQL does not depend on `search_path`.

### 15.4 Real PostgreSQL integration tests

Use a real PostgreSQL database containing both EventSource and Portfolio schemas. Inject a failure at every boundary:

1. after authority lock;
2. after first Fund decision;
3. after capacity update;
4. after TradeOrder header;
5. after a TradeOrder leg;
6. after risk-history write;
7. after event append;
8. after operation receipt;
9. during deferred constraint validation;
10. immediately before commit;
11. during commit acknowledgement.

For cases 1–10, verify all business tables, event stream version, and receipts remain at their prior values. For case 11, verify the response is `CommitOutcomeUnknown` and resolve it through the original receipt without automatically executing the Function again.

Additional integration coverage:

- concurrent commands for the same Portfolio serialize correctly;
- concurrent commands for different Portfolios can progress independently;
- expected-stream and financial-revision conflicts roll back all changes;
- process restart returns the exact committed completion;
- event payload and PostgreSQL table evidence have matching hashes and identities;
- same server but different database names fail startup atomicity validation;
- same physical database with separate schemas succeeds;
- no atomic SQL path opens a second connection.

### 15.5 Migration integration tests

- representative Portfolio/Fund/policy event histories materialize identically in PostgreSQL;
- deleted Draft tombstones do not resurrect rows;
- all retained financial tables migrate with equal counts and deterministic hashes;
- immutable destination conflict stops migration without overwrite;
- interrupted migration resumes from explicit migration state without duplicating rows;
- final delta closes exactly at the recorded event/financial revision;
- old Scylla data remains unchanged during migration;
- post-cutover code performs no Portfolio Scylla read or write.

### 15.6 NATS/actor integration tests

- real `EvaluatePortfolioOrderCompositionCommand` route reaches the Function actor and returns the committed result;
- actor response cannot precede PostgreSQL commit;
- lost response plus identical retry returns the original completion;
- actor cancellation before commit rolls back;
- cancellation after confirmed commit does not relabel success;
- malformed and expired inputs return complete error detail with no writes;
- `ExecuteTradeOrders` is dispatched only after committed completion;
- `NoTradeOrders` terminates the workflow without execution dispatch.

### 15.7 Scylla/EventProjector verification

Where any optional Scylla projection remains:

- PostgreSQL commit succeeds while Scylla is unavailable;
- the existing durable projection queue retains the pending event;
- projection recovery applies the event exactly once to its final visible state;
- projection failure never reruns the Function;
- Supervisor health reports queue depth, age, and failure;
- no new durable queue or polling recovery service is registered.

### 15.8 End-to-end verification

Run a production-shaped ITI workflow through Regime Discovery, Market Condition, Trade Selection, Trade Order Composition, RiskManager orchestration, and Portfolio order-composition acceptance. Verify the exact final composition hash, per-Fund decisions, accepted TradeOrders, capacity deltas, completed event, workflow continuation, and restart query results.

### 15.9 Performance and allocation tests

Benchmark and measure:

- event-only Function completion baseline;
- atomic no-trade completion;
- atomic one-Fund acceptance;
- atomic representative multi-Fund acceptance;
- same-Portfolio contention;
- independent-Portfolio concurrency;
- standalone provider calls versus enlisted calls;
- allocation rate and bytes allocated per completion;
- connection opens per transaction, which must equal one;
- p50, p95, p99, throughput, lock wait, serialization time, and commit time.

Performance optimization may not weaken the single-transaction invariant, validation, idempotency, or error detail.

## 16. Implementation gates

### PPG-01 — Baseline and governing-document correction

**Implementation:** inventory current source/tests/data; record clean targeted baselines; update Portfolio design/specification statements that describe PortfolioDb as Scylla; approve the retirement matrix.

**Exit:** one authoritative written topology states PostgreSQL PortfolioDb plus EventSource-owned atomic enlistment; unresolved domain ownership conflicts are identified before code moves.

### PPG-02 — Shared EventSource enlistment contracts

**Implementation:** introduce storage-neutral Function transaction contracts, request-scoped enlisted PostgreSQL operations, lifetime guards, and connection identity validation.

**Exit:** shared unit tests prove ownership/lifetime rules; no Portfolio code required.

### PPG-03 — EventSource transaction coordinator

**Implementation:** add the special EventSource Function enlist method, event append, generic receipt, rollback/retry/unknown-commit policy, and instrumentation.

**Exit:** real PostgreSQL fault-matrix tests prove event-only and enlisted atomic commits.

### PPG-04 — Base FunctionActor integration

**Implementation:** route `AtomicBusinessAndEvent` through the EventSource coordinator while preserving all standard actor maps and event-only behavior.

**Exit:** every existing FunctionActor builds and passes lifecycle/convention tests; no existing calculation Function is forced onto PostgreSQL business writes.

### PPG-05 — PostgreSQL Portfolio schema and context

**Implementation:** replace Portfolio CQL/context/schema with versioned PostgreSQL SQL, one concrete `PortfolioDbContext`, explicit standalone/enlisted call paths, constraints, indexes, and query parity.

**Exit:** real PostgreSQL integration tests pass every existing supported Portfolio query and mutation contract; runtime registration contains no Scylla `PortfolioDbContext`.

### PPG-06 — PortfolioFinancial consolidation

**Implementation:** relocate retained ledger, capacity, authority, query, export, telemetry, and migration responsibilities; remove `PortfolioFinancialDbContext`; retire obsolete staged/recovery components according to the matrix.

**Exit:** all authoritative Portfolio financial operations resolve through the PostgreSQL PortfolioDb boundary; no second Portfolio DbContext remains.

### PPG-07 — Data migration tooling and qualification

**Implementation:** build explicit inventory, materialization, financial-copy, hash comparison, delta, and cutover commands. No continuous migration service.

**Exit:** representative and full development datasets migrate with exact counts/hashes/revisions and no immutable overwrite.

### PPG-08 — PortfolioOrderComposition Function

**Implementation:** add `PortfolioOrderCompositionFunctionActor`, `EvaluatePortfolioOrderCompositionCommand`, completed/failed contracts, domain model, transactional handler, Portfolio table mappings, RiskManager handoff, and atomic accepted/no-trade outcomes.

**Exit:** BDD/unit/real PostgreSQL/NATS/verification tests prove zero, one, and multiple accepted TradeOrders plus complete rollback and idempotency behavior.

### PPG-09 — Scylla retirement and query/UI cutover

**Implementation:** switch Portfolio query actors and UI APIs to PostgreSQL; remove Portfolio Scylla registration and CQL dependencies; retain only explicitly named external projections.

**Exit:** repository audit finds no production `PortfolioDbConnection` registered as Scylla and no Portfolio query depends on a Scylla row.

### PPG-10 — Full qualification and release evidence

**Implementation:** run solution builds, all affected unit/BDD/integration/verification/UI tests, migration rehearsal, fault matrix, concurrency runs, and performance benchmarks; publish evidence and operational rollback instructions.

**Exit:** all tests pass without unexplained skip, all schemas are version verified, the atomic fault matrix has zero partial writes, and the implementation document records exact commits and results.

## 17. Gate dependency order

```text
PPG-01
  -> PPG-02
  -> PPG-03
  -> PPG-04
  -> PPG-05
  -> PPG-06
  -> PPG-07
  -> PPG-08
  -> PPG-09
  -> PPG-10
```

Schema and migration design may be prepared while shared infrastructure is developed, but runtime cutover cannot occur before the EventSource atomic fault matrix passes. Portfolio order composition cannot become operational before the PostgreSQL context and retained financial behavior are qualified.

## 18. Gate results and verification evidence

| Gate | Result | Evidence |
| --- | --- | --- |
| PPG-01 | Complete | The topology, context/interface constraints, transaction ownership, compatible migration rule, and retirement matrix are recorded here. |
| PPG-02 | Complete | `IEnlistedPostgresTransaction`, database identity comparison, and completed-scope guards have passing unit coverage. |
| PPG-03 | Complete | Real PostgreSQL tests prove atomic event/business/receipt commits, rollback on expiry, and rollback after a mid-write constraint failure. |
| PPG-04 | Complete | `AtomicBusinessAndEvent` remains opt-in; Portfolio Function mapping, replay, policy, and full error-detail unit tests pass. Existing Trade and Portfolio actor suites remain green. |
| PPG-05 | Complete | PostgreSQL `PortfolioDbContext`, additive schema, query/mutation parity, projection tombstones, bounded queries, and host registrations are implemented and tested. |
| PPG-06 | Complete | One physical `PortfolioDb` source folder owns the retained stores and SQL. `PortfolioFinancialDbContext` and the old physical folder are removed. The existing financial namespace remains temporarily for source compatibility. |
| PPG-07 | Complete for compatible migration | Additive schema creation, bounded event-history projection rebuilding, immutable legacy inventory, writer fencing, retention, and exact conflict checks pass real PostgreSQL tests. Existing `portfolio_financial` rows remain in place rather than being destructively copied. |
| PPG-08 | Function complete; runtime handoff remains | Model, contracts, standard FunctionActor, transactional store, typed API, host maps, zero/one/multiple Fund decisions, post-acceptance sequence identity allocation, idempotency, rollback, and isolated-NATS tests pass. Runtime handoff must translate each canonical contract ID through the active market-data epoch before calling Portfolio. |
| PPG-09 | Complete | Production audit finds no Portfolio Scylla provider registration, CQL class, or Scylla-dependent Portfolio query. Other bounded contexts retain their own Scylla registrations. |
| PPG-10 | Partially qualified | Affected builds and automated tests pass. Workflow activation, end-to-end execution, and performance qualification remain after PPG-08 runtime integration. The repository-wide build separately identifies the intentionally removed legacy OptionTrade UI command service still referenced by `EndOfDayProcessViewModel`; that legacy UI conversion remains outside this backend gate. |

Verification results on 2026-09-12:

- Portfolio unit: 216 passed.
- Portfolio BDD: 34 passed.
- Selected real PostgreSQL Portfolio integration suite: 27 passed, including 5 focused order-composition cases and the added mid-write rollback case.
- Isolated real-NATS Portfolio Function API: 1 passed; the temporary broker was stopped and removed after the run.
- Trade unit: 1,086 passed.
- Affected TradeFlow BDD: 5 passed; integrated: 5 passed; verification: 8 passed; storage integration: 1 passed.
- Production SQL audit under `Application.Storage/PortfolioDb`: zero inline SQL statements outside `PortfolioDbSql.cs`.
- Context audit: exactly `PortfolioDbContext`, `IPortfolioDbReadContext`, and `IPortfolioDbWriteContext`; the old `Application.Storage/PortfolioFinancial` directory is absent.
- 2026-09-13 focused qualification: 22 market-data API/epoch tests, 9 Portfolio order-composition unit tests, and 5 real-PostgreSQL order-composition integration tests pass after adding the contract-to-instrument handoff boundary and post-acceptance identity allocation.

## 19. Blockers requiring a domain decision

Implementation stops only for one of these unresolved conditions:

1. `PortfolioDbConnection` and `EventSourceActorDbConnection` are required to point to different physical PostgreSQL databases.
2. A required Portfolio financial invariant depends on a Scylla, NATS, broker, or other external write being atomic with PostgreSQL.
3. Source inventory finds conflicting immutable financial records that cannot be deterministically reconciled without changing business history.
4. Two components claim authoritative ownership of the same Portfolio balance, capacity, risk decision, or accepted TradeOrder.

No listed domain blocker is currently active. The remaining runtime handoff work has an approved rule:

- `CompositionLeg.InstrumentId` is the canonical contract ID. Resolve it through the active market-data epoch's provider-neutral `IMarketDataApi.TryGetMarketInstrumentId` boundary before calling Portfolio, populate the resulting `TradeLegDefinition.MarketInstrumentId`, and reject a missing, conflicting, or zero translation without creating Portfolio rows.
- Portfolio determines the accepted Funds first. It then allocates Order IDs and reserved Trade IDs for those accepted Fund orders through `IPortfolioBusinessIdAllocator` and commits the resulting identities in the same Portfolio/EventSource transaction. Sequence gaps caused by a rolled-back attempt are allowed; fabricated, zero, or hash-derived business IDs are not.

Ordinary compile failures, test failures, migration defects, provider refactoring, or performance work are implementation work and are not domain blockers.

## 20. Definition of done

The implementation is complete only when:

- `PortfolioDbContext` is PostgreSQL and is the only Portfolio DbContext;
- `PortfolioDbContext` is a single sealed, non-partial implementation;
- every Portfolio-owned production SQL statement and fixed fragment is defined in `PortfolioDbSql.cs`;
- Portfolio persistence has exactly two interfaces in exactly two files: `IPortfolioDbReadContext.cs` and `IPortfolioDbWriteContext.cs`;
- both interfaces resolve to the same concrete `PortfolioDbContext` instance;
- `PortfolioFinancialDbContext` and the Portfolio Scylla context are absent from production registration;
- Portfolio and EventSource atomic tables share one physical PostgreSQL database;
- EventSource owns the special Function enlist method and transaction lifecycle;
- all atomic Portfolio SQL uses the explicit enlisted session and opens no second connection;
- completed Portfolio events and every related table change commit or roll back together;
- every FunctionActor can opt into the same mechanism without containing SQL;
- calculation-only FunctionActors remain behaviorally unchanged;
- Scylla delivery, where retained, uses the existing EventProjector durable queue after commit;
- no background service reruns Portfolio order-composition decisions;
- immutable historical events and financial definitions are preserved;
- migration, rollback, idempotency, concurrency, NATS, UI/query, failure, allocation, and benchmark evidence passes and is recorded;
- `PortfolioOrderCompositionFunctionActor` returns either `ExecuteTradeOrders` with a nonempty list or `NoTradeOrders` with an empty list from one committed Portfolio decision.
