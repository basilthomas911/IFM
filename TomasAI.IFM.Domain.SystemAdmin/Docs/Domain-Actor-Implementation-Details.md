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

## Graceful cancellation status

The coordinated solution-wide cancellation pass now covers SystemAdmin. Command-audit observation and the event-sourced command pipeline honor the supervisor token before the durable commit boundary. The query actor, its database-name resolver, and the direct in-process query API also accept cancellation; a canceled query sends no stale actor reply and remains an `OperationCanceledException` rather than a normal failure result.

Database-name resolution is an immutable in-memory lookup, so there is no lower storage operation to cancel. The no-token API continues to reuse its cached completed task without adding hot-path allocation.

## Extension points

Place new administrative writes beneath `Command`, notifications beneath `Event`, and read-only operations beneath `Query`. Update mailbox verb maps and shared contracts together so subjects remain routable.
