# Dataset worker logging

The local dataset worker (including GLBX.MDP3) writes daily structured JSON logs to
`Logs/ifm-dataset-worker-YYYYMMDD.log` beside the worker executable. Seven daily files
are retained. The file sink uses a bounded 4096-event asynchronous buffer and does
not block market processing when full. Multiple worker processes share the file.

Logs export through the same bounded OTLP sink used by the API, with service name
`TomasAI.IFM.Application.MarketData.Host.Worker`. Defaults target the local Collector at
`http://localhost:4318/v1/logs`. Override inherited environment variables:

- `Telemetry__Logs__Enabled=false` disables export.
- `Telemetry__Logs__OtlpEndpoint` sets the Collector endpoint.
- `Telemetry__Logs__OtlpProtocol` accepts `http/protobuf` or `grpc`.
- `Telemetry__Logs__Headers` supplies exporter headers when needed.

Worker startup, manifest installation, handled command failures, unexpected control
pipe closure and fatal startup failures include method and relevant scalar arguments.
Dataset, value date, process, worker and generation identities correlate runtime logs.
The runtime receives the Serilog logger factory; its existing diagnostic logs now
reach these sinks too. No per-tick logging was added. Authentication tokens and pipe
handles are not logged; startup/control exception text redacts inherited secrets.

Logging starts before argument validation and the worker handshake. Failures before
managed entry (for example an unavailable .NET host) cannot reach these sinks.

## Control-pipe pricing snapshots

Internal control protocol 4 requires separate health pipes and uses bounded, connection-local MessagePack references for immutable
pricing contexts, calendars and reference rates (4096 definitions per type and direction).
Updated contexts are sent as new definitions; reconnecting starts fresh dictionaries. Host and
workers must be deployed together. Public API and NATS payloads remain self-contained.
A partial, cancelled or invalid frame faults that pipe direction: the existing recovery path
must replace the connection rather than continue with uncertain context references.
Serialization and frame reads borrow array-pool buffers only until I/O/deserialization completes.
Published snapshots retain owned immutable values, never rented byte or contract-state arrays.

## Recovery deadlines

Host recovery owns one two-minute deadline (`MarketDataHost:SoftRecoveryTimeout`, default and
maximum 120 seconds) shared across attempts, reset/readiness and option lease restoration.
Worker commands in that recovery flow do not start independent command/start timers. Recovery
RPCs and the dataset monitor do not add another timer; explicit cancellation/shutdown still works.
At expiry the Host records terminal failure before cleanup, and the API relays a visible UI error
for Futures or Options with dataset, operation, phase and deadline. Dataset-only cleanup then
confirms termination and retirement; it cannot postpone the failure notification. Normal commands
outside recovery retain their own deadlines. Host and workers must be rebuilt/restarted together.

## Recovery and control isolation

- Health uses separate inherited anonymous pipes and a dedicated worker read loop. Subscription acquisition, pricing snapshots, and evidence persistence cannot occupy the health channel.
- The monitor allows ten seconds for a health response. This is failure detection, outside the recovery operation. A business command active for two minutes is reported as stalled and requires process replacement.
- A failed control or health frame permanently invalidates that connection. Host replaces only that role/dataset child, preserving its logical worker identity and Host-owned subscription intent, with a new process, generation, and fresh pipes.
- Soft reconstruction, process replacement, startup, and restoration share one recovery deadline (two minutes by default). Replacement does not add handshake or start timers. Exhaustion publishes the terminal error before containment cleanup; other datasets remain running.
- Recovery snapshots temporary leases and committed ownership together. Released/expired temporary leases are not restored. Old-generation publications remain fenced.
- Option evidence retains its persistence acknowledgment. Waiting for that acknowledgment does not hold the quote writer; Host drains evidence through a bounded retention queue. The Options relay assigns delivery sequences when forwarding, so delayed persisted evidence cannot invalidate later quotes or be rejected behind them. Native evidence identity and timestamps are unchanged.
- The API recovery notifier owns a JetStream producer for durable `Event` subjects and stops it during shutdown.

Integration verification uses an isolated published directory (`IFM_MARKETDATA_TEST_HOST_DIRECTORY`). Notification transport tests require a separate JetStream broker (`IFM_RECOVERY_TEST_NATS_URL`) because they publish real desktop event subjects. Never point that variable at the running development broker.
