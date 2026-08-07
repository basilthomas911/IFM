# Option Pricer Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.OptionPricer` implements option-pricing actor APIs and the spread-distribution and spread-distribution-job actor pipelines.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.OptionPricer/`.

```text
Command/Api/
Docs/
Query/Api/
SpreadDistribution/Command/Actor/
SpreadDistribution/Command/State/
SpreadDistribution/Event/Actor/
SpreadDistribution/Job/Command/Actor/
SpreadDistribution/Job/Command/Exceptions/
SpreadDistribution/Job/Command/State/
SpreadDistribution/Job/Command/Validation/
SpreadDistribution/Job/Event/Actor/
SpreadDistribution/Job/Event/Extensions/
SpreadDistribution/Job/Services/Contracts/
SpreadDistribution/Query/Actor/
bin/Debug/net10.0/runtimes/win-x64/native/
bin/Release/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

Every leaf includes its parent folders. `bin/` and `obj/` are generated build trees.

## Folder responsibilities

- `Command/Api/` provides the actor-backed option-pricer command API and factory.
- `Query/Api/` provides storage-backed pricing queries.
- `SpreadDistribution/Command/` contains the event-sourced command actor and state repository.
- `SpreadDistribution/Event/Actor/` consumes distribution events.
- `SpreadDistribution/Query/Actor/` serves distribution reads.
- `SpreadDistribution/Job/Command/` contains job command routing, state, validation, and job-specific exceptions.
- `SpreadDistribution/Job/Event/` contains job event handling and actor-context helpers.
- `SpreadDistribution/Job/Services/Contracts/` defines internal job service boundaries; `Services/` contains their implementations.
- `Docs/` contains this document and the recurring [domain actor optimization report](Domain-Actor-Optimization-Details.md); the root assembly marker supports discovery.

## Implemented actors

`SpreadDistributionCommandActor`, `SpreadDistributionEventActor`, and `SpreadDistributionQueryActor` implement the distribution lifecycle. `SpreadDistributionJobCommandActor` and `SpreadDistributionJobEventActor` coordinate longer-running distribution jobs. The command and query APIs expose those capabilities to other bounded contexts.

## Processing model

Distribution commands are parsed, validated, applied to event-sourced state, and persisted. Resulting events update downstream consumers. Job actors separate asynchronous job coordination from the distribution aggregate and use job services behind explicit contracts. Query actors/API read stored option-pricing results without mutating command state.

## Extension points

Keep pricing-domain state beneath `SpreadDistribution`; keep scheduling, progress, and retry behavior beneath `Job`. Add cross-domain request/response types to Shared projects and register new mailbox verbs in all corresponding maps.

## Graceful cancellation status

The coordinated solution-wide cancellation pass now covers the complete OptionPricer query/read-model path. `SpreadDistributionQueryActor`, its handler, all three direct in-process `IActorOptionPricerQueryApi` operations, the OptionPricer storage contract, and the concrete database operations accept and propagate the worker token. Canceled actor reads do not publish stale replies, and direct API cancellation remains an `OperationCanceledException` rather than being converted into a normal service failure.

The event-sourced command path already propagates cancellation through command-audit observation and state replay. It retains the solution-wide commit boundary: once event persistence begins, required denormalization and publication complete without caller cancellation so a durable mutation is never reported as safely abandoned.

The current OptionPricer optimization decisions, top-ten findings, and BenchmarkDotNet baseline are recorded in [Domain-Actor-Optimization-Details.md](Domain-Actor-Optimization-Details.md).
