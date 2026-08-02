# IBKR Shared Broker Connection Specification

**Document version:** 1.1  
**Status:** Implementation specification  
**Target runtime:** .NET 10 or later  
**Implementation project:** `Framework.TradeBroker.InteractiveBrokers`  
**Implementation module:** `Framework.TradeBroker.InteractiveBrokers.Connection`  
**Broker API:** Official Interactive Brokers TWS API for C#  
**Host:** Trader Workstation or IB Gateway  
**Primary deployment scope:** One supervised TWS API connection shared by all in-process IBKR feature implementations  
**Companion specifications:** `IbkrOrderExecutionAdapterSpecification.md`, `IbkrBrokerAccountSpecification.md`, `OrderExecutionWorkflowSpecification.md`  
**Last updated:** 2026-08-02

---

## 1. Purpose

This document specifies the shared physical connection component used by every framework implementation that communicates through the Interactive Brokers TWS socket API.

`Framework.TradeBroker.InteractiveBrokers.Connection` shall provide:

- one supervised `EClientSocket` connection to TWS or IB Gateway;
- one official C# API reader topology and one `EWrapper` callback entry point;
- one serialized outbound API-call dispatcher;
- one process-wide request/ticker-ID allocator and route registry;
- one shared order-ID allocator synchronized with `nextValidId`;
- callback routing to independently implemented IBKR provider modules;
- singleton-subscription ownership and leases;
- connection epochs, readiness, liveness, disconnect, and reconnect behavior;
- connection-level error normalization, pacing coordination, observability, security, and tests.

It exists to prevent every IBKR feature from creating its own socket, client ID, `EWrapper`, reader thread, ID namespace, and recovery loop.

---

## 2. Normative Architecture Decision

The trading process shall use one shared TWS API connection for all IBKR socket capabilities associated with the same configured TWS/IB Gateway session.

The following modules are contained by the single concrete provider project and depend on its shared connection module:

```text
Framework.TradeBroker.InteractiveBrokers
        +-- Connection
        +-- OrderExecution
        +-- BrokerAccount
        +-- ContractReference
        +-- MarketDataVerification
        +-- MarginPreview
        +-- Reporting/Flex
```

The `Connection` module owns transport and correlation infrastructure. Provider modules own the meaning, normalization, lifecycle, projections, and broker-neutral mappings of their capabilities.

`Framework.TradeBroker.InteractiveBrokers.Reporting.Flex` does **not** use the `Connection` module. Flex is an HTTPS reporting API with its own authentication token, query IDs, polling, pacing, and failure model.

If Client Portal Web API is ever added, it shall also use a separate HTTP/WebSocket transport and session. It must not be disguised as a consumer of the TWS socket connection.

The wider provider architecture is:

| Provider-neutral API | Concrete external-provider implementation | Responsibility |
|---|---|---|
| `Framework.TradeBroker` | `Framework.TradeBroker.InteractiveBrokers` | Connection, account, orders, contracts, margin preview, broker verification data, and reporting |
| `Framework.MarketData` | `Framework.MarketData.Databento` | Primary real-time/historical market data and option-chain feed |

Domain actors depend on the provider-neutral APIs. Only the composition root selects the concrete providers. `Framework.TradeBroker.InteractiveBrokers` must not depend on `Framework.MarketData.Databento`; broker order requests receive broker-neutral contract/price inputs through the application workflow.

---

## 3. Project and Type Boundaries

### 3.1 `Framework.TradeBroker.InteractiveBrokers.Connection` owns

- Official `IBApi` package version manifest and compatibility validation.
- `EClientSocket`, `EReaderSignal`, `EReader`, and the single `EWrapper` implementation.
- Host, port, client ID, paper/live environment, and process-instance identity.
- Physical connect, handshake, reader startup, disconnect, and reconnect.
- Monotonically increasing session epochs.
- Process-wide request ID, ticker ID, and order ID allocation.
- One outbound API-call queue and writer.
- Callback ingress sequencing and routing.
- Request, order, ticker, singleton-stream, and broadcast callback registrations.
- Shared API pacing budgets and per-feature admission control.
- Connection lease and duplicate-owner protection.
- Connectivity/system error classification.
- Feature-ready and feature-resynchronization notifications.
- Shared connection health, metrics, tracing, and diagnostic capture.

### 3.2 Provider modules own

| Module | Exclusive responsibility |
|---|---|
| `.OrderExecution` | Order translation, submit/modify/cancel semantics, order/execution/commission normalization, durable correlation, order reconciliation |
| `.BrokerAccount` | Account subscriptions, balances, margin, positions, portfolio, P&L, account freshness, account reconciliation |
| `.ContractReference` | Contract queries, option parameters, market rules, ambiguity handling, contract cache |
| `.MarketDataVerification` | Optional broker quote/mark subscriptions, ticker normalization, market-data-line budgeting within its allocation |
| `.MarginPreview` | `WhatIf` preview lifecycle, rate limiting, preview/order isolation, margin-impact normalization |

### 3.3 The `Connection` module must not own

- Execution workflow or policy.
- Account snapshot or trading gate.
- Position reconciliation business decisions.
- Contract-resolution cache or matching rules.
- Market-data order books or strategy prices.
- P&L interpretation.
- Margin-approval policy.
- Flex reports or historical accounting.
- Actor persistence or domain events other than connection lifecycle facts.
- Any method that lets UI, strategy, risk, or domain code issue raw IBKR calls.

### 3.4 `IBApi` boundary

Official `IBApi` types may appear only inside the concrete `Framework.TradeBroker.InteractiveBrokers` provider project. They must not cross into broker-neutral framework contracts, actors, policies, projections, caches shared with non-IBKR implementations, or UI DTOs.

Provider modules may create `IBApi.Contract`, `IBApi.Order`, or other request objects internally, but the shared writer is the only code allowed to invoke the physical `EClientSocket`.

### 3.5 Internal module enforcement

Because the IBKR capabilities share one concrete provider assembly, enforce the boundaries with namespaces, `internal` visibility, dependency-injection registration, and architecture tests:

```text
Framework.TradeBroker.InteractiveBrokers.Connection
Framework.TradeBroker.InteractiveBrokers.BrokerAccount
Framework.TradeBroker.InteractiveBrokers.OrderExecution
Framework.TradeBroker.InteractiveBrokers.ContractReference
Framework.TradeBroker.InteractiveBrokers.MarketDataVerification
Framework.TradeBroker.InteractiveBrokers.MarginPreview
Framework.TradeBroker.InteractiveBrokers.Reporting.Flex
```

