# System Administration Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.SystemAdmin` implements administrative command, event, and query actors, including database-name discovery.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.SystemAdmin/`.

```text
Command/Actor/
Command/State/
Docs/
Event/Actor/
Query/Actor/
Query/Api/
```

Generated `bin/` and `obj/` trees are intentionally omitted.

## Folder responsibilities

- `Command/Actor/` contains `SystemAdminCommandActor` and its command routing.
- `Command/State/` contains `SystemAdminCommandState` and its event-source repository.
- `Event/Actor/` contains the intentionally empty `SystemAdminEventActor`, retained as the domain-default publication target.
- `Query/Actor/` contains `SystemAdminQueryActor`; its current mailbox name is `DatabaseNamesQuery`.
- `Query/Api/` contains the query API used to obtain administration data.
- `Docs/` contains this document and the recurring optimization report.
- The root `SystemAdminActorAssembly` marker supports actor discovery.

## Processing model

The command actor follows the shared event-sourced lifecycle: parse, validate, create state, dispatch, persist changes, publish events, and report failures. Backup execution does not depend on prior backup state, so it creates fresh state rather than reading and replaying the stream; persistence still appends every immutable event. The event actor is an intentional no-op default publication target. The query actor serves one cached immutable database-name snapshot, keeping reads separate from command state.

Command-audit persistence begins during parsing without synchronously blocking the actor. Validation awaits that same operation before command execution continues, preserving audit failure semantics.

## TODO: solution-wide cancellation

Do not add domain-local cancellation parameters in isolation. After all root-domain optimization passes, implement one coordinated change that propagates supervisor cancellation through the actor pipeline, APIs, repositories, storage providers, messaging, timers, and external I/O. Define graceful-stop semantics around event persistence and denormalization before changing these contracts.

## Extension points

Place new administrative writes beneath `Command`, notifications beneath `Event`, and read-only operations beneath `Query`. Update mailbox verb maps and shared contracts together so subjects remain routable.
