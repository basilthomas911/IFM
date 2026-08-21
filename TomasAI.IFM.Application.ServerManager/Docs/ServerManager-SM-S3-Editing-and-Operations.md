# IFM Server Manager SM-S3 Editing and Operations Gate

**Status:** Complete

**Date:** 2026-08-20

SM-S3 changes the Scheduler Host pipe from a read-only dashboard boundary into a local, audited control plane while
retaining PostgreSQL as the only scheduling authority.

## Delivered behavior

- Structured validation for cron, interval, and one-time definitions with ten-fire preview.
- Explicit timezone, misfire, runtime, and retention validation.
- Create, update, enable/disable, and soft-delete operations; new schedules always start disabled.
- Optimistic concurrency and PostgreSQL request receipts for duplicate request IDs.
- Auditing with the pipe-authenticated Windows identity.
- Durable outbox dispatch for manual runs and explicit retries.
- Targeted cancellation through the task control pipe before Job Object fallback.
- Independently bounded, continuously drained stdout/stderr with redacted paging and retention.
- Dependency probes before launch.
- WPF editing, preview, enable/disable, Run now, cancel, retry, and bounded output viewing.

## Safety invariants

- WPF never accesses scheduler tables directly.
- Operator input cannot select an arbitrary executable or shell command.
- Market-sensitive schedules require `DoNothing` misfire behavior.
- Enabling requires a deployed/hash-valid executable and an operator reason.
- Deleting requires a disabled, unchanged definition and preserves run history.
- `Abandoned` work is not retryable because its business outcome is ambiguous.
- Retention never selects active or abandoned evidence.

The automated suites cover validation, preview, concurrency, replay, audit, output bounds/redaction, cooperative and
forced cancellation, retention selection, overlap prevention, restart recovery, pipe operations, and existing
API/UI supervision.
