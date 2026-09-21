# Isolated acceptance-host implementation

Status: qualification entry point and launcher implemented; composition verified; full running host blocked by shared Docker AIO capacity. No production deployment or schema activation.

## Implemented

The real API executable accepts --event-log-qualification=<12 lowercase hex digits> only in the Test environment. A process-local activation object selects the internal candidate constructor; ordinary DI/public construction is unchanged. Malformed qualification flags are rejected rather than silently falling back to normal startup. Maintenance switches cannot be combined.

The generated profile fixes PostgreSQL to 127.0.0.1:25432 with a per-run benchmark/synthetic database, Scylla to 29042 with six per-run synthetic keyspaces, Redis to 26379, NATS/JetStream to 24223, and HTTP to 25443. Unknown connection entries, additional HTTP listeners and deviations from the generated settings are rejected.

Candidate registration selects BinaryCopy with marker batching. Normal schema initialization is followed by the name-preserving three-index migration only in qualification mode. The migration has bounded lock/statement timeouts, checks the PK shape and requires exactly three indexes.

Qualification uses SyntheticCi/Synthetic, emulator-only broker registration and a per-run emulator ledger. Automatic application-start dispatch, strategy automation, historical warm-up, FMP refresh and telemetry export are disabled. The Treasury refresh hosted service is not registered. Hazelcast is replaced with process-local distributed cache in this mode; Redis remains a disposable real service. Factory-created HTTP clients reject requests except to the isolated API. Live Databento credentials are rejected by the entry point and removed from the launcher child environment.

The launcher scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 has explicit Prepare, InitializeStores, Verify, Run and Cleanup actions. It checks container labels/images/loopback bindings, requires an empty dedicated PostgreSQL server before preparation, and provisions only synthetic stores. Run starts an owned hidden API process, waits a bounded time for bootstrap health, records logs and stops that child in finally. It does not launch production services or automatically run trading workflows.

## Verification

- API Release build: zero warnings/errors.
- 13/13 qualification-profile unit tests passed.
- Real qualification composition root passed --verify-startup-only with synthetic source selected.
- 3/3 existing normal startup composition process tests passed after the changes.
- git diff --check passed.

Artifacts: BenchmarkDotNet.Artifacts/event-log-v2/acceptance-host-20260920/qualification-guards.trx and normal-startup-regression.trx.

These checks do not prove runtime database initialization, actor readiness, end-to-end financial workflows, UI operation or broad network isolation. The HTTP restriction covers IHttpClientFactory clients; the synthetic/no-live-credentials profile is also required for native adapters. Auto-start remains disabled until explicit synthetic workflow testing is implemented.

## Infrastructure blocker

The isolated Scylla 6.2.2 instance could not initialize Seastar:

> Your system does not satisfy minimum AIO requirements.

The shared Docker host reported aio-max-nr=65536 and aio-nr=65530. Scylla requested at least 66557 (116556 recommended in its diagnostic). A container-local retry with the documented epoll reactor backend failed identically. No shared kernel setting or existing service was changed/stopped.

The initial launcher attempt also exposed PowerShell treating expected native stderr as terminating; the readiness loop now handles connection refusal, has a deadline and detects the AIO diagnostic. The completed readiness attempt timed out without starting the API.

All three owned disposable containers/volumes and the synthetic PostgreSQL database were removed after diagnosis. The dedicated benchmark PostgreSQL container was stopped. No synthetic store backup exists; source and test reports remain.

## Resume

First resolve AIO capacity using an approved shared-host change or a separate suitable Docker host. The current launcher targets local Docker; a separate host would require deliberate endpoint/profile changes.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Prepare
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Verify
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Run
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/EventLogQualification/Invoke-IsolatedAcceptance.ps1 -RunId 092020260032 -Action Cleanup
```

Run is a bootstrap smoke test, not complete stage-3 acceptance. Reference seeding, explicit synthetic workflow acceptance, controlled restart/reconciliation and a UI pinned to these endpoints remain to be exercised/finished after infrastructure becomes available. Do not use normal Development configuration as a substitute.
