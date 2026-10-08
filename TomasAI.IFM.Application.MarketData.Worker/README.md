# Dataset worker logging

The local dataset worker (including GLBX.MDP3) writes daily structured JSON logs to
`Logs/ifm-dataset-worker-YYYYMMDD.log` beside the worker executable. Seven daily files
are retained. The file sink uses a bounded 4096-event asynchronous buffer and does
not block market processing when full. Multiple worker processes share the file.

Logs export through the same bounded OTLP sink used by the API, with service name
`TomasAI.IFM.Application.MarketData.Worker`. Defaults target the local Collector at
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