Allowed internal dependencies:

- every TWS-backed module may depend on `.Connection`;
- `.OrderExecution` may consume a narrow `.ContractReference` interface, not its cache implementation;
- `.MarginPreview` may share order-ID and callback infrastructure through `.Connection`, not order workflow state;
- `.Reporting.Flex` may depend on shared provider-neutral report contracts but not `.Connection`;
- `.Connection` must not depend on any feature module;
- `.BrokerAccount` and `.OrderExecution` must not depend on each other's implementations;
- no IBKR module may depend on `Framework.MarketData.Databento`.

Architecture tests must fail the build when these dependency rules are violated.

---

## 4. Consumers and Connection Sharing

| Capability | Shares TWS connection | Shared identifiers/resources | Feature-owned state |
|---|---:|---|---|
| Orders and executions | Yes | Order IDs, request IDs, order/error routes, writer | Execution aggregate and reconciliation |
| Account and portfolio | Yes | Request IDs, singleton position lease, account routes | Account snapshot, freshness, gate |
| Contract reference | Yes | Request IDs, pacing, contract callback routes | Contract cache and resolution results |
| IBKR market-data verification | Yes | Ticker IDs, pacing, market-data allocation, tick routes | Quote state and source-selection policy |
| `WhatIf` margin preview | Yes | Order IDs, order-state routes, pacing | Preview lifecycle and normalized impact |
| Flex reports | No | None | HTTPS query/report lifecycle |
| Client Portal Web API | No | None | Separate authenticated HTTP/WebSocket session |

Provider modules must not depend on one another to reach the broker. Each registers independently with the `Connection` module.

---

## 5. Delivery Phases

| Phase | Name | Required outcome |
|---|---|---|
| 1 | Contracts, configuration, and API baseline | Project scaffold, pinned official API, options, version manifest, connection state, feature contracts |
| 2 | Physical connection and pumps | Lease, connect/handshake, reader, single `EWrapper`, outbound writer, session epoch, controlled shutdown |
| 3 | ID allocation and callback routing | Request/ticker/order allocation, route registries, singleton leases, broadcast/system routing, late-callback handling |
| 4 | Reconnect, pacing, and feature recovery | Connection health, system codes, bounded reconnect, new-epoch invalidation, feature resynchronization, admission control |
| 5 | Observability, scripted testing, and paper acceptance | Metrics, traces, redaction, deterministic callback harness, multi-feature coexistence, operational runbook |

All five phases are required for V1. Additional IBKR provider modules may be added incrementally without changing physical connection ownership.

---

## 6. Suggested Project Structure

```text
Framework.TradeBroker.InteractiveBrokers/
  Connection/
    Api/
      IbkrApiVersionManifest.cs
      IbkrApiCompatibilityValidator.cs
    Configuration/
      IbkrConnectionOptions.cs
      IbkrConnectionConfigurationValidator.cs
    Lifecycle/
      IbkrConnectionService.cs
      IbkrConnectionStatus.cs
      IbkrConnectionSnapshot.cs
      IbkrConnectionLease.cs
      IbkrSessionEpoch.cs
    Reader/
      IbkrEWrapperBridge.cs
      IbkrReaderLoop.cs
      IbkrCallbackEnvelope.cs
      IbkrCallbackIngress.cs
      IbkrCallbackRouter.cs
    Writer/
      IbkrClientDispatcher.cs
      IbkrOutboundOperation.cs
      IbkrDispatchReceipt.cs
      IbkrWriterLoop.cs
    Identifiers/
      IbkrRequestIdAllocator.cs
      IbkrOrderIdAllocator.cs
      IbkrOrderIdHighWaterStore.cs
    Routing/
      IbkrRequestRouteRegistry.cs
      IbkrOrderRouteRegistry.cs
      IbkrTickerRouteRegistry.cs
      IbkrSingletonStreamRegistry.cs
      IbkrBroadcastRegistry.cs
    Features/
      IbkrFeatureRegistry.cs
      IbkrFeatureLease.cs
      IbkrFeatureState.cs
      IbkrResynchronizationNotice.cs
    Pacing/
      IbkrPacingCoordinator.cs
      IbkrFeatureQuota.cs
      IbkrPacingDecision.cs
    Errors/
      IbkrConnectionErrorClassifier.cs
      IbkrSystemMessageCatalog.cs
      IbkrErrorRouteResolver.cs
    Diagnostics/
      IbkrConnectionMetrics.cs
      IbkrConnectionHealthCheck.cs
      IbkrDiagnosticCapture.cs

  BrokerAccount/
  OrderExecution/
  ContractReference/
  MarketDataVerification/
  MarginPreview/
  Reporting/Flex/
```

Names may follow existing repository conventions, but ownership boundaries are normative.

---

## 7. Official API Baseline

### 7.1 Source policy

- Use the official Interactive Brokers C# API.
- Pin the API assembly/package version centrally for the `Framework.TradeBroker.InteractiveBrokers` provider project.
- Record file hash, assembly version, minimum supported TWS version, minimum supported IB Gateway version, and verification date.
- Do not load multiple `IBApi` versions in one process.
- Do not place a third-party wrapper between the connection component and official API objects.

### 7.2 Upgrade policy

An API/TWS/IB Gateway upgrade requires:

1. Official changelog and connection-documentation review.
2. Public request/callback signature comparison.
3. Rebuild of all `.InteractiveBrokers` projects.
4. Callback-router coverage comparison against the current `EWrapper` surface.
5. Deterministic scripted callback replay.
6. Paper connect, disconnect, reconnect, account, order, position, contract, and market-data tests.
7. New compatibility manifest and explicit release approval.

### 7.3 Handshake sequencing

The reader must not interpret messages before the initial API/TWS version negotiation is complete. Implement the connection and `EReader` startup sequence exactly as required by the pinned official C# API.

---

## 8. Configuration

```csharp
public enum IbkrEnvironment : byte
{
    Paper = 1,
    Live = 2
}

public enum IbkrHostApplication : byte
{
    TraderWorkstation = 1,
    IbGateway = 2
}

public sealed record IbkrConnectionOptions(
    string Host,
    int Port,
    int ClientId,
    IbkrEnvironment Environment,
    IbkrHostApplication HostApplication,
    string InstanceIdentity,
    TimeSpan ConnectTimeout,
    TimeSpan HandshakeTimeout,
    TimeSpan HeartbeatInterval,
    TimeSpan HeartbeatTimeout,
    TimeSpan ReconnectMinimumDelay,
    TimeSpan ReconnectMaximumDelay,
    int OutboundCapacity,
    int CallbackIngressCapacity,
    string ApiCompatibilityProfileVersion);
```

