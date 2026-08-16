# Infrastructure

G0 infrastructure provides:

- configuration and explicit Development prerequisite validation;
- non-short-circuit step/result recording with `Passed`, `Failed`, `BlockedDependency`, and `SkippedDependency` outcomes;
- exact PID ownership, redirected/redacted API and UI logs, bounded process waits, and owned-tree cleanup fallback;
- TCP, API-health, actor-count, and desktop `netstat` transport probes;
- FlaUI main-window discovery, stable-control reads, normal close, screenshots, and automation-tree dumps;
- typed NATS observers for both reference-data imports, market-data feed lifecycle, status messages, and RSI/ATR/ADX/MACD start/stop events;
- NATS query clients for current ES, value date, seed state, and post-import durable storage checks;
- redacted `result.json`, `summary.md`, process, network, log, screenshot, and automation-tree artifacts.

`G0InfrastructureTests` exercises the recorder continuation behavior, readiness JSON contract, endpoint parsing, evidence serialization, and credential redaction without launching the live application.
