# System Administration Domain Actor Implementation

## Purpose

`TomasAI.IFM.Domain.SystemAdmin` implements administrative command, event, and query actors, including database-name discovery.

## Root-to-leaf directory inventory

Paths are relative to `TomasAI.IFM.Domain.SystemAdmin/`.

```text
Command/Actor/
Command/Model/
Command/State/
Docs/
Event/Actor/
Query/Actor/
Query/Api/
bin/Debug/net10.0/runtimes/win-x64/native/
bin/Release/net10.0/runtimes/win-x64/native/
obj/Debug/net10.0/ref/
obj/Debug/net10.0/refint/
obj/Release/net10.0/ref/
obj/Release/net10.0/refint/
```

Every leaf path includes all parent folders. `bin/` and `obj/` are generated build trees.

## Folder responsibilities

- `Command/Actor/` contains `SystemAdminCommandActor` and its command routing.
- `Command/Model/` contains administrative write-side data.
- `Command/State/` contains `SystemAdminCommandState` and its event-source repository.
- `Event/Actor/` contains `SystemAdminEventActor` for administrative events.
- `Query/Actor/` contains `SystemAdminQueryActor`; its current mailbox name is `DatabaseNamesQuery`.
- `Query/Api/` contains the query API used to obtain administration data.
- `Docs/` contains this document.
- The root `SystemAdminActorAssembly` marker supports actor discovery.

## Processing model

The command actor follows the shared event-sourced lifecycle: parse, validate, restore state, dispatch, persist changes, publish events, and report failures. The event actor handles published administration events. The query actor delegates read operations to `ActorSystemAdminQueryApi`, keeping reads separate from command state.

## Extension points

Place new administrative writes beneath `Command`, notifications beneath `Event`, and read-only operations beneath `Query`. Update mailbox verb maps and shared contracts together so subjects remain routable.