Validation rules:

- Host is explicit and allow-listed. Prefer loopback when TWS/IB Gateway runs on the same machine.
- Port is explicit; never infer paper/live safety solely from conventional port numbers.
- Client ID is explicit and stable for the deployed process.
- Environment is explicit and verified by deployment/account configuration outside this component.
- `InstanceIdentity` is stable, unique, and safe for logs.
- All timeouts and capacities are positive and bounded.
- Reconnect maximum is not shorter than reconnect minimum.
- Paper and live instances use separate ports/configuration profiles, state, metrics, and operational authorization.
- Credentials and two-factor codes are not accepted by this options object.
- A live configuration is rejected unless a separate deployment authorization enables the process; this connection flag alone never authorizes orders.

Account IDs do not belong in physical connection options. Account selection and verification belong to `.BrokerAccount`; provider modules separately configure the account they operate on and verify agreement with the broker-account read model.

---

## 9. Connection Lease and Single Ownership

One process may own a configured `(environment, host, port, clientId)` tuple.

```csharp
public readonly record struct IbkrConnectionIdentity(
    IbkrEnvironment Environment,
    string HostAlias,
    int Port,
    int ClientId,
    string InstanceIdentity);

public interface IIbkrConnectionLease : IAsyncDisposable
{
    ValueTask<IbkrConnectionLeaseResult> TryAcquireAsync(
        IbkrConnectionIdentity identity,
        CancellationToken cancellationToken);
}
```

Rules:

- Acquire the lease before creating/connecting the socket.
- Refuse startup when another healthy owner holds it.
- Never choose a random client ID after a collision.
- Release only after feature dispatch stops, the writer drains/stops, the reader stops, and the socket disconnects.
- Clear a stale lease only after validating the owning process is gone and broker recovery will run.
- The connection lease is not a distributed leader-election mechanism for two simultaneous live trading processes.

Multiple official API connections may be technically possible, but that is not a reason to create one per framework feature. Additional physical connections require an explicit deployment reason, separate client ID, separate owner, and reconciliation design.

---

## 10. Connection Lifecycle

### 10.1 State

```csharp
public enum IbkrConnectionStatus : byte
{
    Stopped = 0,
    AcquiringLease = 1,
    ConnectingSocket = 2,
    NegotiatingProtocol = 3,
    StartingReader = 4,
    AwaitingSessionEvidence = 5,
    Ready = 6,
    ConnectivityLost = 7,
    Reconnecting = 8,
    Faulted = 9,
    Stopping = 10
}
```

### 10.2 Connection snapshot

```csharp
public sealed record IbkrConnectionSnapshot(
    IbkrConnectionStatus Status,
    IbkrSessionEpoch? SessionEpoch,
    bool SocketConnected,
    bool ReaderHealthy,
    bool WriterHealthy,
    bool CallbackRouterHealthy,
    bool NextValidOrderIdReceived,
    bool ManagedAccountsEvidenceReceived,
    DateTimeOffset? LastConnectionEvidenceAtUtc,
    string ApiVersion,
    string HostVersion,
    string? FaultCode);
```

`Ready` means the transport can accept eligible feature requests for the current epoch. It does not mean:

- the broker account is synchronized;
- order execution is authorized;
- an instrument is resolved;
- market data is available;
- a strategy may trade.

Each feature computes its own readiness from connection readiness plus feature-specific synchronization and safety gates.

### 10.3 Startup

1. Validate configuration and compatibility manifest.
2. Acquire the physical connection lease.
3. Create `EReaderSignal`, `IbkrEWrapperBridge`, and `EClientSocket` in the sequence required by the pinned API.
4. Connect using configured host, port, and client ID.
5. Complete protocol negotiation/handshake.
6. Create/start `EReader` only when allowed by the negotiated connection state.
7. Start the reader-processing loop and shared writer loop.
8. Receive required initial evidence such as connection time/session information and `nextValidId`.
9. Create a new session epoch.
10. Mark the transport `Ready`.
11. Notify registered features to perform current-epoch synchronization.

If startup fails after any resource is created, stop in reverse ownership order and retain a diagnostic failure record.

### 10.4 Shutdown

1. Reject new feature registrations and outbound operations.
2. Publish `Stopping` to feature implementations.
3. Let features cancel subscriptions within a bounded drain interval.
4. Stop accepting outbound operations.
5. Drain only explicitly safe queued operations; never transmit a stale order mutation merely to empty the queue.
6. Stop the writer.
7. Disconnect the official client.
8. Wake and stop the reader loop.
9. Complete callback channels and registrations.
10. Release the physical connection lease.
11. Publish `Stopped`.

---

## 11. Session Epoch

```csharp
public readonly record struct IbkrSessionEpoch(
    Guid Value,
    long Sequence,
    DateTimeOffset ConnectedAtUtc,
    int ClientId,
    IbkrEnvironment Environment);
```

Rules:

- Create a new epoch for every successful physical reconnection.
- Sequence increases monotonically for the process instance.
- Attach the epoch to outbound operations, callback envelopes, route registrations, IDs, dispatch receipts, and feature notifications.
- Registrations from an old epoch are retired when connection loss makes their validity ambiguous.
- Old-epoch callbacks may be logged/quarantined but cannot complete current-epoch requests.
- A feature may retain old-epoch data only as stale historical evidence.
- The connection never maps an old callback into a new request merely because the integer ID matches.

---

## 12. Reader and Callback Ingress

### 12.1 Topology

The pinned official C# API normally requires `EClientSocket`, `EReaderSignal`, `EReader`, and an `EWrapper` implementer.

Required topology:

1. The physical connection owns all official connection objects.
2. The long-running reader-processing loop waits for the signal and invokes official message processing.
3. `IbkrEWrapperBridge` is the single `EWrapper` implementation.
4. Each callback captures required primitive fields and immutable copies of mutable `IBApi` objects.
5. It assigns an ingress sequence, session epoch, receive UTC time, and monotonic time.
6. It enqueues an `IbkrCallbackEnvelope` to bounded ingress.
7. The router resolves the feature route and delivers to the registered feature channel.

### 12.2 Callback envelope

