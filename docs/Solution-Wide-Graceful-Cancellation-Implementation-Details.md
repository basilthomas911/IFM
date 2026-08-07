# Solution-Wide Graceful Cancellation Implementation Details

## Status

Implementation started in August 2026 as the first item in the revised application-wide optimization order. The command/event-source path is complete. Query/read-model migration is proceeding bounded context by bounded context; Fund, FundTransaction, MarketData, YieldCurveRate, Analytics actor and direct in-process queries, Securities, Reference, OptionPricer, SystemAdmin, and Trade now have explicit end-to-end token propagation.

The legacy Interactive Brokers market-data implementation is excluded. Databento is the replacement and will be the reference design for any later IBKR implementation.

## Required semantics

Cancellation and command success are separate concerns. Cancellation stops work only while it is still safe to abandon. A command may succeed without changing state, and a state update result continues to indicate state change rather than command success.

The command pipeline uses this boundary:

1. Parsing, validation, command-log observation, state replay, and pre-commit storage operations honor cancellation.
2. `OperationCanceledException` propagates as cancellation. It is not converted into a command failure event.
3. Once command execution enters event persistence, the persistence and required event publication/denormalization sequence completes without caller cancellation. This avoids returning cancellation for an operation that may already have committed.
4. A command audit write that was started during parsing is allowed to finish even if the actor stops waiting for it. Audit durability is preserved while `WaitAsync(cancellationToken)` bounds the actor's wait.
5. A cancellation token is never serialized across NATS. Canceling a client request cancels publishing or the client's response wait; it does not revoke work already accepted by a remote actor.

## Graceful supervisor shutdown

`IActorSupervisor.ShutdownAsync` owns one idempotent shutdown task:

1. Stop NATS and JetStream consumers so new external messages are no longer accepted.
2. Complete the shared ready queue and drain every message already accepted by actor mailboxes.
3. Stop actors and their producers only after the worker pool finishes draining.
4. Continue later cleanup stages when an earlier stage fails, then report an aggregate failure.

The caller's shutdown token bounds only that caller's wait. Once shutdown starts, cancellation of one waiter does not abandon the shared drain. Actor cleanup also becomes non-cancelable after an actor has atomically entered its stopping state, preventing a half-stopped producer/actor pair.

The API server now awaits actor startup directly and awaits supervisor shutdown after ASP.NET stops accepting requests. The former `Task.Run(...).Wait()` startup bridge has been removed from both the production host and its integration-test host.

Host disposal order is also treated as an idempotent lifecycle boundary. A JetStream loop whose shared connection is released immediately after its stop token is canceled exits normally, and a projector stop issued after its durable queue has already been disposed is a no-op. This prevents correct host teardown from surfacing disposed transport primitives as aggregate shutdown failures.

Command, query, event, and denormalizer actor startup hooks now receive the host startup token. Cancellation raised after producer startup but before the actor becomes runnable rolls the producer back and resets the lifecycle state. Fund's owned durable projector additionally stops its partially initialized queue during this rollback.

Both the production API host and actor integration host use `ActorRuntimeStartup` for registration and startup. Actor, producer, and consumer registration is inside the same guarded operation as consumer/actor startup. Cancellation or failure at any point invokes `ShutdownAsync(CancellationToken.None)`, so already registered or started resources are cleaned up without a second host-specific implementation drifting from the first.

## Implemented propagation

Cancellation-aware overloads now cover:

- actor, supervisor, producer, consumer, actor-service, and command-context contracts;
- centralized production/integration host actor registration and rollback-safe startup orchestration;
- command, event, query, and denormalizer base dispatch and startup hooks;
- NATS publish/request and NATS/JetStream lifecycle operations;
- optimized/current domain command validation, state repositories, snapshot replay, last-N replay, and state persistence;
- event-source stream/name lookup, command log writes, event writes, and map-reduce replay;
- event-projector startup recovery state queries and writes, durable handler registration, and queue startup;
- generic object repository contexts and PostgreSQL/ScyllaDB command, queued-command, scalar, object, immutable-object, and map-reduce operations;
- PostgreSQL connection, transaction, prepare, execute, read, commit, and cancellation rollback paths;
- ScyllaDB session creation, prepared statements, owned driver operations, paging, and canceled-driver-task draining.
- Fund and FundTransaction query actors, query handlers, parallel financial calculations, projection-consistency reads, streaming fallback, and Fund read-model APIs.
- MarketData and YieldCurveRate query actors, query handlers, trading-calendar reads/loops, MarketData read-model APIs, and external yield-curve HTTP/file parsing.
- MarketData Analytics query actors, query handlers, all 21 direct in-process API operations, concurrent ITI composite reads, and PostgreSQL read-model operations across RSI, MACD, ATR, ADX, TDI, Trade Signal, and ITI.
- Securities query actors, all contract/option read-model operations, projection-fence reads, and the direct in-process `IActorMarketDataQueryApi`, including concurrent aggregate reads spanning the Securities and MarketData stores.
- Reference query actors, handlers, all 18 direct in-process API operations, projection-fence validation, economic-calendar parallel bucket reads and streaming fallback, scheduled-job reads, seed-reservation pre-submit cancellation, and external calendar parsing.
- OptionPricer's spread-distribution query actor and handler, all three direct in-process query API operations, and concrete OptionPricer read-model storage calls.
- SystemAdmin command-audit/replay processing and its immutable database-name query actor, resolver, and direct in-process API.
- Trade's two query actors, handlers/query model, all 15 direct in-process query API operations, and concrete read-model storage, including bounded hydrated-trade graph fan-out.

Existing no-token methods remain as compatibility entry points and retain the original no-token dependency calls. Runtime dispatch selects the cancellation-aware overload when it has a cancellable worker token. This avoids a flag-day change for tests and callers while keeping the production actor path explicitly cancellation-aware.

## Commit boundary

`BaseEventSourceCommandActor` passes its worker token through validation and state reconstruction. It deliberately invokes state saving with `CancellationToken.None` after command execution begins. `BaseEventSourceActorRepository` checks cancellation immediately before `SaveEventsAsync`; after persistence returns, denormalization and publication complete without caller cancellation.

This policy favors an unambiguous durable outcome over a misleading fast cancellation response. A storage exception still fails reconstruction or persistence through the existing actor exception pipeline. No new domain exception is introduced for missing snapshots or missing events.

Securities projection fallback uses the same rule. Cancellation is honored through projection-fence reads and repair-journal acquisition. After the fallback begins deleting/repopulating a durable projection, the canonical scan, verification, and journal completion finish without caller cancellation.

## Event and denormalizer boundary audit

The 26 production `BaseEventActor` implementations and four legacy queue denormalizers were reviewed. Their receives are downstream of durable event persistence: they publish follow-up events, update projections, schedule recurring indicator work, or intentionally do nothing as same-domain event sinks. None performs cancellable work before the durable boundary.

No derived token-aware receive overload was added. Once a persisted event begins handler execution, its projection and required follow-up publication finish without the actor worker token. Empty handlers such as `YieldCurveRateEventActor` and `FundTransactionEventActor` remain valid by design. The legacy Interactive Brokers feed remains excluded from optimization changes.

## Verification

Behavioral tests cover:

- actor-service cancellation remaining an `OperationCanceledException`;
- consumer intake stopping before actors;
- cancellation of one shutdown waiter while the shared shutdown continues;
- projector/JetStream cleanup remaining idempotent when dependency-injection disposal races actor shutdown;
- cancellation during command, query, event, and denormalizer actor startup rolling back the started producer;
- cancellation during Fund projector recovery preventing worker startup and rolling back projector ownership;
- cancellation between actor registrations invoking shared host-level supervisor cleanup before consumers start;
- previously existing actor-pool tests proving accepted mailbox messages drain before worker disposal.

The Release solution build and relevant unit-test suites are the verification gate for this phase.

## Remaining work in this priority

- Decide whether a separately named force-stop operation is needed. It must not overload graceful `ShutdownAsync` semantics or discard accepted messages silently.
- Add operational metrics for shutdown duration, drained messages, cancellation count, and failed cleanup stages.

## Explicit exclusions

Do not optimize or extend cancellation specifically for:

- `TomasAI.IFM.Framework.MarketData.InteractiveBrokers`;
- `TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers`;
- queueing code used only by that legacy feed implementation.

Databento lifecycle, native ring-buffer consumption, tick-channel backpressure, and the future tick manager/aggregator actors will be handled as part of the Databento architecture rather than by extending the legacy feed.
