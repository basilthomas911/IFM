# Actor Message Types and Delivery Conventions

**Status:** Authoritative messaging convention; transport mapping implemented, participation constraints documented but not yet fully enforced

**Created:** 2026-08-12

**Last updated:** 2026-08-12

## 1. Purpose

This document defines how actor and non-actor components communicate throughout IFM. It is the single reference for:

- actor message roles;
- the transport assigned to each `ActorType`;
- durability and delivery expectations;
- which kinds of consumers may participate in each message flow;
- how messages cross between realtime, durable actor, and external-observer boundaries; and
- how UI, console, service, and other non-actor components participate in messaging.

The central convention is:

> Message semantics select the `ActorType`, and the `ActorType` selects exactly one transport.

Callers must not choose Core NATS or JetStream independently from the message type. A message is published once on the transport assigned to its `ActorType`; it is not dual-written to both transports.

## 2. Design goals

The convention keeps the actor model deliberately small and predictable:

- each consumer handles exactly one actor type;
- each consumer subscribes beneath one `[ActorType].>` subject namespace;
- each actor type has one delivery transport;
- durable and non-durable intent is visible in the message type rather than a flag;
- a boundary crossing is represented by a new message with the correct semantics;
- UI and console consumers explicitly choose observational or durable participation; and
- no duplicate publication is required to make the same message visible on two transports.

This design avoids a `Durable` flag on individual messages. Such a flag would make transport selection a per-message decision and would weaken the meaning of the subject namespace. Durability is instead part of the actor-type contract.

## 3. Actor type and transport mapping

| `ActorType` | Numeric value | Transport | Durable | Primary messaging pattern | Intended role |
| --- | ---: | --- | --- | --- | --- |
| `Unknown` | 0 | None | No | None | Uninitialized or invalid subject; must not be published |
| `Command` | 2 | NATS Core | No | Request/reply or directed send | Ask an actor to perform work or change state |
| `Event` | 3 | NATS JetStream | Yes | Durable publish/consume | Record and deliver a workflow fact with replay and redelivery |
| `Query` | 4 | NATS Core | No | Request/reply | Read large projection state without changing actor state |
| `Notify` | 5 | NATS Core | No | Publish/subscribe | Best-effort notification to external observers |
| `Realtime` | 7 | NATS Core | No | Low-latency publish/subscribe | High-rate, latency-sensitive delivery to realtime actors |

Numeric values `1` and `6` are reserved. They previously represented `Supervisor` and `UI` and must not be reused, because persisted `ActorSubject` payloads could otherwise be interpreted as a different actor type.

The code-level delivery mapping is represented by `ActorDeliveryType`:

| `ActorDeliveryType` | Meaning |
| --- | --- |
| `Unknown` | No valid messaging transport |
| `NatsCore` | Non-durable Core NATS delivery |
| `NatsJetStream` | Durable JetStream delivery |

`ActorType.GetDeliveryType()` is the authoritative mapping function. Actor producer and consumer implementations use this mapping to reject a transport that is incompatible with the subject's actor type.

## 4. Transport guarantees

### 4.1 NATS Core

Core NATS is used for `Command`, `Query`, `Notify`, and `Realtime`.

Core NATS provides the lowest-latency path, but it does not retain messages for an offline consumer. A subscriber normally sees only messages published while it is connected and subscribed. Consequently:

- delivery is best effort;
- there is no replay after a subscriber reconnects;
- there is no durable acknowledgement or redelivery contract;
- request/reply timeouts are part of normal failure handling; and
- transient observational messages may be lost without damaging durable workflow state.

### 4.2 NATS JetStream

JetStream is used for `Event`.

JetStream persists events and supports durable consumers, acknowledgements, redelivery, and replay. Consequently:

- delivery is at least once rather than exactly once;
- consumers must tolerate duplicate delivery and be idempotent where required;
- a consumer can resume after being offline;
- acknowledgement represents successful handling by that consumer; and
- a failure or negative acknowledgement can cause redelivery.

Only one JetStream stream may own the `Event.>` subject namespace. Actor consumers, UI listeners, console listeners, and other durable workflow participants reuse that stream and create separate durable consumers with the filters they require. They do not create overlapping streams and the producer does not publish a second copy.