```csharp
public sealed record IbkrCallbackEnvelope(
    IbkrCallbackKind Kind,
    long ReceiveSequence,
    IbkrSessionEpoch SessionEpoch,
    DateTimeOffset ReceivedAtUtc,
    long ReceivedAtMonotonicTicks,
    int? RequestOrTickerId,
    int? OrderId,
    string SourceCallback,
    object Payload);
```

`Payload` is an internal immutable callback DTO or discriminated union, never a mutable official object retained after callback return. Generated production code should prefer a closed typed hierarchy over arbitrary `object`.

### 12.3 Reader-thread restrictions

The reader/callback thread must not:

- call actor business logic directly;
- perform database, Redis, HTTP, file, or UI I/O;
- wait on feature code;
- calculate strategy, risk, execution, or reconciliation decisions;
- call another `EClientSocket` operation;
- perform unbounded serialization/logging;
- silently drop order, execution, position, account-completion, error, or connection callbacks.

### 12.4 Backpressure

- Callback ingress is bounded and instrumented.
- Critical callbacks are lossless at the process level.
- Replaceable market-data ticks may use a feature-specific coalescing channel after routing, never in the common ingress in a way that risks other callback types.
- When common callback retention cannot be guaranteed, mark the connection unhealthy, close all feature safety gates, retain an incident marker, and require resynchronization.
- Feature-channel overflow is reported to that feature and the connection health projection; critical feature overflow may fault the shared connection because broker state can no longer be proven.

---

## 13. Outbound Client Dispatcher

### 13.1 Single writer

Only `IbkrClientDispatcher` may invoke `EClientSocket` request methods.

```csharp
public interface IIbkrClientDispatcher
{
    IbkrDispatchReceipt TryDispatch(IbkrOutboundOperation operation);
}

public sealed record IbkrOutboundOperation(
    Guid OperationId,
    string FeatureName,
    IbkrSessionEpoch ExpectedEpoch,
    IbkrOperationKind Kind,
    int? CorrelationId,
    DateTimeOffset CreatedAtUtc,
    long CreatedAtMonotonicTicks,
    TimeSpan MaxQueueAge,
    Action<EClientSocket> Invoke);

public sealed record IbkrDispatchReceipt(
    Guid OperationId,
    IbkrSessionEpoch SessionEpoch,
    IbkrDispatchStatus Status,
    long AcceptedSequence,
    DateTimeOffset AcceptedAtUtc,
    string? FailureCode);
```

The delegate/type carrying `EClientSocket` remains internal to `.InteractiveBrokers` infrastructure. A source-generated or closed command hierarchy is preferred if it improves validation and replay.

### 13.2 Semantics

- `Accepted` means locally validated and queued, not accepted by IBKR.
- Assign a monotonically increasing accepted sequence.
- Preserve FIFO order by accepted sequence in V1.
- Check feature registration, expected epoch, connection status, route registration, queue age, and operation-specific prerequisites immediately before invocation.
- An operation whose epoch changed or queue age expired is rejected locally and never sent.
- Do not automatically retry submit, modify, cancel, exercise, or `WhatIf` operations after an ambiguous invocation exception.
- Ambiguous mutations are reported to the owning feature for reconciliation.
- Subscription/query retries are owned by the feature lifecycle, not the generic writer.

### 13.3 Admission control

- Bounded total queue.
- Per-feature configured queue quota.
- No feature may consume all capacity.
- Reject optional market-data or bulk-reference work before risking order/account recovery capacity.
- V1 uses deterministic quotas and FIFO; any future priority policy must be explicit, versioned, starvation-tested, and never reorder two mutations for the same logical order.
- Global cancellation is not an ordinary shared operation and is unavailable unless an explicitly authorized emergency component is added.

---

## 14. Identifier Allocation

### 14.1 Request and ticker IDs

Use one collision-free process-wide integer allocator for request IDs and ticker IDs, with purpose metadata.

```csharp
public enum IbkrIdPurpose : byte
{
    AccountSummary,
    AccountPnl,
    PositionPnl,
    ContractDetails,
    OptionParameters,
    MarketRule,
    MarketData,
    ExecutionQuery,
    CompletedOrderQuery,
    MarginPreview,
    Other
}

public interface IIbkrRequestIdAllocator
{
    IbkrAllocatedRequestId Allocate(
        IbkrIdPurpose purpose,
        string featureName,
        IbkrSessionEpoch epoch);

    bool TryRetire(
        int id,
        IbkrIdPurpose purpose,
        string featureName,
        IbkrSessionEpoch epoch);
}
```

Requirements:

- Never allocate an active integer twice.
- Associate purpose, feature, epoch, start time, and lifecycle.
- Do not reuse until completion/cancellation drain policy permits it.
- Retire all old-epoch IDs on reconnect, while retaining tombstones long enough to classify late callbacks.
- Integer wrap/exhaustion faults allocation safely; never reset to an unsafe low number in-process.

### 14.2 Order IDs

Order IDs are connection/client-ID scoped and strictly increasing for new orders.

```csharp
public interface IIbkrOrderIdAllocator
{
    ValueTask SynchronizeAsync(
        int brokerNextValidId,
        long durableHighWaterMark,
        IbkrSessionEpoch epoch,
        CancellationToken cancellationToken);

    ValueTask<IbkrOrderIdReservation> ReserveAsync(
        string featureName,
        Guid logicalOperationId,
        IbkrSessionEpoch epoch,
        CancellationToken cancellationToken);
}
```

Rules:

- The usable next ID is greater than or equal to broker `nextValidId` and greater than every durable/reserved ID that must remain protected.
- Persist the reservation before submitting a new order.
- Never reuse an ID for a different logical order.
- Modify an existing order with its existing order ID; do not allocate a new ID merely for price modification.
- `.OrderExecution` and `.MarginPreview` share the allocator and register distinct ownership.
- A margin-preview order ID can never be reclassified as a transmitted execution order.
- Client ID 0 behavior and manual-order binding are excluded from the normal V1 connection profile.

---

## 15. Callback Routing

### 15.1 Route classes

| Route | Key | Examples | Owner behavior |
|---|---|---|---|
| Request route | `(epoch, requestId)` | Contract details, P&L, execution query | One registered feature/request lifecycle |
| Ticker route | `(epoch, tickerId)` | Market-data ticks | Market-data feature subscription |
| Order route | `(clientId, orderId)` plus epoch context | Order status, open order, errors | Order execution or margin preview |
| Singleton stream | `(epoch, streamKind)` | Positions, managed accounts, account updates | One owner or shared lease/fan-out |
| Broadcast | `(epoch, callbackKind)` | Connection closed, system messages, next valid ID | All eligible registered features |
| Unroutable | Diagnostic | Unknown/late IDs | Quarantine, metric, feature recovery if critical |

