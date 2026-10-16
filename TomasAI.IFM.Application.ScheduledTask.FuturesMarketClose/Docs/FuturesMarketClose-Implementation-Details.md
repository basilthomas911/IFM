# Futures Market Close

## Execution boundary

The one-shot task calls `IMarketCloseApi.CloseMarketAsync()` on the running API server.
It sends an empty HTTP POST to `/api/market-data/close`: no date, contract list, position IDs,
or scheduler-run parameters. `ApiServer:BaseUrl` selects the API address.

Development Market Open and Market Close bypass runtime scheduler admission policy, artifact
matching, dependency admission and the worker admission wait. Other environments retain scheduler
admission. The close endpoint requires authorization outside Development.

## Backend workflow

`Domain.SystemAdmin/MarketClose/MarketCloseCoordinator` owns sequencing and serializes close calls.
`Api.Server.Core/MarketData/Sessions/MarketCloseOperations` adapts the existing actor APIs and transport maintenance.

1. Resolve the execution date once from the API clock and Eastern exchange-session rules. Explicit execution is permitted at any time.
   Intraday close uses the actual execution time, and retries reuse the original persisted boundary.
2. Read the durable EOD completion marker. A completed date still stops feeds and performs maintenance without repeating position finalization.
3. Create an API-owned execution record using existing event-sourced scheduled-run receipts.
   Its accepted/start records are bookkeeping and invoke no schedule-admission policy.
4. Stop feeds and await that exact stop command's terminal completion event.
5. Persist `FeedsStopped`; page all open positions, deduplicate routes and skip already-sealed dates.
6. Submit deterministic position EOD commands for the captured date and close boundary.
7. Persist `PositionsFinalized` only after every position source commitment succeeds. The existing
   atomic projection updates `scheduled_task.futures_completed_end_of_day`, including direct launches.
8. Purge only completed/drained JetStream transport messages using the existing maintenance implementation.
9. Record completion and return the backend-confirmed date, execution ID and position count.

The API uses its shutdown token and a bounded deadline; a task/client disconnect does not cancel
financial EOD in progress. Failures are logged and recorded. A maintenance failure does not undo a
committed EOD marker. A retry of a finalized date stops feeds again and repeats maintenance.

## Backup ownership

Market Close does not request, configure or depend on database backups. Backups are a separate
scheduled operation. No backup task is scheduled by this change.

## Configuration

- Task: `ApiServer:BaseUrl` (Development default `http://localhost:22543/`).
- Backend: existing `Nats:Url`; optional `JetStreamEndOfDayPurge` overrides the maintenance defaults.
- Task logging and telemetry remain configured normally.

## Verification

Isolated tests cover intraday execution and stable retry boundaries, pinned dates, paginated/deduplicated positions,
already-sealed positions, stop/EOD failures, durable completion ordering, maintenance retries,
parameterless HTTP requests and Development admission policy. Live execution is an explicit operational action, including during trading hours.

### Development intraday validation — 2026-10-15

Executed the parameterless task against the managed Development API at 07:42 ET, during trading hours.
The task returned HTTP 200 and exit code 0 in approximately 26 seconds. Run
`641433f5-ca2c-4413-879a-e0ae10acb27f` is durably `Succeeded` / `MarketClosed`, with
`CompletedEndOfDayValueDate=2026-10-15`. One position was finalized. Read-only actor queries
confirmed feeds stopped and operational value date advanced to 2026-10-16; live health reported
planned closure. The scheduler deployment was updated to the same tested task build.

The first attempt exposed duplicate command-audit reservation in position EOD persistence.
PostgreSQL position actors now let the atomic event/projection transaction own that reservation.
Identical EOD retries can recover an orphan reservation under a row lock after payload verification;
changed payloads and already-committed events remain rejected. The isolated PostgreSQL regression
passed, alongside 33 close/scheduler and 18 position tests. API build completed with zero errors
and existing dependency advisory warnings. No backup operation is invoked by Market Close.
