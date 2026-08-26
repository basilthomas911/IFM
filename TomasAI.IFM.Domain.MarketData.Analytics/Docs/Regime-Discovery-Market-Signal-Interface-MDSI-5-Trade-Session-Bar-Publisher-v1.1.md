# MDSI-5 Futures Trade Session Bar Publisher

Futures Trade Session Bar Publisher v1.1

| Item | Value |
| --- | --- |
| Gate | `MDSI-5 - Shared trade-session bar publication` |
| Status | Implemented; qualification refreshed 2026-08-26 |
| Original completion | 2026-08-25 |
| Architecture correction | 2026-08-26 |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Gate conclusion

MDSI-5 now uses the business-specific name `FuturesTradeSessionBarPublisher`.
It converts accepted futures trades into immutable, session-aligned OHLCV bars
without assigning durable state or persistence responsibilities to a Realtime
actor.

The correction enforces the application actor convention:

- only the Command actor is event sourced and owns durable state;
- the Realtime actor routes live events and has no projector;
- the actor-specific calculation object is a concrete Model, not a generic
  application service;
- the EventProjector is located under `Command` and updates ScyllaDB; and
- the Event actor is stateless and publishes the downstream Realtime event only
  after successful projection.

## 2. Actor topology

```text
FuturesMarketPriceUpdatedRealtimeEvent
    -> FuturesTradeSessionBarPublisherRealtimeActor
    -> FuturesTradeSessionBarAccumulator (Realtime/Model)
    -> PublishFuturesTradeSessionBarCommand
    -> FuturesTradeSessionBarPublisherCommandActor
    -> ACID event log: FuturesTradeSessionBarPublishedEvent
    -> FuturesTradeSessionBarPublisherEventProjector
    -> ScyllaDB: futures_trade_session_bar
    -> FuturesTradeSessionBarPublishedCompleteEvent
    -> FuturesTradeSessionBarPublisherEventActor
    -> FuturesTradeSessionBarClosedRealtimeEvent
    -> bar-derived signal Realtime actors
```

`FuturesTradeSessionBarPublishedFailEvent` terminates the flow when projection
fails. An unpersisted bar is never sent to bar-derived consumers.

## 3. Realtime actor and Model

`FuturesTradeSessionBarPublisherRealtimeActor` owns route and timer lifecycle
only. Its type-keyed receive map sends each supported event to a dedicated
extension handler:

- `FuturesMarketPriceUpdatedRealtimeEvent` invokes the live-trade handler; and
- `FuturesTradeSessionBarPublisherBarrierRealtimeEvent` invokes the elapsed-bar
  handler.

The concrete `FuturesTradeSessionBarAccumulator` resides in `Realtime/Model`.
There is deliberately no accumulator interface because this is one targeted,
actor-centric computation with one implementation and one consumer. DI creates
one singleton instance and passes it through the closed generic Realtime
context.

The Model retains only ephemeral open-bucket computation:

- OHLC, exact trade-size volume, trade count, and price-volume sum;
- accepted source sequence and exchange-time bounds;
- duplicate, missing-ordinal, and invalid-epoch guards;
- contract-roll invalidation; and
- session-aligned 15-second, 1-minute, 5-minute, 15-minute, 1-hour, 4-hour,
  and Daily buckets.

The Realtime mailbox serializes trade and clock-barrier access to the Model.
Open buckets are not durable. A process restart therefore requires historical
trade replay/backfill to reconstruct an unfinished interval; completed bars
remain durable through the Command actor.

## 4. Command actor and durable state

`PublishFuturesTradeSessionBarCommand` carries one complete immutable bar. Its
`CommandId` is the deterministic `FuturesTradeSessionBarId`, making retries use
the same command identity.

`FuturesTradeSessionBarPublisherCommandActor` validates routing and bar
lineage, applies `FuturesTradeSessionBarPublishedEvent`, and commits it to the
PostgreSQL ACID event log through
`FuturesTradeSessionBarPublisherStateRepository`. Its state records the last
published deterministic bar identity and is reconstructable through event
replay.

No Realtime or Event actor owns this state.

## 5. Projection and downstream publication

`FuturesTradeSessionBarPublisherEventProjector`, located under `Command`,
projects committed bars to the ScyllaDB `futures_trade_session_bar` table. A
Daily bar also updates `futures_eod_observation` with raw session facts.

The stateless Event actor dispatches source, complete, and failed events to
separate handlers. Only `FuturesTradeSessionBarPublishedCompleteEvent` creates
`FuturesTradeSessionBarClosedRealtimeEvent`. This preserves persistence-before-
publication ordering.

The old `futures_analytics_observation` table name and Realtime projector are
removed from the development schema. Existing development data under the old
table is intentionally not migrated.

## 6. Dependency registration

Both the API host and actor integration host register:

```csharp
services.AddSingleton<IFuturesTradeSessionBarSeriesResolver>(...);
services.AddSingleton<FuturesTradeSessionBarAccumulator>();
```

Open generic registration continues to construct the Command, Event, and
Realtime actor contexts. The accumulator is injected as a concrete readonly
Realtime-context property.

## 7. Qualification scope

Tests cover:

- all configured interval schedules and the Daily session barrier;
- deterministic bar identity;
- duplicates, source gaps, invalid epochs, recovery epochs, and contract rolls;
- MessagePack command/bar round-trip;
- idempotent Command-state application;
- conventional complete/failed event conversion; and
- API and actor-integration host compilation with the new registrations.

Qualification completed on 2026-08-26:

| Suite/build | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Analytics unit | 894 | 0 | 0 |
| Analytics BDD | 462 | 0 | 0 |
| Analytics integration (including live-to-durable publisher) | 42 | 0 | 0 |
| API Server build | 1 | 0 | 0 |
| Actor integration host build | 1 | 0 | 0 |

The integration listeners now filter terminal events by the expected routed
entity, preventing unrelated shared-host traffic from overwriting scenario
results.

## 8. Exit decision

One logical completed interval produces one deterministic bar command and one
event-sourced publication. ScyllaDB projection precedes downstream Realtime
publication. The Realtime and Event actors remain stateless, and the only
durable actor state resides in the Command actor.
