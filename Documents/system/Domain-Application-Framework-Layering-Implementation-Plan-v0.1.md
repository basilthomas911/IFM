# Domain / Application / Framework layering implementation plan

Date: 2026-10-25
Status: Implemented and qualified offline on 2026-10-25. See [verification report](Domain-Application-Framework-Layering-Verification-v0.1.md) for evidence, deployment requirements and remaining live acceptance.

## 1. Objective and boundaries

Implement the dependency direction:

```text
Domain → Application → Framework
            ↓              ↓
       neutral Shared contracts
```

All layers may use neutral Shared contracts. Domain implementation code must use Application capabilities and contracts, with no direct or transitive use of Framework implementation types. Reusable Application libraries must not depend on Domain. Framework and neutral Shared must not depend on higher layers.

Executable composition roots may wire Domain, Application and Framework together. Identify these explicitly; do not exempt reusable libraries just because a host currently consumes them. Ordinary platform/runtime dependencies are unaffected.

Preserve business behavior, actor identities, transport subjects and client contracts. No new business actors, new processes, UI migration, pricing changes, backtesting implementation, or production event-log rewrite are required for this work.

## 2. Inventory and completed prerequisite

Use the [design](Domain-Application-Framework-Layering-Design-v0.1.md), [project inventory](Domain-Application-Framework-Layering-Project-Inventory.csv), and [source inventory](Domain-Application-Framework-Layering-Source-Inventory.csv) as the baseline. Refresh them before implementation because the working tree may have changed.

The baseline identifies 284 relocation candidates, four direct Domain-to-Framework edges, 50 Application-to-Domain references across 11 projects, and nine Framework upward edges across eight projects. Candidate destinations require signature/dependency verification; these are not promises that every file moves unchanged.

The [event identity compatibility step](Event-Type-Identity-Compatibility-Implementation-v0.1.md) is complete: old CLR identities and explicitly registered stable/historical aliases can be read, including typed snapshot selection. Its 100 focused tests passed. Only three initial production stable read aliases are registered. Writers remain unchanged, and no contract classes have moved.

## 3. Implementation phases

### Phase 1 - Preserve persisted write identities and qualify compatibility

This is the first implementation task and a mandatory gate before any contract, namespace or assembly relocation.

1. Inventory every durable writer that derives type identities from CLR types: event-log metadata and appenders, snapshots, command audit, projector outbox, durable replay envelopes and relevant maintenance tools. Record which paths persist event type names versus other contract identities.
2. Introduce explicit persisted write-identity selection alongside the existing read resolver. Register the exact current legacy identity for each affected contract before moving it; retain current behavior for unaffected types. Reject conflicting mappings and accidental identity rebinding.
3. Route all applicable durable writers through this selection without changing payload serialization, compression, transport subjects, event IDs, deduplication keys or deterministic message IDs. Keep stable-name writes disabled.
4. Initialize the identity catalog in every relevant writer/reader process. Keep the generic mechanism independent of business contract assemblies and prevent new dependency cycles.
5. Freeze existing names and payloads in fixtures. Test normal writes still emit the exact legacy identity, and use an isolated fixture contract relocated to a different assembly/namespace to prove the configured legacy write identity survives a real CLR identity change.
6. Read those newly written records through the existing compatibility reader using legacy, relocated and registered stable identities. Verify full reconstructed state, latest snapshot/range selection, plain/LZ4 bytes, outbox/replay behavior and unchanged deterministic identities.
7. Verify old-reader compatibility for records written with preserved legacy identities using the original contract assemblies. Document deployment/rollback requirements and any unavailable historical fixtures or stores. Preserving a name alone does not establish payload-schema compatibility.
8. Publish the writer inventory, mappings and focused test results. Extend the catalog and rerun contract-specific qualification before each later move.

**Exit:** all inventoried applicable writers use the identity policy; unchanged contracts retain identical stored names and payloads; an actual test-only relocation preserves its legacy write identity and replays correctly; conflicts, unknown identities and corrupt payloads fail as designed. No production classes move until this gate passes. The existing 100 reader tests must continue to pass.

### Phase 2 - Freeze the baseline and enforce the intended graph

