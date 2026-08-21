# IFM Server Manager SM-S4 Real Task Modernization

**Status:** Software modernization complete; task enablement acceptance pending

**Date:** 2026-08-20

| Task | Modernization result |
| --- | --- |
| Futures Market Close | .NET 10, typed NATS APIs, durable backup-command IDs, explicit failure/cancellation outcome |
| Futures Market Open | .NET 10, current NATS APIs, checked start-command result, no forced self-kill |
| Set Closing Price | .NET 10, current NATS APIs, configured timezone/close time/symbols, aggregate failure result |
| Futures ITI predictive-model training | .NET 10, hard-coded test mode removed, configured inputs, current NATS command submission |

All four use `TomasAI.IFM.Application.ScheduledTask.Shared` for exit code `0` on success, `1` on failure, `2` on
cooperative cancellation, observable exception handling, host-token cancellation, and `IFM_TASK_CONTROL_PIPE`.
They are included in `TomasAI.IFM.sln` and build in Release with zero warnings and errors.

Scheduler Host contains immutable catalog entries and disabled templates for all four tasks. Templates have explicit
timezone/misfire/runtime policy and "Review Before Enable" names. Configuration rejects any enabled seed.

No real template is approved for enablement yet. Each still requires execution in the approved Development/paper
environment, real dependency/credential validation, durable command completion and idempotency proof, exchange
holiday/early-close review, measured runtime/output, rollback rehearsal, and named operator approval. These checks
can change application/trading state and are intentionally not run by repository automation.