### 15.2 Feature registration

```csharp
public interface IIbkrFeatureRegistry
{
    ValueTask<IIbkrFeatureLease> RegisterAsync(
        IbkrFeatureRegistration registration,
        CancellationToken cancellationToken);
}

public sealed record IbkrFeatureRegistration(
    string FeatureName,
    IReadOnlySet<IbkrCallbackKind> BroadcastInterests,
    int CallbackCapacity,
    int OutboundQuota,
    bool IsRequiredForProcessReadiness);

public interface IIbkrFeatureLease : IAsyncDisposable
{
    string FeatureName { get; }
    ChannelReader<IbkrCallbackEnvelope> Callbacks { get; }
}
```

Rules:

- Feature names are unique and stable.
- Duplicate registration fails; it does not replace an active owner.
- Route creation requires an active feature lease and current epoch.
- Releasing a feature lease retires its routes and subscriptions safely.
- The connection does not call feature business methods from the reader thread.

### 15.3 Request route lifecycle

1. Allocate ID.
2. Register route before dispatching request.
3. Dispatch with expected epoch.
4. Route zero or more callbacks.
5. Observe documented completion, cancellation acknowledgement/drain policy, error, or timeout.
6. Feature retires route and ID.
7. Retain a late-callback tombstone for diagnostics.

If dispatch fails locally, retire the route according to the no-callback policy. If dispatch may have reached the socket, keep correlation until the feature reconciles or times out safely.

### 15.4 Order route lifecycle

- Register a durable order owner before `placeOrder` dispatch.
- Route order-status, open-order, execution association, commission, and relevant error callbacks to the order feature.
- Route `WhatIf` order state only to the margin-preview owner.
- Permanent IDs and execution IDs are secondary evidence maintained by feature modules.
- Do not retire an order route merely because a cancel command was queued or `PendingCancel` appeared.
- Retain correlation through terminal evidence and late-fill/reconciliation window.

### 15.5 Error routing

For `EWrapper.error`-style callbacks:

1. Classify connection/system messages first.
2. If the identifier matches a live order route, deliver to its owner.
3. Else if it matches a request/ticker route, deliver to that owner.
4. Else if it is a broadcast/system condition, deliver to subscribed features.
5. Else quarantine as unroutable and alert when severity requires it.

The connection categorizes connectivity and routing. The feature interprets business meaning such as order rejection, contract failure, account subscription failure, or market-data permission failure.

---

## 16. Singleton Streams and Shared Leases

Some TWS API streams are not naturally request-ID scoped or have global/single-account restrictions.

```csharp
public enum IbkrSingletonStreamKind : byte
{
    ManagedAccounts,
    Positions,
    AccountUpdates,
    OpenOrdersForClient,
    CompletedOrders,
    NextValidOrderId
}

public interface IIbkrSingletonStreamLeaseManager
{
    ValueTask<IIbkrSingletonStreamLease> AcquireAsync(
        IbkrSingletonStreamKind stream,
        string featureName,
        IbkrSessionEpoch epoch,
        CancellationToken cancellationToken);
}
```

### 16.1 Positions

- `.BrokerAccount` is the primary owner of the live positions subscription.
- `.OrderExecution` may consume the normalized/fan-out position evidence needed for reconciliation without starting a second subscription.
- Reference-counted consumers do not each call `reqPositions` or `cancelPositions`.
- Only the lease manager starts/cancels the physical singleton subscription.

### 16.2 Account updates

- Exclusive ownership belongs to `.BrokerAccount` for the configured account.
- A second account-updates owner cannot replace it silently.
- An attempt to subscribe another account is rejected and reported as a configuration/scope conflict.

### 16.3 Managed accounts and next valid ID

- Connection/session evidence is captured centrally.
- Managed account data is routed to broker-account verification and may be observed by order readiness.
- `nextValidId` synchronizes the shared order allocator and is broadcast as connection evidence; feature modules do not maintain competing counters.

### 16.4 Open/completed orders

- Order execution owns order-query collection.
- Margin preview receives only its registered preview orders.
- Account and contract modules cannot acquire order-stream ownership.

---

## 17. Pacing and Resource Coordination

### 17.1 Purpose

A shared connection means all feature requests contribute to broker/API limits and local queue pressure. The connection therefore coordinates admission while feature modules retain semantic retry behavior.

### 17.2 Resource classes

- total outbound queue capacity;
- request-ID capacity;
- active account-summary subscriptions;
- singleton stream ownership;
- contract/reference request rate;
- market-data-line/ticker allocation;
- historical-data requests if later enabled;
- `WhatIf` preview rate;
- reconnect/resubscription burst budget.

### 17.3 Rules

- Each feature declares a deterministic quota profile.
- Critical order recovery and account synchronization capacity is reserved.
- Optional IBKR market-data verification cannot starve execution/account/contract recovery.
- The pacing coordinator rejects or defers before dispatch; it never sleeps on the reader thread.
- The rejected/deferred result includes category, current usage, retry-not-before time when known, and policy version.
- A feature owns bounded retries and cancellation.
- Pacing profiles are versioned, observable, and tested against reconnect bursts.

The connection does not pretend all official pacing rules are one universal requests-per-second number. Capabilities have different constraints and must use documented, measured profiles.

---

## 18. Connectivity, Errors, and Reconnect

### 18.1 Connection error categories

```csharp
public enum IbkrConnectionErrorCategory : byte
{
    Socket,
    Protocol,
    Authentication,
    ClientIdConflict,
    BrokerConnectivityLost,
    BrokerConnectivityRestoredDataLost,
    BrokerConnectivityRestoredDataMaintained,
    SocketPortReset,
    ReaderFailure,
    WriterFailure,
    CallbackOverflow,
    Pacing,
    Unknown
}
```

The numeric-code mapping is versioned and based on current official documentation plus paper validation.

### 18.2 Required system behavior