1. Refresh evaluated project references, Compile ownership, persisted-type candidates, public APIs and actual bound symbol usage.
2. Classify every project as Domain implementation, reusable Application, Framework, neutral Shared, contract library, or executable composition root.
3. Add architecture tests for project cycles and forbidden reference direction. Track existing violations explicitly and prevent new ones during staged migration; remove migration exceptions as each violation is resolved.
4. Add compiled-symbol/public-signature checks so transitive Framework, NATS, database and provider types cannot leak through Application ports into Domain.
5. Record current build/test results, existing actor discovery and relevant contract bytes before edits.

**Exit:** repeatable baseline and tests detecting both direct and transitive violations. No blanket allowlists or disabled gates.

### Phase 3 - Extract neutral Shared contracts and isolate transport

1. Create `TomasAI.IFM.Shared.Contracts` for neutral identities, envelopes, minimal value types and transport-independent message ports. Compute their complete signature closure before moving them.
2. Keep transport/library implementation types outside that assembly. Move NATS-specific carriers and helpers into the Framework messaging adapter and map them at the boundary.
3. Update Framework consumers to reference the neutral contract assembly rather than the monolithic Shared runtime.
4. Separate DTO serialization shape from runtime behavior where necessary, including read-model methods that invoke event codecs.
5. Preserve MessagePack keys, enum values, subjects, IDs and native layouts. Apply the persisted-identity gate below to every affected serialized type, including nested payload types.

**Exit:** neutral contracts compile independently; Framework does not need actor/event-sourcing runtime ownership in Shared. No Shared-to-Application forwarding cycle is introduced.

### Phase 4 - Correct business-contract and adapter ownership

1. Extract Application-facing business commands, queries, events and DTOs from `Domain.*.Shared` into explicitly owned Application contract libraries. Preserve namespaces where practical; namespace cleanup is not a prerequisite for assembly extraction.
2. Move provider-neutral inputs/outputs needed by Framework into neutral Shared; perform business mappings in Application.
3. Remove the nine Framework upward edges, including the backup-provider inversion. Move provider selection/orchestration upward or introduce a neutral port as appropriate.
4. Replace reusable Application references to Domain with Application contracts/capabilities. Keep actual actor implementation registration in identified composition roots.
5. Do not move every Domain.Shared class blindly: retain pure Domain behavior in Domain and separate mixed behavior/DTO types deliberately.

**Exit:** Framework has no Application/Domain dependency; reusable Application contracts do not depend on Domain. Move prerequisite contract batches before runtime batches that require them.

### Phase 5 - Move generic actor and event-sourcing runtime into Application

1. Create `TomasAI.IFM.Application.EventModelActor` and `TomasAI.IFM.Application.EventSourcing` with only the dependencies permitted by their role.
2. Move generic actor base/runtime types, supervisor and scheduling/mailbox coordination, factories, generic repositories, audit/deduplication and replay orchestration in compilable batches.
3. Move Shared.Domain decorator pipelines into Application ownership. Inject business validators and error converters through ports instead of referencing Domain implementations.
4. Split `Domain.EventSourcing`: move generic infrastructure; place specialized projections/migration adapters according to their actual contract ownership.
5. Replace business-name strings in generic recovery policy with explicit capabilities registered by the composition root. Preserve existing reset eligibility and durable-operation safeguards.
6. Update DI, assembly scanning, actor discovery, reflection lookups and all consumers. Do not introduce a permanent Shared facade pointing back into Application.

**Exit:** Domain actor implementations use Application runtime; generic runtime no longer lives in Shared/Domain; actor identities and lifecycle behavior remain unchanged.

### Phase 6 - Finish Domain isolation

1. Remove the four explicit Domain-to-Framework references and all remaining Framework symbol use through transitive dependencies.
2. Replace raw database/provider/NATS types in consumed Application APIs with Application capability ports and neutral results.
3. Replace Domain implementation-to-implementation calls with Application-facing capabilities where required, including Trade's Portfolio/BrokerAccount implementation dependencies.
4. Retire or reclassify obsolete Domain.Shared and Domain.EventSourcing projects once their responsibilities have moved. Update solution, build scripts, test references and documentation.
5. Make the final architecture gates strict: no remaining migration exceptions for forbidden edges or bound symbols.

