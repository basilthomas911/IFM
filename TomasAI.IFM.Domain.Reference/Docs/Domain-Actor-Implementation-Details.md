# Reference Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.Reference` implements the lookup-type actor pipeline, aggregate reference queries, and the reference lookup actor service. Economic-calendar ownership moved to `TomasAI.IFM.Domain.MarketData`.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.Reference/`.

```text
Docs/
LookupType/Command/Actor/
LookupType/Command/Exceptions/
LookupType/Command/State/
LookupType/Command/Validation/
LookupType/Event/Actor/
LookupType/Query/Actor/
Query/Actor/
Query/Api/
Services/
bin/Debug/net10.0/runtimes/win-x64/native/
bin/Debug/net8.0/
bin/Release/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Debug/net8.0/ref/
obj/Debug/net8.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

Every leaf includes its parent folders. `bin/` and `obj/` are generated; `net8.0` leaves are legacy artifacts for this .NET 10 project.

## Folder responsibilities

- `LookupType/` has the equivalent pipeline for lookup metadata.
- `Query/Actor/` contains the aggregate `ReferenceQueryActor`; `Query/Api/` provides storage-backed reference reads.
- `Services/` contains `ReferenceLookupActorService`, which combines actor messaging with blackboard-backed lookup access.
- `Docs/` contains this document; `ReferenceActorAssembly` marks the assembly for scanning.

## Implemented actors

Lookup Type provides command, event, and query actors. `ReferenceQueryActor` provides cross-reference queries, while `ActorReferenceQueryApi` performs database access.

## Processing model

Commands validate and update event-sourced state, repositories persist changes, and event actors receive published changes. Query actors separate reads from write state. The lookup service supplies a higher-level interface for resolving reference values through actor and blackboard services.

## Extension points

Add a new reference entity as a separate command/event/query vertical. Keep common cross-entity reads in the root `Query` branch and orchestration in `Services`. Preserve class/file naming alignment when touching the Lookup Type event actor.

## Optimization and graceful cancellation status

The 2026-08-05 root-to-leaf pass is documented in `Domain-Actor-Optimization-Details.md`. The Lookup Type event actor remains an intentional default publication target. The historical calendar optimizations and their benchmarks moved with the feature to MarketData.

The coordinated solution-wide cancellation pass covers the remaining Reference query path, its handlers, direct in-process API operations, Reference storage reads, projection-fence validation, and scheduled-job projection reads. Canceled actor reads do not publish stale replies, and direct API cancellation remains an `OperationCanceledException` rather than a normal failure result.

Seed allocation is intentionally treated as a mutation even though it is exposed through a query API. Cancellation is honored before each compare-and-set submission. Once a reservation is submitted, it resolves without caller cancellation so an allocated ID is never reported as safely abandoned.

The default futures-contract and strike-price definition queries continue to start their independent lookup reads together. The cancellation change therefore preserves their existing parallel latency behavior while allowing one supervisor token to stop the entire fan-out.
