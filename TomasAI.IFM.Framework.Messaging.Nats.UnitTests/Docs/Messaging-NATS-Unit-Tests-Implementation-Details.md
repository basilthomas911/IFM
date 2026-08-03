# Messaging NATS Unit Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Messaging.Nats.UnitTests` provides transport-free tests for event route preparation and the durable JetStream replay queue state machine. It targets .NET 10 and uses xUnit, FluentAssertions, NSubstitute, and coverlet.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.Messaging.Nats.UnitTests/`. Each leaf includes all intermediate folders.

```text
Docs/
NatsJSDurableQueue/
bin/Debug/net10.0/cs/
bin/Debug/net10.0/de/
bin/Debug/net10.0/es/
bin/Debug/net10.0/fr/
bin/Debug/net10.0/it/
bin/Debug/net10.0/ja/
bin/Debug/net10.0/ko/
bin/Debug/net10.0/pl/
bin/Debug/net10.0/pt-BR/
bin/Debug/net10.0/ru/
bin/Debug/net10.0/runtimes/win/lib/net10.0/
bin/Debug/net10.0/tr/
bin/Debug/net10.0/zh-Hans/
bin/Debug/net10.0/zh-Hant/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
```

- `Docs/` contains this implementation record.
- `NatsJSDurableQueue/` contains the fake transport, sample event, and durable replay tests.
- `bin/` locale leaves contain test-platform resources; its runtime leaf contains Windows runtime assets.
- `obj/` contains generated reference and intermediate assemblies.
- The root contains `NatsEventProducerTests.cs` and the test project file.

## Event-producer tests

`NatsEventProducerTests` exercises the internal route-preparation helpers exposed through `InternalsVisibleTo`:

- A missing subject is derived from the event's public `Actor`/`Verb` route and key, while delivery metadata is initialized.
- A valid existing subject and identity are preserved.
- An event without a resolvable actor route throws `InvalidOperationException`.

These tests do not open a NATS connection.

## Durable replay fake transport

`FakeNatsJSDurableQueueTransport` implements the internal queue-transport interface using in-memory process and replay channels. It records queue settings, published message IDs, ACK/NAK behavior, and configurable publish/ack failures. Fake message delivery counts allow retry and terminal-action paths to be tested deterministically.

`SampleData.cs` provides a concrete `IEvent` with stable actor/event fields for queue scenarios.

## Durable replay coverage

`NatsJSDurableReplayQueueTests` verifies:

- Deterministic process and replay configuration at startup.
- Successful processing and acknowledgement.
- Process failure handoff to replay.
- Process NAK/redelivery when replay publication fails.
- Stable replay IDs when process ACK fails after handoff.
- De-duplication IDs for repeated enqueue of one event.
- Replay NAK until a later attempt succeeds.
- Terminal action and ACK at maximum delivery.
- Isolation of state and handlers by projector name.
- Non-overwriting retry configuration.
- Worker restart after idle timeout or explicit stop.
- Rejection of non-positive maximum replay attempts.

The suite uses short in-memory timing windows; failures should be diagnosed for synchronization assumptions before increasing delays.

## Current coverage boundaries

This project does not validate a real NATS server, stream/consumer provisioning, server-side message de-duplication, durable restart recovery, Core NATS producer/consumer I/O, JetStream actor dispatch, serializers, `NatsActorThreadQueue`, or the alternate message ring buffer. Those areas require integrated coverage or focused unit tests.

## Running the tests

```powershell
dotnet test TomasAI.IFM.Framework.Messaging.Nats.UnitTests/TomasAI.IFM.Framework.Messaging.Nats.UnitTests.csproj --configuration Debug
```

No external NATS service should be required for this unit-test project.

## Safe extension points

Extend the fake transport when queue protocol behavior changes, and assert both the externally visible result and the ACK/NAK/publish sequence. Keep real server assumptions in the integrated-test project.
