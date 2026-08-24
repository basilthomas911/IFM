# UI service and model boundary decision record

| Item | Decision |
| --- | --- |
| Status | Accepted for incremental Development migration |
| Date | 2026-08-24 |
| Gate | UIR-0 |
| Scope | `TomasAI.IFM.UI.Net` presentation project family |

## Context

`TomasAI.IFM.UI.Net.Models` is named as a presentation-model assembly, but most of its classes are backend adapters. They own typed command/query APIs, NATS event consumers, transport-result handling, and listener lifecycle. ViewModels resolve those classes through the application root's generic `GetModel<TModel>()` service locator and frequently expose backend read models directly as screen state.

The UIR-0 inventory establishes the current baseline:

- 37 C# source files in `TomasAI.IFM.UI.Net.Models`, including two contracts;
- 32 classes derived from `BaseModel`;
- 150 `GetModel<TModel>()` call sites across 25 ViewModel files;
- 31 ViewModel files that reference backend `*ReadModel` contracts;
- 14 project references from `TomasAI.IFM.UI.Net.Models`, including messaging, domain-shared, application-shared, and event-consumer assemblies.

This makes backend transport concerns appear to be UI data models, permits screen code to depend on backend contract shapes, and hides dependencies behind a service locator. It also makes it difficult to reuse presentation models independently of the current NATS client and event-consumer implementation.

## Decision

Introduce `TomasAI.IFM.UI.Net.Services` as the presentation-facing application service boundary. Migrate the current command, query, event, and subscription adapters from `TomasAI.IFM.UI.Net.Models` into domain-oriented services in that assembly.

The target dependency direction is:

```text
UI.Net -> Views -> ViewModels -> Services -> backend client/event infrastructure
                         |
                         +------> Models
Views --------------------------> Models
```

The responsibilities are:

| Layer | Responsibility |
| --- | --- |
| Models | UI-owned immutable data, editable state, selection state, validation state, and display policies used by ViewModels and Views. |
| Services | Typed backend commands and queries, event subscriptions, transport error translation, cancellation, and mapping from backend contracts to UI models. |
| ViewModels | Screen state and user workflow orchestration expressed through explicit injected service interfaces. |
| Views | Rendering, control binding, UI-thread dispatch, and collection of user input. |
| UI.Net | Composition root, process lifetime, runtime configuration, and executable assets. |

Backend-to-UI mapping belongs at the Services boundary. A ViewModel may temporarily retain an existing backend read-model type during its assigned migration gate, but no newly migrated workflow may introduce a new backend contract dependency into ViewModels or Models.

Operations are organized by business domain and presentation workflow, not by transport verb. A domain folder may contain service interfaces, service implementations, mappings, subscriptions, and operation-specific UI models. ViewModels remain the owner of presentation workflow; Services do not navigate windows or manipulate controls.

## Dependency rules

The completed architecture must enforce these rules:

- Models do not reference NATS, backend `ServiceApi` contracts, domain command/query/event APIs, or `TomasAI.IFM.UI.EventConsumer`.
- ViewModels depend on explicit service interfaces and UI models; they do not call `IAppRoot.GetModel<TModel>()`.
- Services may reference backend shared contracts, typed client APIs, messaging abstractions, and event consumers required to implement the UI-facing interfaces.
- Views do not invoke backend services directly.
- The composition root registers service interfaces explicitly or through a narrowly defined convention and verifies the container.
- Event subscription ownership is explicit, cancellable, idempotent, and disposed by the workflow or application lifetime that created it.
- Public service and model types receive XML documentation as they are migrated.

## Compatibility strategy

The migration is vertical and incremental. Each gate moves one coherent domain slice, updates its ViewModels and tests, and removes only the superseded adapter types. Existing backend actor APIs, event-consumer implementations, and backend contracts are not redesigned by this refactor.

Temporary references from ViewModels to existing Models adapters are allowed only for domains whose gate has not started. Compatibility shims must be local to the active gate, documented, and removed before that gate closes.

## Consequences

The refactor creates explicit, testable dependencies and lets UI models evolve for presentation needs without changing backend wire contracts. It also makes transport and subscription lifecycles visible. During migration, the project family will briefly contain both the legacy Models adapters and the new Services boundary, so architecture assertions must distinguish migrated domains until UIR-12 removes the legacy mechanism.

## Non-goals

UIR does not change backend actor base classes, NATS message contracts, persistence, the WinForms-to-WPF migration, or production deployment. It does not replace the existing accepted UI process gates. Those behaviors remain protected by the current unit and system-test suites.
