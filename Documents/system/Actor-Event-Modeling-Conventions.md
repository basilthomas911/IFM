# Actor Event Modeling Conventions

**Status:** Initial high-level convention  
**Applies to:** Domain command actors, their event-sourced states, event projectors, and public lifecycle events

## 1. Intent is a concrete command

An externally meaningful operation has one named command contract, one stable actor verb, and one mapped command handler. A command that accepts an operation enum and branches into unrelated lifecycle actions is not a substitute for those contracts. An actor may receive several commands belonging to one cohesive domain owner, but each command has an explicit entry in its parse, validation, and receive maps.

The command contract carries the identity, expected generation, operator, reason, and bounded deadline needed to validate and audit its own intent. A caller may expose a convenience method that selects a concrete command, but no generic operation-envelope command crosses the actor boundary. Unsupported intent is not advertised as a successful operation.

## 2. One event family per operation

Each operation records its accepted intent or completed domain decision in a private source event and has separate public Complete and Fail event contracts. A cohesive operation family may share one private source-event schema when that event carries an explicit operation kind and all operation-specific outcome fields; the shared source schema is not a generic public command or public terminal event. A Complete event means the operation reached its defined success condition; a Fail event contains a stable failure classification and reason. A command reply reports only command acceptance or failure and its command ID. It is not a substitute for the operation's projected lifecycle outcome.

Concrete commands and events own complete, permanent MessagePack schemas. New fields append keys. Published actor names, verbs, and keys are not silently repurposed. Contracts live in the owning subdomain's mirrored Shared hierarchy, with one command per file and one event family per file.

## 3. Event-source and projector order

For an event-sourced CommandActor, the standard path is parse, validate, load state, execute the mapped handler, apply a private event to state, commit pending events, then submit committed events to the command-owned projector. The handler does not publish a public outcome event directly. The projector applies the read-model effect and emits the matching Complete or Fail event.

Disabling a projector's durable replay queue does **not** disable source-event persistence. A non-durable descriptor receives the committed source event through a transient queue. It must be selected deliberately for an ephemeral action and documented as a delivery tradeoff. A durable descriptor needs idempotent, generation-fenced effects before replay is safe.

## 4. Authoritative state belongs to the CommandActor

Only a CommandActor owns and changes authoritative actor state. Its state belongs under that actor's `Command/State` folder. A mapped command handler makes a domain decision and applies a private event; the state model's handler for that event performs the state transition. The event handler may use an actor-owned `Command/Model` for pure computation, but a model must not mutate state, persist data, publish messages, or perform external effects.

An EventActor that receives an event requiring an authoritative state change sends a concrete command to the owning CommandActor. It must not update that state or its store directly, or invoke the state model on the CommandActor's behalf. The command then follows normal mailbox ordering, validation, event application, and persistence. Its resulting events must be distinct from the triggering event, with correlation and idempotency rules that prevent a command/event feedback loop.

QueryActors do not mutate authoritative state. An EventProjector may write a rebuildable read model after a source event is committed; that projection is not the CommandActor's authoritative state. Likewise, an independent operational service may retain observations for monitoring, but a resulting change to actor-owned state still requires a concrete command. Do not create `Event/State` or `Query/State` as an alternative authority. The detailed actor layout is defined in [Actor Implementation Conventions](Actor-Implementation-Conventions.md).

## 5. External side effects and uncertainty

A lifecycle operation that starts, stops, pauses, or restarts another actor is an external side effect relative to the command event log. Neither a command acknowledgement nor a transient projector queue proves that the effect completed. Complete is emitted only after the effect is observed. Fail is emitted for a known terminal failure. If a process stops between effect and durable outcome, the status is unknown until reconciliation; do not infer success or issue an unfenced repeat.

Operator authorization, target generation, and deadline are validated before the effect. An unsupported operation produces a typed failure and never a Complete event. Repeated or delayed commands are constrained by the durable command ID and target-generation fence.

## 6. Supervisor lifecycle operation families

The Supervisor defines separate commands and Complete/Fail event pairs for Pause, Drain, Resume, Stop, Restart, Quarantine, Retire, Recycle, and Acknowledge Incident. Similar low-level implementations do not erase the distinct business intents. The Stop operation remains unsupported until a safe implementation and qualification are approved; its command must fail explicitly rather than silently alias another operation.

Each concrete operation handler owns its authorization decision and invokes a named lifecycle capability. The lifecycle implementation may share deadline, serialization, validation, and exception containment internally, but it must not select the action through a generic operation-enum switch. A shared command helper may only construct/apply the already-decided private outcome event and return its acknowledgement; it does not authorize or execute an operation.

The actor-specific context exposes only the capabilities needed by the actor role. Command mutation authority is not available to health queries or event readers. Supervisor actor roles and their Shared contracts follow the owning subdomain hierarchy in [Actor Implementation Conventions](Actor-Implementation-Conventions.md).

The Recovery canary is a separate soft-recovery actor group. Its command validates the correlation-routed subject, applies a private source event to event-sourced state, and commits that event before its command-owned projector publishes the public accepted event. This database-backed path is used only after soft-recovery infrastructure qualification; the Databento-only hard-recovery loop never calls it.

The health action coordinator currently performs generation-fenced automatic restarts directly through `IActorSupervisor`, outside the operator-command actor and its Complete/Fail event pipeline. It owns those automatic actions and records them in the Supervisor operation store. Routing automatic health actions through NATS commands would change the recovery dependency and must be separately designed and qualified; the concrete operator-command refactor does not silently change that path.

## 7. Qualification

For every operation family, test contract serialization, exact route mapping, validation, authorization, generation fencing, command acknowledgement, committed source event, one projected Complete or Fail event, and duplicate command delivery. Test exceptions and cancellation at the pre-effect, post-effect, commit, and projector boundaries. A non-durable projector must be tested for the documented loss window; a durable one must be tested for idempotent replay.