## 5. Subject and consumer convention

Actor subjects are grouped by actor type:

```text
[ActorType].[ActorName].[Verb].[EntityId-or-thread]
```

The exact remaining tokens depend on the existing `ActorSubject` contract, but the first token is always the actor type and therefore determines delivery.

A consumer subscribes to one actor-type namespace:

```text
Command.>
Event.>
Query.>
Notify.>
Realtime.>
```

A single consumer must not combine multiple actor types. A component that participates in more than one delivery role creates a separate consumer task for each role. For example, a UI requiring both live notifications and durable workflow events uses:

- one Core NATS listener for `Notify.>`; and
- one JetStream durable listener for the required `Event.>` subjects.

There is intentionally no unrestricted Core NATS listener that represents all actor message types.

## 6. Message semantics and participation rules

### 6.1 `Command`: enter or direct the actor system

A command asks an actor to perform work. It is directed at the actor responsible for the requested behavior and uses Core NATS.

Conventions:

- use a command when a caller wants an actor to change state or execute behavior;
- realtime processing that needs to enter the normal actor system must send a command;
- a realtime message itself must not be republished into a normal actor's subject namespace;
- the receiving actor decides whether the command produces durable events; and
- a timeout or failed request does not imply that a durable workflow fact exists.

Example:

```text
Market feed -> Realtime message -> Realtime actor
Realtime actor -> Command -> Trade actor
Trade actor -> Event -> Durable actor workflow and durable external participants
```

The command is a semantic handoff. It is a new message, not the original realtime message resent through another transport.

### 6.2 `Event`: durable actor workflow participation

An event is a durable fact within an actor workflow and always uses JetStream.

Conventions:

- event actors consume events through JetStream;
- actor-to-actor durable workflow facts use `Event`;
- an external non-actor consumer of an event is participating in the actor workflow;
- an external workflow participant must use a JetStream durable consumer;
- a durable UI or console function uses an `Event` listener, not a Core listener;
- event consumers acknowledge successful processing and must handle possible redelivery; and
- an event is published once to JetStream, even when several durable consumers require it.

"External non-actor" includes UI backends, console services, projectors, gateways, integration services, and similar components that do not run as actors. If such a component needs reliable delivery, recovery after disconnection, or workflow-significant processing, it is an event participant and must consume through JetStream.

Observing an event does not give a non-actor permission to mutate actor state directly. Any requested actor behavior still enters through a command.

### 6.3 `Query`: read projection state

A query requests read-only projection state and uses Core NATS request/reply.

Conventions:

- any actor or non-actor component may issue a query;
- queries are suitable for UI, console, service, and diagnostic clients;
- queries read projection state and do not change actor state;
- large read models belong behind query handlers rather than being reconstructed from notifications;
- query consumers return a response or a typed failure; and
- callers must handle timeouts because Core NATS does not retain a query for later processing.

The broad rule is: **anyone may query, but queries are read-only**.

### 6.4 `Notify`: external observation only

A notification is a non-durable, best-effort message for external observers and uses Core NATS publish/subscribe.

Conventions:

- only external observers should listen for `Notify` messages;
- actors should not depend on or consume `Notify` as part of a workflow;
- notifications may be used for UI updates, console output, monitoring, status display, and diagnostics;
- losing a notification while an observer is offline must not corrupt actor or workflow state;
- notification handlers must not be required for the originating workflow to complete; and
- an observer that needs replay or reliable participation must consume an `Event` instead.

`Notify` replaces the former idea of a UI actor type. UI and console applications are external components whose chosen listener expresses whether they are observing (`Notify`) or participating durably (`Event`).

### 6.5 `Realtime`: low-latency realtime processing

A realtime message is a non-durable, latency-sensitive message and uses Core NATS publish/subscribe.

Conventions:

