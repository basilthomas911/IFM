# Futures Market Open — Implementation Details

## Execution contract

The .NET 10 one-shot task sends `StartMarketDataFeedAsync()` without business parameters.
The running API derives the feed value date at execution from its clock and Eastern exchange-session rules,
and owns the prepared contract assignments. Completed financial EOD dates do not move feed replay into the future. The task may execute
at any time of day, including a closed session, a pending EOD indication or after an intraday
Market Close. It does not query the calendar/session to admit the start or compare calendar
and operational dates before issuing the command.

Development runs immediately without scheduler admission. Existing Production scheduler
admission policy remains separately configured. Actor-managed launches retain their execution
receipts; direct Development launches do not invent a scheduler occurrence.

## Workflow

1. Connect the query/command producer to NATS.
2. Subscribe for correlated start completion and submit the parameterless feed-start command.
3. Use the value date returned by the API completion event.
4. Observe the running Databento generation and expected subscriptions for that returned date.
   These observations report startup outcome; they do not admit or prevent execution by time of day.
5. Record `FeedsStarted` for actor-managed runs and exit successfully after readiness is confirmed.
6. On an actual startup failure or timeout, log the error and return a nonzero process exit code.

The task does not start a second API process, run application initialization, pass contracts or
a value date, or request backups. Stopping feeds is owned by Market Close or the explicit stop action.
Persisted financial EOD records are not reset by a feed start.

## Configuration

- `Nats:Url`: backend transport, default `nats://localhost:4222`.
- `DOTNET_ENVIRONMENT`: selects environment settings.
- `Serilog` and `Telemetry`: task logs and metrics.

## Verification

Backend regression cases cover start-date resolution for open/closed sessions with and without
pending EOD. Readiness tests verify that startup is only reported successful for a running,
subscribed generation. The date used for readiness comes from backend completion, not a task input.

## Development live verification — 2026-10-15

At 08:38 ET the revised task completed successfully with exit code 0 and backend-selected feed
value date 2026-10-15, despite financial EOD having advanced the operational date to 2026-10-16.
The feed runtime was running with a Healthy Databento generation. GLBX.MDP3 acknowledged 1/1
subscriptions and XCBF.PITCH acknowledged 2/2, with both producers and aggregation workers alive.
The watchdog now honors an explicit start outside the session-hours label until an explicit stop.
Regression coverage: 12 feed/date tests, 12 readiness tests, 35 watchdog tests and 7 close tests.
