# Messaging NATS Integrated Tests Implementation Details

## Purpose

`TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests` verifies concurrent actor queues, live Core NATS event-listener behavior, and live JetStream durable replay behavior. It targets .NET 10 and uses xUnit, FluentAssertions, NSubstitute, and coverlet.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests/`. Each leaf includes all intermediate parents.

```text
Docs/
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
bin/Debug/net10.0/runtimes/win/lib/net8.0/
bin/Debug/net10.0/tr/
bin/Debug/net10.0/zh-Hans/
bin/Debug/net10.0/zh-Hant/
bin/Debug/net8.0/cs/
bin/Debug/net8.0/de/
bin/Debug/net8.0/es/
bin/Debug/net8.0/fr/
bin/Debug/net8.0/it/
bin/Debug/net8.0/ja/
bin/Debug/net8.0/ko/
bin/Debug/net8.0/pl/
bin/Debug/net8.0/pt-BR/
bin/Debug/net8.0/ru/
bin/Debug/net8.0/runtimes/win/lib/net8.0/
bin/Debug/net8.0/tr/
bin/Debug/net8.0/zh-Hans/
bin/Debug/net8.0/zh-Hant/
bin/Release/net10.0/cs/
bin/Release/net10.0/de/
bin/Release/net10.0/es/
bin/Release/net10.0/fr/
bin/Release/net10.0/it/
bin/Release/net10.0/ja/
bin/Release/net10.0/ko/
bin/Release/net10.0/pl/
bin/Release/net10.0/pt-BR/
bin/Release/net10.0/ru/
bin/Release/net10.0/runtimes/win/lib/net10.0/
bin/Release/net10.0/tr/
bin/Release/net10.0/zh-Hans/
bin/Release/net10.0/zh-Hant/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Debug/net8.0/ref/
obj/Debug/net8.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

`Docs/` contains this document. `bin/` and `obj/` are generated test output, localized test-platform resources, runtime assets, and reference assemblies. `net8.0` leaves are legacy/generated artifacts; the project currently targets `net10.0`.

The project has no test-source subfolders. Its source-owned root files are:

- `NatsActorEventListenerTests.cs`
- `NatsActorSpscRingBufferTests.cs`
- `NatsJSDurableReplayQueueIntegrationTests.cs`
- `TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests.csproj`

## Event-listener coverage

`NatsActorEventListenerTests` covers constructor validation, initial/running/stopped state, message count, listener and event-map validation, the one-mailbox constraint, null handlers, idempotent start/stop, repeated lifecycle cycles, concurrent start/stop, and live handler invocation.

The options point to `nats://localhost:4222`; startup and message-delivery cases therefore require a running Core NATS server. NSubstitute is used for options, logger, and handlers, not for the transport connection.

## SPSC ring-buffer coverage

`NatsActorSpscRingBufferTests` validates capacity rules, initial state, single-thread flow, cancellation when full/empty, producer/consumer unblock behavior, concurrent processing of all messages, small-capacity waiter hand-offs without lost wake-ups, and terminal disposal behavior. These tests are in-process concurrency tests and do not require NATS.

## Durable JetStream coverage

`NatsJSDurableReplayQueueIntegrationTests` uses `IFM_NATS_URL` when set and otherwise connects to `nats://localhost:4222`. It creates unique projector, stream, subject, and consumer names for each test and removes its process/replay streams during async cleanup.

The tests verify:

- Process consumers use explicit ACK with unlimited redelivery.
- Publishing the same event twice is stored and processed once through JetStream message IDs.
- A failed replay publish causes process redelivery and eventually completes the handoff.
- A process message published before transport disposal is recovered after queue restart.

A decorator around the real transport injects the first replay-publish failure while all other operations use live JetStream.

## Environment requirements

Run a disposable NATS server with JetStream enabled. The configured account must allow stream and consumer creation, inspection, publication, ACK/NAK, and deletion. Do not point these tests at shared production streams.

```powershell
$env:IFM_NATS_URL = 'nats://localhost:4222'
dotnet test TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests/TomasAI.IFM.Framework.Messaging.Nats.IntegratedTests.csproj --configuration Debug
```

The suite uses ten-second durable-queue timeouts and small lifecycle delays. A slow or remote server can cause timing-sensitive failures.

## Current coverage boundaries

The integrated project does not comprehensively exercise Core actor producer request/reply, striped `NatsActorConsumer`, JetStream actor consumer routing, serializer failure behavior, `NatsActorMessageRingBuffer`, or multi-account/cluster topology. Add focused scenarios when changing those paths.

## Safe extension points

Use unique resource names and guaranteed async cleanup for every live JetStream scenario. Keep transport-free concurrency cases independent of server availability, and classify all live-server tests with the Integration trait.
