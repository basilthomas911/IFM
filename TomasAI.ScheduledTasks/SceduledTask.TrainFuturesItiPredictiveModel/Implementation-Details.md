# Train Futures ITI Predictive Model Scheduled Task

The SM-S4 implementation is a one-shot .NET 10 worker using the current NATS actor client. It loads the IFM value
date, reads the symbol and training start date from configuration, validates the date range, and submits a typed
`BuildFuturesItiTrendModelCommand`. The prior hard-coded `testMode = true` path is removed.

The shared runtime returns exit code `0` for accepted success, `1` for failure, and `2` for cancellation. The task
supports `IFM_TASK_CONTROL_PIPE` and never kills its own process. Its disabled Scheduler Host template must remain
disabled until real Development data, command idempotency/completion, runtime, output, and rollback are accepted.
