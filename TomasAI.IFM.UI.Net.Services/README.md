# UI service conventions

`TomasAI.IFM.UI.Net.Services` is the backend-facing boundary for presentation workflows. ViewModels use named services from `IUiServiceCatalog`; Services map display-specific state into UI-owned records and keep command/query/event execution out of `UI.Net.Models`.

The catalog intentionally has no generic `Get<T>` or `Resolve<T>` method. Rich domain aggregates that existing desktop editors modify and submit are governed by `UI.Net/Docs/UI-Backend-Boundary-DTOs.md`; new display-only state belongs in `UI.Net.Models` and is mapped here.

## Operations and errors

- Display-oriented queries and commands return `ValueTask<UiOperationResult<TValue>>` or `ValueTask<UiOperationResult>`.
- Backend `ServiceResult<T>` values and transport exceptions do not cross into ViewModels. Reviewed rich editor aggregates may cross as the boundary DTOs listed in the inventory.
- Stable backend failure codes and safe messages map to `UiOperationError`.
- ViewModels may call `RequireValue()` when their workflow uses exception-based operation reporting; this throws `UiOperationException` with the mapped code.

## Cancellation

- Every public asynchronous service operation accepts an optional `CancellationToken`.
- The token is checked before dispatch. It is also forwarded whenever the typed backend API supports cancellation.
- Cancellation remains `OperationCanceledException`; it is not converted into a service failure.

## Mapping

- Backend-to-UI mapping belongs in the concrete service implementation.
- UI records contain only state needed by presentation workflows. A shared domain enum may be retained as a value when creating another UI enum would add no semantic boundary.
- ViewModels own selection, editing, correlation, status messages, and other presentation coordination.

## Event subscriptions

- A listener is exposed as `IUiEventSubscription`; the consumer owns it with `await using` or `DisposeAsync()`.
- Start, stop, and disposal are idempotent and serialized.
- A screen-owned service containing a listener consumer is registered transiently so separate screens receive separate listener ownership. Application-lifetime catalog services delegate to singleton consumers whose start/stop lifecycle is owned by the application ViewModel.
- Services map backend events to transport-neutral UI notifications before invoking a ViewModel handler.

## Public API documentation

Changed public service interfaces, methods, properties, and UI records require XML summaries. Parameters with presentation semantics are documented at the interface or record declaration; concrete implementations use `<inheritdoc />`.