| Condition | Shared connection behavior | Feature behavior |
|---|---|---|
| Socket disconnected | Leave `Ready`; stop mutation dispatch; reconnect | Close safety/readiness gates and retain stale state |
| IB/TWS connectivity lost | Publish normalized loss event | Freeze actions requiring broker certainty |
| Connectivity restored, data lost | Require affected subscriptions to be recreated | Resubscribe and fully reconcile affected streams |
| Connectivity restored, data maintained | Publish restored evidence | Still validate feature state before reopening gates |
| Socket port reset | Reject embedded/unvalidated port change; follow configuration/runbook | Remain not ready |
| Client-ID conflict | Fault startup | No feature auto-selects another ID |
| Reader/writer failure | Fault connection and stop unsafe dispatch | Close gates; reconcile after recovery |
| Common callback overflow | Fault/force full recovery | No feature may claim current state |

### 18.3 Reconnect sequence

1. Atomically leave `Ready`.
2. Reject new outbound mutations and requests that cannot be proven safe.
3. Notify all feature leases of connection loss.
4. Stop/replace physical connection objects according to official API requirements.
5. Retry using bounded exponential backoff with deterministic jitter disabled in replay or supplied by an injected strategy.
6. Complete a new handshake and create a new session epoch.
7. Synchronize the order-ID allocator with new `nextValidId` and durable high-water state.
8. Retire old routes and create new current-epoch registries.
9. Publish `Ready` for transport queries.
10. Send `IbkrFeatureResynchronizationRequired` to every registered feature in stable registration order.
11. Features recreate subscriptions/queries and reconcile independently.

The connection never reports feature readiness on their behalf.

### 18.4 Existing orders during disconnect

Disconnect does not prove that broker-native orders were cancelled. The connection preserves correlation and requires the order feature to query/reconcile after recovery. It must not issue speculative duplicate submissions.

---

## 19. Public Internal Interfaces

These interfaces are infrastructure-facing, not domain/public-service APIs.

```csharp
public interface IIbkrConnection
{
    IbkrConnectionSnapshot GetSnapshot();
    IIbkrClientDispatcher Dispatcher { get; }
    IIbkrRequestIdAllocator RequestIds { get; }
    IIbkrOrderIdAllocator OrderIds { get; }
    IIbkrFeatureRegistry Features { get; }
    IIbkrSingletonStreamLeaseManager SingletonStreams { get; }
    IIbkrPacingCoordinator Pacing { get; }
}
```

Rules:

- `GetSnapshot` is local and immutable.
- No interface exposes raw socket connect/disconnect to feature modules.
- Only the connection supervisor controls lifecycle.
- No domain actor can resolve `IIbkrClientDispatcher` from dependency injection.
- Registration and dispatch operations identify feature owner and expected epoch.
- Disposal is explicit and bounded.

---

## 20. Dependency Injection and Hosting

Register one physical connection singleton per configured IBKR session:

```csharp
services.AddSingleton<IIbkrConnection, IbkrConnectionService>();
services.AddHostedService(sp =>
    (IbkrConnectionService)sp.GetRequiredService<IIbkrConnection>());

services.AddSingleton<IBrokerOrderGateway, IbkrBrokerOrderGateway>();
services.AddSingleton<IBrokerAccountGateway, IbkrBrokerAccountGateway>();
services.AddSingleton<IBrokerContractReferenceGateway, IbkrContractReferenceGateway>();
```

The exact registration API may match existing framework conventions. Required behavior:

- one shared connection instance;
- feature adapters receive it by interface;
- feature registration occurs at host startup before connection readiness is published;
- host shutdown stops feature dispatch before physical connection disposal;
- tests can replace `IIbkrConnection` with the scripted broker connection without loading `IBApi`.

Do not use a service locator inside feature callbacks.

---

## 21. Threading Model

- One dedicated/long-running official reader-processing loop according to the C# API model.
- One shared single-writer loop for outbound calls.
- `EWrapper` callbacks do bounded copy/enqueue work only.
- Callback router performs bounded route lookup and channel write.
- Each feature consumes its own channel and transfers normalized messages into its actor/mailbox model.
- Market-data verification uses a separate feature channel/ring-buffer design so high tick volume cannot delay order/account callbacks.
- Feature registration, route registries, and ID allocation are concurrency-safe without coarse locks on the reader hot path.
- Database, Redis, NATS, and UI work never run on connection threads.

The order and broker-account actors retain sequential business processing; sharing the physical connection does not merge their actor state or threading.

---

## 22. Persistence

Persist only connection facts required for safe recovery:

- API compatibility manifest/version;
- physical connection instance identity;
- session epoch history and lifecycle transitions;
- durable order-ID high-water mark/reservations through the order allocator store;
- critical connection incidents and callback-loss gaps;
- optional redacted raw callback diagnostic captures with bounded retention.

Do not persist account values, positions, orders, contract cache, or market data in the `Connection` module. Their provider-module owners persist them.

On process restart, a prior `Ready` state is historical only. The connection starts `Stopped`, performs a new handshake/epoch, and every feature resynchronizes.

---

## 23. Security

- Host and port come from approved configuration.
- Prefer loopback or a secured private network; never expose the TWS socket port publicly.
- Do not accept broker username/password/2FA through this component.
- Redact host details when sensitive, client IDs when operational policy requires it, and all account IDs in shared logs.
- Paper and live connection identities, ports, secrets, metrics, and runbooks are separate.
- Feature names and correlation IDs are logged; raw order/account values are not logged by the connection.
- Callback diagnostic capture is disabled by default in live or encrypted/restricted with bounded retention.
- Manual connect/disconnect/reconnect operations require authenticated operational access and audit.
- A connection-ready state cannot override account, risk, or order-execution gates.

---

## 24. Observability

### 24.1 Metrics

```text
ibkr_connection_status{environment,instance}
ibkr_connection_session_epoch{environment,instance}
ibkr_connection_attempts_total{environment,result}
ibkr_connection_reconnects_total{environment,reason}
ibkr_connection_evidence_age_seconds{environment,instance}
ibkr_reader_alive{environment,instance}
ibkr_writer_alive{environment,instance}
ibkr_callback_ingress_depth{environment,instance}
ibkr_callback_ingress_high_water{environment,instance}
ibkr_callbacks_total{environment,callback_group}
ibkr_callbacks_unroutable_total{environment,callback_group,reason}
ibkr_callback_route_latency_seconds{environment,feature}
ibkr_outbound_depth{environment,instance}
ibkr_outbound_dispatch_total{environment,feature,kind,result}
ibkr_outbound_queue_age_seconds{environment,feature}
ibkr_active_request_routes{environment,feature,purpose}
ibkr_active_order_routes{environment,feature}
ibkr_active_singleton_leases{environment,stream}
ibkr_pacing_rejections_total{environment,feature,resource}
```

Avoid account IDs, symbols, `conId`, request IDs, order IDs, or raw messages as high-cardinality labels.