- only realtime actors may consume `Realtime` messages as active workflow inputs;
- realtime market-data feeds, realtime analytics, and comparable high-rate paths use this message type when durability is not required;
- a realtime actor may transform, aggregate, or react to the message at low latency;
- an external non-actor may subscribe to realtime messages only as an observer;
- an external realtime observer has the same non-participating status as a `Notify` observer;
- no actor workflow may depend on an external realtime observer receiving the message; and
- when realtime processing needs behavior from a normal actor, it sends a new `Command` to that actor.

A normal command or event actor must not consume a realtime message directly. The explicit command handoff prevents high-rate ephemeral traffic from silently becoming actor workflow state.

#### Realtime primary destination and routing

Every realtime publication must address one registered primary realtime actor, including publications originating from a market-data provider or another non-actor component. The subject actor name is therefore the primary actor mailbox name rather than a virtual topic name. For example, a futures market-price publication uses a primary `FuturesMarketPriceRealtimeActor` mailbox named `FuturesMarketPrice`:

```text
Realtime.FuturesMarketPrice.Updated.{contractId}
```

Additional realtime actors may register routes for the complete source identity (`ActorType`, actor name, and verb). The Core NATS consumer receives the publication once and creates independent in-process mailbox branches for the primary actor and every deduplicated registered route. Each routed branch preserves the source verb, entity ID, and serialized payload while replacing the mailbox destination.

Realtime routing follows these rules:

- the source and every routed destination must use `ActorType.Realtime`;
- the primary actor must be registered when the message arrives, otherwise actor delivery is rejected;
- the primary actor is included exactly once even if it was also registered as a route;
- primary and routed mailbox handoffs are independent and may execute concurrently;
- routing does not wait for primary actor processing to finish and does not imply primary-handler success;
- Core NATS provides no acknowledgement, redelivery, or replay for a rejected route branch;
- `Notify`, `Command`, and `Query` messages never participate in realtime routing; and
- external Core NATS subscribers remain observers and do not become actor routes.

If downstream delivery must depend on successful primary processing or requires a transformed contract, the primary actor emits a distinct message after processing instead of relying on realtime fan-out ordering.

For futures market prices, TickAggregation first atomically stores the normalized `FuturesMarketPriceSnapshot` in its stream-independent hot cache and then publishes `FuturesMarketPriceUpdatedRealtimeEvent` containing that same snapshot. Trade observations trigger the realtime publication; quote observations refresh only the cached quote side until a dedicated observer contract is designed. Realtime ITI actors use the event payload directly. Slower timer-derived signal workflows check `IsTickDataStreamActive` when live data is required and then sample `TryGetLastTickPrice` at their time boundary. Futures-option consumers use the equivalent `TryGetLastOptionTickPrice` hot-cache operation. Price reads never acquire ownership or extend stream lifetime.

## 7. Actor and non-actor interaction matrix

| Sender | Receiver | Intent | Message type | Transport | Receiver participation |
| --- | --- | --- | --- | --- | --- |
| Actor | Actor | Request behavior or state change | `Command` | Core | Active actor workflow |
| Actor | Actor | Deliver durable workflow fact | `Event` | JetStream | Durable actor workflow |
| Realtime source or actor | Realtime actor | Deliver latency-sensitive input | `Realtime` | Core | Active realtime processing |
| Realtime actor | Normal actor | Enter normal actor workflow | New `Command` | Core | Active actor workflow |
| Actor | External non-actor | Reliable workflow-significant delivery | `Event` | JetStream | Durable workflow participant |
| Actor or service | External non-actor | Best-effort status or display update | `Notify` | Core | Observer only |
| Realtime source | External non-actor | Best-effort live display or telemetry | `Realtime` | Core | Observer only |
| Actor or external non-actor | Query actor | Read projection state | `Query` | Core | Read-only caller |
| External non-actor | Actor | Request behavior or state change | `Command` | Core | Enters actor workflow |

## 8. Handoff rules

Changing delivery semantics requires a new message. The original message must not be dual-written, forwarded unchanged under a different actor type, or made durable by a message-level flag.

Use these handoffs:

