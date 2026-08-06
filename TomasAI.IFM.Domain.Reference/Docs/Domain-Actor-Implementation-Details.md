# Reference Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.Reference` implements actor pipelines for economic calendars and lookup types, plus aggregate reference queries and a lookup actor service.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.Reference/`.

```text
Docs/
EconomicCalendar/Command/Actor/
EconomicCalendar/Command/Exceptions/
EconomicCalendar/Command/State/
EconomicCalendar/Command/Validation/
EconomicCalendar/Event/Actor/
EconomicCalendar/Query/Actor/
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

- `EconomicCalendar/` contains command, event, and query actors. Command state, validation, and exceptions support event-sourced calendar changes.
- `LookupType/` has the equivalent pipeline for lookup metadata.
- `Query/Actor/` contains the aggregate `ReferenceQueryActor`; `Query/Api/` provides storage-backed reference reads.
- `Services/` contains `ReferenceLookupActorService`, which combines actor messaging with blackboard-backed lookup access.
- `Docs/` contains this document; `ReferenceActorAssembly` marks the assembly for scanning.

## Implemented actors

Economic Calendar and Lookup Type each provide command, event, and query actors. `ReferenceQueryActor` provides cross-reference queries, while `ActorReferenceQueryApi` performs database access. The Lookup Type event implementation is held in a file currently named `EconomicCalendarEventActor.cs`, although the declared class and mailbox are `LookupTypeEventActor` and `LookupTypeEvent`.

## Processing model

Commands validate and update event-sourced state, repositories persist changes, and event actors receive published changes. Query actors separate reads from write state. The lookup service supplies a higher-level interface for resolving reference values through actor and blackboard services.

## Extension points

Add a new reference entity as a separate command/event/query vertical. Keep common cross-entity reads in the root `Query` branch and orchestration in `Services`. Preserve class/file naming alignment when touching the Lookup Type event actor.

## Optimization and graceful cancellation status

The 2026-08-05 root-to-leaf pass is documented in `Domain-Actor-Optimization-Details.md`. Empty Economic Calendar and Lookup Type event actors remain intentional default publication targets. Calendar import streams use the cumulative `EconomicCalendarsImportedEvent` batch snapshot, while singular calendar streams continue to use `EconomicCalendarAddedEvent`.

The coordinated solution-wide cancellation pass has reached the Reference storage boundary. Token-aware overloads now cover all Reference reads, projection-fence validation, month-bucket economic-calendar fan-out, canonical streaming fallback, scheduled-job projection reads, and external economic-calendar file parsing.

Seed allocation is intentionally treated as a mutation even though it is exposed through a query API. Cancellation is honored before each compare-and-set submission. Once a reservation is submitted, it resolves without caller cancellation so an allocated ID is never reported as safely abandoned.

Reference query actors, handlers, and the direct in-process query API remain the next part of this bounded-context migration; callers do not yet have end-to-end Reference cancellation until that layer is complete.