### 24.2 Logs and traces

Include:

- environment and instance alias;
- connection status and epoch sequence;
- feature name;
- operation ID and accepted sequence;
- request/order/ticker ID only in restricted structured diagnostics;
- callback kind and route type;
- normalized connection error category/code;
- queue depths/ages;
- reconnect attempt and reason;
- API/TWS/IB Gateway compatibility versions.

Do not log credentials, account values, positions, full orders, or access tokens at the connection layer.

### 24.3 Alerts

- live connection not ready during active trading;
- repeated reconnect/fault loop;
- client-ID conflict;
- reader/writer death;
- callback ingress overflow;
- unroutable critical order/position/account callback;
- order-ID allocator synchronization failure;
- route leak or request-ID exhaustion;
- pacing/resource starvation affecting required features;
- paper/live configuration mismatch.

---

## 25. Determinism and Replay

Connection replay must reproduce routing and feature-visible evidence from recorded callback envelopes.

Requirements:

- Inject UTC and monotonic clocks.
- Record receive sequence, epoch, callback kind, identifiers, and immutable payload.
- In replay, replace physical reader/writer with deterministic scripted components.
- Stable registrations and input sequences produce the same routes, dispatch decisions, errors, and feature messages.
- Reconnect and timer firings are explicit scripted inputs.
- ID allocators accept seeded baselines.
- Queue capacity/pacing outcomes are deterministic from configuration and input order.
- No random reconnect jitter is used unless the random source/seed is injected and recorded.

This replay proves local connection behavior; it does not simulate IBKR exchange/broker semantics. Those belong to the scripted broker harness.

---

## 26. Testing Strategy

### 26.1 Unit tests

- configuration and paper/live validation;
- API compatibility manifest validation;
- connection state transitions;
- epoch increment and old-epoch rejection;
- request/ticker allocation and retirement;
- order allocator synchronization/high-water rules;
- request, order, ticker, singleton, broadcast, and unroutable routing;
- error route precedence;
- feature registration/duplicate registration/disposal;
- singleton lease reference counting and exclusivity;
- writer FIFO, expiry, epoch check, and capacity;
- pacing quotas and reserved capacity;
- redaction and metric-cardinality rules;
- graceful shutdown order.

### 26.2 Property tests

- An integer ID is never active for two owners in one epoch.
- An order ID is never reserved for two logical operations.
- No old-epoch callback reaches a current-epoch request route.
- A feature cannot dispatch without a current registration.
- No socket operation runs outside the shared writer.
- Connection `Ready` never implies any feature `ReadyForOrders`.
- Releasing one consumer does not cancel a singleton stream while another lease remains.
- Exclusive account-updates ownership cannot be replaced silently.
- Optional feature load cannot exceed reserved required-feature capacity.
- Callback sequence and routing remain stable under concurrency stress.

### 26.3 Scripted multi-feature scenarios

1. Order, account, and contract features register before connection.
2. Normal handshake and `nextValidId` synchronization.
3. Interleaved order/account/contract callbacks route correctly.
4. Request ID collision attempt is rejected.
5. Order ID collision attempt is rejected.
6. Positions subscription is shared by account and order reconciliation.
7. Second account-updates owner is rejected.
8. Contract-request burst respects quota without delaying order cancel.
9. Market-data tick burst remains isolated from order callbacks.
10. Unknown request callback is quarantined.
11. Late old-epoch callback after reconnect is quarantined.
12. Connection code 1100 freezes features.
13. Restored-with-data-lost event triggers resubscription notices.
14. Restored-with-data-maintained still requires feature validation.
15. Socket-port-reset event does not accept an unvalidated port.
16. Writer throws before/after ambiguous mutation invocation.
17. Callback ingress fills and forces fail-closed recovery.
18. Feature channel fills and is isolated/classified correctly.
19. Connection shuts down with active subscriptions.
20. Restart with durable order-ID high-water above broker baseline.
21. `WhatIf` and real order routes remain distinct.
22. Flex reporting operates without connection registration.

### 26.4 Paper integration

- Connect using the pinned official C# API and intended TWS/IB Gateway version.
- Verify handshake/reader startup and `nextValidId` delivery.
- Run account, position, contract, order-query, and limited market-data requests over one connection.
- Confirm request/order/ticker callbacks route to the correct feature.
- Exercise disconnect/reconnect and nightly-reset-style recovery.
- Verify order and account gates stay closed until feature resynchronization.
- Verify only one physical socket/client ID exists for the process.
- Capture redacted callback fixtures.
- Run an extended paper soak with all V1 features registered.

---

## 27. Phase-by-Phase Codex Implementation Plan

### Phase 1 — Contracts, configuration, API baseline

Codex shall:

1. Create the `Framework.TradeBroker.InteractiveBrokers` concrete provider project and its `Connection` module.
2. Add the centrally pinned official `IBApi` reference.
3. Implement options, validation, compatibility manifest, enums, snapshots, epochs, and internal interfaces.
4. Add feature registration and ownership documentation.
5. Add compilation/analyzer tests proving broker-neutral projects do not reference `IBApi`.

Exit criteria:

- project compiles on .NET 10;
- configuration rejects unsafe profiles;
- official API version is pinned and recorded;
- feature and connection ownership is unambiguous.

### Phase 2 — Physical connection and pumps

Codex shall:

1. Implement lease, connect/handshake, official reader topology, one `EWrapper`, one writer, and controlled shutdown.
2. Implement connection state and session epoch.
3. Implement bounded callback ingress and outbound queue.
4. Add liveness and thread-ownership instrumentation.
5. Add scripted lifecycle tests without a live broker.

Exit criteria:

- exactly one socket, reader topology, wrapper, and writer exist;
- no feature invokes `EClientSocket` directly;
- callback ingress is bounded/loss-detecting;
- shutdown releases resources and lease safely.

### Phase 3 — IDs and routing

Codex shall:

1. Implement request/ticker and durable order-ID allocators.
2. Implement feature, request, ticker, order, singleton, and broadcast registries.
3. Implement error-route precedence and unroutable quarantine.
4. Implement singleton stream leases and fan-out.
5. Integrate order, broker-account, and contract-reference adapters.

Exit criteria:

- no collision or old-epoch property test fails;
- positions can be shared without duplicate physical subscription;
- account updates remain exclusive;
- all callback families reach only intended consumers.

### Phase 4 — Reconnect and pacing

Codex shall:

1. Implement system-code classification and connection-health state.
2. Implement bounded reconnect/new epoch.
3. Implement feature resynchronization notifications.
4. Implement quotas, pacing decisions, and required-feature reserved capacity.
5. Add disconnect, restored-data-lost, restored-data-maintained, port-reset, burst, and starvation tests.

Exit criteria:

- features fail closed on connection ambiguity;
- full resynchronization follows new epoch;
- optional modules cannot starve V1 safety operations;
- no automatic duplicate broker mutation occurs.

### Phase 5 — Operational acceptance

Codex shall:

1. Implement metrics, traces, logs, alerts, diagnostic capture, and redaction.
2. Extend the scripted broker harness for multi-feature routing.
3. Run paper coexistence, reconnect, restart, and soak tests.
4. Write connect/reconnect/client-ID/callback-loss/pacing runbooks.
5. Produce a compatibility and acceptance report.

Exit criteria:

- multi-feature paper soak passes;
- operational alerts are demonstrated;
- no sensitive data leak or critical callback loss remains;
- V1 feature specifications consume this shared component without ownership duplication.

---

## 28. V1 Acceptance Checklist

### Ownership

- [ ] Concrete provider project is named `Framework.TradeBroker.InteractiveBrokers`.
- [ ] `.OrderExecution` uses `.Connection`.
- [ ] `.BrokerAccount` uses `.Connection`.
- [ ] `.ContractReference` uses `.Connection`.
- [ ] No feature owns another `EClientSocket`, `EReader`, or `EWrapper`.
- [ ] Flex reporting has a separate HTTPS transport.

### Lifecycle

- [ ] One connection lease protects the configured tuple.
- [ ] Handshake and reader startup follow pinned official API requirements.
- [ ] Every physical reconnect creates a new epoch.
- [ ] Transport readiness never authorizes an order.
- [ ] Shutdown ordering is bounded and tested.

### Routing and IDs

- [ ] One request/ticker-ID allocator serves all features.
- [ ] One durable order-ID allocator serves execution and margin preview.
- [ ] Request, ticker, order, singleton, and broadcast routes are tested.
- [ ] Old/unknown callbacks are quarantined and observable.
- [ ] Positions use a shared subscription lease.
- [ ] Account-updates ownership is exclusive.

### Safety and recovery

- [ ] One writer invokes every `EClientSocket` request.
- [ ] Ambiguous mutations are not retried automatically.
- [ ] Callback loss closes feature gates and forces recovery.
- [ ] Reconnect notifies all features to resynchronize.
- [ ] Optional features cannot starve required recovery capacity.
- [ ] Paper/live environments and authorization are isolated.

### Quality

- [ ] Unit, property, scripted, paper, and soak tests pass.
- [ ] Metrics, logs, traces, alerts, and redaction pass review.
- [ ] Official API/TWS/IB Gateway versions are pinned.
- [ ] Runbooks and compatibility report are complete.

---

## 29. Instructions to Codex

1. Implement this project before generating production feature adapters.
2. Inspect and extract any connection code already generated inside the provider's `OrderExecution` module; preserve feature logic while moving physical ownership into `Connection`.
3. Do not copy socket/session code into `BrokerAccount` or later provider modules.
4. Use the official C# API signatures from the pinned package.
5. Keep `IBApi` types inside `.InteractiveBrokers` projects.
6. Use one `EWrapper` bridge and explicit callback routing; do not use reflection-based event guessing in the hot path.
7. Make all capacities, timeouts, quotas, pacing profiles, and compatibility versions explicit configuration.
8. Implement connection readiness separately from account readiness and order readiness.
9. Register callback routes before dispatching their requests.
10. Persist order-ID reservations before submission.
11. Reject stale-epoch operations immediately before the socket invocation.
12. Generate unit/property/scripted tests with each phase.
13. Treat nullable warnings, analyzer violations, callback coverage gaps, and failing tests as implementation failures.
14. Never include live account IDs, credentials, tokens, orders, positions, or balances in fixtures.
15. Produce a phase report containing files changed, tests run, official versions, assumptions, ownership changes, and remaining risks.

---

## 30. Definition of Done

The shared connection is complete when:

- all in-process TWS API feature implementations use one supervised physical connection;
- socket, reader, wrapper, writer, IDs, epochs, callback routing, pacing, and reconnect are owned in one project;
- order, account, contract, market-data, and margin-preview modules remain independently testable and broker-neutral outside their adapters;
- callback streams are routed deterministically without request/order/ticker collisions;
- singleton streams cannot be duplicated or replaced accidentally;
- connection failure immediately invalidates feature readiness without inventing broker state;
- reconnect creates a new epoch and triggers complete feature resynchronization;
- optional V1.x modules cannot impair V1 order/account safety;
- Flex reporting remains correctly separate;
- scripted and paper multi-feature acceptance tests pass.

---

## 31. Official Reference Baseline

- [TWS API introduction](https://www.interactivebrokers.com/docs/tws-api/doc/introduction)
- [Establishing an API connection](https://www.interactivebrokers.com/docs/tws-api/doc/connectivity/establishing-an-api-connection)
- [Verifying an API connection](https://www.interactivebrokers.com/docs/tws-api/doc/connectivity/verify-api-connection)
- [C#, C++, and Java EReader implementation](https://www.interactivebrokers.com/docs/tws-api/doc/connectivity/the-e-reader-thread/c-c-and-java-implementations)
- [Logging into multiple applications](https://www.interactivebrokers.com/docs/tws-api/doc/connectivity/logging-into-multiple-applications)
- [System message codes](https://www.interactivebrokers.com/docs/tws-api/ref/system-message-codes)
- [Receive next valid ID](https://www.interactivebrokers.com/docs/tws-api/doc/next-valid-id/receive-next-valid-id)
- [Order ID requirements](https://www.interactivebrokers.com/docs/tws-api/doc/quick-start/order-id)
- [API client open orders](https://www.interactivebrokers.com/docs/tws-api/doc/order-management/requesting-currently-active-orders/api-clients-orders)
- [Daily and weekly reauthentication](https://www.interactivebrokers.com/docs/tws-api/doc/tws-settings/daily-weekly-reauthentication)
- [Account update callbacks](https://www.interactivebrokers.com/docs/tws-api/doc/account-portfolio-data/account-updates/receiving-account-updates)

If current official documentation or the pinned C# assembly conflicts with a signature/example in this document, update the adapter mapping and tests while preserving the ownership, isolation, correlation, epoch, and fail-closed safety invariants.
