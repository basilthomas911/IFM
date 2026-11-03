# Shared DatabaseBackup implementation

This folder belongs to the **Domain.SystemAdmin** project. It contains implementation reused by the DatabaseBackup concerns:

| Folder | Responsibility |
| --- | --- |
| `Model` | Immutable decision results, source-event construction, lifecycle guards and receipt validation |
| `Translation` | Translation from service observations into application commands |
| `EventHandling` | Common observation delivery and acknowledgement handling |
| `Query` | Common typed query replies |
| `State` | Common replay and receipt-deduplication mechanics for concern-owned state |
| `Persistence` | Transaction and execution-outbox mechanics shared by concern repositories |
| `Projection` | Projection worker mechanics and complete read-model rebuilding |
| `Validation` | Common ingress validation |
| `Legacy` | Historical combined replay/testing view; never owned by a runtime actor |

These helpers may depend on domain state and actor services. Concern-specific command decisions and authoritative state remain with their owner; each concern supplies its own actor, state, repository and projector.

Shared wire contracts live separately in `TomasAI.IFM.Application.Contracts.SystemAdmin/DatabaseBackup`, retaining the existing `TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup` namespace and persisted identities. The contracts project must not reference the domain implementation. A shared namespace prefix does not make these projects interchangeable.
