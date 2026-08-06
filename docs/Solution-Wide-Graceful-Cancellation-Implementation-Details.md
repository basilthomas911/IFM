# Solution-Wide Graceful Cancellation Implementation Details

## Status

Implementation started in August 2026 as the first item in the revised application-wide optimization order. The command/event-source path is complete. Query/read-model migration is proceeding bounded context by bounded context; Fund and FundTransaction now have explicit end-to-end token propagation.

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

## Implemented propagation

Cancellation-aware overloads now cover:

- actor, supervisor, producer, consumer, actor-service, and command-context contracts;
- command, event, query, and denormalizer base dispatch;
- NATS publish/request and NATS/JetStream lifecycle operations;
- optimized/current domain command validation, state repositories, snapshot replay, last-N replay, and state persistence;
- event-source stream/name lookup, command log writes, event writes, and map-reduce replay;
- generic object repository contexts and PostgreSQL/ScyllaDB command, queued-command, scalar, object, immutable-object, and map-reduce operations;
- PostgreSQL connection, transaction, prepare, execute, read, commit, and cancellation rollback paths;
- ScyllaDB session creation, prepared statements, owned driver operations, paging, and canceled-driver-task draining.
- Fund and FundTransaction query actors, query handlers, parallel financial calculations, projection-consistency reads, streaming fallback, and Fund read-model APIs.

Existing no-token methods remain as compatibility entry points and retain the original no-token dependency calls. Runtime dispatch selects the cancellation-aware overload when it has a cancellable worker token. This avoids a flag-day change for tests and callers while keeping the production actor path explicitly cancellation-aware.

## Commit boundary

`BaseEventSourceCommandActor` passes its worker token through validation and state reconstruction. It deliberately invokes state saving with `CancellationToken.None` after command execution begins. `BaseEventSourceActorRepository` checks cancellation immediately before `SaveEventsAsync`; after persistence returns, denormalization and publication complete without caller cancellation.

This policy favors an unambiguous durable outcome over a misleading fast cancellation response. A storage exception still fails reconstruction or persistence through the existing actor exception pipeline. No new domain exception is introduced for missing snapshots or missing events.

## Verification

Behavioral tests cover:

- actor-service cancellation remaining an `OperationCanceledException`;
- consumer intake stopping before actors;
- cancellation of one shutdown waiter while the shared shutdown continues;
- projector/JetStream cleanup remaining idempotent when dependency-injection disposal races actor shutdown;
- previously existing actor-pool tests proving accepted mailbox messages drain before worker disposal.

The Release solution build and relevant unit-test suites are the verification gate for this phase.

## Remaining work in this priority

- Continue the explicit query/read-model migration for MarketData, Analytics, Securities, Reference, OptionPricer, SystemAdmin, and Trade.
- Add token-aware event and denormalizer handlers where they perform cancellable pre-commit I/O. Required post-commit projection/publication work must retain the non-cancelable rule.
- Decide whether a separately named force-stop operation is needed. It must not overload graceful `ShutdownAsync` semantics or discard accepted messages silently.
- Add host-level tests that cancel startup partway through actor registration and verify producer rollback.
- Add operational metrics for shutdown duration, drained messages, cancellation count, and failed cleanup stages.

## Explicit exclusions

Do not optimize or extend cancellation specifically for:

- `TomasAI.IFM.Framework.MarketData.InteractiveBrokers`;
- `TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers`;
- queueing code used only by that legacy feed implementation.

Databento lifecycle, native ring-buffer consumption, tick-channel backpressure, and the future tick manager/aggregator actors will be handled as part of the Databento architecture rather than by extending the legacy feed.
