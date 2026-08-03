# Messaging NATS Implementation Details

## Purpose

`TomasAI.IFM.Framework.Messaging.Nats` is the NATS transport implementation for the IFM actor and event infrastructure. It supports Core NATS publish/subscribe and request/reply, JetStream durable actor delivery, event-producer/consumer compatibility APIs, actor mailbox queues, payload serializers, and a durable event-projector replay queue.

The project targets .NET 10 and references NATS.Net 2.6.11, MessagePack 3.1.8, Newtonsoft.Json 13.0.4, Framework Serialization, and Shared actor contracts.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.Messaging.Nats/`. Each leaf includes every intermediate parent folder.

```text
Contracts/
Docs/
Serializers/
bin/Debug/net10.0/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
```

- `Contracts/` contains transport configuration and data-serializer interfaces.
- `Docs/` contains this overview and the focused `NatsJSDurableReplayQueue.md` design document.
- `Serializers/` contains the active byte-array, JSON, and MessagePack serializers.
- `bin/` and `obj/` are generated .NET 10 Debug output and intermediate trees.
- The project root contains all transport, queue, option, and project-definition files.

## Namespace and project rename

The folder and assembly are named `TomasAI.IFM.Framework.Messaging.Nats`, but most runtime types currently retain the namespace `TomasAI.IFM.Framework.Messaging.NatsJetStream`. This preserves existing consumers after the project rename but is important when adding imports or moving types.

The root `NatsMessagePackDataSerializer.cs` is explicitly excluded from compilation. The active implementation is `Serializers/NatsMessagePackDataSerializer.cs`; new code should not depend on the excluded duplicate.

## Configuration contracts

- `INatsProducerOptions` configures the Core NATS URL, optional queue group, and JSON options.
- `INatsConsumerOptions` configures the URL, JSON options, and actor dispatch-stripe count.
- `INatsEventListenerOptions` configures the listener URL.
- `INatsJetStreamProducerOptions` configures URL, subject prefix, and JSON options.
- `INatsJetStreamConsumerOptions` configures URL, stream name, durable-consumer name, and dispatch stripes.
- `IDataSerializer` defines reference-type byte serialization.

Concrete options default to `nats://localhost:4222`. Consumer dispatcher counts default to four and are clamped to at least one at startup.

## Core NATS actor producer

`NatsActorProducer` owns a `NatsConnection`, MessagePack payload serialization, and raw-byte NATS message serialization. `StartAsync` creates the connection for a mailbox and configures a two-minute request timeout. Send overloads publish commands and events to an `ActorSubject`; request overloads send query or command-request messages and deserialize `ServiceResult<TResult>` replies. Send/request calls lazily restart the producer when it is not running. Transport failures are logged and rethrown.

`NatsActorMessage` wraps `NatsMsg<byte[]>`, converts payloads to typed commands/events/queries, exposes parsed actor subjects, and sends MessagePack replies when `ReplyTo` is present.

## Core NATS actor consumer

`NatsActorConsumer` subscribes to `{ActorType}.>` and forwards each message to the matching actor mailbox under `IActorSupervisor.Children`.

Startup creates bounded dispatch channels with capacity 4,096. A message's `ActorThreadId` hash selects a stripe, preserving ordering for a given actor thread while allowing different entities to dispatch concurrently. Each stripe has one writer and one reader and applies backpressure when full.

Supervisor, Command, Event, and Notify actor types use the publish/subscribe loop; Query uses the request/reply loop. Current switch logic does not start loops for other actor types even where comments mention them. Dispatch resolves the target actor by `ActorId` and writes to its thread queue. Shutdown cancels the subscription, completes/drains stripe channels, awaits dispatcher tasks, and disposes the NATS client.

## Event listener and compatibility APIs

`NatsActorEventListener` groups subscriptions by event mailbox, subscribes to `{mailbox}.>`, filters configured verbs case-insensitively, invokes one async handler, counts messages, and tracks listener state across start/stop cycles.

`NatsEventConsumer` is the compatibility base for domain consumers. Derived types register event prototypes and callbacks in `ConnectEvents`; the base resolves mailbox/verb routes, builds one listener map, dynamically deserializes concrete event types, and suppresses duplicate event identifiers through a bounded static map.