**Exit:** Domain implementations depend on Application and neutral Shared; the declared graph and compiled API surface both satisfy the intended direction.

### Phase 7 - Qualify the complete stack and close the migration

1. Build supported hosts, libraries, managed clients and both UI variants. Verify DI/container registration and actor discovery.
2. Run historical replay, mixed-name latest-snapshot/range, expected-version concurrency, deduplication, transaction rollback, projection completion and outbox/replay tests.
3. Verify transport contract bytes, subjects, reply/cancellation behavior, reconnect and current native ABI boundaries.
4. Run offline behavior checks for Iron Condor metrics, ITI/EMA/Bollinger, risk, portfolio postings, health and console history.
5. With a configured session, verify trade loading, live updates, market lifecycle, bounded two-minute recovery, UI failure notification and configured API restart/restore. Identify separately any acceptance case requiring open markets; do not claim an offline substitute proves live delivery.
6. Verify both hybrid and standalone UIs against the same backend behavior. Leave UI cutover under user control.
7. Refresh inventory and publish the final evidence report. Remove obsolete implementation wrappers and reference exceptions; retain historical identity aliases while any stored data or supported backup can require them.

**Exit:** all final gates pass, with reproducible results and no unqualified runtime acceptance claims.

## 4. Mandatory persisted-identity gate for every move

Changing an event's assembly changes its `AssemblyQualifiedName`, even when its namespace is unchanged. Current writers derive identities from CLR types; therefore the completed reader work alone does not freeze future writes after a move.

Before each affected contract batch:

1. Inventory old identities in event metadata, snapshots, command audit, outbox, durable queues and supported backups. Use read-only inspection of configured stores when available; record any uninspected store explicitly.
2. Capture historical payload fixtures and expected reconstructed state. Cover nested serialized types and any custom resolver behavior.
3. Register explicit old and new identities against the relocated contract, preserving existing Portfolio payload compatibility. Do not guess by short class name. Qualify payload upcasters separately if field shape changes.
4. Use the write-identity policy implemented and qualified in Phase 1. Add the affected contract’s exact legacy identity before moving it, and verify its writers preserve that identity. Preserve legacy writes until a deliberate write cutover is approved.
5. Ensure every relevant host, queue consumer, projector and maintenance tool initializes the required registry before replay.
6. Test old and new names, plain/LZ4 bytes, full state reconstruction, latest snapshot/range selection, corruption failure, metadata and deterministic message IDs.
7. Confirm deployment/rollback compatibility. Old binaries cannot automatically resolve newly emitted stable IDs. Deploy read support first; keep existing write identities until rollback support is established.

**Stop the affected batch** if identities/payloads cannot be mapped unambiguously, a cycle is required, or historical state cannot be reconstructed. Do not rewrite production logs to conceal a failing test.

## 5. Work discipline and acceptance evidence

- Keep each phase/batch compilable and reviewable; run focused tests for changed boundaries before broad final qualification.
- Preserve unrelated working-tree changes and current hybrid UI source behavior.
- Use isolated databases/queues for destructive fixtures. No automatic production migrations, process resets or live fault injection as a side effect of file relocation.
- Record moved types, old/new assembly and namespace, aliases, writer policy, consumer registrations, tests and rollback notes per batch.
- Architecture acceptance: zero project cycles; zero Domain-to-Framework, reusable Application-to-Domain, Framework-upward or Shared-upward edges; zero forbidden implementation types in Domain's bound usage or consumed Application signatures.
- Runtime acceptance: historical state and current business results remain equivalent, supported hosts discover/register the same actor responsibilities, and both clients retain expected behavior.

## 6. Recommended execution order

Complete Phase 1 write-identity preservation and its compatibility tests first. Then complete Phase 2 architecture enforcement before Phase 3 neutral contract extraction. Resolve business-contract prerequisites before moving dependent generic runtime batches. Finish Domain isolation only after reverse dependencies have been removed. Qualify the whole stack before declaring the layering migration complete.

The existing event-reader compatibility work is a completed prerequisite, not a reason to skip per-contract historical replay qualification.