| From | Required destination behavior | Handoff |
| --- | --- | --- |
| `Realtime` | A normal actor must perform work | Send a new `Command` |
| `Realtime` | A durable fact has been established | Emit a new `Event` from the responsible workflow boundary |
| `Notify` | Reliable external processing is required | Define and emit an `Event` |
| `Event` | An observer only needs a transient display update | Optionally emit a separate `Notify` with observer-specific content |
| `Query` result | Actor state must change | Send a separate `Command`; never mutate through the query |
| External event participant | Actor behavior is required | Send a `Command` after processing the event |

The new message may contain correlated identifiers, but it represents a distinct intent or fact and should have its own message identity.

## 9. UI and console conventions

UI and console applications are not actor types. Their listener choice defines their role:

| Requirement | Listener |
| --- | --- |
| Live status, progress, logs, or display refresh where missed messages are acceptable | Core NATS `Notify` listener |
| Live realtime display where missed samples are acceptable | Core NATS `Realtime` listener acting as an observer |
| Reliable workflow participation, replay after downtime, or acknowledgement | JetStream `Event` durable listener |
| Read current or large projection state | Core NATS `Query` request |
| Ask an actor to perform work | Core NATS `Command` request/send |

A UI may therefore run more than one consumer task, but each task remains specific to one actor type and its assigned transport.

## 10. Publication and delivery rules

The following rules apply system-wide:

1. Determine the message semantics first.
2. Select the corresponding `ActorType`.
3. Derive the transport from `ActorType.GetDeliveryType()`.
4. Publish the message exactly once through that transport.
5. Use a separate consumer for each actor type.
6. Use a new semantic message when crossing a boundary.
7. Do not use Core NATS as a live duplicate of a JetStream event.
8. Do not add a per-message durability flag.
9. Do not treat `Notify` or externally observed `Realtime` delivery as workflow participation.
10. Do not use a query to mutate actor state.

## 11. Current enforcement boundary

The transport mapping is implemented and producer/consumer implementations reject incompatible combinations such as:

- publishing an `Event` through the Core actor producer;
- publishing `Realtime` through the JetStream actor producer;
- starting a Core actor consumer for the durable `Event` actor type; or
- starting a JetStream actor consumer for a Core-only actor type.

The following participation constraints are conventions documented here and are **not yet fully enforced** by runtime type checks, NATS credentials, or subject permissions:

- only realtime actors actively consume `Realtime` messages;
- external non-actors consume `Realtime` only observationally;
- only external observers listen for `Notify` messages;
- actors do not depend on `Notify` messages;
- an external non-actor consuming `Event` is a durable workflow participant; and
- normal actors are entered from realtime processing through a new `Command`.

These rules should guide new development, code review, tests, and future authorization policy. Adding runtime or NATS credential enforcement requires a separate implementation decision and is outside the scope of this document.

## 12. Decision examples

### "The UI should show the latest market price."

Use a `Realtime` listener if the UI wants the raw low-latency feed and missed samples are acceptable. The UI is an observer. Alternatively, query the current projection when opening or refreshing the screen.

### "The console should display actor progress."

Use `Notify` over Core NATS. Progress display must not be required for workflow completion.

### "A service must process every completed trade, including after downtime."

Use an `Event` durable consumer over JetStream. The service is participating in the actor workflow and must handle redelivery safely.

### "Realtime analytics identified a trade opportunity and the trade actor should act."

The realtime actor sends a new `Command` to the trade actor. It does not send the realtime message directly to the trade actor and does not change the original message's transport.

### "A UI needs a large current portfolio view."

Issue a `Query` for the projection state. Do not depend on reconstructing the complete state from `Notify` or `Realtime` messages.

### "An external service received a durable event and now needs an actor to do more work."

After processing the event, the service sends a new `Command` to the responsible actor. The service does not mutate actor state directly.

## 13. Summary

The IFM messaging model is based on semantic roles rather than caller-selected transport:

- `Command` enters or directs actor behavior through Core NATS;
- `Event` carries durable actor workflow facts through JetStream;
- `Query` provides read-only access to projection state through Core NATS;
- `Notify` provides non-durable external observation through Core NATS; and
- `Realtime` provides non-durable low-latency processing for realtime actors, with external subscribers acting only as observers.

This convention provides one transport per actor type, one publication per message, explicit handoffs between messaging roles, and a clear distinction between workflow participants and observers.