`NatsEventProducer` is the compatibility base for domain producers. Its parameterless constructor disables transport for BDD tests. Other constructors enable Core NATS or dual Core NATS/JetStream publishing. It preserves a valid event subject or derives one from public static `Actor` and `Verb` members plus the supplied entity key, initializes event delivery metadata, and lazily starts producers behind semaphores.

## JetStream actor transport

`NatsJetStreamActorProducer` creates its own NATS client and JetStream context, MessagePack-serializes commands/events, publishes to their actor subjects, and requires a successful server acknowledgement. It does not implement request/reply.

`NatsJetStreamActorConsumer` creates or updates an actor-type stream for `{ActorType}.>`, creates a durable explicit-ack consumer with deliver-all policy, and dispatches through the same 4,096-item striped-channel design. It acknowledges after mailbox delivery and invokes supervisor event routing. Routed copies re-enter a stripe without acknowledging the original message twice.

If stream creation reports overlapping subjects, the current recovery block enumerates and deletes every listed stream before retrying. This is operationally broad and should only run against an isolated account or be narrowed to the actual conflicting stream.

## Actor mailbox queues

- `NatsActorSpscRingBuffer` is the active pooled single-producer/single-consumer buffer. It requires a power-of-two capacity, uses padded monotonic indices, spins before parking on reset events, supports cancellation, and returns its array to `ArrayPool` on stop.
- `NatsActorThreadQueue` resolves the SPSC buffer from the actor container, exposes sync/async read enumeration, and delegates writes to the buffer. `SetMessageAvailable` is currently unimplemented and throws.
- `NatsActorMessageRingBuffer` is an alternate semaphore-backed SPSC implementation. Its `EnqueueSpin` and `DequeueSpin` methods are placeholders that do not enqueue/dequeue useful data and should not be used.

Exactly one producer and one consumer may access either SPSC implementation concurrently.

## Serialization

- `NatsByteArrayMessageSerializer` copies NATS byte sequences and writes raw bytes. `CombineWith` is unimplemented.
- `NatsMessagePackDataSerializer` delegates typed serialization to `MessagePackBinarySerializer` and is the normal actor payload serializer.
- `NatsJsonDataSerializer` uses Newtonsoft.Json with object type metadata and UTF-8. It catches serialization/deserialization exceptions and returns an empty array or null, so callers cannot distinguish malformed data from absent data by exception.

## Durable replay queue

`NatsJSDurableReplayQueue` implements `IDurableReplayQueue` for event projectors. Each projector receives isolated deterministic process/replay stream, subject, consumer, worker, handler, retry, and terminal-action state.

`Enqueue` serializes an event envelope and publishes a deterministic process message ID for JetStream de-duplication. The process worker invokes the projector handler: success acknowledges the process message; failure publishes to the replay stream and then acknowledges the process copy. If replay publication fails, the process message is negatively acknowledged for redelivery. The replay worker retries with bounded backoff, invokes the configured terminal action at the maximum delivery count, and acknowledges terminal messages.

Workers can stop after an idle timeout and restart on enqueue/dequeue. `StartAsync`, `StopAsync`, retry configuration, and disposal are isolated per projector. `NatsJSDurableQueueTransport` owns JetStream provisioning, publish, consume, ACK, NAK, and message metadata. See `Docs/NatsJSDurableReplayQueue.md` for the detailed state and failure matrices.

## Operational considerations

- Core NATS is at-most-once transport; JetStream provides durable at-least-once delivery, so consumers must be idempotent.
- Actor subjects are routing contracts. Actor type, mailbox name, verb, and entity/thread identifier must remain consistent between producers and consumers.
- Stop and dispose transport instances during application shutdown to release subscriptions, workers, pooled arrays, and connections.
- Do not use overlapping JetStream subjects or broad stream cleanup in a shared NATS account.
- Monitor bounded dispatch channels, replay backlog, redeliveries, terminal actions, and serializer failures.

## Safe extension points

Add new settings through the option interfaces and concrete option types together. Keep actor payload serialization compatible across producers and consumers. Preserve per-thread stripe selection when changing concurrency. Add both transport-free tests and live JetStream integration tests for changes to ACK/NAK, de-duplication, retry, or lifecycle behavior.
