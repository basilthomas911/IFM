# Regime Discovery Market Signal Interface MDSI-3 Historical Data Loader

Roll-Aware One-Year Historical Data Loader v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-3 - Roll-aware one-year historical data load` |
| Status | Complete |
| Date | 2026-08-25 |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Gate conclusion

MDSI-3 adds the durable data-loader actor surface and the application coordinator
that turns historical provider records into normalized, auditable Analytics
observations. The command actor persists its accepted request to the ACID event
log. Its conventional projector dispatches the committed request to the event
actor, while PostgreSQL owns operational checkpoints/manifests and ScyllaDB
owns immutable raw observations.

## 2. Actor topology

The implementation includes:

- shared record-struct identity, command, Requested/Completed/Failed events,
  diagnostics query, and provider-neutral parameter contracts;
- closed command/event/query contexts and extension properties;
- a concrete full-stream event-source state repository;
- command, event, and query actors; and
- a conventional command projector that dispatches only after the event-log
  commit.

No actor uses `Container.Resolve` for the new dependencies.

## 3. Acquisition, normalization, and restart behavior

The coordinator estimates before acquisition, records the provider job ID as
soon as a batch is submitted, downloads into an attempt-specific staging
directory, verifies immutable file metadata, and advances PostgreSQL
checkpoints. Request hashes exclude the attempt ID so an identical completed
request is reused without another provider job.

CME session normalization produces Daily observations using actual first/last
market-event ordering. ES continuation and VX front/second mappings remain
explicit and roll segments, missing sessions, conflicts, and file hashes are
retained in the audit manifest. Scylla writes use deterministic identities and
insert-if-absent semantics. Private replay receives bounded normalized batches.

## 4. Accepted qualification

The recorded one-year fixture proves:

- at least 252 valid sessions;
- ordered Daily aggregation and calendar boundaries;
- audited contract roll and no unexpected session gaps;
- checkpoint retention of the submitted provider job ID;
- repeat execution under a new attempt ID performs one total acquisition; and
- immutable duplicate writes do not create conflicts.

Supporting results: Application MarketData unit 80 passed, Analytics unit 889
passed, Analytics BDD 462 passed, Analytics integration 39 passed, and the API
Server build completed with no warnings or errors.

## 5. Exit decision

The actor surface, restart checkpoints, roll-aware normalization, idempotent
raw persistence, and repeat-run behavior satisfy the gate. MDSI-3 is complete.
