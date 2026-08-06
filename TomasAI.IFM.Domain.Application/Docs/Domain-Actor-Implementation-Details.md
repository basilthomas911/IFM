# Application Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.Application` implements the event-sourced actors that control the application lifecycle. The project targets .NET 10 and depends on the blackboard, storage, shared actor infrastructure, and application contracts projects.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.Application/`. Every leaf path below includes all of its parent folders.

```text
Command/Actor/
Command/Handlers/
Command/State/
Docs/
Event/Actor/
bin/Debug/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
```

`bin/` and `obj/` are generated output and intermediate trees for the current .NET 10 Debug build.

## Folder responsibilities

- `Command/Actor/` contains `ApplicationCommandActor`, the NATS command mailbox for application start and shutdown commands.
- `Command/Handlers/` converts `StartApplicationCommand` and `ShutdownApplicationCommand` into application lifecycle events and applies them to state.
- `Command/State/` contains the event-sourced `ApplicationCommandState` and `ApplicationStateRepository` used to restore snapshots, persist changes, and publish denormalized events.
- `Event/Actor/` contains `ApplicationEventActor`, the lifecycle event consumer.
- `Docs/` contains this implementation record.
- The project root contains the project definition and `ApplicationActorAssembly`, an assembly marker used for discovery and registration.

## Actor flow

`ApplicationCommandActor` owns the `ApplicationCommand` mailbox. On startup it resolves its state repository. For each message it validates the actor type, actor name, and command verb; deserializes the payload; records the command in event-source storage; validates the command identifier; loads state; invokes the appropriate handler; saves state and publishes resulting events; and converts failures into command exception events.

The state accepts `ApplicationStartupEvent` and `ApplicationShutdownEvent`. The repository restores from an application-startup snapshot and posts both lifecycle event types through the actor service after persistence.

## Event actor behavior

`ApplicationEventActor` owns the `ApplicationEvent` mailbox and accepts startup and shutdown lifecycle events. These events are broadcast notifications whose side effects belong to registered external event listeners, so the domain event actor validates and acknowledges them without introducing another state store or synchronization boundary.

## Extension points

Add a lifecycle command by updating its shared contract, command parse/receive/validation maps, handler, state event application, and repository denormalization. Add an event consumer by aligning the event actor mailbox name and populating both event maps.

## Solution-wide graceful cancellation

The solution-wide cancellation phase is now in progress. Application command validation and state replay honor the actor token through the event-source repository and storage providers. Once event persistence begins, save and required publication complete without caller cancellation so a committed command cannot be reported ambiguously as canceled.

The API host awaits actor startup without `Task.Run(...).Wait()` and invokes the supervisor's idempotent shutdown after HTTP intake stops. The supervisor then stops message consumers, drains accepted mailbox work, and stops actor-owned producers. See `Docs/Solution-Wide-Graceful-Cancellation-Implementation-Details.md` for the complete semantics, implemented coverage, exclusions, and remaining query/read-model work.
